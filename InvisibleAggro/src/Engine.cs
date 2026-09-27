using System.Text.Json;

namespace InvisibleAggro;

record Journal(string Sha, int Pid, long Created, long Base, long Player, uint PlayerId, List<OwnedCell> Cells);
record ViewState(bool Ready, StateMode Mode, string Message, int? Pid, uint? PlayerId, bool NeedsApproval);

sealed class Engine : IDisposable
{
    GameSession? session;
    PatchSet? patches;
    StateMode mode;
    readonly string journalPath;
    readonly string logPath;
    string message = "Oyun bekleniyor.";
    public Engine(string directory)
    {
        Directory.CreateDirectory(directory);
        journalPath = Path.Combine(directory, "owned-state.json");
        logPath = Path.Combine(directory, "events.jsonl");
    }
    void Log(string eventName, object details) => File.AppendAllText(logPath,
        JsonSerializer.Serialize(new { utc = DateTimeOffset.UtcNow, eventName, details }) + Environment.NewLine);
    public ViewState View => new(session is not null, mode, message, session?.Pid, session?.PlayerId, session is null && Profile.NeedsApproval);

    public void Poll()
    {
        if (session is null)
        {
            try { Attach(); }
            catch (Exception ex) { message = ex.Message; }
            return;
        }
        if (session.Exited)
        {
            Log("process_exited", new { session.Pid });
            Forget(); message = "Oyun kapandı. Yeni oturum bekleniyor."; return;
        }
        try
        {
            session.ValidatePlayer(); session.ValidateCode(patches!.Owned);
            if (mode != StateMode.Off && session.Read(session.Player + Profile.Stealth, 1)[0] != 1)
                throw new InvalidOperationException("Oyun Invisible alanını değiştirdi.");
            if (mode == StateMode.Aggro && session.Read(session.Player + Profile.Visual, 1)[0] != 255)
                throw new InvalidOperationException("Oyun Aggro alanını değiştirdi.");
        }
        catch (Exception ex)
        {
            try
            {
                Restore();
                Forget(); message = ex.Message + " Modlar kapatıldı.";
            }
            catch (Exception restoreError)
            {
                message = ex.Message + " Geri alma bekliyor: " + restoreError.Message;
            }
        }
    }
    void Attach()
    {
        GameSession? candidate = null;
        try
        {
            candidate = GameSession.Connect();
            session = candidate;
            patches = new PatchSet(candidate) { SaveJournal = Save };
            if (File.Exists(journalPath))
            {
                Journal j = JsonSerializer.Deserialize<Journal>(File.ReadAllText(journalPath)) ?? throw new IOException("Geri alma kaydı okunamadı.");
                if (j.Sha == Profile.ActiveSha && j.Pid == session.Pid && j.Created == session.Created && j.Base == session.Base)
                {
                    foreach (var c in j.Cells) ValidateJournalCell(j, c);
                    // Old player addresses are never dereferenced after a player/session change.
                    bool samePlayer = j.Player == session.Player && j.PlayerId == session.PlayerId;
                    patches.Import(j.Cells.Where(c => c.Code || samePlayer));
                    if (patches.Owned.Count > 0) Restore();
                    Log("previous_session_cleanup", new { samePlayer, recordedCells = j.Cells.Count });
                }
                else File.Delete(journalPath); // recorded process identity no longer applies
            }
            session.ValidateCode(patches.Owned);
            if (session.Read(session.Player + Profile.Stealth, 1)[0] > 1) throw new InvalidOperationException("Invisible alanı beklenen aralıkta değil.");
            mode = StateMode.Off;
            Profile.Commit();
            message = "Hazır · profil yüklendi · iki mod da kapalı";
            Log("ready", new { session.Pid, session.Created, module = $"0x{session.Base:X}", player = $"0x{session.Player:X}", session.PlayerId, sha = Profile.ActiveSha });
        }
        catch
        {
            // Keep owned writes reachable if recovery itself failed.
            if (patches?.Owned.Count > 0) { message = "Önceki değişikliklerin geri alınması gerekiyor."; throw; }
            candidate?.Dispose(); session = null; patches = null; throw;
        }
    }
    static void ValidateJournalCell(Journal j, OwnedCell c)
    {
        bool valid = c.Address == j.Base + Profile.Writer1 && c.Code && c.Original.SequenceEqual(Profile.Original1) && c.Expected.All(b => b == 0x90) && c.Expected.Length == 6 ||
                     c.Address == j.Base + Profile.Writer2 && c.Code && c.Original.SequenceEqual(Profile.Original2) && c.Expected.All(b => b == 0x90) && c.Expected.Length == 7 ||
                     c.Address == j.Player + Profile.Stealth && !c.Code && c.Original.Length == 1 && c.Original[0] <= 1 && c.Expected.SequenceEqual(new byte[] { 1 }) ||
                     c.Address == j.Player + Profile.Visual && !c.Code && c.Original.Length == 1 && c.Expected.SequenceEqual(new byte[] { 255 });
        if (!valid) throw new InvalidOperationException("Geri alma kaydı doğrulanamadı; uygulanmadı.");
    }
    void Save(List<OwnedCell> cells)
    {
        if (cells.Count == 0) { if (File.Exists(journalPath)) File.Delete(journalPath); return; }
        if (session is null) throw new InvalidOperationException("Oturum yok.");
        string temp = journalPath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(new Journal(Profile.ActiveSha, session.Pid, session.Created, session.Base, session.Player, session.PlayerId, cells)));
        File.Move(temp, journalPath, true);
    }
    public void ScanApproved()
    {
        if (session is not null) throw new InvalidOperationException("Önce mevcut oturumu kapatın.");
        try
        {
            Profile.ScanApproved();
            Attach();
            Log("profile_approved", new { sha = Profile.ActiveSha });
        }
        catch (Exception ex)
        {
            Profile.RejectCandidate();
            message = "Profil kaydedilmedi: " + ex.Message;
            throw;
        }
    }
    public void Toggle(StateMode selected)
    {
        var elapsed = System.Diagnostics.Stopwatch.StartNew();
        if (session is null) Attach();
        StateMode requested = mode == selected ? StateMode.Off : selected;
        try
        {
            if (requested == StateMode.Off) Restore();
            else
            {
                // Reuse resolved sites; live identity and original bytes are checked below.
                using var write = session!.BeginWrite();
                using var paused = new PausedThreads(session.Pid, session.Base);
                session.ValidatePlayer(); session.ValidateCode(patches!.Owned);
                if (session.Read(session.Player + Profile.Stealth, 1)[0] > 1) throw new InvalidOperationException("Invisible alanı doğrulanamadı.");
                var desired = new List<Cell> { new(session.Player + Profile.Stealth, [1], false) };
                if (requested == StateMode.Aggro)
                {
                    desired.Insert(0, new(session.Base + Profile.Writer1, Enumerable.Repeat((byte)0x90, 6).ToArray(), true));
                    desired.Insert(1, new(session.Base + Profile.Writer2, Enumerable.Repeat((byte)0x90, 7).ToArray(), true));
                    desired.Add(new(session.Player + Profile.Visual, [255], false));
                }
                patches.Apply(desired);
                mode = requested;
            }
            message = mode switch { StateMode.Invisible => "Invisible açık", StateMode.Aggro => "Aggro açık", _ => "İki mod da kapalı" };
            Log("mode_changed", new { mode = mode.ToString(), pid = session?.Pid, playerId = session?.PlayerId, ownedCells = patches?.Owned.Count ?? 0, elapsedMs = elapsed.ElapsedMilliseconds });
        }
        catch (Exception ex) { message = ex.Message; Log("mode_failed", new { requested = requested.ToString(), error = ex.Message }); throw; }
    }
    public void Restore()
    {
        if (session is null || patches is null) { mode = StateMode.Off; return; }
        if (session.Exited) { Forget(); return; }
        if (patches.Owned.Count == 0) { mode = StateMode.Off; return; }
        using var write = session.BeginWrite();
        using var paused = new PausedThreads(session.Pid, session.Base);
        if (!session.PlayerStillCurrent())
        {
            Log("player_changed_cleanup", new { session.Pid, dataFieldsNotWritten = true });
            patches.Import(patches.Owned.Where(c => c.Code));
        }
        patches.Apply([], restoring: true);
        mode = StateMode.Off;
        Log("restored", new { session.Pid });
    }
    void Forget()
    {
        if (session is not null && !session.Exited && patches?.Owned.Count > 0)
            throw new InvalidOperationException("Geri alınmamış değişiklikler var.");
        session?.Dispose(); session = null; patches = null; mode = StateMode.Off;
        if (File.Exists(journalPath)) File.Delete(journalPath);
    }
    public void Dispose() { Restore(); Forget(); }
}
