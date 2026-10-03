using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;

namespace UnityTools;

enum ScanStatus { Pending, Scanning, Ready, Waiting, Manual, Failed, Cancelled }
record ScanResult(Feature Feature, ScanStatus Status, string Message);
interface IStartupProbe { Task<ScanResult> ProbeAsync(Feature feature); }

// Deliberately has no Apply/Toggle/Arm dependency.
sealed class StartupScan
{
    internal static readonly Feature[] Order = [Feature.Coordinates, Feature.Speed, Feature.Jump, Feature.Invisible,
        Feature.Aggro, Feature.MobTP, Feature.Aoe, Feature.Counter, Feature.Multikill];
    internal bool Busy { get; private set; }
    internal Dictionary<Feature, ScanResult> Results { get; } = new();
    internal async Task RunAsync(IStartupProbe probe, Action<ScanResult> progress, CancellationToken token)
    {
        if (Busy) return;
        Busy = true;
        try
        {
            foreach (var feature in Order)
            {
                if (token.IsCancellationRequested) { Publish(new(feature, ScanStatus.Cancelled, "İptal edildi.")); continue; }
                Publish(new(feature, ScanStatus.Scanning, "Profil ve canlı yol kontrol ediliyor…"));
                ScanResult result;
                try { result = await probe.ProbeAsync(feature); }
                catch (Exception ex) { result = new(feature, ScanStatus.Failed, ex.Message); }
                Publish(token.IsCancellationRequested ? new(feature, ScanStatus.Cancelled, "Kontrol durduruldu.") : result);
            }
        }
        finally { Busy = false; }
        void Publish(ScanResult result) { Results[result.Feature] = result; progress(result); }
    }
    internal static string SessionKey()
    {
        var processes = Process.GetProcessesByName("TClient");
        try { return string.Join(";", processes.OrderBy(p => p.Id).Select(p => { try { return $"{p.Id}:{p.StartTime.ToUniversalTime().Ticks}"; } catch { return $"{p.Id}:pending"; } })); }
        finally { foreach (var p in processes) p.Dispose(); }
    }
}

sealed class LiveStartupProbe(ToolWorkspace workspace, FeatureActions actions) : IStartupProbe
{
    readonly Dictionary<int, ScanResult> shared = new();
    public async Task<ScanResult> ProbeAsync(Feature feature)
    {
        int page = FeatureActions.Page(feature);
        // A loaded module owns its session and any patch journal. Never touch it
        // through a second session or race its profile state from a scan.
        if (page != 0 && workspace.Pages.ContainsKey(page))
        {
            var state = actions.Read(feature);
            return new(feature, state.Ready || state.Active ? ScanStatus.Ready : ScanStatus.Manual,
                "Yüklü araç: " + state.Message);
        }
        if (page is 2 or 3 && shared.TryGetValue(page, out var previous)) return previous with { Feature = feature };
        var result = feature == Feature.Counter ? await CounterAsync() : await Task.Run(() => Probe(feature));
        if (page is 2 or 3) shared[page] = result;
        return result;
    }
    static ScanResult Probe(Feature feature)
    {
        string profile = "";
        try
        {
            switch (feature)
            {
                case Feature.Coordinates:
                    using (var binary = new PlayerXYZ.Binary(Multikill.Profile.GamePath))
                    {
                        int before = PlayerXYZ.Profiles.ScanCount; PlayerXYZ.Profiles.Load(binary);
                        profile = Source(before == PlayerXYZ.Profiles.ScanCount);
                    }
                    using (var session = PlayerXYZ.Session.Connect(readOnly: true)) { session.Snapshot(); return Ready(feature, profile, session.Pid); }
                case Feature.Speed or Feature.Jump:
                    using (var binary = new SpeedJump.Binary(Multikill.Profile.GamePath))
                    {
                        int before = SpeedJump.Profiles.ScanCount; SpeedJump.Profiles.Load(binary);
                        profile = Source(before == SpeedJump.Profiles.ScanCount);
                    }
                    using (var session = SpeedJump.Session.Connect(readOnly: true)) { session.Health(); return Ready(feature, profile, session.Pid); }
                case Feature.Invisible or Feature.Aggro:
                    int scans = InvisibleAggro.Profile.ScanCount;
                    try { InvisibleAggro.Profile.VerifyDisk(force: true); }
                    catch when (InvisibleAggro.Profile.NeedsApproval) { InvisibleAggro.Profile.ScanApproved(); }
                    profile = Source(scans == InvisibleAggro.Profile.ScanCount);
                    using (var session = InvisibleAggro.GameSession.Connect())
                    {
                        session.ValidatePlayer(); session.ValidateCode([]);
                        if (InvisibleAggro.Profile.NeedsApproval) InvisibleAggro.Profile.Commit();
                        return Ready(feature, profile, session.Pid);
                    }
                case Feature.MobTP:
                    var bundle = MobTP.ProfileStore.Load(Multikill.Profile.GamePath); profile = bundle.Origin;
                    using (var session = new MobTP.Session()) return Ready(feature, profile, session.Pid);
                case Feature.Multikill:
                    Multikill.Profile.Resolve(File.ReadAllBytes(Multikill.Profile.GamePath));
                    profile = Source(Multikill.Profile.LastCacheHit);
                    using (var session = Multikill.Session.Connect())
                    {
                        session.ValidateIdentity(); Multikill.Profile.ValidateLive(session.ReadSite(), false, session.Build);
                        return Ready(feature, profile, session.Pid);
                    }
                case Feature.Aoe:
                    AoeProfileRecovery.Ensure();
                    string sha = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Multikill.Profile.GamePath)));
                    foreach (string path in Directory.EnumerateFiles(Path.Combine(Program.DataRoot, "AOE", "profiles"), "*.json"))
                    {
                        try
                        {
                            using var json = JsonDocument.Parse(File.ReadAllText(path));
                            if (json.RootElement.GetProperty("targetSha256").GetString() == sha)
                                return new(feature, ScanStatus.Manual, "SHA eşleşen profil bulundu. AOE sayfasında ATTACH NOW ve gerekiyorsa Live Validation yap. Arm edilmedi.");
                        }
                        catch (Exception ex) when (ex is JsonException or KeyNotFoundException) { }
                    }
                    return new(feature, ScanStatus.Manual, "Bu SHA için AOE profili yok. AOE sayfasında profil / canlı doğrulama gerekiyor. Arm edilmedi.");
                default: throw new ArgumentOutOfRangeException(nameof(feature));
            }
        }
        catch (Exception ex) { return new(feature, profile.Length > 0 ? ScanStatus.Waiting : ScanStatus.Failed,
            (profile.Length > 0 ? profile + " · Canlı doğrulama bekliyor: " : "") + ex.Message); }
    }
    static string Source(bool cached) => cached ? "SHA profili yüklendi; tam tarama yapılmadı" : "Yeni profil tarandı ve kaydedildi";
    static ScanResult Ready(Feature feature, string source, int pid) => new(feature, ScanStatus.Ready, $"{source} · PID {pid} · canlı yol doğrulandı. İşlev kapalı.");
    static async Task<ScanResult> CounterAsync()
    {
        using var reader = new NativeCountReader(); var count = await reader.ReadAsync();
        return new(Feature.Counter, count.HasValue ? ScanStatus.Ready : ScanStatus.Waiting,
            count.HasValue ? $"SHA profili ve canlı sayaç yolu doğrulandı · {count}. Overlay açılmadı." : "Oyun / sayaç yolu doğrulaması bekleniyor. Overlay açılmadı.");
    }
}
