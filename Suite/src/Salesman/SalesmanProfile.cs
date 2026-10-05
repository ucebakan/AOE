using Iced.Intel;
using PlayerXYZ;
using System.Text.Json;
using UnityTools.Controls;

namespace UnityTools.Salesman;

sealed record Anchor(string Pattern, int Rva);
sealed record Build
{
    public int Version { get; init; } = 1;
    public string Sha { get; init; } = "";
    public int ImageSize { get; init; }
    public int Timestamp { get; init; }
    public int Root { get; init; }
    public int OwnerVtable { get; init; }
    public int PlayerVtable { get; init; }
    public int ShopVtable { get; init; }
    public int PlayerOffset { get; init; }
    public int SessionOffset { get; init; }
    public int ShopOffset { get; init; }
    public int ContextOffset { get; init; }
    public int VisibleOffset { get; init; }
    public int CashOffset { get; init; }
    public int Sender { get; init; }
    public int Finder { get; init; }
    public int NpcRoot { get; init; }
    public int NpcTypeOffset { get; init; }
    public int CloseSite { get; init; }
    public byte[] SenderBytes { get; init; } = [];
    public byte[] FinderBytes { get; init; } = [];
    public byte[] CloseBytes { get; init; } = [];
    public Dictionary<string, Anchor> Anchors { get; init; } = [];
}

static class Profiles
{
    internal const ushort Npc = 22631;
    internal const string ArchiveSha = "B770838B6E1EB731792202A520AE1CCFC73CAE249B0CB46EDEA5272B38822142";
    internal static string DirectoryPath => Path.Combine(Program.DataRoot, "Salesman", "profiles");
    internal static int ScanCount { get; private set; }
    internal static bool Cached { get; private set; }
    static readonly object cacheGate = new();
    static string verifiedSha = "", verifiedText = "";
    internal static int ValidationCount { get; private set; }
    internal static readonly Dictionary<string, string> Patterns = new()
    {
        ["open"] = "48 8B 89 ?? ?? ?? ?? 48 85 C9 74 ?? BA 1F 56 00 00 E8 ?? ?? ?? ?? 33 C0",
        ["close"] = "40 53 48 83 EC 20 45 33 C0 B2 19 48 8B D9 E8 ?? ?? ?? ?? 45 33 C0 B2 0B 48 8B CB E8 ?? ?? ?? ?? 45 33 C0 B2 0C 48 8B CB E8 ?? ?? ?? ??",
        ["shop"] = "48 8B 83 ?? ?? ?? ?? 66 44 89 B8 ?? ?? ?? ?? E9 ?? ?? ?? ??",
        ["npc"] = "E8 ?? ?? ?? ?? 48 85 C0 74 ?? 80 78 ?? 02 74 ?? 45 33 C0 B2 0B 48 8B CB E8 ?? ?? ?? ??",
        ["cleanup"] = "48 8B 1F 48 8B CB E8 ?? ?? ?? ?? 66 44 89 BB ?? ?? ?? ?? E9 ?? ?? ?? ??",
        ["toggle"] = "48 8B 89 ?? ?? ?? ?? 48 85 C9 74 ?? 48 8B 01 FF 50 ?? 85 C0 74 ?? 45 33 C0 B2 16 48 8B CB E8 ?? ?? ?? ?? 33 C0 48 83 C4 20 5B C3 48 8B 8B ?? ?? ?? ?? 48 85 C9 74 ?? BA 1F 56 00 00 E8 ?? ?? ?? ??"
    };
    internal static Build Load(byte[] disk, bool force = false)
    {
        lock (cacheGate) return LoadCore(disk, force);
    }
    static Build LoadCore(byte[] disk, bool force)
    {
        using var binary = new Binary(disk); Cached = false;
        if (force) { verifiedSha = ""; verifiedText = ""; }
        string path = Path.Combine(DirectoryPath, binary.Sha + ".json");
        if (!force)
        {
            try
            {
                string text = File.ReadAllText(path);
                var p = JsonSerializer.Deserialize<Build>(text) ?? throw new IOException("Boş Salesman profili.");
                // Hash the current EXE every time. Reuse semantic proof only for
                // the same exact SHA and unchanged JSON, returning a fresh object.
                if (verifiedSha != binary.Sha || verifiedText != text)
                { Validate(binary, p); ValidationCount++; verifiedSha = binary.Sha; verifiedText = text; }
                Cached = true; return p;
            }
            catch (Exception ex) when (ex is IOException or JsonException or ArgumentException or InvalidOperationException) { }
        }
        ScanCount++; var result = Resolve(binary);
        Directory.CreateDirectory(DirectoryPath);
        string resolvedText = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path + ".tmp", resolvedText);
        File.Move(path + ".tmp", path, true); verifiedSha = binary.Sha; verifiedText = resolvedText; return result;
    }
    internal static Build Resolve(Binary binary, Dictionary<string, Anchor>? saved = null, int shopHint = 0)
    {
        using var image = new RecoveryImage(binary.Data);
        var anchors = new Dictionary<string, Anchor>();
        foreach (var (name, pattern) in Patterns)
        {
            int rva = saved is null ? binary.FindUnique(pattern) : saved[name].Rva;
            if (saved is not null && saved[name].Pattern != pattern) throw new IOException("Salesman imza şeması değişmiş.");
            if (!binary.Executable(rva, Binary.Pattern(pattern).Length) || !Binary.Match(binary.At(rva, Binary.Pattern(pattern).Length), Binary.Pattern(pattern)))
                throw new IOException("Salesman imzası uyuşmuyor: " + name);
            anchors[name] = new(pattern, rva);
        }
        var xyz = PlayerXYZ.Profiles.Load(binary);
        int open = anchors["open"].Rva, close = anchors["close"].Rva + 19, shop = anchors["shop"].Rva, npc = anchors["npc"].Rva;
        int sender = image.Call(open + 17), finder = image.Call(npc), disable = image.Call(close + 8);
        if (image.Call(close + 21) != disable || image.Call(npc + 24) != disable || image.FunctionRoot(shop) != image.FunctionRoot(disable))
            throw new IOException("Salesman UI kapatma / NPC doğrulama bağlantısı değişmiş.");
        var senderRange = image.Function(sender);
        if (senderRange.Start != sender || senderRange.End - sender > 128 || senderRange.End - sender < 40) throw new IOException("Salesman sender sınırı değişmiş.");
        var si = image.FunctionInstructions(sender);
        var packet = si.Where(i => i.Mnemonic == Mnemonic.Mov && RecoveryImage.Reg(i, 0, Register.EDX) && i.Op1Kind == OpKind.Immediate32 && i.Immediate32 == 0x506c).ToArray();
        if (packet.Length != 1 || si.Count(i => i.Mnemonic == Mnemonic.Call) != 5 ||
            !si.Any(i => i.Mnemonic == Mnemonic.Movzx && RecoveryImage.Reg(i, 0, Register.EBX) && RecoveryImage.Reg(i, 1, Register.DX)))
            throw new IOException("Salesman WORD NPC / 0x506c istek fingerprint'i değişmiş.");
        // This leaf has no .pdata entry. Its complete bounded tree fingerprint
        // establishes both returns and the supported layout without unwind data.
        const string tree = "4C 8B 05 ?? ?? ?? ?? 49 8B D0 49 8B 40 08 80 78 19 00 75 18 66 39 48 20 73 06 48 83 C0 10 EB 03 48 8B D0 48 8B 00 80 78 19 00 74 E8 80 7A 19 00 75 06 66 3B 4A 20 73 03 49 8B D0 49 3B D0 75 03 33 C0 C3 48 8B 42 28 C3";
        int finderLength = Binary.Pattern(tree).Length;
        if (!Binary.Match(binary.At(finder, finderLength), Binary.Pattern(tree))) throw new IOException("NPC resource ağaç şeması değişmiş.");
        var fi = image.Decode(finder, finderLength);
        if (fi.Length == 0 || !fi[0].IsIPRelativeMemoryOperand || !RecoveryImage.Reg(fi[0], 0, Register.R8)) throw new IOException("NPC resource kökü doğrulanamadı.");
        if (finderLength is < 40 or > 128 || !fi.Any(i => i.Mnemonic == Mnemonic.Mov && RecoveryImage.Reg(i, 0, Register.RAX) && RecoveryImage.Mem(i, 1, Register.RDX) && i.MemoryDisplacement64 == 0x28))
            throw new IOException("NPC lookup dönüşü değişmiş.");
        int shopVtable = shopHint == 0 ? binary.FindVtable(".?AVCTShopDlg@@") : shopHint;
        binary.ValidateRtti(shopVtable, ".?AVCTShopDlg@@");
        int toggle = anchors["toggle"].Rva;
        var virtualCall = image.Decode(toggle + 15, 3).Single();
        if (virtualCall.Mnemonic != Mnemonic.Call || virtualCall.Op0Kind != OpKind.Memory || virtualCall.MemoryBase != Register.RAX || virtualCall.MemoryDisplacement64 % 8 != 0 || virtualCall.MemoryDisplacement64 > 0x1000 || image.Call(toggle + 60) != sender || image.Call(toggle + 30) != disable || binary.I32(toggle + 46) != binary.I32(open + 3))
            throw new IOException("Shop visibility metot-slot bağlantısı değişmiş.");
        int getter = checked((int)(binary.I64(shopVtable + (int)virtualCall.MemoryDisplacement64) - binary.ImageBase));
        var getterInstructions = image.Decode(getter, 7);
        if (getterInstructions.Length != 2 || getterInstructions[0].Mnemonic != Mnemonic.Mov || !RecoveryImage.Reg(getterInstructions[0], 0, Register.EAX) || !RecoveryImage.Mem(getterInstructions[0], 1, Register.RCX) || getterInstructions[1].Mnemonic != Mnemonic.Ret)
            throw new IOException("Shop IsVisible getter değişmiş.");
        int cleanup = image.Call(anchors["cleanup"].Rva + 6);
        if (image.FunctionRoot(anchors["cleanup"].Rva) != image.FunctionRoot(disable) || binary.I32(anchors["cleanup"].Rva + 15) != binary.I32(shop + 11))
            throw new IOException("Shop context temizleme bağlantısı değişmiş.");
        // Find the cash-opening field in the shop's cleanup method by its free/null pair.
        var cashFields = image.FunctionInstructions(cleanup).Where(i => i.Mnemonic == Mnemonic.Mov && i.Op0Kind == OpKind.Memory && i.MemoryBase == Register.RBX && (i.Op1Kind is OpKind.Immediate32 or OpKind.Immediate32to64) && i.GetImmediate(1) == 0).Select(i => (int)i.MemoryDisplacement64).ToArray();
        int cashOffset = RecoveryImage.Unique(cashFields, "shop cash cleanup field");
        var result = new Build
        {
            Sha = binary.Sha, ImageSize = binary.Pe.PEHeaders.PEHeader!.SizeOfImage, Timestamp = binary.Pe.PEHeaders.CoffHeader.TimeDateStamp,
            Root = xyz.RootRva, OwnerVtable = xyz.CtclientgameVtableRva, PlayerVtable = xyz.CtclientcharVtableRva, PlayerOffset = xyz.OwnerToPlayerOffset,
            ShopVtable = shopVtable, SessionOffset = binary.I32(open + 3), ShopOffset = binary.I32(shop + 3), ContextOffset = binary.I32(shop + 11),
            VisibleOffset = (int)getterInstructions[0].MemoryDisplacement64, CashOffset = cashOffset, Sender = sender, Finder = finder,
            NpcRoot = checked((int)fi[0].IPRelativeMemoryAddress), NpcTypeOffset = binary.At(npc + 12, 1)[0], CloseSite = close,
            SenderBytes = binary.At(sender, senderRange.End - sender), FinderBytes = binary.At(finder, finderLength), CloseBytes = binary.At(close, 26), Anchors = anchors
        };
        foreach (int offset in new[] { result.PlayerOffset, result.SessionOffset, result.ShopOffset, result.ContextOffset, result.VisibleOffset, result.CashOffset })
            if (!RecoveryImage.Field((ulong)offset)) throw new IOException("Salesman alan operandı sınır dışı.");
        if (result.CashOffset + 8 != result.ContextOffset || image.FunctionRoot(open) != image.Function(open).Start || !binary.Executable(result.Sender) || binary.Executable(result.NpcRoot))
            throw new IOException("Salesman alan / metot semantiği uyuşmuyor.");
        return result;
    }
    internal static void Validate(Binary binary, Build build)
    {
        if (build.Version != 1 || build.Sha != binary.Sha || build.Anchors.Count != Patterns.Count) throw new IOException("Salesman profil SHA/sürüm uyuşmuyor.");
        var derived = Resolve(binary, build.Anchors, build.ShopVtable);
        if (JsonSerializer.Serialize(derived) != JsonSerializer.Serialize(build)) throw new IOException("Salesman profil operandları veya kod fingerprint'i uyuşmuyor.");
    }
}
