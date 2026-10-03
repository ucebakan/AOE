using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PlayerXYZ;

record Signature(string Pattern, int Rva);
sealed class BuildProfile
{
    public int Version {get;set;}=2;
    public string Sha256 {get;set;}="";
    public int ImageSize {get;set;}
    public int PeTimestamp {get;set;}
    public int CtclientgameVtableRva {get;set;}
    public int CtclientcharVtableRva {get;set;}
    public int RootRva {get;set;}
    public int OwnerToPlayerOffset {get;set;}
    public int[] CoordinateA {get;set;}=[];
    public int[] CoordinateB {get;set;}=[];
    public Dictionary<string,Signature> Signatures {get;set;}=[];
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
        using var stream=typeof(Profiles).Assembly.GetManifestResourceStream("PlayerXYZ.signatures.json")!;
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
        int a=p.Signatures["coords_a"].Rva,c=p.Signatures["coords_b"].Rva;
        p.CoordinateA=[unchecked((sbyte)bin.At(a+4,1)[0]),unchecked((sbyte)bin.At(a+10,1)[0]),unchecked((sbyte)bin.At(a+16,1)[0])];
        p.CoordinateB=[bin.I32(c+10),bin.I32(c+30),bin.I32(c+57)];
        p.RootRva=p.Signatures["root"].Rva+7+bin.I32(p.Signatures["root"].Rva+3);
        p.OwnerToPlayerOffset=bin.I32(p.Signatures["owner"].Rva+21);
        Validate(bin,p); return p;
    }
    public static void Validate(Binary b,BuildProfile p)
    {
        if(p.Version!=2 || p.Sha256!=b.Sha || p.ImageSize!=b.Pe.PEHeaders.PEHeader!.SizeOfImage || p.PeTimestamp!=b.Pe.PEHeaders.CoffHeader.TimeDateStamp)throw new IOException("Profile identity mismatch.");
        foreach(var (name,t) in Templates())
        {
            if(!p.Signatures.TryGetValue(name,out var s) || s.Pattern!=t.Pattern || !b.Executable(s.Rva,Binary.Pattern(t.Pattern).Length) || !Binary.Match(b.At(s.Rva,Binary.Pattern(t.Pattern).Length),Binary.Pattern(t.Pattern)))throw new IOException("Profile signature mismatch: "+name);
        }
        int a=p.Signatures["coords_a"].Rva,c=p.Signatures["coords_b"].Rva,o=p.Signatures["owner"].Rva;
        int[] aa=[unchecked((sbyte)b.At(a+4,1)[0]),unchecked((sbyte)b.At(a+10,1)[0]),unchecked((sbyte)b.At(a+16,1)[0])];
        int[] bb=[b.I32(c+10),b.I32(c+30),b.I32(c+57)];
        if(!p.CoordinateA.SequenceEqual(aa)||!p.CoordinateB.SequenceEqual(bb))throw new IOException("Coordinate displacement mismatch.");
        foreach(var v in new[]{aa,bb})if(v[0]<0x10||v[0]>0x10000||(v[0]&3)!=0||v[1]!=v[0]+4||v[2]!=v[0]+8)throw new IOException("XYZ layout is not a contiguous float triplet.");
        if(aa[0]<bb[0]+12 && bb[0]<aa[0]+12)throw new IOException("Coordinate groups overlap.");
        if(p.OwnerToPlayerOffset!=b.I32(o+21)||p.OwnerToPlayerOffset!=b.I32(o+37)||p.OwnerToPlayerOffset<0x100||p.OwnerToPlayerOffset>0x10000)throw new IOException("Owner displacement mismatch.");
        b.ValidateRtti(p.CtclientcharVtableRva,".?AVCTClientChar@@");b.ValidateRtti(p.CtclientgameVtableRva,".?AVCTClientGame@@");
        using var recovery=new UnityTools.Controls.RecoveryImage(b.Data);
        int vectorSlot=recovery.MethodSlot(p.CtclientcharVtableRva,a), pairSlot=recovery.MethodSlot(p.CtclientcharVtableRva,c);
        int update=recovery.Call(o+25);
        int updateSlot=recovery.MethodSlot(p.CtclientcharVtableRva,update);
        if(vectorSlot==pairSlot || pairSlot==updateSlot || recovery.Function(update).Start!=update ||
            b.I32(c+16)+8!=b.I32(c+36) || b.I32(c+16)<0 || b.I32(c+36)>vectorSlot ||
            p.RootRva!=p.Signatures["root"].Rva+7+b.I32(p.Signatures["root"].Rva+3) || p.RootRva<0 || p.RootRva>=p.ImageSize-8 || b.Executable(p.RootRva,8))
            throw new IOException("Class method / root linkage changed.");
        if(b.Sha==KnownSha && (p.CtclientgameVtableRva!=0xCDC970 || p.CtclientcharVtableRva!=0xCDBCC0 || !aa.SequenceEqual(new[]{0x70,0x74,0x78}) || !bb.SequenceEqual(new[]{0xB0,0xB4,0xB8}) || p.OwnerToPlayerOffset!=0x2710))throw new IOException("Current anchors mismatch.");
    }
}
