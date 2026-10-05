using System.Runtime.InteropServices;
using System.Text.Json;
using System.Diagnostics;
using PlayerXYZ;

namespace UnityTools.Salesman;

static class Tests
{
    internal static object Benchmark()
    {
        var disk = File.ReadAllBytes(Multikill.Profile.GamePath);
        var samples = new List<double>();
        var watch = Stopwatch.StartNew(); var profile = Profiles.Load(disk); double cold = watch.Elapsed.TotalMilliseconds;
        for (int i = 0; i < 3; i++) { watch.Restart(); Profiles.Load(disk); samples.Add(watch.Elapsed.TotalMilliseconds); }
        string archive = Path.Combine(Path.GetDirectoryName(Multikill.Profile.GamePath)!, "FileMerger.unity");
        watch.Restart(); DiskFingerprint.Read(archive); double archiveCold = watch.Elapsed.TotalMilliseconds;
        watch.Restart(); DiskFingerprint.Read(archive); double archiveCached = watch.Elapsed.TotalMilliseconds;
        return new { profile.Sha, cold_ms = cold, validated_profile_ms = samples, archive_hash_ms = archiveCold, archive_cached_ms = archiveCached, game_memory_writes = 0, game_function_calls = 0 };
    }
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int BridgeTest();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate nint BeginFixture();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int ReadFixture(out uint calls, out uint npc, out uint thread);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int Open(nint window, ref OpenRequest request, out uint error);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int Layout(out ulong module, out uint rva);
    [DllImport("user32.dll", SetLastError = true)] static extern bool PostMessageW(nint window, uint message, nint w, nint l);
    [DllImport("kernel32.dll")] static extern ulong GetTickCount64();
    sealed record FixtureState(long Window, int Pid, long Created, uint Thread, long Base, int Site);
    internal static int FixtureChild(string readyPath, string resultPath)
    {
        nint window = NativeModules.Function<BeginFixture>("UnitySalesman.dll", "BeginSalesmanFixture")();
        if (window == 0) return 2;
        uint thread = NativeModules.GetWindowThreadProcessId(window, out uint pid);
        using var process = Process.GetCurrentProcess();
        NativeModules.Function<Layout>("UnitySalesman.dll", "SalesmanFixtureLayout")(out ulong module, out uint rva);
        File.WriteAllText(readyPath + ".tmp", JsonSerializer.Serialize(new FixtureState(window.ToInt64(), (int)pid, process.StartTime.ToUniversalTime().ToFileTimeUtc(), thread, (long)module, (int)rva)));
        File.Move(readyPath + ".tmp", readyPath);
        Application.Run();
        int ok = NativeModules.Function<ReadFixture>("UnitySalesman.dll", "ReadSalesmanFixture")(out uint calls, out uint npc, out uint actualThread);
        File.WriteAllText(resultPath, JsonSerializer.Serialize(new { calls, npc, thread = actualThread }));
        int restored = NativeModules.Function<BridgeTest>("UnitySalesman.dll", "VerifySalesmanFixture")();
        return ok == 1 && (calls == 0 || (calls == 1 && npc == 22631 && actualThread == thread)) && restored == 1 ? 0 : 1;
    }
    internal static int FixtureLease(string readyPath, string mode)
    {
        try
        {
            var state = JsonSerializer.Deserialize<FixtureState>(File.ReadAllText(readyPath))!;
            using var session = Session.Fixture(state.Pid, state.Created, state.Base);
            using var lease = new Lease(session);
            if (mode == "protected") { session.FixtureProtectOnly(); Environment.Exit(88); }
            lease.Activate();
            if (mode == "abrupt") Environment.Exit(88);
            if (mode == "fault") throw new IOException("Fixture activation failure");
            if (mode == "expired") Thread.Sleep(2500);
            return lease.FinishAsync().GetAwaiter().GetResult() ? 0 : 1;
        }
        catch { return 1; }
    }
    static Process SpawnFixture(params string[] args)
    {
        var start = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden };
        if (Path.GetFileNameWithoutExtension(Environment.ProcessPath!).Equals("dotnet", StringComparison.OrdinalIgnoreCase)) start.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "4UnityTools.dll"));
        foreach (string arg in args) start.ArgumentList.Add(arg);
        return Process.Start(start) ?? throw new IOException("Fixture başlatılamadı.");
    }
    static bool GuardFixture(string mode)
    {
        string directory = Path.Combine(Program.DataRoot, "guard-fixture"); Directory.CreateDirectory(directory);
        string ready = Path.Combine(directory, Guid.NewGuid().ToString("N") + ".ready.json"), result = ready + ".result.json";
        using var child = SpawnFixture("--salesman-bridge-fixture", Program.DataRoot, ready, result);
        FixtureState? state = null;
        try
        {
            var deadline = DateTime.UtcNow.AddSeconds(10);
            while (!File.Exists(ready) && !child.HasExited && DateTime.UtcNow < deadline) Thread.Sleep(20);
            state = JsonSerializer.Deserialize<FixtureState>(File.ReadAllText(ready))!;
            if (state.Pid != child.Id || state.Created != child.StartTime.ToUniversalTime().ToFileTimeUtc()) throw new IOException("Guardian fixture kimliği değişmiş.");
            using var worker = SpawnFixture("--salesman-lease-fixture", Program.DataRoot, ready, mode);
            if (!worker.WaitForExit(15000)) throw new IOException("Guardian worker zaman aşımı.");
            int expected = mode is "abrupt" or "protected" ? 88 : mode == "fault" ? 1 : 0;
            using var session = Session.Fixture(state.Pid, state.Created, state.Base, true);
            bool restored = false; deadline = DateTime.UtcNow.AddSeconds(5);
            while (DateTime.UtcNow < deadline)
            { try { session.ValidateClose(false); restored = true; break; } catch { Thread.Sleep(20); } }
            PostMessageW((nint)state.Window, 0x10, 0, 0);
            return restored && worker.ExitCode == expected && child.WaitForExit(5000) && child.ExitCode == 0;
        }
        finally
        {
            if (state is not null && !child.HasExited) { PostMessageW((nint)state.Window, 0x10, 0, 0); child.WaitForExit(2000); }
            if (!child.HasExited) child.Kill();
        }
    }
    static bool CrossProcessBridge()
    {
        string directory = Path.Combine(Program.DataRoot, "bridge-fixture"); Directory.CreateDirectory(directory);
        string ready = Path.Combine(directory, Guid.NewGuid().ToString("N") + ".ready.json"), result = ready + ".result.json";
        var start = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden };
        if (Path.GetFileNameWithoutExtension(Environment.ProcessPath!).Equals("dotnet", StringComparison.OrdinalIgnoreCase)) start.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "4UnityTools.dll"));
        foreach (string argument in new[] { "--salesman-bridge-fixture", Program.DataRoot, ready, result }) start.ArgumentList.Add(argument);
        using var child = Process.Start(start) ?? throw new IOException("Bridge fixture başlatılamadı.");
        FixtureState? state = null;
        try
        {
            var deadline = DateTime.UtcNow.AddSeconds(10);
            while (!File.Exists(ready) && !child.HasExited && DateTime.UtcNow < deadline) Thread.Sleep(20);
            state = JsonSerializer.Deserialize<FixtureState>(File.ReadAllText(ready)) ?? throw new IOException("Fixture cevabı boş.");
            if (state.Pid != child.Id || state.Created != child.StartTime.ToUniversalTime().ToFileTimeUtc() || NativeModules.GetWindowThreadProcessId((nint)state.Window, out uint pid) != state.Thread || pid != child.Id) throw new IOException("Fixture pencere kimliği uyuşmuyor.");
            var request = new OpenRequest { Version = 1, Pid = (uint)state.Pid, Created = (ulong)state.Created, Deadline = GetTickCount64() + 5000, SenderBytes = new byte[128], FinderBytes = new byte[128] };
            int opened = NativeModules.Function<Open>("UnitySalesman.dll", "OpenSalesman")((nint)state.Window, ref request, out uint error);
            if (opened != 1) throw new IOException($"Ayrı süreç UI bridge sonucu: {error}");
            PostMessageW((nint)state.Window, 0x10, 0, 0);
            if (!child.WaitForExit(5000) || child.ExitCode != 0) throw new IOException("Ayrı süreç bridge doğru thread'de bir kez çalışmadı.");
            using var proof = JsonDocument.Parse(File.ReadAllText(result));
            return proof.RootElement.GetProperty("calls").GetUInt32() == 1 && proof.RootElement.GetProperty("npc").GetUInt32() == 22631 && proof.RootElement.GetProperty("thread").GetUInt32() == state.Thread;
        }
        finally
        {
            if (state is not null && !child.HasExited) { PostMessageW((nint)state.Window, 0x10, 0, 0); child.WaitForExit(2000); }
            if (!child.HasExited) child.Kill(); // Only the fixture process created above.
        }
    }
    internal static object Run()
    {
        var checks = new List<string>();
        void Check(bool ok, string message) { if (!ok) throw new IOException(message); checks.Add(message); }
        void Reject(Action action, string message) { bool blocked = false; try { action(); } catch { blocked = true; } Check(blocked, message); }
        string proofPath = Path.Combine(Program.DataRoot, "resource-proof.bin"); Directory.CreateDirectory(Program.DataRoot);
        File.WriteAllBytes(proofPath, [1,2,3,4]); string firstSha = DiskFingerprint.Read(proofPath); int hashes = DiskFingerprint.HashCount;
        Check(DiskFingerprint.Read(proofPath) == firstSha && DiskFingerprint.HashCount == hashes, "unchanged file ID/change time reuses verified SHA");
        var originalTime = File.GetLastWriteTimeUtc(proofPath); Thread.Sleep(20); File.WriteAllBytes(proofPath, [4,3,2,1]); File.SetLastWriteTimeUtc(proofPath, originalTime);
        Check(DiskFingerprint.Read(proofPath) != firstSha && DiskFingerprint.HashCount == hashes + 1, "same size and restored last-write time cannot hide changed resource contents");
        File.Move(proofPath, proofPath + ".old"); File.WriteAllBytes(proofPath, [1,2,3,4]); File.SetLastWriteTimeUtc(proofPath, originalTime);
        Check(DiskFingerprint.Read(proofPath) == firstSha && DiskFingerprint.HashCount == hashes + 2, "replacement file ID forces new SHA verification");
        DiskFingerprint.Read(proofPath, true);
        Check(DiskFingerprint.HashCount == hashes + 3, "explicit resource recovery bypasses fingerprint cache");
        byte[] disk = File.ReadAllBytes(Multikill.Profile.GamePath); using var b = new Binary(disk);
        var p = Profiles.Load(disk); int count = Profiles.ScanCount, validations = Profiles.ValidationCount; var loaded = Profiles.Load(disk);
        Check(count == Profiles.ScanCount && loaded.Sha == b.Sha && validations == Profiles.ValidationCount, "same exact EXE SHA and unchanged JSON reuse semantic proof without AOB rescan");
        string profilePath = Path.Combine(Profiles.DirectoryPath, p.Sha + ".json");
        File.WriteAllText(profilePath, JsonSerializer.Serialize(p with { SessionOffset = p.SessionOffset + 8 }));
        var rebuilt = Profiles.Load(disk);
        Check(Profiles.ScanCount == count + 1 && rebuilt.SessionOffset == p.SessionOffset, "edited cached JSON cannot bypass semantic checks; corrupted profile rebuilt");
        if (p.Sha == "4DC9C526A895A10113C4CF23F2D199BFF283D2C7CE75F949DC49CEF15D27D622")
            Check(p.Sender == 0x9f900 && p.Finder == 0xb24a50 && p.SessionOffset == 0x2358 && p.ShopOffset == 0x14e8 && p.ContextOffset == 0x2c8 && p.CashOffset == 0x2c0 && p.VisibleOffset == 0x19c && p.CloseSite == 0x167da3, "current game's operands and method links recovered");
        Profiles.Validate(b, p);
        Reject(() => Profiles.Validate(b, p with { SessionOffset = p.SessionOffset + 8 }), "cached operand corruption rejected");
        Reject(() => Profiles.Validate(b, p with { Sha = "BAD" }), "foreign SHA rejected");
        int Raw(int rva) { var section = b.Pe.PEHeaders.SectionHeaders.Single(s => rva >= s.VirtualAddress && rva < s.VirtualAddress + s.SizeOfRawData); return section.PointerToRawData + rva - section.VirtualAddress; }
        var changed = disk.ToArray(); BitConverter.GetBytes(p.SessionOffset + 8).CopyTo(changed, Raw(p.Anchors["open"].Rva + 3));
        using (var inconsistent = new Binary(changed)) Reject(() => Profiles.Resolve(inconsistent), "inconsistent linked session operands rejected");
        BitConverter.GetBytes(p.SessionOffset + 8).CopyTo(changed, Raw(p.Anchors["toggle"].Rva + 46));
        using (var newBuild = new Binary(changed)) { var q = Profiles.Resolve(newBuild); Check(q.Sha != p.Sha && q.SessionOffset == p.SessionOffset + 8, "changed SHA re-derives session operand rather than old RVA profile"); }
        changed = disk.ToArray(); changed[Raw(p.Anchors["open"].Rva)] ^= 1;
        using (var missing = new Binary(changed)) Reject(() => Profiles.Resolve(missing), "missing opening signature fails closed");
        changed = disk.ToArray(); var code = b.Pe.PEHeaders.SectionHeaders.First(s => ((uint)s.SectionCharacteristics & 0x20000000) != 0);
        b.At(p.Anchors["open"].Rva, Binary.Pattern(Profiles.Patterns["open"]).Length).CopyTo(changed, code.PointerToRawData + code.SizeOfRawData - 512);
        using (var duplicate = new Binary(changed)) Reject(() => Profiles.Resolve(duplicate), "duplicate opening signature fails closed");
        changed = disk.ToArray(); changed[Raw(p.Sender + 0x1c)] ^= 1;
        using (var badPacket = new Binary(changed)) Reject(() => Profiles.Resolve(badPacket), "changed request opcode semantic fingerprint rejected");
        Check(Marshal.SizeOf<OpenRequest>() == 384, "managed/native request layout matches");
        Check(NativeModules.Function<BridgeTest>("UnitySalesman.dll", "TestSalesmanBridge")() == 1, "direct bridge calls own UI-thread fixture once; wrong session, expired request and wrong PID rejected; no keyboard input");
        Check(CrossProcessBridge(), "separate-process fixture receives NPC 22631 exactly once on its own UI thread without key input");
        foreach (string mode in new[] { "normal", "fault", "abrupt", "protected", "expired" })
            Check(GuardFixture(mode), "separate-process guardian restores original code and RX protection: " + mode);
        Check(FeatureActions.OneShot(Feature.Salesman) && FeatureActions.Page(Feature.Salesman) == 9 && StartupScan.Order.Contains(Feature.Salesman) && ShutdownSequence.Order.Contains(9), "one-click action, read-only scan and sequential cleanup integrated");
        return new { status = "PASS", game_memory_writes = 0, game_function_calls = 0, p.Sha, p.Sender, p.Finder, p.CloseSite, checks };
    }
}
