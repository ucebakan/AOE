using System.Diagnostics;
using System.Runtime.InteropServices;
using UnityTools.Controls;

namespace UnityTools.Salesman;

sealed class Controller : IDisposable
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int Open(nint window, ref OpenRequest request, out uint error);
    readonly SemaphoreSlim gate = new(1);
    readonly System.Windows.Forms.Timer timer = new() { Interval = 400 };
    Session? session;
    Lease? lease;
    World? world;
    bool quiesced, polling;
    internal bool Ready { get; private set; }
    internal bool Active => lease is not null;
    internal string Message { get; private set; } = "Profili doğrula; Salesman butonu satış penceresini doğrudan açar.";
    internal Controller() { timer.Tick += async (_, _) => await PollAsync(); }
    internal async Task PrepareAsync(bool force = false)
    {
        await gate.WaitAsync();
        var timing = Stopwatch.StartNew();
        try
        {
            if (quiesced) throw new IOException("Salesman geri alma işlemi sürüyor.");
            if (lease is not null) { session!.ValidateCode(true); session.Snapshot(); Ready = true; return; }
            var next = await Task.Run(() =>
            {
                if (force) { Profiles.Load(File.ReadAllBytes(Multikill.Profile.GamePath), true); DiskFingerprint.Read(Path.Combine(Path.GetDirectoryName(Multikill.Profile.GamePath)!, "FileMerger.unity"), true); }
                var s = Session.Connect(false);
                try { s.ValidateCode(false); s.Snapshot(); s.ValidateResource(); return s; } catch { s.Dispose(); throw; }
            });
            session?.Dispose(); session = next; Ready = true;
            Message = $"SHA profili {(Profiles.Cached ? "yüklendi" : "tarandı")} · PID {next.Pid} · canlı yol doğrulandı. Salesman kapalı.";
            next.Log($"PREPARE_MS={timing.ElapsedMilliseconds}");
        }
        catch (Exception ex) { Ready = false; Message = ex.Message; throw; }
        finally { gate.Release(); }
    }
    internal async Task<string> OpenAsync()
    {
        await gate.WaitAsync();
        try
        {
            OperationGate.Check(); if (quiesced || !Ready || session is null) throw new IOException("Salesman profilini önce doğrula.");
            if (lease is not null) { session.ValidateCode(true); return Message = "Salesman penceresi zaten açık."; }
            session.ValidateCode(false); var current = session.Snapshot();
            if (current.Cash != 0) throw new IOException("Mevcut eşya ile açılmış mağaza penceresini kapat ve tekrar bas.");
            if (current.Visible != 0 && current.Context == Profiles.Npc) return Message = "Salesman satış penceresi zaten açık.";
            using var process = Process.GetProcessById(session.Pid); nint window = process.MainWindowHandle;
            if (window == 0 || NativeModules.GetWindowThreadProcessId(window, out uint pid) == 0 || pid != session.Pid) throw new IOException("4Unity oyun penceresi doğrulanamadı.");
            var open = NativeModules.Function<Open>("UnitySalesman.dll", "OpenSalesman");
            var timing = Stopwatch.StartNew(); lease = await Task.Run(() => new Lease(session));
            session.Log($"GUARD_START_MS={timing.ElapsedMilliseconds}"); timing.Restart();
            OperationGate.Check();
            var next = session.Snapshot(); if (next != current) throw new IOException("Salesman doğrulaması sırasında dünya / shop değişti.");
            var request = OpenRequest.From(session, next);
            lease.Activate(); world = current;
            bool sent = await Task.Run(() => { int ok = open(window, ref request, out uint error); if (ok == 0) throw new IOException($"Salesman açılış isteği çalışmadı (kod {error})."); return true; });
            session.Log("DIRECT_REQUEST_SENT npc=22631 keyboard_input=0");
            session.Log($"DIRECT_REQUEST_MS={timing.ElapsedMilliseconds}"); timing.Restart();
            var deadline = DateTime.UtcNow.AddSeconds(6); World observed = current;
            while (DateTime.UtcNow < deadline)
            {
                if (quiesced || OperationGate.Blocked) throw new IOException("Salesman açılışı güvenli durduruldu.");
                observed = session.Snapshot(); if (!SameWorld(current, observed)) throw new IOException("Oyun oturumu değişti.");
                if (observed.Visible == 1 && observed.Context == Profiles.Npc) break;
                await Task.Delay(80);
            }
            if (!sent || observed.Visible != 1 || observed.Context != Profiles.Npc) throw new IOException("Satış penceresi gözlenmedi; varsa oyun uyarısını kontrol et.");
            session.Log($"SHOP_OPEN_OBSERVED context=22631 visible=1 RESPONSE_MS={timing.ElapsedMilliseconds}"); timer.Start();
            return Message = "Salesman açık. SELL TRASH'a oyun içinde sen bas. Pencereyi kapatınca geçici koruma geri alınır.";
        }
        catch (Exception ex)
        {
            Message = ex.Message;
            if (!await RestoreAsync()) Message += " Geri alma doğrulanamadı; yeni işlemler engelli.";
            throw new IOException(Message, ex);
        }
        finally { gate.Release(); }
    }
    static bool SameWorld(World a, World b) => a.Owner == b.Owner && a.Player == b.Player && a.Session == b.Session && a.Shop == b.Shop;
    async Task PollAsync()
    {
        if (polling || lease is null || !gate.Wait(0)) return;
        polling = true;
        try
        {
            try
            {
                session!.ValidateCode(true); var now = session.Snapshot();
                if (!lease.GuardAlive || !SameWorld(world!, now) || now.Visible == 0 || now.Context != Profiles.Npc) throw new IOException("Salesman penceresi / oturumu sona erdi.");
            }
            catch { Message = await RestoreAsync() ? "Salesman kapandı; geçici kod değişikliği geri alındı." : "Salesman geri alınamadı; işlemler engelli."; }
        }
        finally { polling = false; gate.Release(); }
    }
    async Task<bool> RestoreAsync()
    {
        timer.Stop();
        if (lease is null) return true;
        if (!await lease.FinishAsync()) { Ready = false; return false; }
        lease.Dispose(); lease = null; world = null; return true;
    }
    internal void Quiesce() { quiesced = true; Ready = false; }
    internal async Task<bool> StopAsync(bool terminal = false)
    {
        Quiesce(); await gate.WaitAsync();
        try { bool ok = await RestoreAsync(); if (ok) { session?.Dispose(); session = null; quiesced = terminal; Message = "Salesman durduruldu; geri alma doğrulandı."; } return ok; }
        finally { gate.Release(); }
    }
    public void Dispose() { timer.Dispose(); lease?.Dispose(); session?.Dispose(); gate.Dispose(); }
    internal static async Task<string> ProbeAsync()
    {
        return await Task.Run(() => { using var s = Session.Connect(); s.ValidateCode(false); s.Snapshot(); s.ValidateResource(); return $"SHA profili ve Salesman canlı yolu doğrulandı · PID {s.Pid}. Pencere açılmadı."; });
    }
}
