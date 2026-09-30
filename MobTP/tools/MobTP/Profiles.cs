using System.Text.Json;
using PlayerXYZ;
using UnityMonsterList;

namespace MobTP;

sealed class MobProfile
{
    public int Version {get;set;}=1;
    public BuildProfile Player {get;set;}=new();
    public Dictionary<string,Signature> Signatures {get;set;}=[];
    public int RootRva {get;set;}
    public int RegistryOffset {get;set;}
    public int ActorIdOffset {get;set;}
    public int ActorTypeOffset {get;set;}
    public int HomeOffset {get;set;}
    public int ActorSize {get;set;}
    public int MonsterVtableRva {get;set;}
}

sealed class ProfileBundle(MobProfile profile,Binary binary,string origin)
{
    public MobProfile Profile=>profile;
    public string Origin=>origin;
    public void ValidateLive(Func<ulong,int,byte[]> read,ulong mb)
    {
        var b=binary;var p=profile;
        int pe=BitConverter.ToInt32(read(mb+0x3C,4));
        if(pe<0x40||pe>0x1000||!read(mb+(uint)pe,4).SequenceEqual(new byte[]{0x50,0x45,0,0})||
            BitConverter.ToUInt16(read(mb+(uint)pe+4,2))!=0x8664||
            BitConverter.ToInt32(read(mb+(uint)pe+8,4))!=p.Player.PeTimestamp||
            BitConverter.ToInt32(read(mb+(uint)pe+80,4))!=p.Player.ImageSize)throw new IOException("Loaded PE header/profile mismatch");
        foreach(var s in p.Signatures.Values.Concat(p.Player.Signatures.Values))
        {
            int len=Binary.Pattern(s.Pattern).Length;
            if(!read(mb+(uint)s.Rva,len).SequenceEqual(b.At(s.Rva,len)))throw new IOException($"Live kod değişti: +{s.Rva:X}; TP kapalı.");
        }
        foreach(var (vt,name) in new[]{(p.MonsterVtableRva,".?AVCTClientMonster@@"),(p.Player.CtclientgameVtableRva,".?AVCTClientGame@@"),(p.Player.CtclientcharVtableRva,".?AVCTClientChar@@")})
        {
            int col=checked((int)(b.I64(vt-8)-b.ImageBase));int td=b.I32(col+12);
            if(BitConverter.ToUInt64(read(mb+(uint)vt-8,8))!=mb+(uint)col||
                !read(mb+(uint)col,24).SequenceEqual(b.At(col,24))||
                !read(mb+(uint)td+16,name.Length+1).SequenceEqual(System.Text.Encoding.ASCII.GetBytes(name+"\0")))throw new IOException("Live RTTI/profile mismatch");
            foreach(int slot in vt==p.Player.CtclientgameVtableRva?new[]{0}:new[]{0,0xB0,0x450})
            {
                long target=b.I64(vt+slot)-b.ImageBase;
                if(target<0||target>=p.Player.ImageSize||BitConverter.ToUInt64(read(mb+(uint)(vt+slot),8))!=mb+(ulong)target)throw new IOException("Live vtable mismatch");
            }
        }
    }
}

static class ProfileStore
{
    public static string Root=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"MobTP");
    public static string DirectoryPath=>Path.Combine(Root,"Profiles");
    static readonly JsonSerializerOptions Json=new(){WriteIndented=true};
    static readonly Dictionary<string,ProfileBundle> Cache=[];
    static readonly Dictionary<string,string> Failed=[];
    public static void Retry(){lock(Cache)Failed.Clear();}
    public static int ScanCount {get;private set;}
    public static MobProfile? Embedded(Binary b)
    {
        using var stream=typeof(ProfileStore).Assembly.GetManifestResourceStream("MobTP.current-profile.json")!;
        var p=JsonSerializer.Deserialize<MobProfile>(stream)!;
        if(p.Player.Sha256!=b.Sha)return null;
        Validate(b,p);return p;
    }
    public static Dictionary<string,Signature> Templates()
    {
        using var stream=typeof(ProfileStore).Assembly.GetManifestResourceStream("MobTP.signatures.json")!;
        return JsonSerializer.Deserialize<Dictionary<string,Signature>>(stream)!;
    }
    public static MobProfile Resolve(Binary b)
    {
        ScanCount++;var p=new MobProfile{Player=PlayerXYZ.Profiles.Resolve(b)};
        foreach(var (name,s) in Templates().Where(k=>k.Key!="registry_shape"))p.Signatures[name]=s with{Rva=b.FindUnique(s.Pattern)};
        int host=p.Signatures["host"].Rva;
        p.Signatures["registry_shape"]=Templates()["registry_shape"] with{Rva=b.RelativeCall(host+0x38)};
        Derive(b,p,true);Validate(b,p);return p;
    }
    static void Derive(Binary b,MobProfile p,bool assign)
    {
        int root=p.Signatures["root_a"].Rva,root2=p.Signatures["root_b"].Rva,host=p.Signatures["host"].Rva,
            ctor=p.Signatures["constructor"].Rva,home=p.Signatures["home"].Rva,factory=p.Signatures["factory"].Rva;
        int rootRva=checked(root+7+b.I32(root+3)),rootOther=checked(root2+7+b.I32(root2+3));
        int lookup=b.RelativeCall(host+0x38),registry=b.I32(lookup+3),id=b.I32(host+0xD9),type=b.I32(ctor+0x98),size=b.I32(factory+0x15),goal=b.I32(home+0x4A);
        int vt=checked(ctor+0x15+b.I32(ctor+0x11));
        if(rootRva!=rootOther||rootRva<0||rootRva>p.Player.ImageSize-8||b.Executable(rootRva,8)||lookup!=p.Signatures["registry_shape"].Rva||
            b.RelativeCall(factory+6)!=lookup||b.RelativeCall(factory+0x2A)!=ctor||
            b.I32(home+0x54)!=goal+8||b.I32(ctor+0xB4)!=goal||b.I32(ctor+0xBA)!=goal+8)throw new IOException("Root/registry/constructor/Home ilişkisi uyuşmuyor.");
        b.ValidateRtti(vt,".?AVCTClientMonster@@");
        foreach(int slot in new[]{0xB0,0x450})if(b.I64(vt+slot)!=b.I64(p.Player.CtclientcharVtableRva+slot))throw new IOException("Monster/player coordinate method ilişkisi değişti.");
        if(size<0x100||size>0x20000||registry<0x100||registry>0x10000||registry%8!=0||id<0x100||id>size-4||type<0x100||type>=size||goal<0x100||goal>size-12||goal%4!=0)throw new IOException("Geçersiz mob layout.");
        foreach(int coordinate in new[]{p.Player.CoordinateA[0],p.Player.CoordinateB[0]})
            if(coordinate>size-12||coordinate<goal+12&&goal<coordinate+12||coordinate<id+4&&id<coordinate+12||type>=coordinate&&type<coordinate+12)throw new IOException("Yazma alanı overlap/bounds hatası.");
        if(assign){p.RootRva=rootRva;p.RegistryOffset=registry;p.ActorIdOffset=id;p.ActorTypeOffset=type;p.ActorSize=size;p.HomeOffset=goal;p.MonsterVtableRva=vt;}
        else if(p.RootRva!=rootRva||p.RegistryOffset!=registry||p.ActorIdOffset!=id||p.ActorTypeOffset!=type||p.ActorSize!=size||p.HomeOffset!=goal||p.MonsterVtableRva!=vt)throw new IOException("Profil offsetleri instruction operandlarıyla uyuşmuyor.");
    }
    public static void Validate(Binary b,MobProfile p)
    {
        if(p.Version!=1)throw new IOException("Profile version mismatch");
        PlayerXYZ.Profiles.Validate(b,p.Player);
        foreach(var (name,t) in Templates())
        {
            if(!p.Signatures.TryGetValue(name,out var s)||s.Pattern!=t.Pattern||!b.Executable(s.Rva,Binary.Pattern(t.Pattern).Length)||!Binary.Match(b.At(s.Rva,Binary.Pattern(t.Pattern).Length),Binary.Pattern(t.Pattern)))throw new IOException("Mob imzası uyuşmuyor: "+name);
        }
        Derive(b,p,false);
    }
    public static ProfileBundle Load(string path)
    {
        var b=new Binary(path);
        lock(Cache)
        {
            if(Cache.TryGetValue(b.Sha,out var cached)){b.Dispose();return cached;}
            if(Failed.TryGetValue(b.Sha,out var failure)){b.Dispose();throw new IOException(failure);}
            try
            {
                Directory.CreateDirectory(DirectoryPath);string file=Path.Combine(DirectoryPath,b.Sha+".json");MobProfile? p=null;string origin="AOB ile çözüldü";
                if(File.Exists(file))try{p=JsonSerializer.Deserialize<MobProfile>(File.ReadAllText(file));if(p is null)throw new IOException("Empty profile");Validate(b,p);origin="Kayıtlı SHA profili";}catch(Exception ex){Log("Profil reddedildi; yeniden çözüm: "+ex.Message);p=null;}
                if(p is null)
                {
                    p=Embedded(b);if(p is not null)origin="Paket SHA profili";else p=Resolve(b);
                    string temp=file+".tmp";File.WriteAllText(temp,JsonSerializer.Serialize(p,Json));File.Move(temp,file,true);
                }
                var result=new ProfileBundle(p,b,origin);Cache.Add(b.Sha,result);Log(origin+" "+b.Sha);return result;
            }
            catch(Exception ex){Failed[b.Sha]="Profil çözülemedi; TP kapalı: "+ex.Message;Log(Failed[b.Sha]);b.Dispose();throw new IOException(Failed[b.Sha],ex);}
        }
    }
    public static void Log(string message)
    {
        try{Directory.CreateDirectory(Path.Combine(Root,"Logs"));File.AppendAllText(Path.Combine(Root,"Logs","profile.log"),$"{DateTimeOffset.Now:O} {message}\n");}catch(IOException){}
    }
}
