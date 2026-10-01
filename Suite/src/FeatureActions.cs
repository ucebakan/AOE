namespace UnityTools;

enum Feature { Coordinates, Speed, Jump, Invisible, Aggro, MobTP, Aoe, Counter, SafeMode, Multikill }
record FeatureState(bool Ready, bool Active, string Message);
record ActionReply(bool RequiresSetup, string Message);

interface IFeatureBackend
{
    FeatureState Read(Feature feature);
    Task<FeatureState> PrepareAsync(Feature feature);
    Task<string> ApplyAsync(Feature feature);
}

// Validation and action dispatch are shared by overview controls and tests.
sealed class FeatureActions(IFeatureBackend backend)
{
    internal bool Busy { get; private set; }
    internal FeatureState Read(Feature feature) => backend.Read(feature);
    internal async Task<ActionReply> ExecuteAsync(Feature feature)
    {
        if (Busy) return new(false, "İşlem sürüyor; tamamlanmasını bekle.");
        Busy = true;
        try
        {
            var current = await backend.PrepareAsync(feature);
            if (!current.Ready && !current.Active) return new(true, current.Message);
            return new(false, await backend.ApplyAsync(feature));
        }
        catch (Exception ex) { return new(true, ex.Message); }
        finally { Busy = false; }
    }
    internal static int Page(Feature f) => f switch { Feature.Coordinates => 1, Feature.Speed or Feature.Jump => 2, Feature.Invisible or Feature.Aggro => 3, Feature.MobTP => 4, Feature.Aoe => 5, Feature.Multikill => 8, _ => 0 };
    internal static string Name(Feature f) => f switch { Feature.Coordinates => "Işınlan", Feature.Speed => "Speed", Feature.Jump => "Jump", Feature.Invisible => "Invisible", Feature.Aggro => "Aggro", Feature.MobTP => "MobTP uygula", Feature.Aoe => "AOE", Feature.SafeMode => "SafeMode", Feature.Multikill => "Multikill", _ => "PlayerCounter" };
    internal static bool OneShot(Feature f) => f is Feature.Coordinates or Feature.MobTP;
}

sealed class LiveFeatureBackend(ToolWorkspace workspace, bool preview, SafeMode safeMode) : IFeatureBackend
{
    public FeatureState Read(Feature feature)
    {
        if (feature == Feature.SafeMode) return new(true, safeMode.Enabled, safeMode.Message);
        if (feature == Feature.Counter) return new(true, workspace.Counter.Active, "Küçük oyun üstü pencere");
        int page = FeatureActions.Page(feature);
        if (page == 5) return new(workspace.Aoe?.Ready == true, workspace.Aoe?.Active == true, "AOE sayfasında ATTACH NOW ile bağlantıyı doğrula. Live Validation gerekiyorsa tamamlanmasını bekle.");
        if (!workspace.Forms.TryGetValue(page, out var form)) return new(false, false, "İlk kullanımda bağlantı ve gerekli ayarlar kontrol edilir.");
        return form switch
        {
            PlayerXYZ.MainForm xyz => new(xyz.SuiteReady && xyz.SuiteHasCoordinates, false, xyz.SuiteHasCoordinates ? xyz.SuiteMessage : "Player XYZ sayfasında X, Y ve Z değerlerini gir ve bağlantıyı doğrula."),
            SpeedJump.MainForm speed => new(speed.SuiteReady, speed.SuiteActive(feature == Feature.Jump), speed.SuiteMessage),
            InvisibleAggro.MainForm invisible => new(invisible.SuiteReady, invisible.SuiteActive(feature == Feature.Aggro), invisible.SuiteMessage),
            Multikill.MainForm multi => new(multi.SuiteReady, multi.SuiteActive, multi.SuiteMessage),
            MobTP.MainForm mob => new(mob.SuiteReady, false, mob.SuiteValidation),
            _ => new(false, false, "Araç hazır değil.")
        };
    }
    public async Task<FeatureState> PrepareAsync(Feature feature)
    {
        if (feature == Feature.Counter) return Read(feature);
        int page = FeatureActions.Page(feature); workspace.Ensure(page);
        if (preview) return new(false, false, "Önizlemede oyun bağlantısı ve yazma işlemleri kapalı.");
        if (page != 5)
        {
            switch (workspace.Forms[page])
            {
                case PlayerXYZ.MainForm xyz: await xyz.SuiteRefreshAsync(); break;
                case SpeedJump.MainForm speed: await speed.SuiteRefreshAsync(); break;
                case InvisibleAggro.MainForm invisible: await invisible.SuiteRefreshAsync(); break;
                case Multikill.MainForm multi: await multi.SuiteRefreshAsync(); break;
                case MobTP.MainForm mob: await mob.SuiteRefreshAsync(); break;
            }
        }
        return Read(feature);
    }
    public async Task<string> ApplyAsync(Feature feature)
    {
        if (feature == Feature.Counter) { workspace.Counter.Toggle(preview); return workspace.Counter.Active ? "PlayerCounter küçük penceresi açıldı. Sayaç alanından sürükleyebilirsin." : "PlayerCounter kapatıldı."; }
        UnityTools.Controls.OperationGate.Check();
        if (preview) throw new InvalidOperationException("Önizlemede oyun işlemi uygulanamaz.");
        switch (feature)
        {
            case Feature.Coordinates:
                var xyz = (PlayerXYZ.MainForm)workspace.Forms[1]; await xyz.SuiteWriteAsync(); return xyz.SuiteMessage;
            case Feature.Speed or Feature.Jump:
                var speed = (SpeedJump.MainForm)workspace.Forms[2]; await speed.SuiteToggleAsync(feature == Feature.Jump); return speed.SuiteMessage;
            case Feature.Invisible or Feature.Aggro:
                var invisible = (InvisibleAggro.MainForm)workspace.Forms[3]; await invisible.SuiteToggleAsync(feature == Feature.Aggro); return invisible.SuiteMessage;
            case Feature.Multikill:
                var multi = (Multikill.MainForm)workspace.Forms[8]; await multi.SuiteToggleAsync(); return multi.SuiteMessage;
            case Feature.MobTP:
                var mob = (MobTP.MainForm)workspace.Forms[4]; await mob.SuiteTeleportAsync(); return mob.SuiteMessage;
            case Feature.Aoe:
                if (!workspace.Aoe!.Toggle()) throw new InvalidOperationException("AOE doğrulaması tamamlanmadı. İşlev sayfasını kontrol et.");
                return workspace.Aoe.Active ? "AOE continuous arm açık." : "AOE continuous arm kapalı; sürmekte olan cast tamamlanır, tekrar arm edilmez.";
            default: throw new ArgumentOutOfRangeException(nameof(feature));
        }
    }
}
