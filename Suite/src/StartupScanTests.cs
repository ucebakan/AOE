using UnityTools.Controls;

namespace UnityTools;

static class StartupScanTests
{
    internal static async Task Run(Action<bool, string> check)
    {
        var scan = new StartupScan(); var probe = new Probe(); var updates = new List<ScanResult>();
        await scan.RunAsync(probe, updates.Add, CancellationToken.None);
        check(probe.Calls.SequenceEqual(StartupScan.Order) && probe.Peak == 1, "startup resolves all eleven functions including Collection sequentially");
        check(scan.Results[Feature.Aoe].Status == ScanStatus.Manual && scan.Results[Feature.Multikill].Status == ScanStatus.Ready, "manual AOE does not block following functions");
        check(scan.Results[Feature.Jump].Status == ScanStatus.Failed && probe.Calls.Contains(Feature.Invisible), "failed path does not stop scan queue");
        check(!probe.Calls.Contains(Feature.SafeMode) && updates.Count == StartupScan.Order.Length * 2, "startup scan excludes activation and SafeMode toggle");
        using var cancel = new CancellationTokenSource(); probe = new(); scan = new();
        await scan.RunAsync(probe, result => { if (result.Status == ScanStatus.Ready) cancel.Cancel(); }, cancel.Token);
        check(probe.Calls.Count == 1 && scan.Results.Values.Count(r => r.Status == ScanStatus.Cancelled) == StartupScan.Order.Length - 1, "cancel prevents subsequent probes");
        var hold = new TaskCompletionSource(); probe = new() { Hold = hold }; scan = new();
        var running = scan.RunAsync(probe, _ => { }, CancellationToken.None);
        await scan.RunAsync(probe, _ => { }, CancellationToken.None);
        check(scan.Busy && probe.Calls.Count == 1, "concurrent startup scans are ignored"); hold.SetResult(); await running;
        check(!scan.Busy, "scan busy state resets after completion");
        foreach (long moduleBase in new[] { 0x140000000L, 0x7FF700000000L })
        {
            long owner = moduleBase + 0x20000000, player = owner + 0x20000;
            var memory = new Dictionary<long, byte[]> {
                [moduleBase + 0x7C1A20] = Convert.FromHexString("488B0599CF6A00C3"),
                [moduleBase + 0xE6E9C0] = BitConverter.GetBytes(owner), [owner] = BitConverter.GetBytes(moduleBase + 0xCDC970),
                [owner + 0x2710] = BitConverter.GetBytes(player), [player] = BitConverter.GetBytes(moduleBase + 0xCDBCC0)
            };
            (long Owner, long Player) Resolve() => KnownPlayerPath.Resolve(moduleBase, (a, n) => memory[a], 0xCDC970, 0xCDBCC0, 0x2710);
            check(Resolve() == (owner, player), "ASLR and heap addresses recomputed for base " + moduleBase.ToString("X"));
            memory[moduleBase + 0xE6E9C0] = new byte[8]; bool rejected = false;
            try { Resolve(); } catch (IOException) { rejected = true; }
            check(rejected, "missing live root is waiting, never a stale pointer");
        }
        using var binary = new PlayerXYZ.Binary(Multikill.Profile.GamePath);
        PlayerXYZ.Profiles.Load(binary); int xyzScans = PlayerXYZ.Profiles.ScanCount; PlayerXYZ.Profiles.Load(binary);
        check(PlayerXYZ.Profiles.ScanCount == xyzScans, "XYZ reloads same SHA profile without full scan");
        using var speed = new SpeedJump.Binary(Multikill.Profile.GamePath);
        SpeedJump.Profiles.Load(speed); int speedScans = SpeedJump.Profiles.ScanCount; SpeedJump.Profiles.Load(speed);
        check(SpeedJump.Profiles.ScanCount == speedScans, "Speed and Jump reload same SHA profile without full scan");
    }
    internal sealed class Probe : IStartupProbe
    {
        public readonly List<Feature> Calls = new(); public int Peak; int active;
        public TaskCompletionSource? Hold;
        public async Task<ScanResult> ProbeAsync(Feature feature)
        {
            Calls.Add(feature); Peak = Math.Max(Peak, ++active);
            try
            {
                if (Hold is not null) await Hold.Task;
                await Task.Delay(1);
                if (feature == Feature.Jump) throw new IOException("Fixture: profil değişti.");
                return new(feature, feature == Feature.Aoe ? ScanStatus.Manual : ScanStatus.Ready,
                    feature == Feature.Aoe ? "AOE sayfasında canlı doğrulamayı tamamla. İşlev arm edilmedi." : "Kayıtlı SHA profili yüklendi · canlı yol doğrulandı. İşlev kapalı.");
            }
            finally { active--; }
        }
    }
}
