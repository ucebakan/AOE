using System.Runtime.InteropServices;
using UnityTools.Controls;

namespace UnityTools;

sealed class SuiteForm : Form
{
    internal static readonly string[] Names = ["Genel bakış", "Player XYZ", "Speed / Jump", "Invisible / Aggro", "MobTP", "AOE Manager", "PlayerCounter", "SafeMode", "Multikill"];
    static readonly string[] Descriptions = ["İşlevleri buradan aç, kapat veya uygula. Ayrıntılar için karttaki ayarlar bağlantısını kullan.",
        "Canlı koordinatları kontrol et ve hedef X, Y, Z değerlerini hazırla.", "Speed ve Jump bağımsız çalışır.",
        "Mod durumunu ve gerekirse yeni build doğrulamasını kontrol et.", "Mesafeyi, listeyi ve oyun içi kısayolu yönet.",
        "Bağlantı, doğrulama ve AOE çalışma ayarları.", "", "", "JE işlevi, AOB / profil doğrulaması ve patch recovery."];
    readonly Label info = Responsive.Text("Genel bakış düğmeleri işlevleri çalıştırır. İlk kullanımda gerekli doğrulama kontrol edilir."),
        title = Responsive.Text("", 22, true), subtitle = Responsive.Text("");
    readonly TextBox footer = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill, BorderStyle = BorderStyle.None, BackColor = Palette.Elevated, ForeColor = Palette.Muted };
    readonly Panel viewport = new() { Dock = DockStyle.Fill, BackColor = Palette.Background, Margin = new(0, 4, 0, 4), MinimumSize = new(0, 160) };
    readonly FlowLayoutPanel navigation = Responsive.Flow();
    readonly List<NavButton> buttons = new();
    readonly System.Windows.Forms.Timer refresh = new() { Interval = 400 };
    readonly Action<string>? notify;
    readonly bool preview;
    bool scanDialogOpen;
    internal readonly ToolWorkspace Workspace;
    internal readonly FeatureActions Actions;
    internal readonly SafeMode Safety;
    internal readonly OverviewPanel Overview;
    bool closing, allowClose, exitRequested;
    readonly StartupScan startupScan = new();
    readonly System.Windows.Forms.Timer sessionWatch = new() { Interval = 10000 };
    CancellationTokenSource? scanCancel;
    Task? scanTask;
    bool scanAccepted;
    string scannedSession = "";
    DateTime retryUntil;
    internal int SelectedPage { get; private set; }

    public SuiteForm(bool preview = false, IFeatureBackend? backend = null, Action<string>? notify = null, ICountReader? countReader = null)
    {
        this.notify = notify; this.preview = preview;
        Text = "4UnityTools • Control Center"; BackColor = Palette.Background; ForeColor = Palette.Ink;
        AutoScaleDimensions = new(96, 96); AutoScaleMode = AutoScaleMode.Dpi; Font = new("Segoe UI", 10);
        DoubleBuffered = true; ClientSize = new(1240, 900); MinimumSize = new(740, 560); StartPosition = FormStartPosition.CenterScreen;
        Workspace = new(viewport, preview);
        Safety = new(() => countReader ?? (preview ? new PreviewCountReader() : new NativeCountReader()), RestoreForSafety);
        Actions = new(backend ?? new LiveFeatureBackend(Workspace, preview, Safety));
        Overview = new(f => _ = ExecuteFeatureAsync(f), OpenSettings, Workspace.Coordinates, Path.Combine(Program.DataRoot, "card-order.json")); viewport.Controls.Add(Overview); Overview.LayoutError += message => footer.Text = message;

        var chromeScroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
        var layout = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 1, RowCount = 6, Padding = new(24, 12, 24, 10) };
        layout.ColumnStyles.Add(new(SizeType.Percent, 100));
        for (int i = 0; i < 4; i++) layout.RowStyles.Add(new(SizeType.AutoSize));
        layout.RowStyles.Add(new(SizeType.Percent, 100)); layout.RowStyles.Add(new(SizeType.Absolute, 60));
        chromeScroll.Controls.Add(layout); Controls.Add(chromeScroll);
        var brand = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, ColumnCount = 2, Margin = new(0, 0, 0, 12) };
        brand.ColumnStyles.Add(new(SizeType.Percent, 100)); brand.ColumnStyles.Add(new(SizeType.AutoSize));
        var branding = new ContentStack { Padding = new(0) }; branding.AddRow(Responsive.Text("4UNITY  /  TOOLS", 19, true));
        var edition = Responsive.Text("CONTROL CENTER", 8); edition.ForeColor = Palette.Muted; branding.AddRow(edition); brand.Controls.Add(branding, 0, 0);
        var version = Responsive.Text(typeof(SuiteForm).Assembly.GetName().Version!.ToString(3), 11); version.ForeColor = Palette.Accent; version.Anchor = AnchorStyles.Right; brand.Controls.Add(version, 1, 0); layout.Controls.Add(brand, 0, 0);
        var information = new ContentStack { BackColor = Palette.Elevated, Padding = new(12, 3, 12, 3), Margin = new(0, 0, 0, 6) };
        info.ForeColor = Palette.Muted; information.AddRow(info); layout.Controls.Add(information, 0, 1);
        navigation.Margin = new(0, 0, 0, 4);
        foreach (int i in new[] { 0, 1, 2, 3, 4, 5, 8 })
        {
            int index = i; var button = new NavButton { Text = Names[i], AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new(14, 10, 14, 10), Margin = new(0, 3, 5, 3) };
            button.Click += (_, _) => SelectPage(index); buttons.Add(button); navigation.Controls.Add(button);
        }
        var scanButton = new NavButton { Text = "Tarama / durum", AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new(14, 10, 14, 10), Margin = new(0, 3, 5, 3) };
        scanButton.Click += (_, _) => ShowScanDialog(); navigation.Controls.Add(scanButton);
        layout.Controls.Add(navigation, 0, 2);
        var heading = new ContentStack { Padding = Padding.Empty, Margin = new(0, 0, 0, 6) }; subtitle.ForeColor = Palette.Muted;
        heading.AddRow(title); heading.AddRow(subtitle); layout.Controls.Add(heading, 0, 3); layout.Controls.Add(viewport, 0, 4);
        layout.Controls.Add(footer, 0, 5); footer.Text = preview ? "ÖNİZLEME · Oyun bağlantısı ve yazma işlemleri kapalı." : "HAZIR · Açık özellikler sayfa değiştirirken çalışmaya devam eder. PlayerCounter ayrı küçük pencerede görünür.";
        bool sizingChrome = false;
        void FitChrome()
        {
            if (sizingChrome) return; sizingChrome = true;
            var heights = layout.GetRowHeights();
            if (heights.Length == 6)
            {
                int chrome = heights.Take(4).Sum() + heights[5] + layout.Padding.Vertical;
                int required = Math.Max(chromeScroll.ClientSize.Height, chrome + viewport.MinimumSize.Height + viewport.Margin.Vertical);
                if (layout.Height != required) layout.Height = required;
            }
            sizingChrome = false;
        }
        bool fitQueued = false;
        void QueueFit()
        {
            if (fitQueued || !IsHandleCreated || IsDisposed) return;
            fitQueued = true;
            BeginInvoke(() => { fitQueued = false; if (!IsDisposed) FitChrome(); });
        }
        // Layout events occur before TableLayoutPanel finishes measuring its rows.
        // Fit after that pass, using the final measurements rather than stale row heights.
        layout.Layout += (_, _) => QueueFit(); chromeScroll.Resize += (_, _) => QueueFit(); Shown += (_, _) => QueueFit();
        refresh.Tick += async (_, _) => { if (!closing) { await Safety.SampleAsync(); if (IsDisposed) return; Overview.UpdateState(Actions); if (SelectedPage == 5) Workspace.FitAoe(); } };
        FormClosing += OnClosing;
        Shown += (_, _) =>
        {
            int dark = 1; DwmSetWindowAttribute(Handle, 20, ref dark, sizeof(int));
            FitScreenBounds();
            refresh.Start();
            if (!preview) { sessionWatch.Start(); BeginInvoke(ShowScanDialog); }
        };
        sessionWatch.Tick += async (_, _) =>
        {
            if (scanDialogOpen || !scanAccepted || startupScan.Busy || Actions.Busy || closing || exitRequested || Safety.Busy || Safety.Blocked || Workspace.Pages.Count > 0) return;
            string key = StartupScan.SessionKey();
            bool changed = key != scannedSession;
            if (key.Length == 0) { scannedSession = key; footer.Text = "Oyun bekleniyor · SHA profilleri korunuyor. Yeni oturumda pointer’lar yeniden çözülecek."; return; }
            bool pending = startupScan.Results.Values.Any(r => r.Status == ScanStatus.Waiting);
            if (changed || (pending && DateTime.UtcNow < retryUntil))
            {
                if (changed) retryUntil = DateTime.UtcNow.AddMinutes(2);
                await RunScanAsync(_ => { }, CancellationToken.None, automatic: true);
            }
        };
        DpiChanged += (_, _) => BeginInvoke(() => { FitScreenBounds(); Overview.Reflow(); Workspace.FitAoe(); });
        SelectPage(0);
    }
    void ShowScanDialog()
    {
        if (preview) { Notify("Önizleme · canlı tarama kapalı."); return; }
        if (closing || exitRequested || startupScan.Busy || Actions.Busy || Safety.Busy) return;
        using var dialog = new ScanDialog(startupScan, (progress, token) => RunScanAsync(progress, token));
        scanDialogOpen = true;
        try { dialog.ShowDialog(this); } finally { scanDialogOpen = false; }
    }
    async Task RunScanAsync(Action<ScanResult> progress, CancellationToken token, bool automatic = false)
    {
        if (startupScan.Busy || closing || exitRequested || Actions.Busy) return;
        scanCancel = CancellationTokenSource.CreateLinkedTokenSource(token);
        navigation.Enabled = Overview.Enabled = false;
        string identity = StartupScan.SessionKey();
        try
        {
            scanTask = startupScan.RunAsync(new LiveStartupProbe(Workspace, Actions), result =>
            {
                progress(result); footer.Text = $"{FeatureActions.Name(result.Feature)} · {result.Message}";
            }, scanCancel.Token);
            await scanTask;
            if (StartupScan.SessionKey() != identity)
            {
                foreach (var key in startupScan.Results.Keys.ToArray())
                    if (startupScan.Results[key].Status == ScanStatus.Ready)
                    { var invalid = startupScan.Results[key] with { Status = ScanStatus.Waiting, Message = "Tarama sırasında oyun oturumu değişti; yeniden doğrulanmalı." }; startupScan.Results[key] = invalid; progress(invalid); }
            }
            scannedSession = identity;
            scanAccepted = !scanCancel.IsCancellationRequested;
            if (!automatic) retryUntil = DateTime.UtcNow.AddMinutes(2);
            int ready = startupScan.Results.Values.Count(r => r.Status == ScanStatus.Ready);
            footer.Text = $"Tarama: {ready}/9 canlı yol doğrulandı. İşlevler açılmadı. Manuel / bekleyen kontroller için Tarama / durum düğmesini kullan.";
        }
        catch (Exception ex) { footer.Text = "Tarama tamamlanamadı: " + ex.Message; }
        finally { scanCancel.Dispose(); scanCancel = null; scanTask = null; if (!closing && !exitRequested) navigation.Enabled = Overview.Enabled = true; }
    }
    void FitScreenBounds()
    {
        var area = Screen.FromControl(this).WorkingArea;
        var minimum = LogicalToDeviceUnits(new Size(560, 480));
        MinimumSize = new(Math.Min(minimum.Width, area.Width), Math.Min(minimum.Height, area.Height));
        if (WindowState == FormWindowState.Normal && (Width > area.Width || Height > area.Height))
            Size = new(Math.Min(Width, area.Width), Math.Min(Height, area.Height));
    }
    internal void SelectPage(int index)
    {
        if (closing || exitRequested || startupScan.Busy || (index != 0 && Safety.Blocked)) return;
        try
        {
            Workspace.Show(index); Overview.Visible = index == 0; if (index == 0) Overview.BringToFront();
            SelectedPage = index; title.Text = index == 0 ? "Tüm araçların. Tek kontrol alanı." : Names[index]; subtitle.Text = Descriptions[index];
            for (int i = 0; i < buttons.Count; i++) { buttons[i].Selected = (i == 6 ? 8 : i) == index; buttons[i].Invalidate(); }
            Overview.UpdateState(Actions);
        }
        catch (Exception ex) { footer.Text = "Araç açılamadı: " + ex.Message; }
    }
    internal async Task<ActionReply> ExecuteFeatureAsync(Feature feature)
    {
        if (closing || exitRequested || startupScan.Busy) return new(false, "Tarama veya geri alma işleminin tamamlanmasını bekle.");
        if (feature == Feature.SafeMode)
        {
            if (Safety.Busy) return new(false, Safety.Message);
            try { await Safety.ToggleAsync(); footer.Text = Safety.Message; }
            catch (Exception ex) { footer.Text = "SafeMode açılamadı: " + ex.Message; }
            Overview.UpdateState(Actions); return new(false, footer.Text);
        }
        if (Safety.Blocked && feature != Feature.Counter) { footer.Text = Safety.Message; return new(false, Safety.Message); }
        var task = Actions.ExecuteAsync(feature); Overview.UpdateState(Actions);
        var reply = await task; if (closing) return reply; Overview.UpdateState(Actions); footer.Text = reply.Message;
        if (reply.RequiresSetup)
        {
            int page = FeatureActions.Page(feature);
            Notify($"{FeatureActions.Name(feature)} için ayar veya doğrulama gerekiyor.\n\n{reply.Message}\n\n{Names[page]} sayfası açılacak. Doğrulamadan sonra düğmeye tekrar bas; işlev otomatik başlatılmaz.");
            SelectPage(page);
        }
        return reply;
    }
    void OpenSettings(int page)
    {
        if (page == 7) { Notify("SafeMode açıkken Player Count 1 veya üzerindeyse etkin işlemler kapanıştaki gibi geri alınır. Sayaç okunamazsa da işlemler durdurulur. Sayı 0 olduğunda işlevleri kendin yeniden açabilirsin. Önceden tamamlanmış XYZ/MobTP taşıma işlemleri geçmişe döndürülmez."); return; }
        if (page != 6) { SelectPage(page); return; }
        Notify("PlayerCounter · AÇ düğmesi eski görünümde küçük sayaç penceresini açar.\n\nSayaç alanından sürükleyebilirsin. Sağ tıkla veya genel bakıştaki KAPAT düğmesiyle kapatabilirsin. Ana pencere küçültülse de görünür kalır.\n\nExit düğmesi oyuna yöneliktir; otomatik çalışmaz.");
    }
    void Notify(string message) { if (notify is not null) notify(message); else MessageBox.Show(this, message, "4UnityTools · Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information); }
    async Task<bool> RestoreForSafety()
    {
        // Block embedded UI and hotkeys before awaiting outstanding commands.
        Workspace.Quiesce();
        bool result = await CloseTools();
        footer.Text = result ? "SafeMode · etkin işlemler geri alındı." : "SafeMode · geri alma bekliyor; işlemler engelli.";
        return result;
    }
    internal async Task<bool> CloseTools()
    {
        closing = true; navigation.Enabled = Overview.Enabled = false; footer.Text = "İşlemler tamamlanıyor; araçlar kapatılıyor…";
        Workspace.Quiesce();
        scanCancel?.Cancel();
        if (scanTask is not null) await scanTask;
        while (Actions.Busy) await Task.Delay(40);
        var blocked = await Workspace.CloseAsync(page => footer.Text = $"Sırayla kapatılıyor · {Names[page]}…"); closing = false; navigation.Enabled = Overview.Enabled = true;
        if (blocked is int page) { if (!Safety.Blocked) SelectPage(page); footer.Text = "Araç henüz kapanamadı. Geri alma / işlem durumunu kontrol edip yeniden dene."; return false; }
        SelectPage(0); return true;
    }
    async void OnClosing(object? sender, FormClosingEventArgs e)
    {
        if (allowClose) return; e.Cancel = true; if (closing || exitRequested) return;
        exitRequested = true;
        await Safety.PauseAsync();
        if (await CloseTools()) { await Safety.StopAsync(); allowClose = true; Close(); }
        else { exitRequested = false; Safety.Resume(); }
    }
    protected override void Dispose(bool disposing) { if (disposing) { sessionWatch.Dispose(); refresh.Dispose(); Safety.Dispose(); Workspace.Dispose(); } base.Dispose(disposing); }
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(nint hwnd, int attribute, ref int value, int size);
}
