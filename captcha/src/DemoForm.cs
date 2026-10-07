namespace UnityPuzzleTest;

sealed class DemoForm : Form
{
    public Rectangle PuzzleBounds => new(24, 36, 408, 444);
    public int Step { get; private set; } = 1;
    public int Attempts { get; private set; } = 3;
    public int Accepted { get; private set; }
    public int Inputs { get; private set; }
    public bool Finished => Step == 5;
    internal bool PuzzleVisible = true;
    static readonly string[] Targets = ["SUN", "STAR", "MOON", "SUN"];
    static readonly string[][] Choices = [["CROWN", "STAR", "SUN", "LEAF"], ["GEM", "STAR", "CROWN", "LEAF"], ["GEM", "MOON", "SHIELD", "CROWN"], ["SWORD", "SUN", "STAR", "LEAF"]];
    public DemoForm()
    {
        Text = "4Unity · yerel test penceresi"; ClientSize = new(456, 516);
        FormBorderStyle = FormBorderStyle.FixedSingle; MaximizeBox = false;
        BackColor = Color.FromArgb(29, 43, 33); DoubleBuffered = true;
        StartPosition = FormStartPosition.CenterScreen;
    }
    Rectangle ButtonBounds(int slot)
    {
        Rectangle r = PuzzleBounds;
        return new(r.X + (slot % 2 == 0 ? 42 : 220), r.Y + (slot / 2 == 0 ? 327 : 385), 146, 36);
    }
    public void Press(int slot)
    {
        Inputs++;
        if (Finished || Attempts <= 0 || slot is < 0 or > 3) return;
        if (Choices[Step - 1][slot] == Targets[Step - 1]) { Accepted++; Step++; } else Attempts--;
        Invalidate(); Update();
    }
    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button != MouseButtons.Left || Finished) return;
        for (int i = 0; i < 4; i++) if (ButtonBounds(i).Contains(e.Location)) { Press(i); break; }
    }
    internal void PaintScene(Graphics g)
    {
        g.Clear(BackColor);
        using var small = new Font("Segoe UI", 10);
        TextRenderer.DrawText(g, "YEREL SİMÜLASYON · oyun bağlantısı yok", small, new Rectangle(0, 2, ClientSize.Width, 24), Color.WhiteSmoke, TextFormatFlags.HorizontalCenter);
        if (Finished || !PuzzleVisible)
        {
            using var big = new Font("Segoe UI", 22, FontStyle.Bold);
            TextRenderer.DrawText(g, Finished ? "Test tamamlandı" : "Test alanı · normal oyun", big, new Rectangle(0, 215, ClientSize.Width, 70), Color.LightGreen, TextFormatFlags.HorizontalCenter);
            return;
        }
        Rectangle r = PuzzleBounds;
        using var fill = new SolidBrush(Color.FromArgb(11, 16, 9)); g.FillRectangle(fill, r);
        using var edge = new Pen(Color.Olive, 3); g.DrawRectangle(edge, r);
        using var heading = new Font("Segoe UI", 15, FontStyle.Bold);
        using var normal = new Font("Segoe UI", 11, FontStyle.Bold);
        void Center(string text, int top, Color color, Font font) => TextRenderer.DrawText(g, text, font,
            new Rectangle(r.X + 5, r.Y + top, r.Width - 10, 30), color, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        Center("Macro protection puzzle", 8, Color.Khaki, heading);
        Center("Click the matching symbol", 63, Color.OrangeRed, normal);
        Center("Target:  " + Targets[Step - 1], 102, Color.WhiteSmoke, normal);
        Center($"Step {Step} of 4", 162, Color.LightCyan, normal);
        Center($"{Attempts} attempts - 90 seconds", 225, Color.Khaki, normal);
        for (int i = 0; i < 4; i++)
        {
            Rectangle b = ButtonBounds(i);
            using var buttonFill = new SolidBrush(Color.FromArgb(53, 46, 20)); g.FillRectangle(buttonFill, b); g.DrawRectangle(edge, b);
            TextRenderer.DrawText(g, Choices[Step - 1][i], normal, b, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }
    protected override void OnPaint(PaintEventArgs e) { base.OnPaint(e); PaintScene(e.Graphics); }
    internal Bitmap Snapshot()
    {
        using var full = new Bitmap(ClientSize.Width, ClientSize.Height);
        using (var g = Graphics.FromImage(full)) PaintScene(g);
        return full.Clone(PuzzleBounds, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
    }
}
