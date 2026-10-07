namespace UnityPuzzleTest;

sealed class CropForm : Form
{
    readonly Bitmap image;
    Point start, end;
    bool dragging;
    public Rectangle Selected { get; private set; }
    Rectangle DisplayBounds
    {
        get
        {
            var available = new Size(ClientSize.Width - 30, ClientSize.Height - 90);
            double scale = Math.Min(available.Width / (double)image.Width, available.Height / (double)image.Height);
            var size = new Size((int)(image.Width * scale), (int)(image.Height * scale));
            return new((ClientSize.Width - size.Width) / 2, 55, size.Width, size.Height);
        }
    }
    public CropForm(Bitmap image)
    {
        this.image = image;
        Text = "Test ekranının dış çerçevesini seç"; ClientSize = new(1000, 740);
        MinimumSize = new(700, 520); StartPosition = FormStartPosition.CenterParent;
        BackColor = Color.FromArgb(20, 25, 32); ForeColor = Color.White;
        DoubleBuffered = true; KeyPreview = true;
        var accept = new Button { Text = "Alanı kullan", Dock = DockStyle.Bottom, Height = 38 };
        accept.Click += (_, _) =>
        {
            if (Selected.Width < 200 || Selected.Height < 220) { MessageBox.Show(this, "Dört seçenek ve başlık dahil bütün kutuyu seçin."); return; }
            DialogResult = DialogResult.OK;
        };
        Controls.Add(accept);
        KeyDown += (_, e) => { if (e.KeyCode == Keys.Escape) DialogResult = DialogResult.Cancel; };
        MouseDown += (_, e) => { if (e.Button == MouseButtons.Left && DisplayBounds.Contains(e.Location)) { dragging = true; Capture = true; start = end = e.Location; Selected = Rectangle.Empty; Invalidate(); } };
        MouseMove += (_, e) =>
        {
            if (!dragging) return;
            Rectangle d = DisplayBounds;
            end = new(Math.Clamp(e.X, d.Left, d.Right), Math.Clamp(e.Y, d.Top, d.Bottom));
            float sx = image.Width / (float)d.Width, sy = image.Height / (float)d.Height;
            int left = (int)((Math.Min(start.X, end.X) - d.Left) * sx), top = (int)((Math.Min(start.Y, end.Y) - d.Top) * sy);
            int right = (int)((Math.Max(start.X, end.X) - d.Left) * sx), bottom = (int)((Math.Max(start.Y, end.Y) - d.Top) * sy);
            Selected = Rectangle.Intersect(new Rectangle(Point.Empty, image.Size), Rectangle.FromLTRB(left, top, right, bottom)); Invalidate();
        };
        MouseUp += (_, _) => { dragging = false; Capture = false; };
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        TextRenderer.DrawText(e.Graphics, "Kutunun dış çerçevesini fareyle çiz: başlık, hedef, adım ve dört buton dahil.", Font, new Point(16, 18), ForeColor);
        e.Graphics.DrawImage(image, DisplayBounds);
        if (!Selected.IsEmpty)
        {
            Rectangle d = DisplayBounds;
            var selected = new Rectangle(d.Left + Selected.Left * d.Width / image.Width, d.Top + Selected.Top * d.Height / image.Height,
                Selected.Width * d.Width / image.Width, Selected.Height * d.Height / image.Height);
            using var pen = new Pen(Color.Cyan, 3); e.Graphics.DrawRectangle(pen, selected);
        }
    }
}
