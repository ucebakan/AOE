using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SpeedJump;

record Signature(string Pattern, int Rva);
sealed class BuildProfile
{
    public int Version { get; set; } = 1;
    public string Sha256 { get; set; } = "";
    public int ImageSize { get; set; }
    public int PeTimestamp { get; set; }
    public int CtclientgameVtableRva { get; set; }
    public int CtclientcharVtableRva { get; set; }
    public int RootRva {get;set;}
    public int CoordinateOffset {get;set;}
    public int OwnerToPlayerOffset { get; set; }
    public int SpeedFieldOffset { get; set; }
    public int SpeedValue { get; set; } = 23452;
    public int JumpFieldOffset { get; set; }
    public float JumpValue { get; set; } = 20f;
    public int JumpWriterRva { get; set; }
    public string JumpWriterOriginalBytes { get; set; } = "";
    public Dictionary<string, Signature> Signatures { get; set; } = [];
}

static class Files
{
    public static string Root = AppContext.BaseDirectory;
    public static readonly JsonSerializerOptions Json = new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
    public static string Profiles => Path.Combine(Root, "profiles");
    public static void Save<T>(string path, T data)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        using (var stream = new FileStream(path + ".tmp", FileMode.Create, FileAccess.Write, FileShare.None))
        { JsonSerializer.Serialize(stream, data, Json); stream.Flush(true); }
        File.Move(path + ".tmp", path, true);
    }
    static readonly object logLock = new();
    public static void Log(string message)
    {
        lock (logLock)
        {
            // Diagnostics must never interrupt cancellation or code restoration.
            try
            {
                Directory.CreateDirectory(Path.Combine(Root, "logs"));
                File.AppendAllText(Path.Combine(Root, "logs", "movement.log"), $"{DateTimeOffset.Now:O} {message}{Environment.NewLine}");
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("Log unavailable: " + ex.Message); }
        }
    }
}

sealed class Binary : IDisposable
{
    public readonly byte[] Data;
    public readonly PEReader Pe;
    public readonly string Sha;
    public long ImageBase => checked((long)Pe.PEHeaders.PEHeader!.ImageBase);
    public Binary(string path) : this(File.ReadAllBytes(path)) { }
    public Binary(byte[] data)
    {
        Data = data; Pe = new PEReader(new MemoryStream(data));
        if ((int)Pe.PEHeaders.CoffHeader.Machine != 0x8664 || Pe.PEHeaders.PEHeader is null) throw new IOException("x64 PE gerekli.");
        Sha = Convert.ToHexString(SHA256.HashData(data));
    }
    public byte[] At(int rva, int n)
    {
        var s = Pe.PEHeaders.SectionHeaders.Single(s => rva >= s.VirtualAddress && (long)rva+n <= (long)s.VirtualAddress+s.SizeOfRawData);
        return Data.AsSpan(rva-s.VirtualAddress+s.PointerToRawData,n).ToArray();
    }
    public int I32(int rva) => BitConverter.ToInt32(At(rva,4));
    public long I64(int rva) => BitConverter.ToInt64(At(rva,8));
    public bool Executable(int rva, int n=1) => Pe.PEHeaders.SectionHeaders.Any(s => ((uint)s.SectionCharacteristics & 0x20000000)!=0 && rva>=s.VirtualAddress && (long)rva+n <= (long)s.VirtualAddress+s.SizeOfRawData);
    public int RelativeCall(int rva)
    { if (At(rva,1)[0]!=0xE8) throw new IOException("Call opcode uyuşmuyor."); return checked(rva+5+I32(rva+1)); }
    public static byte?[] Pattern(string pattern) => pattern.Split(' ',StringSplitOptions.RemoveEmptyEntries).Select(t=>t=="??" ? (byte?)null : Convert.ToByte(t,16)).ToArray();
    public static bool Match(ReadOnlySpan<byte> data, byte?[] pattern)
    { if (data.Length!=pattern.Length) return false; for(int i=0;i<pattern.Length;i++) if(pattern[i] is byte v && data[i]!=v)return false; return true; }
    public int FindUnique(string pattern)
    {
        var p=Pattern(pattern); var hits=new List<int>();
        foreach(var s in Pe.PEHeaders.SectionHeaders.Where(s=>((uint)s.SectionCharacteristics&0x20000000)!=0))
            for(int i=0;i<=s.SizeOfRawData-p.Length;i++)
                if(Data[s.PointerToRawData+i]==p[0] && Match(Data.AsSpan(s.PointerToRawData+i,p.Length),p)) hits.Add(s.VirtualAddress+i);
        if(hits.Count!=1) throw new IOException($"İmza tekil değil: {hits.Count} eşleşme.");
        return hits[0];
    }
    public void ValidateRtti(int vt,string name)
    {
        int col=checked((int)(I64(vt-8)-ImageBase));
        if(I32(col)!=1 || I32(col+4)!=0 || I32(col+8)!=0 || I32(col+20)!=col ||
           !At(I32(col+12)+16,name.Length+1).SequenceEqual(Encoding.ASCII.GetBytes(name+"\0")) || !Executable(checked((int)(I64(vt)-ImageBase))))
            throw new IOException("RTTI/vtable doğrulanamadı: "+name);
    }
    public int FindVtable(string name)
    {
        var sections=Pe.PEHeaders.SectionHeaders; var types=new HashSet<int>(); var cols=new HashSet<long>(); var tables=new List<int>();
        byte[] text=Encoding.ASCII.GetBytes(name+"\0");
        foreach(var s in sections)
            for(int i=0;i<=s.SizeOfRawData-text.Length;i++)
                if(Data.AsSpan(s.PointerToRawData+i,text.Length).SequenceEqual(text))types.Add(s.VirtualAddress+i-16);
        foreach(var s in sections)
            for(int i=0;i<=s.SizeOfRawData-24;i+=4)
            {
                int a=s.PointerToRawData+i;
                if(BitConverter.ToInt32(Data,a)==1 && BitConverter.ToInt32(Data,a+4)==0 && BitConverter.ToInt32(Data,a+8)==0 && types.Contains(BitConverter.ToInt32(Data,a+12)) && BitConverter.ToInt32(Data,a+20)==s.VirtualAddress+i)cols.Add(ImageBase+s.VirtualAddress+i);
            }
        foreach(var s in sections)
            for(int i=0;i<=s.SizeOfRawData-16;i+=8)
                if(cols.Contains(BitConverter.ToInt64(Data,s.PointerToRawData+i)))
                {
                    long r=BitConverter.ToInt64(Data,s.PointerToRawData+i+8)-ImageBase;
                    if(r>=0 && r<=int.MaxValue && Executable((int)r))tables.Add(s.VirtualAddress+i+8);
                }
        if(tables.Count!=1)throw new IOException($"{name}: {tables.Count} vtable; tekil değil.");
        ValidateRtti(tables[0],name); return tables[0];
    }
    public void Dispose()=>Pe.Dispose();
}

static class Profiles
{
    public const string KnownSha="9CD77CD0C305359A3048D60C3C2FD837B907E2E61BBF8B828BFCC8E5270E5C28";
    public static int ScanCount {get;private set;}
    static Dictionary<string,Signature> Templates()
    {
        using var stream=typeof(Profiles).Assembly.GetManifestResourceStream("SpeedJump.signatures.json")!;
        using var doc=JsonDocument.Parse(stream);
        return doc.RootElement.EnumerateObject().ToDictionary(p=>p.Name,p=>new Signature(p.Value.GetProperty("pattern").GetString()!,p.Value.GetProperty("rva").GetInt32()));
    }
    public static BuildProfile Load(Binary bin)
    {
        string path=Path.Combine(Files.Profiles,bin.Sha+".json");
        if(File.Exists(path))
        {
            try
            {
                var p=JsonSerializer.Deserialize<BuildProfile>(File.ReadAllText(path),Files.Json) ?? throw new IOException("Boş profil.");
                Validate(bin,p); Files.Log("Profile loaded "+bin.Sha+"; no AOB rescan"); return p;
            }
            catch(Exception ex){Files.Log("Profile invalid; controlled resolver: "+ex.Message);}
        }
        var resolved=Resolve(bin); Files.Save(path,resolved); Files.Log("Profile generated "+bin.Sha); return resolved;
    }
    public static BuildProfile Resolve(Binary bin)
    {
        ScanCount++; var p=new BuildProfile{Sha256=bin.Sha,ImageSize=bin.Pe.PEHeaders.PEHeader!.SizeOfImage,PeTimestamp=bin.Pe.PEHeaders.CoffHeader.TimeDateStamp};
        p.Signatures=Templates().ToDictionary(k=>k.Key,k=>k.Value with {Rva=bin.FindUnique(k.Value.Pattern)});
        p.CtclientgameVtableRva=bin.FindVtable(".?AVCTClientGame@@"); p.CtclientcharVtableRva=bin.FindVtable(".?AVCTClientChar@@");
        p.JumpWriterRva=p.Signatures["jump"].Rva+26;
        p.JumpFieldOffset=bin.I32(p.JumpWriterRva+4);
        p.JumpWriterOriginalBytes=Convert.ToHexString(bin.At(p.JumpWriterRva,8));
        p.CoordinateOffset=bin.I32(p.Signatures["pair_write"].Rva+17)-8;
        p.SpeedFieldOffset=bin.I32(p.Signatures["pair_compare"].Rva+14);
        p.RootRva=p.Signatures["root"].Rva+7+bin.I32(p.Signatures["root"].Rva+3);
        p.OwnerToPlayerOffset=bin.I32(p.Signatures["owner"].Rva+21);
        Validate(bin,p); return p;
    }
    public static void Validate(Binary b,BuildProfile p)
    {
        if(p.Version!=1 || p.Sha256!=b.Sha || p.ImageSize!=b.Pe.PEHeaders.PEHeader!.SizeOfImage || p.PeTimestamp!=b.Pe.PEHeaders.CoffHeader.TimeDateStamp || p.SpeedValue!=23452 || p.JumpValue!=20f)throw new IOException("Profil kimliği/değerleri uyuşmuyor.");
        foreach(var (name,t) in Templates())
        {
            if(!p.Signatures.TryGetValue(name,out var s) || s.Pattern!=t.Pattern || !b.Executable(s.Rva,Binary.Pattern(t.Pattern).Length) || !Binary.Match(b.At(s.Rva,Binary.Pattern(t.Pattern).Length),Binary.Pattern(t.Pattern)))throw new IOException("Profil imzası geçersiz: "+name);
        }
        int c=p.Signatures["pair_compare"].Rva,w=p.Signatures["pair_write"].Rva,j=p.Signatures["jump"].Rva,o=p.Signatures["owner"].Rva;
        int first=b.I32(c+3),second=b.I32(c+14);
        if(p.CoordinateOffset!=b.I32(w+17)-8 || p.CoordinateOffset<0x20 || p.CoordinateOffset>0x10000 || (p.CoordinateOffset&3)!=0)throw new IOException("Coordinate operand mismatch.");
        if(first<0x100 || second!=first+4 || second>0x10000 || b.I32(w+6)!=first || b.I32(w+27)!=second || p.SpeedFieldOffset!=second)throw new IOException("Adjacent pair displacement uyuşmuyor.");
        if(p.JumpWriterRva!=j+26 || !b.At(j+26,4).SequenceEqual(Convert.FromHexString("F30F118B")) || p.JumpFieldOffset!=b.I32(j+30) || p.JumpFieldOffset<0x100 || p.JumpFieldOffset>0x10000 || p.JumpWriterOriginalBytes!=Convert.ToHexString(b.At(j+26,8)))throw new IOException("MOVSS [RBX+disp32],XMM1 doğrulanamadı.");
        if(p.OwnerToPlayerOffset!=b.I32(o+21) || p.OwnerToPlayerOffset!=b.I32(o+37) || p.OwnerToPlayerOffset<0x100 || p.OwnerToPlayerOffset>0x10000)throw new IOException("Owner offset uyuşmuyor.");
        b.ValidateRtti(p.CtclientcharVtableRva,".?AVCTClientChar@@");b.ValidateRtti(p.CtclientgameVtableRva,".?AVCTClientGame@@");
        using var recovery=new UnityTools.Controls.RecoveryImage(b.Data);
        int setter=recovery.Function(c).Start, update=recovery.Call(o+25);
        recovery.MethodSlot(p.CtclientcharVtableRva,c); recovery.MethodSlot(p.CtclientcharVtableRva,update);
        if(recovery.FunctionRoot(w)!=setter || recovery.FunctionRoot(j)!=update || update==setter ||
            p.RootRva!=p.Signatures["root"].Rva+7+b.I32(p.Signatures["root"].Rva+3) || p.RootRva<0 || p.RootRva>=p.ImageSize-8 || b.Executable(p.RootRva,8))
            throw new IOException("CTClientChar method/root ilişkisi uyuşmuyor.");
        var cmp=recovery.Decode(c,Binary.Pattern(p.Signatures["pair_compare"].Pattern).Length);
        var write=recovery.Decode(w,Binary.Pattern(p.Signatures["pair_write"].Pattern).Length);
        if(cmp[0].Mnemonic!=Iced.Intel.Mnemonic.Cmp || cmp[3].Mnemonic!=Iced.Intel.Mnemonic.Cmp ||
            !UnityTools.Controls.RecoveryImage.Mem(cmp[0],0,Iced.Intel.Register.RBX) || !UnityTools.Controls.RecoveryImage.Mem(cmp[3],0,Iced.Intel.Register.RBX) ||
            write[3].Mnemonic!=Iced.Intel.Mnemonic.Movss || !UnityTools.Controls.RecoveryImage.Mem(write[3],0,Iced.Intel.Register.RBX) || cmp[0].Op1Register!=write[1].Op1Register || cmp[3].Op1Register!=write[5].Op1Register)
            throw new IOException("Speed compare/write dataflow uyuşmuyor.");
        if(b.Sha==KnownSha && (p.CtclientgameVtableRva!=0xCDC970 || p.CtclientcharVtableRva!=0xCDBCC0 || p.SpeedFieldOffset!=0x1204 || p.JumpFieldOffset!=0x8E0 || p.JumpWriterRva!=0x845B8F || p.OwnerToPlayerOffset!=0x2710))throw new IOException("Current-build anchors uyuşmuyor.");
    }
}
