using PlayerXYZ;
using System.Text.Json;
using UnityTools.Controls;

namespace UnityTools.Collection;

// Collection shares the established registry signatures, with its own profile
// and the Suite's PlayerXYZ types. MobTP's private copy of PlayerXYZ is separate.
sealed class MobSchema
{
    public int Version { get; set; } = 1;
    public BuildProfile Player { get; set; } = new();
    public Dictionary<string, Signature> Signatures { get; set; } = [];
    public int RootRva { get; set; }
    public int RegistryOffset { get; set; }
    public int ActorIdOffset { get; set; }
    public int ActorTypeOffset { get; set; }
    public int ActorSize { get; set; }
    public int MonsterVtableRva { get; set; }
}
static class RegistryProfile
{
    internal static Dictionary<string, Signature> Templates()
    {
        using var stream=typeof(RegistryProfile).Assembly.GetManifestResourceStream("Collection.registry.json")!;
        return JsonSerializer.Deserialize<Dictionary<string,Signature>>(stream)!;
    }
    internal static MobSchema Resolve(Binary b)
    {
        var p=new MobSchema { Player=PlayerXYZ.Profiles.Resolve(b) };
        foreach(var (name,s) in Templates().Where(k=>k.Key!="registry_shape"))p.Signatures[name]=s with {Rva=b.FindUnique(s.Pattern)};
        p.Signatures["registry_shape"]=Templates()["registry_shape"] with {Rva=b.RelativeCall(p.Signatures["host"].Rva+0x38)};
        Derive(b,p,true);Validate(b,p);return p;
    }
    internal static void Validate(Binary b,MobSchema p)
    {
        if(p.Version!=1)throw new IOException("Collection registry profile version.");
        PlayerXYZ.Profiles.Validate(b,p.Player);
        foreach(var (name,t) in Templates())
            if(!p.Signatures.TryGetValue(name,out var s)||s.Pattern!=t.Pattern||!b.Executable(s.Rva,Binary.Pattern(t.Pattern).Length)||!Binary.Match(b.At(s.Rva,Binary.Pattern(t.Pattern).Length),Binary.Pattern(t.Pattern)))throw new IOException("Collection registry signature: "+name);
        Derive(b,p,false);
    }
    static void Derive(Binary b,MobSchema p,bool assign)
    {
        int root=p.Signatures["root_a"].Rva,root2=p.Signatures["root_b"].Rva,host=p.Signatures["host"].Rva,ctor=p.Signatures["constructor"].Rva,factory=p.Signatures["factory"].Rva;
        int rootRva=checked(root+7+b.I32(root+3)),other=checked(root2+7+b.I32(root2+3));
        int lookup=b.RelativeCall(host+0x38),registry=b.I32(lookup+3),id=b.I32(host+0xd9),type=b.I32(ctor+0x98),size=b.I32(factory+0x15),vt=checked(ctor+0x15+b.I32(ctor+0x11));
        if(rootRva!=other||rootRva!=p.Player.RootRva||rootRva<0||rootRva>p.Player.ImageSize-8||b.Executable(rootRva,8)||lookup!=p.Signatures["registry_shape"].Rva||b.RelativeCall(factory+6)!=lookup||b.RelativeCall(factory+0x2a)!=ctor)throw new IOException("Collection root / registry / constructor links mismatch.");
        b.ValidateRtti(vt,".?AVCTClientMonster@@");
        if(size<0x100||size>0x20000||registry<0x100||registry>0x10000||registry%8!=0||id<0x100||id>size-4||type<0x100||type>=size)throw new IOException("Collection actor schema bounds.");
        if(assign){p.RootRva=rootRva;p.RegistryOffset=registry;p.ActorIdOffset=id;p.ActorTypeOffset=type;p.ActorSize=size;p.MonsterVtableRva=vt;}
        else if(p.RootRva!=rootRva||p.RegistryOffset!=registry||p.ActorIdOffset!=id||p.ActorTypeOffset!=type||p.ActorSize!=size||p.MonsterVtableRva!=vt)throw new IOException("Collection registry profile operands mismatch.");
    }
}
