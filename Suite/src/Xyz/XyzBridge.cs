using System.Diagnostics;
using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace UnityTools.Xyz;

sealed record Request(string Token, string Command, float[]? Values = null, long Window = 0);
sealed record Reply(bool Ready, bool Blocked, bool Success, string Message, float[]? Live = null, string[]? Draft = null);
interface IXyzClient { Task<Reply> SendAsync(string command, float[]? values = null); }

sealed class PipeClient(string pipe, string token) : IXyzClient
{
    internal Task<Reply> RegisterWindowAsync(nint window)=>SendRequestAsync(new(token,"window_ready",Window:window.ToInt64()));
    public async Task<Reply> SendAsync(string command, float[]? values = null)
        => await SendRequestAsync(new(token, command, values));
    async Task<Reply> SendRequestAsync(Request request)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        using var connection = new NamedPipeClientStream(".", pipe, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        await connection.ConnectAsync(1200, timeout.Token);
        using var reader = new StreamReader(connection, Encoding.UTF8, false, 1024, true);
        using var writer = new StreamWriter(connection, new UTF8Encoding(false), 1024, true) { AutoFlush = true };
        await writer.WriteLineAsync(JsonSerializer.Serialize(request).AsMemory(), timeout.Token);
        string line = await reader.ReadLineAsync(timeout.Token) ?? throw new IOException("Ana uygulama bağlantısı kapandı.");
        return JsonSerializer.Deserialize<Reply>(line) ?? throw new IOException("XYZ yanıtı okunamadı.");
    }
}

// The child has no game handle. Every write returns to the validated parent session and OperationGate.
sealed class WindowBridge(Control dispatcher, Func<Request, Task<Reply>> handler) : IDisposable
{
    internal const int ReopenMessage=0x8000+170, ExitMessage=0x8000+171;
    readonly string pipe = "4UnityTools.XYZ." + Guid.NewGuid().ToString("N");
    readonly string token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    readonly CancellationTokenSource stop = new();
    Task? server;
    Task? opening;
    Process? child;
    nint childWindow;
    string? companion;
    internal bool Open => child is { HasExited: false };
    internal int ChildId => child?.Id ?? 0;
    internal PipeClient Client => new(pipe, token);
    internal Task<Reply> DispatchAsync(Request request)
    {
        if (request.Token != token) return Task.FromResult(new Reply(false, true, false, "XYZ bağlantısı doğrulanamadı."));
        if (request.Command == "window_ready")
        {
            GetWindowThreadProcessId((nint)request.Window,out uint pid);
            if (!Open || pid != child!.Id) return Task.FromResult(new Reply(false,true,false,"XYZ pencere sahibi eşleşmedi."));
            childWindow=(nint)request.Window;
            return Task.FromResult(new Reply(false,false,true,"XYZ penceresi hazır."));
        }
        var completion = new TaskCompletionSource<Reply>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (stop.IsCancellationRequested || dispatcher.IsDisposed) return Task.FromResult(new Reply(false, true, false, "Ana uygulama kapalı."));
        try
        {
            dispatcher.BeginInvoke(async () =>
            {
                try { completion.TrySetResult(await handler(request)); }
                catch (Exception ex) { completion.TrySetResult(new(false, true, false, ex.Message)); }
            });
        }
        catch (Exception ex) { completion.TrySetResult(new(false, true, false, ex.Message)); }
        return completion.Task;
    }
    internal async Task OpenAsync(string? fixtureReport = null)
    {
        if (opening is not null) { await opening; return; }
        try { opening = OpenCoreAsync(fixtureReport); await opening; }
        finally { opening = null; }
    }
    async Task OpenCoreAsync(string? fixtureReport)
    {
        if (Open)
        {
            child!.Refresh(); nint window = childWindow != 0 ? childWindow : child.MainWindowHandle;
            if (window != 0) { PostMessage(window,ReopenMessage,0,0); ShowWindow(window, 9); SetForegroundWindow(window); }
            return;
        }
        server ??= ServeAsync();
        string source = Environment.ProcessPath!;
        bool managedTest = Path.GetFileNameWithoutExtension(source).Equals("dotnet", StringComparison.OrdinalIgnoreCase);
        string executable = source;
        if (!managedTest)
            executable = companion ??= await Task.Run(() =>
            {
                using var stream = File.OpenRead(source); string sha = Convert.ToHexString(SHA256.HashData(stream));
                string target = Path.Combine(Program.DataRoot, "companions", sha[..16], "PlayerXYZ.exe");
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                // Reuse the exact UAC-enabled binary; never overwrite a running companion.
                if (!File.Exists(target)) { File.Copy(source, target + ".tmp", true); File.Move(target + ".tmp", target, true); }
                using var saved = File.OpenRead(target);
                if (Convert.ToHexString(SHA256.HashData(saved)) != sha) throw new IOException("PlayerXYZ EXE doğrulanamadı.");
                return target;
            });
        var start = new ProcessStartInfo(executable) { UseShellExecute = false, WindowStyle = ProcessWindowStyle.Normal };
        if (managedTest) start.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "4UnityTools.dll"));
        foreach (string arg in new[] { "--xyz-window", pipe, token }) start.ArgumentList.Add(arg);
        start.ArgumentList.Add(Environment.ProcessId.ToString());
        if (managedTest) { start.ArgumentList.Add(Program.DataRoot); if (fixtureReport is not null) start.ArgumentList.Add(fixtureReport); }
        childWindow=0; child?.Dispose(); child = Process.Start(start) ?? throw new IOException("PlayerXYZ açılamadı.");
    }
    async Task ServeAsync()
    {
        while (!stop.IsCancellationRequested)
        {
            try
            {
                using var connection = new NamedPipeServerStream(pipe, PipeDirection.InOut, 1, PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await connection.WaitForConnectionAsync(stop.Token);
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stop.Token); timeout.CancelAfter(TimeSpan.FromSeconds(25));
                using var reader = new StreamReader(connection, Encoding.UTF8, false, 1024, true);
                using var writer = new StreamWriter(connection, new UTF8Encoding(false), 1024, true) { AutoFlush = true };
                // Commands never contain files or lists. Bound the request before deserializing it.
                var buffer = new StringBuilder(); var character = new char[1];
                while (await reader.ReadAsync(character.AsMemory(), timeout.Token) != 0 && character[0] != '\n')
                { if (buffer.Length >= 2048) throw new IOException("XYZ isteği çok büyük."); buffer.Append(character[0]); }
                var request = JsonSerializer.Deserialize<Request>(buffer.ToString()) ?? throw new IOException("XYZ isteği okunamadı.");
                var reply = await DispatchAsync(request).WaitAsync(timeout.Token);
                await writer.WriteLineAsync(JsonSerializer.Serialize(reply).AsMemory(), timeout.Token);
            }
            catch (OperationCanceledException) { }
            catch (IOException) { }
            catch (JsonException) { }
        }
    }
    public void Dispose() { stop.Cancel(); if(childWindow!=0)PostMessage(childWindow,ExitMessage,0,0); child?.Dispose(); }
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool PostMessage(nint window,int message,nint w,nint l);
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(nint window,out uint pid);
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool SetForegroundWindow(nint window);
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool ShowWindow(nint window, int command);
}
