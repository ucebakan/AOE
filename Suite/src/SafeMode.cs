using System.Runtime.InteropServices;
using UnityTools.Controls;

namespace UnityTools;

interface ICountReader : IDisposable { Task<ulong?> ReadAsync(); }
sealed class PreviewCountReader : ICountReader
{
    public Task<ulong?> ReadAsync() => Task.FromResult<ulong?>(null);
    public void Dispose() { }
}

sealed class NativeCountReader : ICountReader
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate nint Create();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int Poll(nint reader, out ulong count);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate void Destroy(nint reader);
    readonly Poll poll;
    readonly Destroy destroy;
    readonly nint reader;
    public NativeCountReader()
    {
        poll = NativeModules.Function<Poll>("UnityCounter.dll", "PollCount");
        destroy = NativeModules.Function<Destroy>("UnityCounter.dll", "DestroyCountReader");
        reader = NativeModules.Function<Create>("UnityCounter.dll", "CreateCountReader")();
    }
    public Task<ulong?> ReadAsync() => Task.Run<ulong?>(() => poll(reader, out var count) != 0 ? count : null);
    public void Dispose() => destroy(reader);
}

sealed class SafeMode : IDisposable
{
    readonly Func<ICountReader> factory;
    readonly Func<Task<bool>> restore;
    ICountReader? reader;
    Task? pending;
    bool disposed, restored, needsCleanup, samplingPaused;
    DateTime retry;
    public bool Enabled { get; private set; }
    public bool Blocked { get; private set; }
    public bool Busy => pending is { IsCompleted: false };
    public string Message { get; private set; } = "Kapalı · eşik: Player Count ≥ 1";
    public SafeMode(Func<ICountReader> factory, Func<Task<bool>> restore) { this.factory = factory; this.restore = restore; }
    void Block(bool value) { Blocked = value; OperationGate.Blocked = value; }
    public async Task ToggleAsync()
    {
        if (Busy) await pending!;
        if (Enabled)
        {
            // A failed cleanup must remain blocked, even if the user turns monitoring off.
            if (Blocked && !restored) { Message = "Geri alma tamamlanmadı; SafeMode açık kalıyor."; return; }
            Enabled = false; Block(false); Message = "SafeMode kapalı."; return;
        }
        reader ??= factory(); Enabled = true; restored = needsCleanup = false; retry = DateTime.MinValue; Block(true);
        Message = "Sayaç doğrulanıyor…"; await SampleAsync();
    }
    public Task SampleAsync()
    {
        if (disposed || !Enabled || samplingPaused) return Task.CompletedTask;
        if (Busy) return pending!;
        return pending = SampleCore();
    }
    async Task SampleCore()
    {
        ulong? count = null;
        try { count = await reader!.ReadAsync(); } catch { }
        if (disposed) return;
        bool unsafeCount = count is null || count >= 1;
        if (!unsafeCount && (!needsCleanup || restored))
        {
            Block(false); restored = needsCleanup = false; retry = DateTime.MinValue; Message = "SafeMode açık · Player Count: 0"; return;
        }
        OperationGate.StopContinuous = true; Block(true); needsCleanup = true;
        string reason = count is null ? "Sayaç doğrulanamadı" : $"Player Count: {count}";
        if (!restored && DateTime.UtcNow >= retry)
        {
            Message = reason + " · işlemler geri alınıyor…";
            try { restored = await restore(); } catch { restored = false; }
            retry = DateTime.UtcNow.AddSeconds(2);
        }
        Message = reason + (restored ? " · işlemler kapalı" : " · geri alma tamamlanmadı; yeniden deneniyor");
    }
    public async Task StopAsync() { Enabled = false; if (pending is not null) await pending; }
    public async Task PauseAsync() { samplingPaused = true; if (pending is not null) await pending; }
    public void Resume() { samplingPaused = false; }
    public void Dispose()
    {
        disposed = true; Enabled = false;
        if (Busy) { _ = pending!.ContinueWith(_ => reader?.Dispose()); } else reader?.Dispose();
        Block(false);
    }
}
