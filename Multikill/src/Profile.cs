using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text.Json;

namespace Multikill;

sealed record Signature(string Sha256, int ImageSize, int Timestamp, int SignatureRva, int PatchOffset, string Aob, string Original, string Patched)
{
    internal int PatchRva => SignatureRva + PatchOffset;
    internal byte[] Pattern => Convert.FromHexString(Aob);
}
static class Profile
{
    internal const string GamePath = @"C:\Games\4Unity\TClient.exe";
    internal static readonly Signature Known = JsonSerializer.Deserialize<Signature>(typeof(Profile).Assembly.GetManifestResourceStream("Multikill.profile")!, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
    internal static byte[] Pattern => Convert.FromHexString(Known.Aob);
    internal static byte[] Original => Convert.FromHexString(Known.Original);
    internal static byte[] Patched => Convert.FromHexString(Known.Patched);
    internal static int PatchRva => Known.SignatureRva + Known.PatchOffset;
    internal static string CacheDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "4UnityMultikill", "profiles");
    internal static int ScanCount { get; private set; }
    internal static bool LastCacheHit { get; private set; }
    sealed record CachedProfile(int Version, Signature Build);
    internal static Signature Resolve(byte[] disk, string? cacheDirectory = null, bool forceScan = false)
    {
        string directory = cacheDirectory ?? CacheDirectory;
        string sha = Convert.ToHexString(SHA256.HashData(disk));
        LastCacheHit = false;
        try
        {
            if (forceScan) throw new IOException("Tam tarama istendi.");
            var saved = JsonSerializer.Deserialize<CachedProfile>(File.ReadAllText(Path.Combine(directory, sha + ".json")));
            if (saved is not { Version: 1 } || saved.Build.Sha256 != sha) throw new IOException("Profil sürümü/SHA uyuşmuyor.");
            var candidate = BuildRecovery.Resolve(disk, saved.Build.SignatureRva);
            // Recheck the bounded semantic path, operands, PE identity and exact disk bytes.
            // The known profile includes one extra trailing byte beyond the descriptor.
            if (sha == Known.Sha256)
            {
                if (saved.Build != Known || candidate.PatchRva != Known.PatchRva || !Known.Pattern.AsSpan(0, candidate.Pattern.Length).SequenceEqual(candidate.Pattern)) throw new IOException("Bilinen profil değişmiş.");
            }
            else if (candidate != saved.Build) throw new IOException("Kayıtlı profil kodla uyuşmuyor.");
            LastCacheHit = true; return saved.Build;
        }
        catch (Exception ex) when (ex is IOException or JsonException or ArgumentException or InvalidOperationException or BadImageFormatException or NullReferenceException) { }
        ScanCount++;
        Signature result;
        if (sha == Known.Sha256) { Validate(disk); result = Known; }
        else result = BuildRecovery.Resolve(disk);
        SaveValidated(result, directory); return result;
    }
    internal static void SaveValidated(Signature build, string directory)
    {
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, build.Sha256 + ".json"), temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllText(temp, JsonSerializer.Serialize(new CachedProfile(1, build))); File.Move(temp, path, true); }
        finally { if (File.Exists(temp)) File.Delete(temp); }
        // Only disk-derived profiles are persisted. PID, module base and heap pointers never are.
    }
    internal static byte[] VerifyDisk()
    {
        byte[] disk = File.ReadAllBytes(GamePath); Validate(disk); return disk;
    }
    internal static void Validate(byte[] disk)
    {
        if (Convert.ToHexString(SHA256.HashData(disk)) != Known.Sha256) throw new IOException("TClient SHA değişmiş; yeni profil analizi gerekli. Patch uygulanmadı.");
        using var pe = new PEReader(new MemoryStream(disk)); var h = pe.PEHeaders;
        if ((int)h.CoffHeader.Machine != 0x8664 || h.CoffHeader.TimeDateStamp != Known.Timestamp || h.PEHeader?.SizeOfImage != Known.ImageSize) throw new IOException("PE profil kimliği uyuşmuyor.");
        int found = -1, matches = 0;
        foreach (var s in h.SectionHeaders.Where(s => ((uint)s.SectionCharacteristics & 0x20000000) != 0))
        {
            var data = disk.AsSpan(s.PointerToRawData, s.SizeOfRawData); int offset = 0;
            while (offset <= data.Length - Pattern.Length)
            {
                int hit = data[offset..].IndexOf(Pattern); if (hit < 0) break;
                found = s.VirtualAddress + offset + hit; matches++; offset += hit + 1;
            }
        }
        if (matches != 1 || found != Known.SignatureRva) throw new IOException("AOB tekil değil veya doğrulanmış RVA ile uyuşmuyor.");
        if (!Pattern.AsSpan(Known.PatchOffset, 6).SequenceEqual(Original)) throw new IOException("Patch profili tutarsız.");
    }
    internal static void ValidateLive(byte[] live, bool patched, Signature? build = null)
    {
        build ??= Known;
        var expected = build.Pattern;
        if (patched) Convert.FromHexString(build.Patched).CopyTo(expected, build.PatchOffset);
        if (!live.SequenceEqual(expected)) throw new IOException("Canlı AOB/branch değişmiş; işlem engellendi.");
    }
}
