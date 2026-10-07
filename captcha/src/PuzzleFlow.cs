namespace UnityPuzzleTest;

enum ReadingKind { Absent, Uncertain, Ready }
record Option(string Word, Point Center);
record Reading(ReadingKind Kind, string Target, int Step, int Attempts, Option[] Options, string Detail)
{
    public static Reading Absent => new(ReadingKind.Absent, "", 0, -1, [], "Test ekranı görünmüyor");
    public static Reading Unknown(string detail) => new(ReadingKind.Uncertain, "", 0, -1, [], detail);
    public string Key => $"{Step}:{Target}:{Attempts}:" + string.Join("|", Options.Select(o => $"{o.Word}@{o.Center.X},{o.Center.Y}"));
    public bool Valid => Kind == ReadingKind.Ready && Step is >= 1 and <= 4 && Attempts > 0 &&
        Options.Length == 4 && Options.Select(o => o.Word).Distinct().Count() == 4 && Options.Count(o => o.Word == Target) == 1;
}

// This model is also the future farm integration boundary. It never sends input itself.
sealed class PuzzleFlow
{
    public bool Blocked { get; private set; }
    public bool Faulted { get; private set; }
    public bool Complete { get; private set; }
    public int LastClicked { get; private set; }
    public string Status { get; private set; } = "Hazır";
    string stableKey = "";
    int stableCount, absentCount;
    double deadline, firstAbsent;
    public void Reset()
    {
        Blocked = Faulted = Complete = false; LastClicked = stableCount = absentCount = 0;
        deadline = firstAbsent = 0; stableKey = ""; Status = "Test ekranı bekleniyor";
    }
    public void Fail(string reason) { Faulted = Blocked = true; Complete = false; Status = reason; }
    public Option? Observe(Reading r, double now)
    {
        if (Faulted) return null;
        if (r.Kind == ReadingKind.Absent)
        {
            stableKey = ""; stableCount = 0;
            if (!Blocked) { if (!Complete) Status = "Test ekranı bekleniyor"; return null; }
            if (absentCount++ == 0) firstAbsent = now;
            if (absentCount >= 3 && now - firstAbsent >= 1)
            {
                if (LastClicked == 4) { Blocked = false; Complete = true; Status = "4/4 tamamlandı; ekran kapandı"; deadline = 0; }
                else Fail("Ekran erken kapandı; tamamlanma doğrulanamadı. Durdur / başlat.");
            }
            if (deadline > 0 && now >= deadline && Blocked) Fail("Ekranın kapanması doğrulanamadı. Durdur / başlat.");
            return null;
        }
        absentCount = 0;
        if (Complete)
        {
            Reset();
            Blocked = true;
            if (!r.Valid || r.Step != 1) { Status = "Yeni test için 1/4 bekleniyor"; return null; }
        }
        Blocked = true;
        if (deadline > 0 && now >= deadline) { Fail("Sonraki adım gelmedi; tekrar tıklanmadı. Durdur / başlat."); return null; }
        if (!r.Valid) { stableKey = ""; stableCount = 0; Status = r.Detail.Length > 0 ? r.Detail : "Okuma belirsiz; tıklama bekletiliyor"; return null; }
        if (r.Step < LastClicked || r.Step > LastClicked + 1) { Fail("Beklenmeyen adım sırası; tıklama durdu."); return null; }
        if (r.Step == LastClicked) { stableKey = ""; stableCount = 0; Status = LastClicked == 4 ? "4/4 seçildi; ekranın kapanması bekleniyor" : $"{LastClicked}/4 seçildi; sonraki adım bekleniyor"; return null; }
        if (r.Key != stableKey) { stableKey = r.Key; stableCount = 1; Status = $"{r.Step}/4 okunuyor; ikinci görüntü bekleniyor"; return null; }
        if (++stableCount < 2) return null;
        Option choice = r.Options.Single(o => o.Word == r.Target);
        LastClicked = r.Step; deadline = now + 12; stableKey = ""; stableCount = 0;
        Status = $"{r.Step}/4 · {r.Target} seçiliyor";
        return choice;
    }
}
