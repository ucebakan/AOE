using System.Text.Json;

namespace InvisibleAggro;

static class SelfTests
{
    sealed class FakeMemory : IMemory
    {
        public Dictionary<long, byte[]> Cells = new() { [1] = [0x88, 0x97], [2] = [0xC6, 0x87], [3] = [0], [4] = [85] };
        public int Writes, FailAt = -1;
        public byte[] Read(long address, int count) => Cells[address].ToArray();
        public void Write(long address, byte[] bytes, bool code)
        {
            Writes++;
            if (Writes == FailAt)
            {
                Cells[address][0] = bytes[0]; // failure after a partial mutation
                throw new IOException("injected partial write failure");
            }
            Cells[address] = bytes.ToArray();
        }
    }
    static readonly Cell[] Aggro = [new(1, [0x90, 0x90], true), new(2, [0x90, 0x90], true), new(3, [1], false), new(4, [255], false)];
    static readonly Cell[] Invisible = [new(3, [1], false)];
    static void Assert(bool value, string text) { if (!value) throw new Exception(text); }
    public static int Run(string output)
    {
        var results = new List<object>();
        int failures = 0;
        void Test(string name, Action action)
        {
            try { action(); results.Add(new { name, status = "PASS" }); }
            catch (Exception ex) { failures++; results.Add(new { name, status = "FAIL", error = ex.Message }); }
        }
        Test("Invisible modifies only the flag and restores its baseline", () =>
        {
            var m = new FakeMemory(); var p = new PatchSet(m);
            p.Apply(Invisible); Assert(m.Writes == 1 && m.Cells[3][0] == 1, "unexpected writes");
            p.Apply([], true); Assert(m.Cells[3][0] == 0 && p.Owned.Count == 0, "restore failed");
        });
        Test("Aggro to Invisible restores both writers and visual state", () =>
        {
            var m = new FakeMemory(); var p = new PatchSet(m);
            p.Apply(Aggro); Assert(p.Owned.Count == 4, "ownership missing");
            p.Apply(Invisible);
            Assert(m.Cells[1][0] == 0x88 && m.Cells[2][0] == 0xC6 && m.Cells[4][0] == 85 && m.Cells[3][0] == 1, "switch failed");
            p.Apply([], true); Assert(m.Cells[3][0] == 0, "off failed");
        });
        Test("Natural pre-existing stealth is preserved", () =>
        {
            var m = new FakeMemory(); m.Cells[3] = [1]; var p = new PatchSet(m);
            p.Apply(Invisible); p.Apply([], true);
            Assert(m.Writes == 0 && m.Cells[3][0] == 1, "natural state overwritten");
        });
        Test("Every failed write, including partial writes, rolls the batch back", () =>
        {
            for (int fail = 1; fail <= 4; fail++)
            {
                var m = new FakeMemory { FailAt = fail }; var p = new PatchSet(m); bool rejected = false;
                try { p.Apply(Aggro); } catch (IOException) { rejected = true; }
                Assert(rejected && m.Cells[1].SequenceEqual(new byte[] { 0x88, 0x97 }) && m.Cells[2].SequenceEqual(new byte[] { 0xC6, 0x87 }) &&
                    m.Cells[3][0] == 0 && m.Cells[4][0] == 85 && p.Owned.Count == 0, $"failure {fail} not rolled back");
            }
        });
        Test("Journal failure prevents all writes", () =>
        {
            var m = new FakeMemory(); var p = new PatchSet(m) { SaveJournal = _ => throw new IOException("disk full") };
            try { p.Apply(Aggro); } catch (IOException) { }
            Assert(m.Writes == 0, "journal failed after mutation");
        });
        Test("Unowned external code changes are not overwritten", () =>
        {
            var m = new FakeMemory(); var p = new PatchSet(m); p.Apply(Aggro); m.Cells[1] = [0xCC, 0xCC];
            int writes = m.Writes; bool failed = false;
            try { p.Apply([], true); } catch (InvalidOperationException) { failed = true; }
            Assert(failed && writes == m.Writes && m.Cells[1][0] == 0xCC, "external code overwritten");
        });
        Test("Journal commit failure rolls successful writes back", () =>
        {
            var m = new FakeMemory(); var p = new PatchSet(m); int saves = 0;
            p.SaveJournal = _ => { if (++saves == 2) throw new IOException("commit failed"); };
            bool rejected = false;
            try { p.Apply(Aggro); } catch (IOException) { rejected = true; }
            Assert(rejected && m.Cells[1][0] == 0x88 && m.Cells[2][0] == 0xC6 && m.Cells[3][0] == 0 && p.Owned.Count == 0, "commit rollback failed");
        });
        Test("External data changes survive cleanup while code restores", () =>
        {
            var m = new FakeMemory(); var p = new PatchSet(m); p.Apply(Aggro); m.Cells[4] = [100];
            p.Apply([], true);
            Assert(m.Cells[4][0] == 100 && m.Cells[1][0] == 0x88 && p.Owned.Count == 0, "external state overwritten");
        });
        Test("Interrupted-session ownership can be restored", () =>
        {
            var m = new FakeMemory(); var p = new PatchSet(m); p.Apply(Aggro);
            string saved = JsonSerializer.Serialize(p.Owned);
            var restarted = new PatchSet(m); restarted.Import(JsonSerializer.Deserialize<List<OwnedCell>>(saved)!); restarted.Apply([], true);
            Assert(m.Cells[1][0] == 0x88 && m.Cells[2][0] == 0xC6 && m.Cells[3][0] == 0 && m.Cells[4][0] == 85, "recovery failed");
        });
        Test("Current TClient SHA and all eight unique signatures", () =>
        {
            byte[] disk = Profile.VerifyDisk();
            Assert(Profile.FindSites(disk).SequenceEqual(Profile.Signatures.Select(s => s.Rva)), "unique sites");
            Assert(Profile.At(disk, Profile.Writer1, 6).SequenceEqual(Profile.Original1), "writer 1");
            Assert(Profile.At(disk, Profile.Writer2, 7).SequenceEqual(Profile.Original2), "writer 2");
            Assert(BitConverter.ToInt32(Profile.At(disk, Profile.Signatures[2].Rva+2, 4))==Profile.Stealth, "stealth 1");
            Assert(BitConverter.ToInt32(Profile.At(disk, Profile.Signatures[3].Rva+16, 4))==Profile.Stealth, "stealth 2");
            long col = BitConverter.ToInt64(Profile.At(disk, Profile.Vtable - 8, 8)) - 0x140000000;
            long descriptor = BitConverter.ToUInt32(Profile.At(disk, col + 12, 4));
            string name = System.Text.Encoding.ASCII.GetString(Profile.At(disk, descriptor + 16, 32)).Split('\0')[0];
            Assert(name == ".?AVCTClientChar@@", "player RTTI");
        });
        Test("Known SHA and repeated cache access do not rescan", () =>
        {
            Profile.VerifyDisk(force: true);
            int before = Profile.ScanCount;
            byte[] first = Profile.VerifyDisk();
            for (int i = 0; i < 20; i++) Assert(ReferenceEquals(first, Profile.VerifyDisk()), "disk cache missed");
            Assert(Profile.ScanCount == before, "unexpected full scan");
        });
        Test("Unknown SHA automatically scans; live-validated profile persists and reloads", () =>
        {
            string original = Profile.GamePath, directory = Profile.DirectoryPath;
            string temp = Path.Combine(Path.GetTempPath(), "InvisibleAggro-test-" + Guid.NewGuid());
            Directory.CreateDirectory(temp);
            try
            {
                byte[] image = File.ReadAllBytes(original);
                int previousRva = Profile.Signatures[0].Rva;
                using (var pe = new System.Reflection.PortableExecutable.PEReader(new MemoryStream(image)))
                {
                    var section = pe.PEHeaders.SectionHeaders.Single(s => previousRva >= s.VirtualAddress && previousRva < s.VirtualAddress + s.SizeOfRawData);
                    int raw = previousRva - section.VirtualAddress + section.PointerToRawData;
                    Profile.At(image, previousRva, Profile.Signatures[0].Mask.Length).CopyTo(image, raw + 0x300);
                    image[raw] ^= 0xFF;
                }
                Profile.GamePath = Path.Combine(temp, "TClient.exe");
                Profile.DirectoryPath = Path.Combine(temp, "profiles");
                File.WriteAllBytes(Profile.GamePath, image.Concat(new byte[] { 0x42 }).ToArray());
                int before = Profile.ScanCount;
                Profile.VerifyDisk(force: true);
                Assert(Profile.NeedsApproval && Profile.ScanCount == before+1, "unknown build did not scan automatically");
                Assert(Profile.Writer1 == previousRva + 0x300 + 20, "moved writer was not resolved");
                Assert(Profile.ScanCount == before + 1 && !Directory.Exists(Profile.DirectoryPath), "candidate prematurely persisted");
                Profile.Commit(); // Simulates the live-validation gate, without a process or any memory writes.
                Profile.VerifyDisk(force: true);
                Assert(Profile.Writer1 == previousRva + 0x300 + 20, "moved address not persisted");
                Assert(!Profile.NeedsApproval && Profile.ScanCount == before + 1, "saved SHA was rescanned");
                File.WriteAllText(Path.Combine(Profile.DirectoryPath, Profile.ActiveSha + ".json"), "{}");
                Profile.VerifyDisk(force: true);
                Assert(Profile.NeedsApproval && Profile.ScanCount == before + 2, "corrupt profile did not recover");
            }
            finally
            {
                Profile.GamePath = original; Profile.DirectoryPath = directory;
                Profile.VerifyDisk(force: true);
                Directory.Delete(temp, true);
            }
        });
        Test("Recovery rejects missing or ambiguous signatures", () =>
        {
            byte[] image = Profile.VerifyDisk().ToArray();
            using var pe = new System.Reflection.PortableExecutable.PEReader(new MemoryStream(image));
            int rva = Profile.Signatures[0].Rva;
            var section = pe.PEHeaders.SectionHeaders.Single(s => rva >= s.VirtualAddress && rva < s.VirtualAddress + s.SizeOfRawData);
            int offset = rva - section.VirtualAddress + section.PointerToRawData;
            byte[] signature = Profile.At(image, rva, Profile.Signatures[0].Mask.Length);
            signature.CopyTo(image, offset + 0x100);
            bool blocked = false;
            try { Profile.FindSites(image); } catch (InvalidOperationException) { blocked = true; }
            Assert(blocked, "ambiguous signature accepted");
            image[offset] ^= 0xFF; image[offset + 0x100] ^= 0xFF;
            blocked = false;
            try { Profile.FindSites(image); } catch (InvalidOperationException) { blocked = true; }
            Assert(blocked, "missing signature accepted");
        });
        File.WriteAllText(output, JsonSerializer.Serialize(new { failures, tests = results, liveGameplayTest = false }, new JsonSerializerOptions { WriteIndented = true }));
        return failures == 0 ? 0 : 1;
    }
}
