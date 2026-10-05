using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using PlayerXYZ;

namespace UnityTools.Salesman;

sealed record Journal(int Version, int Pid, long Created, long Base, string Sha, int ParentPid, long ParentCreated, string ParentPath, string Stem, bool Fixture);
sealed class Lease : IDisposable
{
    readonly Session session;
    readonly Mutex mutex;
    readonly EventWaitHandle ready = null!, done = null!;
    readonly Process guard = null!;
    readonly string path = "";
    bool finished;
    internal bool GuardAlive => !guard.HasExited;
    internal Lease(Session session)
    {
        this.session = session;
        mutex = new(false, Name(session.Pid, session.Created), out bool first);
        if (!first) { mutex.Dispose(); throw new IOException("Bu oyunda başka bir Salesman testi / koruması hâlâ açık."); }
        try
        {
            string stem = @"Local\4UnitySalesmanLease_" + Guid.NewGuid().ToString("N");
            ready = new(false, EventResetMode.ManualReset, stem + "_ready"); done = new(false, EventResetMode.ManualReset, stem + "_done");
            string dir = Path.Combine(Program.DataRoot, "Salesman", "journals"); Directory.CreateDirectory(dir); path = Path.Combine(dir, Guid.NewGuid().ToString("N") + ".json");
            using var parent = Process.GetCurrentProcess();
            var journal = new Journal(1, session.Pid, session.Created, session.Base, session.Profile.Sha, parent.Id, parent.StartTime.ToUniversalTime().ToFileTimeUtc(), Environment.ProcessPath!, stem, session.IsFixture);
            using (var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read)) { JsonSerializer.Serialize(file, journal); file.Flush(true); }
            var start = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden };
            if (Path.GetFileNameWithoutExtension(Environment.ProcessPath!).Equals("dotnet", StringComparison.OrdinalIgnoreCase)) start.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "4UnityTools.dll"));
            start.ArgumentList.Add("--salesman-guard"); start.ArgumentList.Add(path);
            if (session.IsFixture) start.ArgumentList.Add(Program.DataRoot);
            guard = Process.Start(start) ?? throw new IOException("Salesman koruması başlatılamadı.");
            if (!ready.WaitOne(10000) || guard.HasExited) throw new IOException("Geri alma koruması hazır değil; değişiklik yapılmadı.");
        }
        catch { done?.Set(); guard?.Dispose(); ready?.Dispose(); done?.Dispose(); mutex.Dispose(); throw; }
    }
    internal static string Name(int pid, long created) => @"Local\4UnityShopLease_" + pid + "_" + created;
    internal static int Guard(string path)
    {
        try
        {
            string full = Path.GetFullPath(path), dir = Path.GetFullPath(Path.Combine(Program.DataRoot, "Salesman", "journals")) + Path.DirectorySeparatorChar;
            if (!full.StartsWith(dir, StringComparison.OrdinalIgnoreCase)) throw new IOException("Salesman journal yolu geçersiz.");
            var j = JsonSerializer.Deserialize<Journal>(File.ReadAllText(full)) ?? throw new IOException("Salesman journal boş.");
            if (j.Version != 1 || !j.Stem.StartsWith(@"Local\4UnitySalesmanLease_", StringComparison.Ordinal)) throw new IOException("Salesman journal sürümü geçersiz.");
            using var parent = Process.GetProcessById(j.ParentPid);
            if (parent.StartTime.ToUniversalTime().ToFileTimeUtc() != j.ParentCreated || !string.Equals(parent.MainModule?.FileName, j.ParentPath, StringComparison.OrdinalIgnoreCase) || !string.Equals(j.ParentPath, Environment.ProcessPath, StringComparison.OrdinalIgnoreCase)) throw new IOException("Salesman koruma parent kimliği değişmiş.");
            using var session = j.Fixture ? Session.Fixture(j.Pid, j.Created, j.Base) : Session.Connect(false, j.Pid, j.Created, j.Base, j.Sha);
            if (session.Profile.Sha != j.Sha) throw new IOException("Guardian fixture/native SHA değişmiş.");
            session.ValidateClose(true);
            using var lease = Mutex.OpenExisting(Name(j.Pid, j.Created));
            using var ready = EventWaitHandle.OpenExisting(j.Stem + "_ready"); using var done = EventWaitHandle.OpenExisting(j.Stem + "_done");
            session.Log("GUARD_READY"); ready.Set(); var deadline = j.Fixture ? DateTime.UtcNow.AddSeconds(2) : DateTime.UtcNow.AddMinutes(10);
            while (!done.WaitOne(100) && !parent.HasExited && session.Alive && DateTime.UtcNow < deadline) { }
            if (session.Alive) session.SetClose(false); else session.Log("GUARD_TARGET_EXITED");
            File.Delete(full); return 0;
        }
        catch (Exception ex) { Diagnostics.Write("GUARD_ERROR " + ex); return 1; }
    }
    internal void Activate()
    {
        if (!GuardAlive) throw new IOException("Salesman koruma süreci sona erdi.");
        session.ValidateClose(false); session.SetClose(true);
    }
    internal async Task<bool> FinishAsync()
    {
        if (finished) return true;
        bool ok = true;
        try { if (session.Alive) session.SetClose(false); }
        catch (Exception ex) { ok = false; Diagnostics.Write("PARENT_RESTORE_ERROR " + ex); }
        done.Set();
        if (!guard.HasExited && !await Task.Run(() => guard.WaitForExit(5000))) return false;
        if (!guard.HasExited || guard.ExitCode != 0) return false;
        try { if (session.Alive) session.ValidateClose(false); } catch { return false; }
        if (ok) { finished = true; if (File.Exists(path)) File.Delete(path); } return ok;
    }
    public void Dispose()
    {
        if (!finished) done.Set(); guard.Dispose(); ready.Dispose(); done.Dispose(); mutex.Dispose();
    }
}
