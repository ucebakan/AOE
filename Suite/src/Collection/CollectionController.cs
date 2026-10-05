using UnityTools.Controls;

namespace UnityTools.Collection;

sealed class DeathObserver
{
    record State(Row Row, long Event);
    readonly Dictionary<uint, State> states = [];
    readonly LinkedList<State> pending = [];
    long serial;
    static bool Same(Row a, Row b) => a.Id == b.Id && a.Actor == b.Actor && a.Node == b.Node;
    internal bool HasPending => pending.Count > 0;
    internal void Observe(IEnumerable<Row> rows)
    {
        var next = new Dictionary<uint, State>();
        foreach (var row in rows)
        {
            bool same = states.TryGetValue(row.Id, out var old) && Same(row, old.Row);
            long stamp = same && old!.Row.Dead == row.Dead ? old.Event : ++serial;
            var state = new State(row, stamp); if (!next.TryAdd(row.Id, state)) throw new IOException("Collection duplicate registry ID.");
            if (same && !old!.Row.Dead && row.Dead) pending.AddLast(state);
        }
        states.Clear(); foreach (var pair in next) states.Add(pair.Key, pair.Value);
        for (var node = pending.First; node is not null;)
        {
            var following = node.Next; var p = node.Value;
            if (!states.TryGetValue(p.Row.Id, out var current) || current.Event != p.Event || !Same(current.Row, p.Row) || !current.Row.Dead) pending.Remove(node);
            node = following;
        }
        if (pending.Count > 4096) throw new IOException("Collection ölüm kuyruğu sınır dışı.");
    }
    internal List<Row> TakeBatch()
    {
        var result = new List<Row>();
        while (pending.First is { } first && result.Count < 64) { result.Add(first.Value.Row); pending.RemoveFirst(); }
        return result;
    }
}

sealed class Controller : IDisposable
{
    readonly Func<bool, IEngine> factory;
    readonly SemaphoreSlim gate = new(1);
    IEngine? engine;
    CancellationTokenSource? stop;
    Task<bool>? worker;
    volatile bool ready, active, quiesced;
    string message = "Collection profili ve canlı yolu doğrula.";
    internal bool Ready => ready;
    internal bool Active => active;
    internal string Message => Volatile.Read(ref message);
    internal Controller(Func<bool, IEngine>? factory = null)
    {
        if (factory is not null) this.factory = factory;
        else { var bridge = new NativeBridge(); this.factory = force => Session.Connect(force, bridge); }
    }
    internal async Task PrepareAsync(bool force = false)
    {
        if (force && Active && !await StopAsync()) throw new IOException("Collection çağrı temizliği henüz tamamlanmadı.");
        await gate.WaitAsync();
        try
        {
            if (quiesced) throw new IOException("Collection kapanışı / SafeMode durdurması sürüyor.");
            if (active) return; // Never refresh a running reader through a second session.
            var next = await Task.Run(() => { var e = factory(force); try { e.ReadWorld(); return e; } catch { e.Dispose(); throw; } });
            engine?.Dispose(); engine = next; ready = true;
            message = $"Collection hazır · {next.Identity} · {(Profiles.Cached ? "SHA profili yüklendi" : "profil doğrulandı")}. Toplama kapalı.";
        }
        catch (Exception ex) { ready = false; message = ex.Message; throw; }
        finally { gate.Release(); }
    }
    internal async Task<string> ToggleAsync()
    {
        if (Active) { if (!await StopAsync()) throw new IOException(Message); return Message; }
        await gate.WaitAsync();
        try
        {
            OperationGate.Check(); OperationGate.CheckContinuous();
            if (quiesced || !ready || engine is null) throw new IOException("Collection canlı yolunu önce doğrula.");
            stop?.Dispose(); stop = new(); var source = engine;
            var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            worker = Task.Run(() => Run(source, stop.Token, started)); await started.Task;
            return Message;
        }
        finally { gate.Release(); }
    }
    bool Run(IEngine source, CancellationToken token, TaskCompletionSource started)
    {
        Mutex? mutex = null; bool owned = false;
        string end = "Collection kapalı; yeni loot isteği gönderilmez.";
        try
        {
            if (source is Session real)
            {
                mutex = new(false, $@"Local\4UnityCollectionAuto_{real.Pid}_{real.Created:x16}");
                try { owned = mutex.WaitOne(0); } catch (AbandonedMutexException) { owned = true; }
                if (!owned) throw new IOException("Başka Collection otomasyonu aktif; V7 / eski araçta durdur.");
            }
            OperationGate.Check(); OperationGate.CheckContinuous(); token.ThrowIfCancellationRequested();
            source.Begin(); token.ThrowIfCancellationRequested();
            var observer = new DeathObserver(); observer.Observe(source.ReadWorld());
            active = true; message = "Collection açık · hedef seçmeden yeni ölümlerden hızlı grup toplama.";
            started.TrySetResult(); Diagnostics.Write("START " + source.Identity);
            while (!token.IsCancellationRequested)
            {
                OperationGate.Check(); OperationGate.CheckContinuous();
                observer.Observe(source.ReadWorld());
                if (token.IsCancellationRequested) break;
                var rows = observer.TakeBatch();
                if (rows.Count > 0)
                {
                    OperationGate.Check(); OperationGate.CheckContinuous(); if (token.IsCancellationRequested) break;
                    var result = source.Dispatch(rows);
                    message = $"Collection açık · son grup: {result.Sent} istek, {result.Skipped} atlandı. Loot hakkını sunucu kontrol eder.";
                    if (result.Cancelled) break;
                }
                if (!observer.HasPending && token.WaitHandle.WaitOne(50)) break;
            }
        }
        catch (OperationCanceledException) { started.TrySetCanceled(); }
        catch (Exception ex) { ready = false; end = "Collection durdu: " + ex.Message; started.TrySetException(ex); Diagnostics.Write("STOP_ERROR " + ex); }
        finally
        {
            source.Cancel();
            // Retain the session/mutex while a native callback or unhook is pending.
            // The UI never blocks on this loop; StopAsync reports pending and can be retried.
            while (!source.Cleanup()) { active = true; ready = false; message = "Collection durdu; sürmekte olan çağrının temizliği bekleniyor."; Thread.Sleep(50); }
            source.Dispose(); if (ReferenceEquals(engine, source)) engine = null;
            active = ready = false; message = end;
            if (owned) mutex!.ReleaseMutex(); mutex?.Dispose(); Diagnostics.Write("STOP_CLEAN");
        }
        return true;
    }
    internal void Quiesce() { quiesced = true; ready = false; stop?.Cancel(); engine?.Cancel(); }
    internal async Task<bool> StopAsync(bool terminal = false)
    {
        Quiesce(); await gate.WaitAsync();
        try
        {
            if (worker is not null)
            {
                if (await Task.WhenAny(worker, Task.Delay(8000)) != worker) return false;
                await worker; worker = null;
            }
            if (engine is not null)
            {
                if (!engine.Cleanup()) return false;
                engine.Dispose(); engine = null;
            }
            active = ready = false; quiesced = terminal; message = "Collection kapalı; çağrı temizliği tamamlandı."; return true;
        }
        finally { gate.Release(); }
    }
    public void Dispose() { Quiesce(); if (worker is null || worker.IsCompleted) { engine?.Dispose(); engine = null; stop?.Dispose(); } }
    internal static Task<string> ProbeAsync() => Task.Run(() => { using var s = Session.Connect(); s.ReadWorld(); return $"Collection SHA profili / canlı mob listesi doğrulandı · {s.Identity}. Toplama açılmadı."; });
}
