using System.Drawing.Drawing2D;

namespace UnityTools;

static class Palette
{
    public static readonly Color Background = Color.FromArgb(15, 17, 24), Surface = Color.FromArgb(23, 26, 34),
        Elevated = Color.FromArgb(32, 36, 48), Line = Color.FromArgb(49, 54, 72), Ink = Color.FromArgb(234, 237, 246),
        Muted = Color.FromArgb(155, 165, 188), Accent = Color.FromArgb(161, 143, 250), Blue = Color.FromArgb(133, 169, 255);
    public static void Apply(Control root)
    {
        root.BackColor = root is TextBoxBase or NumericUpDown or ComboBox ? Elevated : Surface;
        root.ForeColor = Ink;
        if (root is Button b)
        {
            b.FlatStyle = FlatStyle.Flat; b.FlatAppearance.BorderColor = Line; b.BackColor = Elevated; b.Cursor = Cursors.Hand;
            b.Paint += (_, e) =>
            {
                if (b.Enabled) return;
                e.Graphics.Clear(Elevated); using var pen = new Pen(Line); e.Graphics.DrawRectangle(pen, 0, 0, b.Width - 1, b.Height - 1);
                TextRenderer.DrawText(e.Graphics, b.Text, b.Font, b.ClientRectangle, Muted, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            };
        }
        if (root is ComboBox combo)
        {
            combo.FlatStyle = FlatStyle.Flat; combo.DrawMode = DrawMode.OwnerDrawFixed;
            combo.DrawItem += (_, e) =>
            {
                using var brush = new SolidBrush(Elevated); e.Graphics.FillRectangle(brush, e.Bounds);
                string text = e.Index >= 0 ? combo.GetItemText(combo.Items[e.Index])! : combo.Text;
                TextRenderer.DrawText(e.Graphics, text, combo.Font, e.Bounds, Ink, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
                e.DrawFocusRectangle();
            };
        }
        if (root is TextBox t) t.BorderStyle = BorderStyle.FixedSingle;
        if (root is DataGridView g)
        {
            g.BackgroundColor = Surface; g.BorderStyle = BorderStyle.None; g.EnableHeadersVisualStyles = false;
            g.GridColor = Line; g.ColumnHeadersDefaultCellStyle.BackColor = Elevated; g.ColumnHeadersDefaultCellStyle.ForeColor = Muted;
            g.DefaultCellStyle.BackColor = Surface; g.DefaultCellStyle.ForeColor = Ink;
            g.DefaultCellStyle.SelectionBackColor = Color.FromArgb(67, 57, 105); g.DefaultCellStyle.SelectionForeColor = Ink;
            g.RowTemplate.Height = 32;
        }
        foreach (Control child in root.Controls) Apply(child);
    }
    public static GraphicsPath Round(RectangleF r, float radius)
    {
        float d = radius * 2; var p = new GraphicsPath();
        p.AddArc(r.X, r.Y, d, d, 180, 90); p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); p.AddArc(r.X, r.Bottom - d, d, d, 90, 90); p.CloseFigure(); return p;
    }
}

sealed class NavButton : Button
{
    internal bool Selected;
    bool hover;
    public NavButton() { DoubleBuffered = true; FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0; Cursor = Cursors.Hand; Font = new("Segoe UI", 10, FontStyle.Bold); }
    protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias; e.Graphics.Clear(Palette.Background);
        using var shape = Palette.Round(new RectangleF(2, 3, Width - 4, Height - 6), 8);
        using var brush = new SolidBrush(Selected ? Color.FromArgb(48, 41, 73) : hover ? Palette.Elevated : Palette.Background);
        e.Graphics.FillPath(brush, shape);
        TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, Selected ? Palette.Accent : Palette.Muted, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(ClientRectangle, -5, -5));
    }
}
