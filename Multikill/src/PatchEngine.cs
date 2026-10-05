using System.Text.Json;
using UnityTools.Controls;

namespace Multikill;

record PatchJournal(int Version, string Sha, int Pid, long Created, long Base, int Rva, string Original, string Patched, uint Protection);
interface IPatchSession : IDisposable
{
    Signature Build { get; }
    int Pid { get; } long Created { get; } long Base { get; } bool Exited { get; }
    byte[] ReadSite(); uint Protection(); void ValidateIdentity(); void WriteOpcode(byte value, uint restoreProtection); void RestoreProtection(uint protection);
}
sealed class PatchEngine : IDisposable
{
    readonly string directory, journalPath;
    readonly Func<IPatchSession> connect;
    IPatchSession? session;
    FileStream? owner;
    PatchJournal? owned;
    public bool Ready => session is not null && owned is null && !faulted;
    public bool Active => owned is not null;
    bool faulted, forceScan;
    Signature Build => session?.Build ?? Profile.Known;
    public string Message { get; private set; } = "Profili doğrula; Multikill başlangıçta kapalıdır.";
    public PatchEngine(string directory, Func<IPatchSession>? connect = null)
    { this.directory = directory; journalPath = Path.Combine(directory, "patch-recovery.json"); this.connect = connect ?? (() => Session.Connect(Path.Combine(directory, "profiles"), forceScan)); }
    public void Validate() => ValidateCore(true);
    public void PrepareReadOnly() => ValidateCore(false);
    void ValidateCore(bool allowRecovery)
    {
        if (session is not null) { Poll(); return; }
        Directory.CreateDirectory(directory);
        owner ??= new FileStream(Path.Combine(directory, "owner.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        try
        {
            session = connect();
            if (File.Exists(journalPath))
            {
                var j = JsonSerializer.Deserialize<PatchJournal>(File.ReadAllText(journalPath)) ?? throw new IOException("Kurtarma kaydı okunamadı.");
                if (j.Pid == session.Pid && j.Created == session.Created && j.Base == session.Base)
                {
                    ValidateJournal(j, Build);
                    if (!allowRecovery) throw new IOException("Önce Multikill sayfasında Profili doğrula ile bekleyen oturum geri almasını tamamla.");
                    owned = j; Restore();
                }
                else File.Move(journalPath, journalPath + ".stale-" + DateTime.UtcNow.Ticks); // Never write to a reused PID.
            }
            session.ValidateIdentity(); Profile.ValidateLive(session.ReadSite(), false, Build); faulted = false;
            Profile.SaveValidated(Build, Path.Combine(directory, "profiles"));
            Message = $"Profil doğrulandı · PID {session.Pid} · {(Build.Sha256 == Profile.Known.Sha256 ? "Bilinen build" : "Patch Recovery: semantik eşleşme")} · RVA {Build.PatchRva:X} · Multikill kapalı";
        }
        catch
        {
            faulted = true;
            if (owned is null) { session?.Dispose(); session = null; owner?.Dispose(); owner = null; }
            throw;
        }
    }
    internal static void ValidateJournal(PatchJournal j, Signature? build = null)
    {
        build ??= Profile.Known;
        if (j.Version != 1 || j.Sha != build.Sha256 || j.Rva != build.PatchRva || j.Original != build.Original || j.Patched != build.Patched || j.Pid <= 0 || j.Created <= 0 || j.Base < 0x10000 || j.Protection is not (0x10 or 0x20 or 0x40 or 0x80))
            throw new IOException("Kurtarma kaydı doğrulanamadı; bellek değiştirilmedi.");
    }
    void Save(PatchJournal journal)
    {
        byte[] data = JsonSerializer.SerializeToUtf8Bytes(journal);
        using (var file = new FileStream(journalPath + ".tmp", FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough)) { file.Write(data); file.Flush(true); }
        File.Move(journalPath + ".tmp", journalPath, true);
    }
    public void Rescan()
    {
        Dispose(); faulted = false; forceScan = true;
        try { Validate(); } finally { forceScan = false; }
    }
    public void Toggle()
    {
        if (owned is not null) { Restore(); return; }
        OperationGate.Check();
        if (!Ready) throw new IOException("Önce profil doğrulamasını tamamla.");
        session!.ValidateIdentity(); Profile.ValidateLive(session.ReadSite(), false, Build);
        var j = new PatchJournal(1, Build.Sha256, session.Pid, session.Created, session.Base, Build.PatchRva, Build.Original, Build.Patched, session.Protection());
        Save(j); owned = j; // durable recovery BEFORE changing page protection or opcode
        try
        {
            OperationGate.Check(); session.ValidateIdentity(); Profile.ValidateLive(session.ReadSite(), false, Build);
            session.WriteOpcode(0x84, j.Protection); Profile.ValidateLive(session.ReadSite(), true, Build);
            OwnedCodePatchRegistry.Register(this, ValidateOwnedPatch);
            Message = "Multikill JE açık · branch doğrulandı";
        }
        catch { faulted = true; try { Restore(); } catch { } throw; }
    }
    public void Restore()
    {
        OwnedCodePatchRegistry.Remove(this);
        if (owned is null) return;
        if (session is null) throw new IOException("Kurtarma oturumu yok.");
        if (!session.Exited)
        {
            session.ValidateIdentity(); var live = session.ReadSite();
            try { Profile.ValidateLive(live, false, Build); }
            catch { Profile.ValidateLive(live, true, Build); session.WriteOpcode(0x86, owned.Protection); }
            session.RestoreProtection(owned.Protection); Profile.ValidateLive(session.ReadSite(), false, Build);
        }
        File.Delete(journalPath); owned = null; faulted = false; Message = "Multikill kapalı · geri alma doğrulandı";
    }
    public void Poll()
    {
        if (session is null) return;
        if (session.Exited) { Restore(); session.Dispose(); session = null; Message = "Oyun kapandı; yeni oturumu doğrula."; return; }
        try { session.ValidateIdentity(); Profile.ValidateLive(session.ReadSite(), Active, Build); }
        catch { faulted = true; OwnedCodePatchRegistry.Remove(this); throw; }
    }
    bool ValidateOwnedPatch(OwnedCodePatchQuery query)
    {
        var s = session; var j = owned; var lease = owner;
        if (faulted || s is null || j is null || s.Exited || lease is null || lease.SafeFileHandle.IsClosed ||
            query.Pid != j.Pid || query.Created != j.Created || query.ModuleBase != j.Base || query.Sha != j.Sha || query.Rva != j.Rva ||
            !query.Original.SequenceEqual(Convert.FromHexString(j.Original)) || !query.Patched.SequenceEqual(Convert.FromHexString(j.Patched))) return false;
        ValidateJournal(j, s.Build);
        if (JsonSerializer.Deserialize<PatchJournal>(File.ReadAllText(journalPath)) != j) return false;
        s.ValidateIdentity(); Profile.ValidateLive(s.ReadSite(), true, s.Build);
        return ReferenceEquals(owned, j) && ReferenceEquals(session, s) && !faulted && !lease.SafeFileHandle.IsClosed;
    }
    public void Dispose() { Restore(); session?.Dispose(); session = null; owner?.Dispose(); owner = null; }
}
