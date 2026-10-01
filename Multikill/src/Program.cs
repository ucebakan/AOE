namespace Multikill;
static class Program
{
    [STAThread] static int Main(string[] args)
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2); Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
        if (args.Contains("--self-test")) return SelfTests.Run(args.Last());
        if (args.Contains("--verify-live"))
        {
            try
            {
                using var session = Session.Connect(); var live = session.ReadSite(); bool patched = false;
                try { Profile.ValidateLive(live, false, session.Build); } catch { Profile.ValidateLive(live, true, session.Build); patched = true; }
                File.WriteAllText(args.Last(), System.Text.Json.JsonSerializer.Serialize(new { session.Pid, session.Created, session.Base, sha = session.Build.Sha256, patched, verified = true, writesToGame = 0 })); return 0;
            }
            catch (Exception ex) { File.WriteAllText(args.Last(), System.Text.Json.JsonSerializer.Serialize(new { verified = false, error = ex.Message, writesToGame = 0 })); return 1; }
        }
        Application.Run(new MainForm()); return 0;
    }
}
