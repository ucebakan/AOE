using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Text.Json;

namespace InvisibleAggro;

static class Profile
{
    static byte[]? cached;
    static long cachedLength, cachedTime;
    static bool verified;
    public static bool NeedsApproval { get; private set; }
    public static string ActiveSha { get; private set; } = Sha;
    public static int ImageSize { get; private set; } = 0xF48000;
    public static int ScanCount { get; private set; }
    public static string DirectoryPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "4UnityInvisibleAggro", "profiles");
    record SavedProfile(int Version, string Sha, int[] Rvas, long Vtable);
    public static string GamePath = @"C:\Games\4Unity\TClient.exe";
    public const string Sha = "9CD77CD0C305359A3048D60C3C2FD837B907E2E61BBF8B828BFCC8E5270E5C28";
    public static long Context { get; private set; } = 0xE6E9C0;
    public static long Player {get;private set;} = 0x2710;
    public static long ActorId {get;private set;} = 0x768;
    public static long ActorType {get;private set;} = 0x7E1;
    public static long Vtable { get; private set; } = 0xCDBCC0;
    public static long Stealth {get;private set;} = 0x7E2;
    public static long Visual {get;private set;} = 0x47E;
    public static long Writer1 => Signatures[0].Rva + 20;
    public static long Writer2 => Signatures[1].Rva + 13;
    public static byte[] Original1 {get;private set;} = Convert.FromHexString("88977E040000");
    public static byte[] Original2 {get;private set;} = Convert.FromHexString("C6877E04000055");
    static readonly (int Rva, string Pattern, string Mask)[] BaseSignatures = [
        (0x86DEA3, "69 8F CC 08 00 00 FF 00 00 00 B8 1F 85 EB 51 F7 E1 C1 EA 08 88 97 7E 04 00 00 E8 00 00 00 00 41 BE F4 01 00 00", "xxxxxxxxxxxxxxxxxxxxxxxxxxx????xxxxxx"),
        (0x86DFF3, "3C 03 74 1B 3C 01 75 0E 80 F9 80 73 19 C6 87 7E 04 00 00 55 EB 10 3C 07 75 0C 80 F9 FF 74 07 C6 87 7E 04 00 00 FF", "xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx"),
        (0x86DE04, "80 BF E2 07 00 00 00 74 00 80 BF D6 07 00 00 00 74 00 48 8B 07 48 8B CF FF 90 40 02 00 00", "xx????xx?xx????xx?xxxxxxxxxxxx"),
        (0x7A09AA, "41 38 87 D7 07 00 00 0F 85 00 00 00 00 41 38 87 E2 07 00 00 74 00 41 0F B6 8F E1 07 00 00", "xxx????xx????xxx????x?xxxx????"),
        (0x7C1A20, "48 8B 05 00 00 00 00 C3 CC CC CC CC CC CC CC CC 41 B0 FF 80 FA 85 77 51", "xxx????xxxxxxxxxxxxxxxxx")
    ];
    public static readonly (int Rva,string Pattern,string Mask)[] Signatures=CreateSignatures();
    static (int,string,string)[] CreateSignatures()
    {
        var list=BaseSignatures.ToList();
        var s=list[0];char[] mask=s.Mask.ToCharArray();for(int i=2;i<6;i++)mask[i]='?';list[0]=(s.Rva,s.Pattern,new(mask));
        s=list[2];mask=s.Mask.ToCharArray();for(int i=25;i<29;i++)mask[i]='?';list[2]=(s.Rva,s.Pattern,new(mask));
        string owner="4D 8B 8F ?? ?? ?? ?? 4D 8B 87 ?? ?? ?? ?? 48 8D 55 E0 49 8B 8F ?? ?? ?? ?? E8 ?? ?? ?? ?? 48 8D 55 F0 49 8B 8F ?? ?? ?? ?? E8 ?? ?? ?? ??";
        void Add(string p){var tokens=p.Split(' ');list.Add((0,string.Join(" ",tokens.Select(t=>t=="??"?"00":t)),string.Concat(tokens.Select(t=>t=="??"?"?":"x"))));}
        Add(owner);
        using var json=JsonDocument.Parse(typeof(Profile).Assembly.GetManifestResourceStream("Recovery.mob.json")!);
        Add(json.RootElement.GetProperty("constructor").GetProperty("Pattern").GetString()!);
        Add(json.RootElement.GetProperty("host").GetProperty("Pattern").GetString()!);
        return list.ToArray();
    }

    public static byte[] VerifyDisk(bool force = false)
    {
        var info = new FileInfo(GamePath);
        if (force || cached is null || info.Length != cachedLength || info.LastWriteTimeUtc.Ticks != cachedTime)
        {
            cached = File.ReadAllBytes(GamePath);
            ActiveSha = Convert.ToHexString(SHA256.HashData(cached));
            cachedLength = info.Length; cachedTime = info.LastWriteTimeUtc.Ticks;
            verified = false; NeedsApproval = false;
        }
        if (verified) return cached;
        byte[] image = cached;
        using var pe = new PEReader(new MemoryStream(image));
        if ((int)pe.PEHeaders.CoffHeader.Machine != 0x8664 || pe.PEHeaders.PEHeader is null)
            throw new InvalidOperationException("TClient PE kimliği uyuşmuyor.");
        ImageSize = pe.PEHeaders.PEHeader.SizeOfImage;
        if (ActiveSha == Sha)
        {
            SetRvas(FindSites(image));
            Vtable = 0xCDBCC0;
        }
        else
        {
            try
            {
                var saved = JsonSerializer.Deserialize<SavedProfile>(File.ReadAllText(Path.Combine(DirectoryPath, ActiveSha + ".json")));
                if (saved is null || saved.Version != 1 || saved.Sha != ActiveSha || saved.Rvas.Length != Signatures.Length) throw new IOException();
                SetRvas(saved.Rvas);
                Vtable = saved.Vtable;
                ValidateSites(image);
            }
            catch
            {
                SetRvas(FindSites(image));
                Vtable=FindVtable(image);
                NeedsApproval = true; // Candidate is saved only after live validation.
            }
        }
        ValidateSites(image);
        verified = true;
        return image;
    }

    static void SetRvas(int[] rvas)
    {
        for (int i = 0; i < Signatures.Length; i++) Signatures[i] = (rvas[i], Signatures[i].Pattern, Signatures[i].Mask);
    }

    static void ValidateSites(byte[] image)
    {
        foreach (var sig in Signatures)
        {
            byte[] expected = Convert.FromHexString(sig.Pattern.Replace(" ", ""));
            byte[] actual = At(image, sig.Rva, expected.Length);
            if (actual.Where((b, i) => sig.Mask[i] != '?' && b != expected[i]).Any()) throw new InvalidOperationException("Profil imzası uyuşmuyor.");
        }
        Player=BitConverter.ToInt32(At(image,Signatures[5].Rva+21,4));
        ActorType=BitConverter.ToInt32(At(image,Signatures[6].Rva+0x98,4));
        ActorId=BitConverter.ToInt32(At(image,Signatures[7].Rva+0xD9,4));
        Stealth=BitConverter.ToInt32(At(image,Signatures[2].Rva+2,4));
        Visual=BitConverter.ToInt32(At(image,Writer1+2,4));
        if(Player!=BitConverter.ToInt32(At(image,Signatures[5].Rva+37,4)) ||
            !new[]{Player,ActorType,ActorId,Stealth,Visual}.All(x=>UnityTools.Controls.RecoveryImage.Field((ulong)x)) ||
            BitConverter.ToInt32(At(image,Writer2+2,4))!=Visual ||
            !At(image, Signatures[3].Rva + 16, 4).SequenceEqual(BitConverter.GetBytes((int)Stealth)) ||
            !At(image, Signatures[3].Rva + 26, 4).SequenceEqual(BitConverter.GetBytes((int)ActorType)) || Stealth!=ActorType+1)
            throw new InvalidOperationException("Oyuncu alanlarının yapısı değişmiş; yeni analiz gerekiyor.");
        Original1=At(image,Writer1,6);Original2=At(image,Writer2,7);
        Context = Signatures[4].Rva + 7L + BitConverter.ToInt32(At(image, Signatures[4].Rva + 3, 4));
        using var pe = new PEReader(new MemoryStream(image));
        if(Context<0||Context>=pe.PEHeaders.PEHeader!.SizeOfImage-8||
            !pe.PEHeaders.SectionHeaders.Any(s=>Context>=s.VirtualAddress&&Context+8<=(long)s.VirtualAddress+Math.Max(s.VirtualSize,s.SizeOfRawData)&&((uint)s.SectionCharacteristics&0x20000000)==0))
            throw new InvalidOperationException("Context root is outside data sections.");
        long imageBase = checked((long)pe.PEHeaders.PEHeader!.ImageBase);
        long col = BitConverter.ToInt64(At(image, Vtable - 8, 8)) - imageBase;
        byte[] locator = At(image, col, 24);
        long descriptor = BitConverter.ToUInt32(locator, 12);
        if (BitConverter.ToUInt32(locator) != 1 || BitConverter.ToUInt32(locator, 4) != 0 || BitConverter.ToUInt32(locator, 20) != col ||
            System.Text.Encoding.ASCII.GetString(At(image, descriptor + 16, 18)).TrimEnd('\0') != ".?AVCTClientChar@@")
            throw new InvalidOperationException("Profil oyuncu sınıfı doğrulanamadı.");
    }

    public static void ScanApproved()
    {
        try { VerifyDisk(force: true); } catch when (NeedsApproval) { }
        byte[] image = cached ?? throw new IOException("Oyun dosyası okunamadı.");
        int[] rvas = FindSites(image);
        SetRvas(rvas);
        Vtable = FindVtable(image);
        ValidateSites(image);
        verified = true; // Temporary candidate; persisted only after live validation.
        NeedsApproval = true;
    }

    public static int[] FindSites(byte[] image)
    {
        ScanCount++;
        using var pe = new PEReader(new MemoryStream(image));
        var rvas = new List<int>();
        foreach (var sig in Signatures)
        {
            byte[] pattern = Convert.FromHexString(sig.Pattern.Replace(" ", ""));
            if (pattern.Length != sig.Mask.Length) throw new InvalidOperationException("Geçersiz profil.");
            var hits = new List<int>();
            // Latin-1 preserves one character per byte, including all 0x00..0xFF values.
            string regex = "(?=" + string.Concat(pattern.Select((b, i) => sig.Mask[i] == '?' ? "." : Regex.Escape(((char)b).ToString()))) + ")";
            foreach (var s in pe.PEHeaders.SectionHeaders.Where(s => ((uint)s.SectionCharacteristics & 0x20000000) != 0))
            {
                string data = System.Text.Encoding.Latin1.GetString(image, s.PointerToRawData, s.SizeOfRawData);
                hits.AddRange(Regex.Matches(data, regex, RegexOptions.Singleline).Select(m => s.VirtualAddress + m.Index));
            }
            if (hits.Count != 1) throw new InvalidOperationException($"İmza tekil değil ({hits.Count} eşleşme); yeni analiz gerekiyor.");
            rvas.Add(hits[0]);
        }
        return rvas.ToArray();
    }

    public static void Commit()
    {
        Directory.CreateDirectory(DirectoryPath);
        string path = Path.Combine(DirectoryPath, ActiveSha + ".json");
        File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(new SavedProfile(1, ActiveSha, Signatures.Select(s => s.Rva).ToArray(), Vtable)));
        File.Move(path + ".tmp", path, true);
        NeedsApproval = false;
    }

    public static void RejectCandidate()
    {
        verified = false;
        NeedsApproval = true;
    }

    static long FindVtable(byte[] image)
    {
        using var pe = new PEReader(new MemoryStream(image));
        long imageBase = checked((long)pe.PEHeaders.PEHeader!.ImageBase);
        var sections = pe.PEHeaders.SectionHeaders.Where(s => s.SizeOfRawData > 0).ToArray();
        var types = new HashSet<long>();
        byte[] name = System.Text.Encoding.ASCII.GetBytes(".?AVCTClientChar@@\0");
        foreach (var s in sections)
            for (int offset = 0; offset <= s.SizeOfRawData - name.Length;)
            {
                int index = image.AsSpan(s.PointerToRawData + offset, s.SizeOfRawData - offset).IndexOf(name);
                if (index < 0) break;
                types.Add(s.VirtualAddress + offset + index - 16L);
                offset += index + name.Length;
            }
        var locators = new HashSet<long>();
        foreach (var s in sections)
            for (int i = 0; i <= s.SizeOfRawData - 24; i += 4)
            {
                int raw = s.PointerToRawData + i;
                if (BitConverter.ToUInt32(image, raw) == 1 && BitConverter.ToUInt32(image, raw + 4) == 0 &&
                    BitConverter.ToUInt32(image, raw + 8) == 0 && types.Contains(BitConverter.ToUInt32(image, raw + 12)) &&
                    BitConverter.ToUInt32(image, raw + 20) == s.VirtualAddress + i)
                    locators.Add(imageBase + s.VirtualAddress + i);
            }
        var vtables = new List<long>();
        foreach (var s in sections)
            for (int i = 0; i <= s.SizeOfRawData - 16; i += 8)
                if (locators.Contains(BitConverter.ToInt64(image, s.PointerToRawData + i)))
                {
                    long function = BitConverter.ToInt64(image, s.PointerToRawData + i + 8) - imageBase;
                    if (sections.Any(code => ((uint)code.SectionCharacteristics & 0x20000000) != 0 && function >= code.VirtualAddress && function < (long)code.VirtualAddress + code.SizeOfRawData))
                        vtables.Add(s.VirtualAddress + i + 8L);
                }
        if (vtables.Count != 1) throw new InvalidOperationException("Oyuncu sınıfı tekil bulunamadı; yeni analiz gerekiyor.");
        return vtables[0];
    }

    public static byte[] At(byte[] image, long rva, int count)
    {
        using var pe = new PEReader(new MemoryStream(image));
        var section = pe.PEHeaders.SectionHeaders.Single(s => rva >= s.VirtualAddress && rva + count <= (long)s.VirtualAddress + s.SizeOfRawData);
        return image.AsSpan(checked((int)(rva - section.VirtualAddress + section.PointerToRawData)), count).ToArray();
    }
}
