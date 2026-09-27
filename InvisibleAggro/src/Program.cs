using System.Text.Json;

namespace InvisibleAggro;

static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        if (args.Contains("--self-test")) return SelfTests.Run(args.LastOrDefault() == "--self-test" ? "self-test.json" : args[^1]);
        if (args.Contains("--ui-preview"))
        {
            ApplicationConfiguration.Initialize();
            using var form = new MainForm(preview: true, previewApproval: args.Contains("--approval")) { Opacity = 0, ShowInTaskbar = false };
            form.Show();
            Application.DoEvents();
            using var bitmap = new Bitmap(form.Width, form.Height);
            form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
            bitmap.Save(args[^1], System.Drawing.Imaging.ImageFormat.Png);
            return form.Controls.OfType<Button>().Count(b => b.Visible) == (args.Contains("--approval") ? 3 : 2) ? 0 : 1;
        }
        if (args.Contains("--probe"))
        {
            string output = args.LastOrDefault() == "--probe" ? "probe.json" : args[^1];
            try
            {
                using var session = GameSession.Connect();
                session.ValidateCode([]);
                File.WriteAllText(output, JsonSerializer.Serialize(new { status = "READ_ONLY_READY", session.Pid, session.Created, session.Base, session.Player, session.PlayerId,
                    stealth = session.Read(session.Player + Profile.Stealth, 1)[0], visual = session.Read(session.Player + Profile.Visual, 1)[0], writes = 0 }, new JsonSerializerOptions { WriteIndented = true }));
                return 0;
            }
            catch (Exception ex)
            {
                File.WriteAllText(output, JsonSerializer.Serialize(new { status = "NOT_READY", error = ex.Message, writes = 0 }, new JsonSerializerOptions { WriteIndented = true }));
                return 2;
            }
        }
        using var mutex = new Mutex(true, @"Local\4UnityInvisibleAggro_9CD77CD0", out bool first);
        if (!first) { MessageBox.Show("Uygulama zaten açık.", "4Unity"); return 1; }
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
        return 0;
    }
}

sealed class MainForm : Form
{
    readonly Button invisible = new StateButton(), aggro = new StateButton(), approve = new StateButton();
    readonly Label status = new();
    readonly System.Windows.Forms.Timer timer = new() { Interval = 2000 };
    readonly Engine engine;
    readonly bool previewApproval;
    bool busy, closing, allowClose;
    public MainForm(bool preview = false, bool previewApproval = false)
    {
        this.previewApproval = preview && previewApproval;
        Text = "4Unity · Invisible / Aggro";
        ClientSize = new Size(420, 192);
        FormBorderStyle = FormBorderStyle.FixedSingle; MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(23, 26, 34); ForeColor = Color.WhiteSmoke;
        Font = new Font("Segoe UI", 10);
        var title = new Label { Text = "4UNITY", Location = new Point(22, 16), AutoSize = true, Font = new Font("Segoe UI", 13, FontStyle.Bold) };
        invisible.SetBounds(22, 58, 180, 54); aggro.SetBounds(218, 58, 180, 54);
        foreach (var b in new[] { invisible, aggro }) { b.FlatStyle = FlatStyle.Flat; b.FlatAppearance.BorderSize = 0; b.Font = new Font("Segoe UI", 11, FontStyle.Bold); b.Cursor = Cursors.Hand; }
        status.SetBounds(22, 128, 376, 54); status.ForeColor = Color.FromArgb(190, 197, 211);
        approve.SetBounds(22, 192, 376, 42);
        approve.Text = "Onaylıyorum · Yeni sürümü tara";
        approve.BackColor = Color.FromArgb(106, 79, 200);
        approve.Visible = false;
        Controls.AddRange([title, invisible, aggro, status, approve]);
        engine = new Engine(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "4UnityInvisibleAggro"));
        approve.Click += async (_, _) => await Work(engine.ScanApproved);
        invisible.Click += async (_, _) => await Work(() => engine.Toggle(StateMode.Invisible));
        aggro.Click += async (_, _) => await Work(() => engine.Toggle(StateMode.Aggro));
        timer.Tick += async (_, _) => await Work(engine.Poll);
        if (!preview)
        {
            Shown += async (_, _) => { await Work(engine.Poll); timer.Start(); };
            FormClosing += OnClosing;
        }
        PaintState();
    }
    async Task Work(Action action)
    {
        if (busy || closing) return;
        busy = true; PaintState();
        try { await Task.Run(action); }
        catch (Exception ex) { status.Text = ex.Message; }
        finally { busy = false; if (!IsDisposed) PaintState(); }
    }
    void PaintState()
    {
        var state = engine.View;
        if (previewApproval) state = state with { NeedsApproval = true, Message = "Yeni sürüm bulundu. Taramayı başlatmak için onay verin." };
        invisible.Text = state.Mode == StateMode.Invisible ? "Invisible · AÇIK" : "Invisible";
        aggro.Text = state.Mode == StateMode.Aggro ? "Aggro · AÇIK" : "Aggro";
        invisible.BackColor = state.Mode == StateMode.Invisible ? Color.FromArgb(106, 79, 200) : Color.FromArgb(48, 53, 68);
        aggro.BackColor = state.Mode == StateMode.Aggro ? Color.FromArgb(32, 135, 116) : Color.FromArgb(48, 53, 68);
        invisible.Enabled = aggro.Enabled = !busy && state.Ready;
        approve.Visible = state.NeedsApproval;
        approve.Enabled = !busy && !closing;
        ClientSize = new Size(420, state.NeedsApproval ? 250 : 192);
        status.Text = busy ? "Kontrol ediliyor…" : state.Message;
    }
    async void OnClosing(object? sender, FormClosingEventArgs e)
    {
        if (allowClose) return;
        e.Cancel = true;
        if (closing) return;
        closing = true; timer.Stop(); invisible.Enabled = aggro.Enabled = false;
        while (busy) await Task.Delay(50);
        try
        {
            status.Text = "Değişiklikler geri alınıyor…";
            await Task.Run(engine.Dispose);
            allowClose = true; Close();
        }
        catch (Exception ex)
        {
            closing = false; status.Text = "Kapatılamadı: " + ex.Message;
            invisible.Enabled = aggro.Enabled = true;
            timer.Start();
        }
    }
}

sealed class StateButton : Button
{
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(BackColor);
        TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle,
            Enabled ? Color.WhiteSmoke : Color.FromArgb(173, 181, 197),
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(ClientRectangle, -4, -4));
    }
}
