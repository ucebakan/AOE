using UnityTools.Controls;

namespace PlayerXYZ;

sealed partial class MainForm
{
    internal bool SuiteReady => session is not null && !busy && !closing;
    internal bool SuiteHasCoordinates => inputs.All(x => TryNumber(x.Text, out _));
    internal string SuiteMessage => status.Text;
    internal async Task SuiteRefreshAsync() { while (busy && !closing) await Task.Delay(30); if (!closing) { retry = DateTime.MinValue; await Poll(); timer.Start(); } }
    internal Task SuiteWriteAsync() => WriteAsync();
    internal void SuiteBindCoordinates(CoordinateDraft draft)
    {
        for (int i = 0; i < 3; i++) { int axis = i; inputs[i].Text = draft[i]; inputs[i].TextChanged += (_, _) => draft[axis] = inputs[axis].Text; }
        void Sync() { for (int i = 0; i < 3; i++) if (inputs[i].Text != draft[i]) inputs[i].Text = draft[i]; }
        draft.Changed += Sync; Disposed += (_, _) => draft.Changed -= Sync;
    }
    internal void ConfigureSuiteLayout()
    {
        var stack = new ContentStack(); stack.AddRow(Responsive.Text("Canlı koordinatlar", 17, true));
        Responsive.Readable(player); stack.AddRow(player);
        var table = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, ColumnCount = 2, Margin = new(0, 8, 0, 12) };
        table.ColumnStyles.Add(new(SizeType.Percent, 25)); table.ColumnStyles.Add(new(SizeType.Percent, 75));
        table.Controls.Add(Responsive.Text("Alan / eksen", 10, true), 0, 0); table.Controls.Add(Responsive.Text("Canlı adres · float32 değer", 10, true), 1, 0);
        for (int i = 0; i < 6; i++) { Responsive.Readable(fieldLabels[i]); Responsive.Readable(cells[i]); table.RowStyles.Add(new(SizeType.AutoSize)); table.Controls.Add(fieldLabels[i], 0, i + 1); table.Controls.Add(cells[i], 1, i + 1); }
        stack.AddRow(table); stack.AddRow(Responsive.Text("Hedef koordinatlar", 13, true));
        var coordinates = new TableLayoutPanel { AutoSize = true, ColumnCount = 3, Dock = DockStyle.Fill };
        for (int i = 0; i < 3; i++) { coordinates.ColumnStyles.Add(new(SizeType.Percent, 100f / 3)); coordinates.Controls.Add(Responsive.Text(new[] { "X", "Y · yükseklik", "Z · düzlem" }[i]), i, 0); inputs[i].Dock = DockStyle.Fill; inputs[i].Margin = new(3, 3, 14, 10); coordinates.Controls.Add(inputs[i], i, 1); }
        stack.AddRow(coordinates); Responsive.Button(write); stack.AddRow(Responsive.Flow(write));
        Responsive.Readable(status); stack.AddRow(status);
        stack.AddRow(Responsive.Text("Rakam, tek virgül veya nokta ve başta eksi kullanabilirsin. Üç koordinat birlikte, tek seferde uygulanır."));
        Responsive.Mount(this, stack);
    }
}
