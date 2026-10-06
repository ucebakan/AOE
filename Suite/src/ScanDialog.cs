using UnityTools.Controls;

namespace UnityTools;

sealed class ScanDialog : Form
{
    readonly Dictionary<Feature, Label> rows = new();
    readonly Label status = Responsive.Text("Tarama başlasın mı?", 18, true);
    readonly Button start = new() { Text = "Taramayı başlat" }, close = new() { Text = "Şimdi değil" };
    readonly ScanProgress progress = new() { Dock = DockStyle.Top, Height = 10 };
    bool busy;
    readonly CancellationTokenSource cancel = new();
    internal ScanDialog(StartupScan scan, Func<Action<ScanResult>, CancellationToken, Task> run)
    {
        Text = "4UnityTools · Başlangıç kontrolü"; AutoScaleDimensions = new(96, 96); AutoScaleMode = AutoScaleMode.Dpi;
        Font = new("Segoe UI", 10); ClientSize = new(660, 620); MinimumSize = new(430, 340);
        StartPosition = FormStartPosition.CenterParent; MinimizeBox = false; TopMost = true;
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new(18) };
        layout.ColumnStyles.Add(new(SizeType.Percent, 100));
        layout.RowStyles.Add(new(SizeType.AutoSize)); layout.RowStyles.Add(new(SizeType.Percent, 100)); layout.RowStyles.Add(new(SizeType.AutoSize));
        var header = new ContentStack { Padding = Padding.Empty };
        header.AddRow(status);
        header.AddRow(Responsive.Text($"1–{StartupScan.Order.Length} sırayla kontrol edilir. Aynı SHA için kayıtlı profil kullanılır; canlı pointer’lar yeniden çözülür. Hiçbir işlev açılmaz. AOE doğrulaması manuel kalabilir."));
        header.AddRow(progress); layout.Controls.Add(header, 0, 0);
        var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
        var stack = new ContentStack { Padding = new(0, 0, 12, 0) }; scroll.Controls.Add(stack); layout.Controls.Add(scroll, 0, 1);
        foreach (var feature in StartupScan.Order)
        {
            var label = Responsive.Text($"{rows.Count + 1}. {FeatureName(feature)} · Bekliyor");
            rows[feature] = label; stack.AddRow(label);
        }
        Responsive.Button(start); Responsive.Button(close); layout.Controls.Add(Responsive.Flow(start, close), 0, 2);
        Controls.Add(layout); Palette.Apply(this);
        foreach (var result in scan.Results.Values) UpdateResult(result);
        start.Click += async (_, _) =>
        {
            busy = true; start.Enabled = false; close.Text = "Durdur"; status.Text = "Yollar kontrol ediliyor…";
            foreach (var feature in StartupScan.Order) rows[feature].Text = $"{Array.IndexOf(StartupScan.Order, feature) + 1}. {FeatureName(feature)} · Bekliyor";
            progress.Value = 0;
            try { await run(UpdateResult, cancel.Token); status.Text = cancel.IsCancellationRequested ? "Kontrol durduruldu" : "Kontrol tamamlandı"; }
            catch (Exception ex) { status.Text = "Kontrol tamamlanamadı: " + ex.Message; }
            finally { busy = false; close.Text = "Kapat"; }
        };
        close.Click += (_, _) => { if (busy) { cancel.Cancel(); close.Text = "Durduruluyor…"; } else Close(); };
        FormClosing += (_, e) => { if (busy) { cancel.Cancel(); e.Cancel = true; close.Text = "Durduruluyor…"; } };
        Shown += (_, _) =>
        {
            TopMost = true;
            var area = Screen.FromControl(this).WorkingArea;
            MinimumSize = new(Math.Min(MinimumSize.Width, area.Width), Math.Min(MinimumSize.Height, area.Height));
            Size = new(Math.Min(Width, area.Width), Math.Min(Height, area.Height));
            // The owner is also topmost. Put this newly opened dialog above it once,
            // without a timer that would keep taking focus from the user.
            BeginInvoke(() => WindowLayer.PlaceTopmost(this, focus: true));
        };
    }
    internal void UpdateResult(ScanResult result)
    {
        string state = result.Status switch { ScanStatus.Ready => "Doğrulandı", ScanStatus.Manual => "Manuel doğrulama", ScanStatus.Waiting => "Oyun / doğrulama bekliyor", ScanStatus.Failed => "Bulunamadı", ScanStatus.Scanning => "Kontrol ediliyor", ScanStatus.Cancelled => "İptal", _ => "Bekliyor" };
        rows[result.Feature].Text = $"{Array.IndexOf(StartupScan.Order, result.Feature) + 1}. {FeatureName(result.Feature)} · {state}\n{result.Message}";
        if (result.Status != ScanStatus.Scanning && progress.Value < StartupScan.Order.Length) progress.Value++;
    }
    static string FeatureName(Feature feature) => feature == Feature.Coordinates ? "Player XYZ" : feature == Feature.MobTP ? "MobTP" : FeatureActions.Name(feature);
    protected override void Dispose(bool disposing) { if (disposing) cancel.Dispose(); base.Dispose(disposing); }
}

sealed class ScanProgress : Control
{
    int value;
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    internal int Value { get => value; set { this.value = Math.Clamp(value, 0, StartupScan.Order.Length); Invalidate(); } }
    internal ScanProgress() { DoubleBuffered = true; }
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(Palette.Elevated);
        using var brush = new SolidBrush(Palette.Accent);
        e.Graphics.FillRectangle(brush, 0, 0, Width * value / StartupScan.Order.Length, Height);
    }
}
