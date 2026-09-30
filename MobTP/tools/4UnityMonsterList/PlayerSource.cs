using System.Text.Json;

namespace UnityMonsterList;

// Reuse PlayerXYZ's profile validation, not its writer/session implementation.
static class PlayerSource
{
    static PlayerXYZ.BuildProfile? profile;
    public static void Test()
    {
        var p=Load(@"C:\Games\4Unity\TClient.exe");
        if(p.OwnerToPlayerOffset!=0x2710 || !p.CoordinateA.SequenceEqual(new[]{0x70,0x74,0x78}) || !p.CoordinateB.SequenceEqual(new[]{0xB0,0xB4,0xB8}))throw new Exception("PlayerXYZ layout mismatch");
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"player-profile-test.json"),JsonSerializer.Serialize(new{passed=true,disk_profile_verified=true,live_memory_verified=false,sha=p.Sha256,owner_offset=p.OwnerToPlayerOffset,a=p.CoordinateA,b=p.CoordinateB}));
    }
    public static PlayerXYZ.BuildProfile Load(string path)
    {
        if(profile is not null)return profile;
        using var binary=new PlayerXYZ.Binary(path);
        using var source=typeof(PlayerSource).Assembly.GetManifestResourceStream("PlayerXYZ.current-profile.json")!;
        var candidate=JsonSerializer.Deserialize<PlayerXYZ.BuildProfile>(source,PlayerXYZ.Files.Json)??throw new IOException("PlayerXYZ profile missing");
        PlayerXYZ.Profiles.Validate(binary,candidate);return profile=candidate;
    }
    public static bool ValidateLive(Rpm rpm,long moduleBase,PlayerXYZ.BuildProfile profile)
    {
        foreach(var signature in profile.Signatures.Values)
        {
            var pattern=PlayerXYZ.Binary.Pattern(signature.Pattern);
            byte[]? live=rpm.Bytes(moduleBase+signature.Rva,pattern.Length);
            if(live is null || !PlayerXYZ.Binary.Match(live,pattern))return false;
        }
        return true;
    }
}
