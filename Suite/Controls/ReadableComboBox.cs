namespace UnityTools.Controls;

// Native ComboBox does not reliably print its selected owner-drawn text via WM_PRINT.
// Paint the selected label for both screen rendering and deterministic UI captures.
public sealed class ReadableComboBox : ComboBox
{
    public ReadableComboBox() { DropDownStyle = ComboBoxStyle.DropDownList; DrawMode = DrawMode.OwnerDrawFixed; FlatStyle = FlatStyle.Flat; }
    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        if (e.Index < 0) return;
        using var background = new SolidBrush(BackColor); e.Graphics.FillRectangle(background, e.Bounds);
        TextRenderer.DrawText(e.Graphics, GetItemText(Items[e.Index]), Font, e.Bounds, ForeColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
        e.DrawFocusRectangle();
    }
    void PaintSelection(Graphics graphics)
    {
        var rect = new Rectangle(2, 2, Math.Max(1, Width - SystemInformation.VerticalScrollBarWidth - 4), Math.Max(1, Height - 4));
        using var brush = new SolidBrush(BackColor); graphics.FillRectangle(brush, rect);
        TextRenderer.DrawText(graphics, Text, Font, rect, ForeColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
    }
    protected override void WndProc(ref Message message)
    {
        base.WndProc(ref message);
        if (message.Msg == 0xF) { using var graphics = Graphics.FromHwnd(Handle); PaintSelection(graphics); }
        else if (message.Msg is 0x317 or 0x318 && message.WParam != 0) { using var graphics = Graphics.FromHdc(message.WParam); PaintSelection(graphics); }
    }
}
