using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace SpeedJump;

interface ITarget : IDisposable
{
    bool Alive {get;}
    BuildProfile Profile {get;}
    void Health();
    void Pin(bool jump);
    void EnablePatch();
    void RestorePatch();
}

record Recovery(int Pid,long Created,string Sha256,int WriterRva,string Original,uint OriginalProtection=0);

sealed class Session : ITarget
{
    public BuildProfile Profile {get;private set;}=null!;
    public int Pid {get;private set;}
    public long Created {get;private set;}
    public long Base {get;private set;}
    public long Owner {get;private set;}
    public long Player {get;private set;}
    public string PathName {get;private set;}="";
    IntPtr handle;
    readonly object sync=new();
    public bool Alive=>handle!=IntPtr.Zero && Native.WaitForSingleObject(handle,0)==0x102;
    static readonly byte[] Nops=Enumerable.Repeat((byte)0x90,8).ToArray();
    string Journal=>Path.Combine(Files.Root,"recovery.json");
    Recovery Identity=>new(Pid,Created,Profile.Sha256,Profile.JumpWriterRva,Profile.JumpWriterOriginalBytes);
    bool owned;

    public static Session Connect(bool readOnly=false)
    {
        var choices=new List<(int pid,string path,string sha)>();var errors=new List<string>();
        foreach(var process in Process.GetProcessesByName("TClient"))
        using(process)
        {
            IntPtr h=Native.OpenProcess(0x1000,false,process.Id);
            if(h==IntPtr.Zero){errors.Add($"PID {process.Id}: {Native.Error("Query").Message}");continue;}
            try
            {
                var path=new StringBuilder(32768);uint n=(uint)path.Capacity;
                if(!Native.QueryFullProcessImageName(h,0,path,ref n))throw Native.Error("Process path");
                using var binary=new Binary(path.ToString());
                Files.Log($"Enumerated PID={process.Id} path={path} SHA={binary.Sha}");
                if(string.Equals(path.ToString(),@"C:\Games\4Unity\TClient.exe",StringComparison.OrdinalIgnoreCase) || binary.Sha==Profiles.KnownSha || File.Exists(System.IO.Path.Combine(Files.Profiles,binary.Sha+".json")))choices.Add((process.Id,path.ToString(),binary.Sha));
            }
            catch(Exception ex){errors.Add($"PID {process.Id}: {ex.Message}");}
            finally{Native.CloseHandle(h);}
        }
        foreach(string error in errors)Files.Log(error);
        var preferred=choices.Where(c=>string.Equals(c.path,@"C:\Games\4Unity\TClient.exe",StringComparison.OrdinalIgnoreCase)).ToList();
        if(preferred.Count==1)choices=preferred;
        if(choices.Count!=1)throw new IOException(choices.Count>1?"Birden fazla doğrulanabilir 4Unity süreci; seçim belirsiz.":errors.Count>0?string.Join("; ",errors):"4Unity bekleniyor.");
        var selected=choices.Single();var s=new Session{Pid=selected.pid,PathName=selected.path};
        try
        {
            s.handle=Native.OpenProcess(readOnly?0x100410u:0x100438u,false,s.Pid);
            if(s.handle==IntPtr.Zero)throw Native.Error("4Unity OpenProcess");
            if(!Native.GetProcessTimes(s.handle,out long created,out _,out _,out _))throw Native.Error("Process creation time");s.Created=created;
            var actualPath=new StringBuilder(32768);uint len=(uint)actualPath.Capacity;
            if(!Native.QueryFullProcessImageName(s.handle,0,actualPath,ref len) || !string.Equals(actualPath.ToString(),selected.path,StringComparison.OrdinalIgnoreCase))throw new IOException("PID/path değişti.");
            using var b=new Binary(selected.path);
            if(b.Sha!=selected.sha)throw new IOException("EXE seçim sırasında değişti.");
            s.Profile=Profiles.Load(b);
            IntPtr snap=Native.CreateToolhelp32Snapshot(0x18,(uint)s.Pid);
            if(snap==new IntPtr(-1))throw Native.Error("Module snapshot");
            try
            {
                var e=new ModuleEntry{Size=(uint)Marshal.SizeOf<ModuleEntry>()};int matches=0;
                if(!Native.Module32First(snap,ref e))throw Native.Error("Module list");
                do
                {
                    if(!string.Equals(e.Path,selected.path,StringComparison.OrdinalIgnoreCase))continue;
                    if(e.BaseSize!=s.Profile.ImageSize)throw new IOException("Loaded image size uyuşmuyor.");
                    s.Base=e.Base.ToInt64();matches++;Files.Log($"Module PID={s.Pid} path={e.Path} base=0x{s.Base:X}");
                }while(Native.Module32Next(snap,ref e));
                if(matches!=1)throw new IOException("Loaded module tekil değil.");
            }finally{Native.CloseHandle(snap);}
            int peOff=BitConverter.ToInt32(s.Read(s.Base+0x3C,4));
            if(!s.Read(s.Base+peOff,4).SequenceEqual(new byte[]{0x50,0x45,0,0}) || BitConverter.ToInt32(s.Read(s.Base+peOff+8,4))!=s.Profile.PeTimestamp || BitConverter.ToInt32(s.Read(s.Base+peOff+24+56,4))!=s.Profile.ImageSize)throw new IOException("Loaded PE header uyuşmuyor.");
            // Validate fixed live signature windows. Only our journaled jump patch can differ.
            foreach(var sig in s.Profile.Signatures.Values)
            {
                byte[] expected=b.At(sig.Rva,Binary.Pattern(sig.Pattern).Length),actual=s.Read(s.Base+sig.Rva,expected.Length);
                int index=s.Profile.JumpWriterRva-sig.Rva;
                if(index>=0 && index+8<=actual.Length && actual.AsSpan(index,8).SequenceEqual(Nops) && s.JournalMatches())Convert.FromHexString(s.Profile.JumpWriterOriginalBytes).CopyTo(actual,index);
                if(!actual.SequenceEqual(expected))throw new IOException("Loaded code differs: RVA "+sig.Rva.ToString("X"));
            }
            s.ValidateLiveVtable(b,s.Profile.CtclientgameVtableRva);s.ValidateLiveVtable(b,s.Profile.CtclientcharVtableRva);
            s.CheckPatchPage();
            var code=s.Read(s.Base+s.Profile.JumpWriterRva,8);
            if(code.SequenceEqual(Nops))
            {
                if(readOnly)throw new IOException("Journaled patch pending recovery; read-only probe did not restore.");
                s.owned=s.JournalMatches();if(!s.owned)throw new IOException("Sahipsiz NOP; değiştirilmedi.");s.RestorePatch();
            }
            else if(!code.SequenceEqual(Convert.FromHexString(s.Profile.JumpWriterOriginalBytes)))throw new IOException("Unknown jump bytes.");
            else if(!readOnly && s.JournalMatches()){s.owned=true;s.RestorePatch();}
            s.ResolvePlayer();Files.Log($"Session ready PID={s.Pid} SHA={s.Profile.Sha256} owner=0x{s.Owner:X} P=0x{s.Player:X}");return s;
        }
        catch {s.Dispose();throw;}
    }
    void ValidateLiveVtable(Binary b,int rva)
    {
        if(Pointer(Base+rva)!=Base+(b.I64(rva)-b.ImageBase) || Pointer(Base+rva-8)!=Base+(b.I64(rva-8)-b.ImageBase))throw new IOException("Live vtable/RTTI differs.");
    }
    bool JournalMatches()
    {try{return JsonSerializer.Deserialize<Recovery>(File.ReadAllText(Journal),Files.Json) is { } r && r with {OriginalProtection=0}==Identity;}catch{return false;}}
    public byte[] Read(long address,int count)
    {
        if(!Alive)throw new IOException("Process closed.");
        var bytes=new byte[count];if(!Native.ReadProcessMemory(handle,(IntPtr)address,bytes,(nuint)count,out nuint n)||n!=(nuint)count)throw Native.Error("ReadProcessMemory");return bytes;
    }
    long Pointer(long address)=>BitConverter.ToInt64(Read(address,8));
    bool ValidPlayer(long p)
    {
        try
        {
            if(p<0x10000 || p>0x7FFFFFFF0000 || (p&7)!=0 || Pointer(p)!=Base+Profile.CtclientcharVtableRva)return false;
            var coords=Read(p+0xB0,12);for(int i=0;i<3;i++)if(!float.IsFinite(BitConverter.ToSingle(coords,i*4)))return false;
            Read(p+Profile.SpeedFieldOffset,4);Read(p+Profile.JumpFieldOffset,4);return true;
        }catch{return false;}
    }
    void ResolvePlayer()
    {
        if (Profile.Sha256 == UnityTools.Controls.KnownPlayerPath.Sha)
        {
            (Owner, Player) = UnityTools.Controls.KnownPlayerPath.Resolve(Base, Read,
                Profile.CtclientgameVtableRva, Profile.CtclientcharVtableRva, Profile.OwnerToPlayerOffset);
            Health(); Files.Log("Player resolved through validated module-relative path; no heap scan."); return;
        }
        var found=new HashSet<(long owner,long p)>();long address=0;int skipped=0;
        while(address<0x7FFFFFFF0000)
        {
            if(Native.VirtualQueryEx(handle,(IntPtr)address,out var region,(nuint)Marshal.SizeOf<MemoryInfo>())==0)
            {if(Marshal.GetLastWin32Error()==87)break;throw Native.Error("VirtualQueryEx");}
            long end=checked(region.BaseAddress+(long)region.RegionSize);if(end<=address)throw new IOException("Invalid memory region.");
            if(region.State==0x1000 && (region.Protect&0x100)==0 && (region.Protect&0xCC)!=0)
            {
                for(long pos=region.BaseAddress;pos<end;)
                {
                    int n=(int)Math.Min(1<<20,end-pos);byte[] data;
                    try{data=Read(pos,n);}catch{ // retry page by page, never silently claim unique across unreadable regions
                        n=(int)Math.Min(4096,end-pos);try{data=Read(pos,n);}catch{skipped++;pos+=n;continue;}
                    }
                    for(int i=(int)((8-(pos&7))&7);i<=data.Length-8;i+=8)
                    {
                        if(BitConverter.ToInt64(data,i)!=Base+Profile.CtclientgameVtableRva)continue;
                        long owner=pos+i;try{long p=Pointer(owner+Profile.OwnerToPlayerOffset);if(ValidPlayer(p))found.Add((owner,p));}catch{ }
                    }
                    pos+=n;
                }
            }
            address=end;
        }
        if(skipped>0)throw new IOException($"Heap scan eksik: {skipped} okunamayan sayfa; tekillik doğrulanamadı.");
        if(found.Count!=1)throw new IOException($"Local owner/P tekil değil: {found.Count} aday.");
        (Owner,Player)=found.Single();Health();
    }
    public void Health()
    {
        lock(sync)
        {
            if(!Alive || Pointer(Owner)!=Base+Profile.CtclientgameVtableRva)throw new IOException("Owner/session invalid.");
            long p=Pointer(Owner+Profile.OwnerToPlayerOffset);if(!ValidPlayer(p))throw new IOException("Local player invalid.");
            if(p!=Player){Files.Log($"Player changed 0x{Player:X}->0x{p:X}");Player=p;}
        }
    }
    public void Pin(bool jump)
    {
        lock(sync)
        {
            Health();if(jump && !Read(Base+Profile.JumpWriterRva,8).SequenceEqual(Nops))throw new IOException("Jump patch changed.");
            Write(Player+(jump?Profile.JumpFieldOffset:Profile.SpeedFieldOffset),jump?BitConverter.GetBytes(Profile.JumpValue):BitConverter.GetBytes(Profile.SpeedValue));
        }
    }
    void Write(long address,byte[] data)
    {if(!Alive)throw new IOException("Process closed.");if(!Native.WriteProcessMemory(handle,(IntPtr)address,data,(nuint)data.Length,out nuint n)||n!=(nuint)data.Length)throw Native.Error("WriteProcessMemory");}
    void CheckPatchPage()
    {
        long site=Base+Profile.JumpWriterRva;
        if(Native.VirtualQueryEx(handle,(IntPtr)site,out var r,(nuint)Marshal.SizeOf<MemoryInfo>())==0 || r.State!=0x1000 || (r.Protect&0xF0)==0 || (r.Protect&0x100)!=0 || site+8>r.BaseAddress+(long)r.RegionSize)throw new IOException("Jump site executable değil.");
    }
    void Patch(byte[] expected,byte[] replacement)
    {
        long address=Base+Profile.JumpWriterRva;CheckPatchPage();
        using var pause=new PausedThreads(Pid,address);
        if(!Read(address,8).SequenceEqual(expected))throw new IOException("Patch bytes değişti; işlem reddedildi.");
        if(!JournalMatches())throw new IOException("Patch ownership missing.");
        var journal=JsonSerializer.Deserialize<Recovery>(File.ReadAllText(Journal),Files.Json)!;
        if(journal.OriginalProtection==0)
        {
            if(Native.VirtualQueryEx(handle,(IntPtr)address,out var page,(nuint)Marshal.SizeOf<MemoryInfo>())==0)throw Native.Error("Patch protection query");
            journal=journal with {OriginalProtection=page.Protect};Files.Save(Journal,journal);
        }
        uint old=journal.OriginalProtection;
        if(!Native.VirtualProtectEx(handle,(IntPtr)address,8,0x40,out _))throw Native.Error("VirtualProtectEx");
        try
        {
            try{Write(address,replacement);if(!Read(address,8).SequenceEqual(replacement))throw new IOException("Patch readback failed.");}
            catch{Write(address,expected);throw;}
        }
        finally
        {
            bool protection=Native.VirtualProtectEx(handle,(IntPtr)address,8,old,out _);
            bool flush=Native.FlushInstructionCache(handle,(IntPtr)address,8);
            if(!protection||!flush)throw new IOException("Patch protection/cache cleanup failed; recovery journal retained.");
        }
    }
    public void EnablePatch()
    {
        lock(sync)
        {
            Health();byte[] original=Convert.FromHexString(Profile.JumpWriterOriginalBytes);
            if(!Read(Base+Profile.JumpWriterRva,8).SequenceEqual(original))throw new IOException("Jump ON için original bytes gerekli.");
            Files.Save(Journal,Identity);owned=true;Patch(original,Nops);
        }
    }
    public void RestorePatch()
    {
        lock(sync)
        {
            if(!Alive){owned=false;return;}
            if(!owned)return;
            byte[] bytes=Read(Base+Profile.JumpWriterRva,8),original=Convert.FromHexString(Profile.JumpWriterOriginalBytes);
            if(bytes.SequenceEqual(Nops) && JournalMatches())Patch(Nops,original);
            else if(!bytes.SequenceEqual(original))throw new IOException("Unknown patch modification; restore refused.");
            else if(!JournalMatches())throw new IOException("Recovery ownership missing.");
            var journal=JsonSerializer.Deserialize<Recovery>(File.ReadAllText(Journal),Files.Json)!;
            if(journal.OriginalProtection!=0 && !Native.VirtualProtectEx(handle,(IntPtr)(Base+Profile.JumpWriterRva),8,journal.OriginalProtection,out _))throw Native.Error("Original protection recovery");
            if(!Native.FlushInstructionCache(handle,(IntPtr)(Base+Profile.JumpWriterRva),8))throw Native.Error("Recovery cache flush");
            owned=false;File.Delete(Journal);
        }
    }
    public void Dispose(){if(handle!=IntPtr.Zero){Native.CloseHandle(handle);handle=IntPtr.Zero;}}
}
