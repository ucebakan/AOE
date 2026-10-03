using UnityTools.Controls;
using System.Runtime.CompilerServices;
[assembly: InternalsVisibleTo("4UnityTools")]
namespace Multikill;

sealed class MainForm : Form
{
    readonly PatchEngine engine;
    readonly bool preview;
    readonly Label state = Responsive.Text("Profil doğrulanmadı.");
    readonly Button validate = new ReadableButton { Text = "Profili doğrula" }, toggle = new ReadableButton { Text = "Multikill · AÇ" }, recover = new ReadableButton { Text = "Patch Recovery · Yeniden tara" };
    readonly System.Windows.Forms.Timer timer = new() { Interval = 800 };
    bool busy, closing, allowClose;
    public MainForm(bool preview = false)
    {
        this.preview = preview;
        engine = new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "4UnityMultikill"));
        Text = "4Unity · Multikill JE"; Font = new("Segoe UI", 10); AutoScaleMode = AutoScaleMode.Dpi; AutoScaleDimensions = new(96, 96);
        ClientSize = new(780, 510); MinimumSize = new(440, 380); BackColor = Color.FromArgb(17, 19, 28); ForeColor = Color.FromArgb(235, 235, 247);
        var stack = new ContentStack { Padding = new(24) };
        stack.AddRow(Responsive.Text("MULTIKILL  /  JE", 22, true));
        stack.AddRow(Responsive.Text("Doğrulanmış JBE → JE işlevi. Profili doğruladıktan sonra açabilirsin."));
        stack.AddRow(Responsive.Text("Client hedef filtresini değiştirir; bütün hedeflerin hasar alacağını garanti etmez."));
        foreach (var button in new[] { validate, toggle, recover }) { Responsive.Button(button); button.FlatStyle = FlatStyle.Flat; button.Padding = new(12, 8, 12, 8); button.BackColor = Color.FromArgb(45, 39, 70); }
        stack.AddRow(Responsive.Flow(validate, toggle, recover)); stack.AddRow(state);
        stack.AddRow(Responsive.Text("AOB imzası · SHA-256 · canlı PE ve oturum kimliği · byte doğrulaması"));
        stack.AddRow(Responsive.Text("Kapanışta etkin patch geri alınır. Kesinti sonrası aynı oturuma ait kurtarma kaydı profil doğrulamasında işlenir. Oyun güncellenirse AOB ve semantik akıştan yeni konum aranır. Tekil ve canlı doğrulanmış profil olmadan işlev açılmaz."));
        Responsive.Mount(this, stack);
        validate.Click += async (_, _) => await Work(engine.Validate);
        toggle.Click += async (_, _) => await SuiteToggleAsync();
        recover.Click += async (_, _) => await Work(() => { engine.Rescan(); });
        timer.Tick += async (_, _) => { if (!busy && !closing && (engine.Ready || engine.Active)) await Work(engine.Poll); };
        Shown += (_, _) => { if (Enabled) timer.Start(); }; FormClosing += OnClosing; UpdateState();
    }
    internal bool SuiteClosing => closing;
    internal void SuiteQuiesce() { timer.Stop(); Enabled = false; }
    internal bool SuiteReady => engine.Ready && !busy && !closing;
    internal bool SuiteActive => engine.Active;
    internal string SuiteMessage => state.Text;
    internal Task SuiteToggleAsync() => Work(engine.Toggle);
    internal Task SuiteRefreshAsync() => Work(engine.PrepareReadOnly);
    async Task Work(Action action)
    {
        if (busy || closing) return;
        if (preview) { state.Text = "Önizleme · oyun belleğine erişilmez."; return; }
        busy = true; UpdateState();
        try { await Task.Run(action); state.Text = engine.Message; }
        catch (Exception ex) { state.Text = ex.Message; }
        finally { busy = false; UpdateState(); }
    }
    void UpdateState()
    {
        validate.Enabled = !busy && !closing; toggle.Enabled = !busy && !closing && (engine.Ready || engine.Active);
        recover.Enabled = !busy && !closing; toggle.Text = engine.Active ? "Multikill · KAPAT" : "Multikill · AÇ";
    }
    async void OnClosing(object? sender, FormClosingEventArgs e)
    {
        if (allowClose) return; e.Cancel = true; if (closing) return; closing = true; timer.Stop(); UpdateState();
        while (busy) await Task.Delay(30);
        try { await Task.Run(engine.Dispose); allowClose = true; Close(); }
        catch (Exception ex) { state.Text = "Geri alma bekliyor: " + ex.Message; closing = false; if (Enabled) timer.Start(); UpdateState(); }
    }
    protected override void Dispose(bool disposing) { if (disposing) timer.Dispose(); base.Dispose(disposing); }
}

// Windows' default disabled text can become black on this dark standalone form.
sealed class ReadableButton : Button
{
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(BackColor);
        using var border = new Pen(Color.FromArgb(83, 77, 109));
        e.Graphics.DrawRectangle(border, 0, 0, Width - 1, Height - 1);
        TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, Enabled ? Color.FromArgb(239, 237, 249) : Color.FromArgb(170, 174, 190), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(ClientRectangle, -4, -4));
    }
}
