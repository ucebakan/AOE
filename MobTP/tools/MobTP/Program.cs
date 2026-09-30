namespace MobTP;

static class Program
{
    [STAThread] static void Main(string[] args)
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        if(args.Contains("--self-test")){Tests.Run();return;}
        if(args.Contains("--ui-test")){Tests.Preview();return;}
        if(args.Contains("--profile-test")){Tests.Profiles();return;}
        if(args.Contains("--probe")){File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"probe.json"),System.Text.Json.JsonSerializer.Serialize(Engine.Capture(),new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));return;}
        Application.Run(new MainForm());
    }
}
