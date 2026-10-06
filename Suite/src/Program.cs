using System.Reflection;
using System.Security.Cryptography;

namespace UnityTools;

static class Program
{
    public static string DataRoot { get; private set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "4UnityTools");
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Cdecl, CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    delegate int CounterExitProbe(System.Text.StringBuilder message, int capacity);
    [STAThread]
    static int Main(string[] args)
    {
        PlayerXYZ.Files.Root = Path.Combine(DataRoot, "PlayerXYZ");
        if (args.Length == 2 && args[0] == "--salesman-guard") return Salesman.Lease.Guard(args[1]);
        if (args.Length == 3 && args[0] == "--salesman-guard")
        {
            if (!SetFixtureRoot(args[2])) return 2; ExtractResources(); return Salesman.Lease.Guard(args[1]);
        }
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.SetDefaultFont(new Font("Segoe UI", 9));
        if (args.Length is >= 4 and <= 6 && args[0] == "--xyz-window")
        {
            if (!int.TryParse(args[3],out int parentPid) || parentPid<=0) return 2;
            if (args.Length >= 5 && !SetFixtureRoot(args[4])) return 2;
            using var window = new Xyz.XyzWindow(new Xyz.PipeClient(args[1], args[2]), Path.Combine(DataRoot, "XYZ-lists"),reuseOnClose:true,parentPid:parentPid);
            if (args.Length == 6) Xyz.XyzTests.ConfigureChildFixture(window, args[5]);
            Application.Run(window); return 0;
        }
        if (args.Length == 2 && args[0] == "--counter-exit-verify")
        {
            try {
                ExtractResources();var message=new System.Text.StringBuilder(2048);
                int result=NativeModules.Function<CounterExitProbe>("UnityCounter.dll","ProbeCounterExit")(message,message.Capacity);
                File.WriteAllText(args[1],System.Text.Json.JsonSerializer.Serialize(new {status=result==1?"PASS":"FAIL",message=message.ToString(),game_memory_writes=0,exit_dispatch=0}));return result==1?0:1;
            } catch(Exception ex) { File.WriteAllText(args[1],System.Text.Json.JsonSerializer.Serialize(new {status="FAIL",error=ex.ToString(),exit_dispatch=0}));return 1; }
        }
        if (args.Length == 2 && args[0] == "--collection-probe")
        {
            try { File.WriteAllText(args[1], System.Text.Json.JsonSerializer.Serialize(CollectionResearch.Read(), new System.Text.Json.JsonSerializerOptions { WriteIndented = true })); return 0; }
            catch (Exception ex) { File.WriteAllText(args[1], System.Text.Json.JsonSerializer.Serialize(new { status="FAIL", error=ex.ToString(), game_memory_writes=0, game_function_calls=0 })); return 1; }
        }
        if (args.Length == 2 && args[0] == "--salesman-benchmark")
        {
            SetFixtureRoot(Path.Combine(Path.GetTempPath(), "4UnityTools-tests", "benchmark-" + Environment.ProcessId));
            File.WriteAllText(args[1], System.Text.Json.JsonSerializer.Serialize(Salesman.Tests.Benchmark(), new System.Text.Json.JsonSerializerOptions { WriteIndented = true })); return 0;
        }
        if (args.Length == 4 && args[0] == "--salesman-bridge-fixture")
        {
            string root = Path.GetFullPath(args[1]);
            string allowed = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "4UnityTools-tests")) + Path.DirectorySeparatorChar;
            if (!root.StartsWith(allowed, StringComparison.OrdinalIgnoreCase)) return 2;
            DataRoot = root; ExtractResources(); return Salesman.Tests.FixtureChild(args[2], args[3]);
        }
        if (args.Length == 4 && args[0] == "--salesman-lease-fixture")
        { if (!SetFixtureRoot(args[1])) return 2; ExtractResources(); return Salesman.Tests.FixtureLease(args[2], args[3]); }
        if (args.Length == 2 && args[0] == "--salesman-probe")
        {
            try { string result = Salesman.Controller.ProbeAsync().GetAwaiter().GetResult(); File.WriteAllText(args[1], System.Text.Json.JsonSerializer.Serialize(new { status = "PASS", message = result, game_memory_writes = 0, game_function_calls = 0 })); return 0; }
            catch (Exception ex) { File.WriteAllText(args[1], System.Text.Json.JsonSerializer.Serialize(new { status = "FAIL", error = ex.ToString(), game_memory_writes = 0, game_function_calls = 0 })); return 1; }
        }
        if (args.Length == 2 && args[0] == "--collection-verify")
        {
            try { string result = Collection.Controller.ProbeAsync().GetAwaiter().GetResult(); File.WriteAllText(args[1], System.Text.Json.JsonSerializer.Serialize(new { status = "PASS", message = result, game_memory_writes = 0, game_function_calls = 0 })); return 0; }
            catch (Exception ex) { File.WriteAllText(args[1], System.Text.Json.JsonSerializer.Serialize(new { status = "FAIL", error = ex.ToString(), game_memory_writes = 0, game_function_calls = 0 })); return 1; }
        }
        bool collectionTesting=args.Contains("--collection-test");
        bool xyzTesting=args.Contains("--xyz-test");
        bool recoveryTesting=args.Contains("--recovery-test");
        bool testing = args.Contains("--self-test") || args.Contains("--ui-test") || recoveryTesting || collectionTesting || xyzTesting;
        if (testing) DataRoot = Path.Combine(Path.GetTempPath(), "4UnityTools-tests", Environment.ProcessId.ToString());
        using var mutex = new Mutex(true, @"Local\4UnityTools.Suite" + (testing ? ".test." + Environment.ProcessId : ""), out bool first);
        if (!first && args.Contains("--elevated")) { try { first = mutex.WaitOne(TimeSpan.FromSeconds(15)); } catch (AbandonedMutexException) { first = true; } }
        if (!first) { MessageBox.Show("4UnityTools zaten açık.", "4UnityTools"); return 1; }
        try
        {
            ExtractResources();
            AoePatchCompatibility.Initialize();
            PlayerXYZ.Files.Root = Path.Combine(DataRoot, "PlayerXYZ");
            SpeedJump.Files.Root = Path.Combine(DataRoot, "SpeedJump");
            if(collectionTesting) return Collection.Tests.Report(args.Last());
            if(xyzTesting) return Xyz.XyzTests.RunStandalone(args.Last());
            if(recoveryTesting)return RecoveryTests.Run(args.LastOrDefault() is string report&&!report.StartsWith("--")?report:Path.Combine(DataRoot,"recovery-tests.json"));
            if (testing) return SuiteTests.Run(args.LastOrDefault() is string output && !output.StartsWith("--") ? output : Path.Combine(DataRoot, "evidence"));
            Application.Run(new SuiteForm());
            return 0;
        }
        catch (Exception ex)
        {
            Directory.CreateDirectory(DataRoot);
            File.WriteAllText(Path.Combine(DataRoot, "last-error.txt"), ex.ToString());
            if (!testing) MessageBox.Show(ex.Message, "4UnityTools", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 2;
        }
    }
    static void ExtractResources()
    {
        var assembly = Assembly.GetExecutingAssembly();
        foreach (string name in assembly.GetManifestResourceNames().Where(n => n.StartsWith("payload/")))
        {
            using var input = assembly.GetManifestResourceStream(name)!;
            using var buffer = new MemoryStream(); input.CopyTo(buffer); byte[] bytes = buffer.ToArray();
            string relative = name[8..];
            // Native binaries are content-addressed, so an upgrade never overwrites a loaded DLL.
            if (relative.EndsWith(".dll")) relative = Path.Combine("native", Convert.ToHexString(SHA256.HashData(bytes))[..16], relative);
            string path = Path.Combine(DataRoot, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            if (!File.Exists(path) || !File.ReadAllBytes(path).SequenceEqual(bytes))
            { File.WriteAllBytes(path + ".tmp", bytes); File.Move(path + ".tmp", path, true); }
            if (relative.EndsWith(".dll")) NativeModules.Paths[Path.GetFileName(relative)] = path;
        }
        Directory.CreateDirectory(Path.Combine(DataRoot, "AOE", "evidence"));
    }
    static bool SetFixtureRoot(string value)
    {
        string root = Path.GetFullPath(value), allowed = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "4UnityTools-tests")) + Path.DirectorySeparatorChar;
        if (!root.StartsWith(allowed, StringComparison.OrdinalIgnoreCase)) return false;
        DataRoot = root; return true;
    }
}
