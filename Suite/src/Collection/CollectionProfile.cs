using Iced.Intel;
using PlayerXYZ;
using System.Text.Json;
using UnityTools.Controls;

namespace UnityTools.Collection;

sealed record Anchor(string Pattern, int Rva);
sealed record Code(int Rva, byte[] Bytes);
sealed record Build
{
    public int Version { get; init; } = 1;
    public string Sha { get; init; } = "";
    public MobSchema Mob { get; init; } = new();
    public int ImageSize { get; init; }
    public int Timestamp { get; init; }
    public int MaintainVtable { get; init; }
    public int SessionOffset { get; init; }
    public int ActionOffset { get; init; }
    public int HealthOffset { get; init; }
    public int DeadStateOffset { get; init; }
    public int GhostOffset { get; init; }
    public int ParentOffset { get; init; }
    public int ReverseParentOffset { get; init; }
    public int MaintainOffset { get; init; }
    public Dictionary<string, Anchor> Anchors { get; init; } = [];
    public Dictionary<string, Code> Codes { get; init; } = [];
}

static class Profiles
{
    internal static string DirectoryPath => Path.Combine(Program.DataRoot, "Collection", "profiles");
    internal static int ScanCount { get; private set; }
    internal static bool Cached { get; private set; }
    static readonly object sync = new();
    static string verifiedSha = "", verifiedText = "";
    internal static Dictionary<string, string> Patterns()
    {
        using var stream = typeof(Profiles).Assembly.GetManifestResourceStream("Collection.patterns.json")!;
        return JsonSerializer.Deserialize<Dictionary<string, string>>(stream)!;
    }
    internal static Build Load(byte[] disk, bool force = false)
    {
        lock (sync)
        {
            using var b = new Binary(disk); Cached = false;
            if (force) { verifiedSha = ""; verifiedText = ""; }
            string path = Path.Combine(DirectoryPath, b.Sha + ".json");
            if (!force)
            {
                try
                {
                    string text = File.ReadAllText(path); var saved = JsonSerializer.Deserialize<Build>(text) ?? throw new IOException("Boş Collection profili.");
                    if (verifiedSha != b.Sha || verifiedText != text) Validate(b, saved);
                    Cached = true; verifiedSha = b.Sha; verifiedText = text; return saved;
                }
                catch (Exception ex) when (ex is IOException or JsonException or ArgumentException or InvalidOperationException or KeyNotFoundException or NullReferenceException or OverflowException) { }
            }
            ScanCount++; var result = Resolve(b);
            Directory.CreateDirectory(DirectoryPath);
            string resolved = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path + ".tmp", resolved); File.Move(path + ".tmp", path, true);
            verifiedSha = b.Sha; verifiedText = resolved; return result;
        }
    }
    internal static Build Resolve(Binary b, Build? saved = null)
    {
        using var image = new RecoveryImage(b.Data);
        var anchors = new Dictionary<string, Anchor>();
        foreach (var (name, pattern) in Patterns())
        {
            int rva = saved is null ? b.FindUnique(pattern) : saved.Anchors[name].Rva;
            if (saved is not null && saved.Anchors[name].Pattern != pattern) throw new IOException("Collection imza şeması değişmiş.");
            if (!b.Executable(rva, Binary.Pattern(pattern).Length) || !Binary.Match(b.At(rva, Binary.Pattern(pattern).Length), Binary.Pattern(pattern))) throw new IOException("Collection imzası uyuşmuyor: " + name);
            anchors[name] = new(pattern, rva);
        }
        var mob = saved?.Mob ?? RegistryProfile.Resolve(b);
        RegistryProfile.Validate(b, mob);
        int maintainVtable = saved?.MaintainVtable ?? b.FindVtable(".?AVCTClientMaintain@@");
        b.ValidateRtti(maintainVtable, ".?AVCTClientMaintain@@");
        Instruction[] Decode(string name) => image.Decode(anchors[name].Rva, Binary.Pattern(anchors[name].Pattern).Length);
        int Field(Instruction i) => RecoveryImage.Field(i.MemoryDisplacement64) ? checked((int)i.MemoryDisplacement64) : throw new IOException("Collection alan sınırı geçersiz.");
        var sender = Decode("sender");
        int senderRva = anchors["sender"].Rva;
        if (sender.Count(i => i.Mnemonic == Mnemonic.Call) != 5 || sender.Last().Mnemonic != Mnemonic.Ret ||
            !sender.Any(i => i.Mnemonic == Mnemonic.Mov && RecoveryImage.Reg(i, 0, Register.EDX) && i.Op1Kind == OpKind.Immediate32 && i.Immediate32 == 0x512e) ||
            sender.Where(i => i.Mnemonic == Mnemonic.Mov && RecoveryImage.Reg(i, 0, Register.EBX) && RecoveryImage.Mem(i, 1, Register.RBX)).Select(Field).Single() != mob.ActorIdOffset)
            throw new IOException("Collection tek DWORD mob / 0x512e sender semantiği değişmiş.");
        foreach (var i in sender.Where(i => i.Mnemonic == Mnemonic.Call)) image.Call((int)i.IP);
        var manual = Decode("manual");
        var ownerTests = manual.Where(i => i.Mnemonic == Mnemonic.Cmp && RecoveryImage.Mem(i, 0, Register.RCX)).ToArray();
        var dispatch = Decode("dispatch");
        if (ownerTests.Length != 2 || image.FunctionRoot(anchors["manual"].Rva) != image.FunctionRoot(anchors["dispatch"].Rva) ||
            Field(dispatch[0]) != Field(ownerTests[0]) || image.Call(anchors["dispatch"].Rva + 7) != senderRva) throw new IOException("Collection normal GetAll / sender bağlantısı değişmiş.");
        int session = Field(ownerTests[1]);
        var death = Decode("death");
        var states = death.Where(i => i.Mnemonic == Mnemonic.Cmp && RecoveryImage.Mem(i, 0, Register.RCX)).ToArray();
        var actions = death.Where(i => i.Mnemonic == Mnemonic.Je).Take(2).ToArray();
        if (states.Length != 4 || actions.Length != 2 || actions[0].NearBranchTarget != actions[1].NearBranchTarget || Field(states[0]) != mob.ActorTypeOffset || states[0].GetImmediate(1) != 1) throw new IOException("Collection IsDead / actor tipi şeması değişmiş.");
        int action = Field(death.Single(i => i.Mnemonic == Mnemonic.Movzx && RecoveryImage.Reg(i, 0, Register.EAX) && RecoveryImage.Mem(i, 1, Register.RCX)));
        var parent = Decode("parent");
        int parentOffset = Field(parent.Single(i => i.Mnemonic == Mnemonic.Mov && RecoveryImage.Reg(i, 0, Register.RCX) && RecoveryImage.Mem(i, 1, Register.RCX)));
        int reverse = Field(parent.Single(i => i.Mnemonic == Mnemonic.Cmp && RecoveryImage.Mem(i, 0, Register.RCX) && RecoveryImage.Reg(i, 1, Register.RAX)));
        if (reverse + 8 != parentOffset) throw new IOException("Collection parent karşılıklı link şeması değişmiş.");
        var maintain = Decode("maintain");
        int maintainOffset = Field(maintain.Single(i => i.Mnemonic == Mnemonic.Mov && RecoveryImage.Reg(i, 0, Register.RSI) && RecoveryImage.Mem(i, 1, Register.RCX)));
        // The full signatures bind the supported MSVC tree and maintain->skill layout.
        if (!maintain.Any(i => i.Mnemonic == Mnemonic.Mov && RecoveryImage.Reg(i, 0, Register.RDX) && RecoveryImage.Mem(i, 1, Register.RCX) && i.MemoryDisplacement64 == 0x20)) throw new IOException("Collection maintain->skill pointer değişmiş.");
        int lookup = mob.Signatures["registry_shape"].Rva;
        var lookupPattern = RegistryProfile.Templates()["registry_shape"].Pattern;
        if (!Binary.Match(b.At(lookup, Binary.Pattern(lookupPattern).Length), Binary.Pattern(lookupPattern)) || b.I32(lookup + 3) != mob.RegistryOffset) throw new IOException("Collection registry lookup bağlantısı değişmiş.");
        var codes = new Dictionary<string, Code>();
        foreach (string name in new[] { "sender", "death", "maintain", "parent", "manual", "dispatch" }) { int length = Binary.Pattern(anchors[name].Pattern).Length; if (length > 128) throw new IOException("Collection kod guard boyutu."); codes[name] = new(anchors[name].Rva, b.At(anchors[name].Rva, length)); }
        codes["lookup"] = new(lookup, b.At(lookup, Binary.Pattern(lookupPattern).Length));
        foreach (var (name, signature) in mob.Signatures.Concat(mob.Player.Signatures.Select(k => new KeyValuePair<string, PlayerXYZ.Signature>("player_" + k.Key, k.Value))))
            codes["schema_" + name] = new(signature.Rva, b.At(signature.Rva, Binary.Pattern(signature.Pattern).Length));
        var result = new Build { Sha = b.Sha, Mob = mob, ImageSize = b.Pe.PEHeaders.PEHeader!.SizeOfImage, Timestamp = b.Pe.PEHeaders.CoffHeader.TimeDateStamp, MaintainVtable = maintainVtable,
            SessionOffset = session, ActionOffset = action, HealthOffset = Field(states[1]), DeadStateOffset = Field(states[2]), GhostOffset = Field(states[3]), ParentOffset = parentOffset, ReverseParentOffset = reverse, MaintainOffset = maintainOffset, Anchors = anchors, Codes = codes };
        foreach (int offset in new[] { action, result.HealthOffset, result.DeadStateOffset, result.GhostOffset }) if (offset >= mob.ActorSize - 8) throw new IOException("Collection ortak actor alanı boyut dışı.");
        // Parent and maintain belong to CTClientChar, which is larger than a monster.
        // Their displacement bounds and reciprocal link are validated above.
        return result;
    }
    internal static void Validate(Binary b, Build saved)
    {
        if (saved.Version != 1 || saved.Sha != b.Sha || saved.Anchors.Count != Patterns().Count) throw new IOException("Collection profil SHA/sürüm değişmiş.");
        if (JsonSerializer.Serialize(Resolve(b, saved)) != JsonSerializer.Serialize(saved)) throw new IOException("Collection profil operand/kod fingerprint uyuşmuyor.");
    }
}
