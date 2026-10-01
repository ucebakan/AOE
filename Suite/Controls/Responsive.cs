namespace UnityTools.Controls;

public sealed class ContentStack : TableLayoutPanel
{
    public ContentStack()
    {
        ColumnCount = 1; ColumnStyles.Add(new(SizeType.Percent, 100));
        AutoSize = true; AutoSizeMode = AutoSizeMode.GrowAndShrink; Dock = DockStyle.Top;
        Margin = Padding.Empty; Padding = new(18);
    }
    public void AddRow(Control control)
    {
        int row = RowCount++; RowStyles.Add(new(SizeType.AutoSize));
        control.Dock = DockStyle.Fill; Controls.Add(control, 0, row);
    }
}

public static class Responsive
{
    public static Label Text(string text, float size = 10, bool bold = false) => new()
    {
        Text = text, AutoSize = true, Font = new("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular),
        Margin = new(3, 6, 3, 8), Dock = DockStyle.Fill
    };
    public static FlowLayoutPanel Flow(params Control[] controls)
    {
        var flow = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Dock = DockStyle.Fill, WrapContents = true, Margin = new(0, 4, 0, 8) };
        foreach (var control in controls) { control.Dock = DockStyle.None; control.Margin = new(3, 4, 12, 4); flow.Controls.Add(control); }
        return flow;
    }
    public static void Button(Button button)
    {
        button.AutoSize = true; button.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        button.Padding = new(14, 9, 14, 9); button.MinimumSize = new(145, 42);
    }
    public static void Readable(Label label)
    {
        label.AutoEllipsis = false; label.AutoSize = true; label.Dock = DockStyle.Fill; label.Margin = new(3, 6, 3, 8);
    }
    public static void Mount(Form form, ContentStack stack)
    {
        form.SuspendLayout(); form.Controls.Clear(); form.AutoScroll = true; form.MinimumSize = Size.Empty;
        form.AutoScaleMode = AutoScaleMode.Dpi; form.Controls.Add(stack); form.ResumeLayout(true);
    }
}
