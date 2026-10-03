using System.Reflection;
using System.Security.Cryptography;

namespace UnityTools;

static class Program
{
    public static string DataRoot { get; private set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "4UnityTools");
    [STAThread]
    static int Main(string[] args)
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.SetDefaultFont(new Font("Segoe UI", 9));
        bool recoveryTesting=args.Contains("--recovery-test");
        bool testing = args.Contains("--self-test") || args.Contains("--ui-test") || recoveryTesting;
        if (testing) DataRoot = Path.Combine(Path.GetTempPath(), "4UnityTools-tests", Environment.ProcessId.ToString());
        using var mutex = new Mutex(true, @"Local\4UnityTools.Suite" + (testing ? ".test." + Environment.ProcessId : ""), out bool first);
        if (!first && args.Contains("--elevated")) { try { first = mutex.WaitOne(TimeSpan.FromSeconds(15)); } catch (AbandonedMutexException) { first = true; } }
        if (!first) { MessageBox.Show("4UnityTools zaten açık.", "4UnityTools"); return 1; }
        try
        {
            ExtractResources();
            PlayerXYZ.Files.Root = Path.Combine(DataRoot, "PlayerXYZ");
            SpeedJump.Files.Root = Path.Combine(DataRoot, "SpeedJump");
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
}
