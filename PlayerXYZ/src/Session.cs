using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace PlayerXYZ;

sealed class Session : IDisposable
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
            s.ValidateLiveVtable(b,s.Profile.CtclientgameVtableRva);s.ValidateLiveVtable(b,s.Profile.CtclientcharVtableRva);
            foreach(var signature in s.Profile.Signatures.Values)
            {
                int count=Binary.Pattern(signature.Pattern).Length;
                if(!s.Read(s.Base+signature.Rva,count).SequenceEqual(b.At(signature.Rva,count)))throw new IOException("Loaded coordinate code differs from disk.");
            }
            s.ResolvePlayer();Files.Log($"Session ready PID={s.Pid} SHA={s.Profile.Sha256} owner=0x{s.Owner:X} P=0x{s.Player:X}");return s;
        }
        catch {s.Dispose();throw;}
    }
    void ValidateLiveVtable(Binary b,int rva)
    {
        if(Pointer(Base+rva)!=Base+(b.I64(rva)-b.ImageBase) || Pointer(Base+rva-8)!=Base+(b.I64(rva-8)-b.ImageBase))throw new IOException("Live vtable/RTTI differs.");
    }
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
            foreach(int offset in new[]{Profile.CoordinateA[0],Profile.CoordinateB[0]})
            {var coords=Read(p+offset,12);for(int i=0;i<3;i++)if(!float.IsFinite(BitConverter.ToSingle(coords,i*4)))return false;}
            return true;
        }catch{return false;}
    }
    void ResolvePlayer()
    {
        if (Profile.RootRva != 0)
        {
            (Owner, Player) = UnityTools.Controls.KnownPlayerPath.Resolve(Base, Read,
                Profile.CtclientgameVtableRva, Profile.CtclientcharVtableRva, Profile.OwnerToPlayerOffset,
                Profile.Signatures["root"].Rva, Profile.RootRva);
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
            if(!Alive || (Profile.RootRva!=0 && Pointer(Base+Profile.RootRva)!=Owner) || Pointer(Owner)!=Base+Profile.CtclientgameVtableRva)throw new IOException("Owner/session invalid.");
            long p=Pointer(Owner+Profile.OwnerToPlayerOffset);if(!ValidPlayer(p))throw new IOException("Local player invalid.");
            if(p!=Player){Files.Log($"Player changed 0x{Player:X}->0x{p:X}");Player=p;}
        }
    }
    public (long Player,float[] A,float[] B) Snapshot()
    {
        lock(sync){Health();return (Player,Decode(Read(Player+Profile.CoordinateA[0],12)),Decode(Read(Player+Profile.CoordinateB[0],12)));}
    }
    public static float[] Decode(byte[] data)=>Enumerable.Range(0,3).Select(i=>BitConverter.ToSingle(data,i*4)).ToArray();
    public static byte[] Encode(float x,float y,float z)
    {
        if(!float.IsFinite(x)||!float.IsFinite(y)||!float.IsFinite(z))throw new ArgumentException("Koordinatlar sonlu sayılar olmalı.");
        return BitConverter.GetBytes(x).Concat(BitConverter.GetBytes(y)).Concat(BitConverter.GetBytes(z)).ToArray();
    }
    void Write(long address,byte[] data)
    {
        if(!Alive)throw new IOException("Process closed.");
        if(!Native.WriteProcessMemory(handle,(IntPtr)address,data,(nuint)data.Length,out nuint n)||n!=(nuint)data.Length)throw Native.Error("WriteProcessMemory");
    }
    public void SetCoordinates(float x,float y,float z)
    {
        byte[] data=Encode(x,y,z);
        lock(sync)
        {
            Health();
            // Pause only for the two writes and readback; preserve the matrix W fields.
            using var pause=new PausedThreads(Pid,long.MaxValue-16);
            Health();long p=Player;byte[] a=Read(p+Profile.CoordinateA[0],12),b=Read(p+Profile.CoordinateB[0],12);
            try
            {
                UnityTools.Controls.OperationGate.Check();
                Write(p+Profile.CoordinateA[0],data);Write(p+Profile.CoordinateB[0],data);
                if(!Read(p+Profile.CoordinateA[0],12).SequenceEqual(data)||!Read(p+Profile.CoordinateB[0],12).SequenceEqual(data))throw new IOException("Koordinat yazımı doğrulanamadı.");
            }
            catch(Exception failure)
            {
                try{Write(p+Profile.CoordinateA[0],a);Write(p+Profile.CoordinateB[0],b);}catch(Exception rollback){throw new AggregateException("Yazım ve geri alma başarısız.",failure,rollback);}
                throw;
            }
            Files.Log($"Coordinates written P=0x{p:X} X={x} Y={y} Z={z}");
        }
    }
    public void Dispose(){if(handle!=IntPtr.Zero){Native.CloseHandle(handle);handle=IntPtr.Zero;}}
}
