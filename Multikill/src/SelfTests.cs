using System.Text.Json;
namespace Multikill;
static class SelfTests
{
    public static int Run(string output)
    {
        var checks = new List<string>(); string root = Path.Combine(Path.GetTempPath(), "4UnityMultikill-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root); int result = 0;
        string originalCache = Profile.CacheDirectory; Profile.CacheDirectory = Path.Combine(root, "profile-cache");
        void Check(bool condition, string text) { if (!condition) throw new Exception(text); checks.Add(text); }
        void Reject(Action action, string text) { try { action(); } catch { checks.Add(text); return; } throw new Exception(text); }
        try
        {
            Profile.VerifyDisk(); checks.Add("actual disk SHA, PE and unique AOB validated (read-only)");
            var invalid = File.ReadAllBytes(Profile.GamePath); invalid[^1] ^= 1; Reject(() => Profile.Validate(invalid), "changed SHA rejected");
            var disk = File.ReadAllBytes(Profile.GamePath);
            int before = Profile.ScanCount; Profile.Resolve(disk);
            Check(Profile.ScanCount == before + 1 && !Profile.LastCacheHit, "first known SHA resolves unique AOB");
            before = Profile.ScanCount; Profile.Resolve(disk);
            Check(Profile.ScanCount == before && Profile.LastCacheHit, "same SHA cache skips full AOB scan");
            string cache = Path.Combine(Profile.CacheDirectory, Profile.Known.Sha256 + ".json");
            File.WriteAllText(cache, "{}"); Profile.Resolve(disk);
            Check(Profile.ScanCount == before + 1 && !Profile.LastCacheHit, "corrupt profile falls back to full validation");
            Profile.Resolve(invalid); before = Profile.ScanCount; Profile.Resolve(invalid);
            Check(Profile.ScanCount == before && Profile.LastCacheHit, "new SHA semantic profile persists and reloads without full scan");
            before = Profile.ScanCount; Profile.Resolve(disk, forceScan: true);
            Check(Profile.ScanCount == before + 1 && !Profile.LastCacheHit, "explicit recovery rescan bypasses cache");
            var recovered = RecoveryTests.Run(Check, Reject);
            var recoveredMemory = new MemorySession { Build = recovered };
            string recoveredDirectory = Path.Combine(root, "recovered-build");
            using (var recoveredEngine = new PatchEngine(recoveredDirectory, () => recoveredMemory))
            {
                recoveredEngine.Validate(); Check(recoveredMemory.Writes == 0, "new build validation never automatically enables JE");
                recoveredEngine.Toggle();
                var journal = JsonSerializer.Deserialize<PatchJournal>(File.ReadAllText(Path.Combine(recoveredDirectory, "patch-recovery.json")))!;
                Check(journal.Rva == recovered.PatchRva && journal.Sha == recovered.Sha256 && journal.Original == recovered.Original, "new build patch journal uses resolved RVA SHA and branch bytes");
                recoveredEngine.Restore(); Check(recoveredMemory.Opcode == 0x86, "recovered build restores its own original branch");
            }
            var fake = new MemorySession(); string dir = Path.Combine(root, "normal");
            using (var engine = new PatchEngine(dir, () => fake))
            {
                Reject(engine.Toggle, "enable blocked before explicit validation");
                engine.Validate(); Check(engine.Ready && !engine.Active, "validation never enables patch");
                engine.Toggle(); Check(engine.Active && fake.Opcode == 0x84 && File.Exists(Path.Combine(dir, "patch-recovery.json")), "enable writes JE with durable journal");
                engine.Toggle(); Check(!engine.Active && fake.Opcode == 0x86 && !File.Exists(Path.Combine(dir, "patch-recovery.json")), "disable restores JBE and clears journal");
                UnityTools.Controls.OperationGate.Blocked = true; Reject(engine.Toggle, "SafeMode blocks Multikill enable"); UnityTools.Controls.OperationGate.Blocked = false;
                engine.Toggle();
            }
            Check(fake.Opcode == 0x86, "closing restores active patch");
            PatchJournal Journal(MemorySession f) => new(1, Profile.Known.Sha256, f.Pid, f.Created, f.Base, Profile.PatchRva, Profile.Known.Original, Profile.Known.Patched, 0x20);
            void Seed(string dir, PatchJournal journal) { Directory.CreateDirectory(dir); File.WriteAllText(Path.Combine(dir, "patch-recovery.json"), JsonSerializer.Serialize(journal)); }
            fake = new(); fake.Opcode = 0x84; dir = Path.Combine(root, "read-only-prepare"); Seed(dir, Journal(fake));
            using (var engine = new PatchEngine(dir, () => fake))
            {
                Reject(engine.PrepareReadOnly, "automatic preparation defers pending crash recovery");
                Check(fake.Writes == 0 && File.Exists(Path.Combine(dir, "patch-recovery.json")), "read-only preparation preserves patch and journal");
            }
            fake = new(); fake.Opcode = 0x84; dir = Path.Combine(root, "crash"); Seed(dir, Journal(fake));
            using (var engine = new PatchEngine(dir, () => fake)) { engine.Validate(); Check(fake.Opcode == 0x86 && engine.Ready && !engine.Active, "same-session crash journal restored before enabling"); }
            fake = new(); dir = Path.Combine(root, "reused-pid"); Seed(dir, Journal(fake) with { Created = fake.Created - 1 });
            using (var engine = new PatchEngine(dir, () => fake)) { engine.Validate(); Check(fake.Writes == 0, "PID reuse never restores old session address"); }
            fake = new(); dir = Path.Combine(root, "bad-journal"); Seed(dir, Journal(fake) with { Rva = 0x1234 });
            using (var engine = new PatchEngine(dir, () => fake)) { Reject(engine.Validate, "tampered recovery RVA rejected"); Check(fake.Writes == 0, "invalid journal performs zero writes"); }
            fake = new(); fake.Opcode = 0x84;
            using (var engine = new PatchEngine(Path.Combine(root, "foreign"), () => fake)) { Reject(engine.Validate, "foreign JE without ownership rejected"); Check(fake.Writes == 0, "foreign patch is not overwritten"); }
            fake = new(); dir = Path.Combine(root, "failed-write");
            using (var engine = new PatchEngine(dir, () => fake)) { engine.Validate(); fake.FailAfterWrite = true; Reject(engine.Toggle, "write failure propagated"); Check(fake.Opcode == 0x86 && !engine.Active, "partial failure is rolled back"); }
            fake = new(); dir = Path.Combine(root, "restore-failure"); var failure = new PatchEngine(dir, () => fake); failure.Validate(); failure.Toggle(); fake.Corrupt = true;
            Reject(failure.Restore, "unexpected live bytes reject recovery"); Check(File.Exists(Path.Combine(dir, "patch-recovery.json")) && failure.Active, "failed cleanup retains journal and ownership"); fake.Corrupt = false; failure.Dispose();
        }
        catch (Exception ex) { result = 1; checks.Add("FAIL: " + ex); }
        finally { UnityTools.Controls.OperationGate.Blocked = false; Profile.CacheDirectory = originalCache; }
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
        File.WriteAllText(output, JsonSerializer.Serialize(new { status = result == 0 ? "PASS" : "FAIL", count = checks.Count, writesToGame = 0, checks }, new JsonSerializerOptions { WriteIndented = true }));
        return result;
    }
    sealed class MemorySession : IPatchSession
    {
        public Signature Build { get; set; } = Profile.Known;
        public int Pid => 123; public long Created => 123456; public long Base => 0x140000000; public bool Exited => false;
        public byte Opcode = 0x86; public bool FailAfterWrite, Corrupt; public int Writes;
        public byte[] ReadSite() { var b = Build.Pattern; b[Build.PatchOffset + 1] = Opcode; if (Corrupt) b[0] ^= 1; return b; }
        public uint Protection() => 0x20; public void ValidateIdentity() { }
        public void WriteOpcode(byte value, uint protection) { Opcode = value; Writes++; if (FailAfterWrite) { FailAfterWrite = false; throw new IOException("simulated failure after write"); } }
        public void RestoreProtection(uint protection) { }
        public void Dispose() { }
    }
}
