using UnityTools.Controls;

namespace MobTP;

sealed partial class MainForm
{
    internal bool SuiteClosing => closing;
    internal void SuiteQuiesce() { timer.Stop(); Enabled = false; }
    internal bool SuiteReady => last?.Player is not null && !closing && !moving;
    internal string SuiteMessage => result.Text;
    internal string SuiteValidation => last?.Status ?? state.Text;
    internal async Task SuiteRefreshAsync()
    {
        while(moving&&!closing)await Task.Delay(30);
        if(closing)return;
        await gate.WaitAsync();gate.Release();
        if(hotkey is null&&settings.Hotkey is not null)AssignKey(settings.Hotkey);
        await RefreshWorld();if (Enabled) timer.Start();
    }
    internal Task SuiteTeleportAsync() => Teleport();
    internal void ConfigureSuiteLayout()
    {
        // Reuse the controls and event handlers, but replace fixed-height rows with measured flow rows.
        var old = (TableLayoutPanel)Controls[0]; var actionRow = old.GetControlFromPosition(0, 3)!;
        var keyRow = old.GetControlFromPosition(0, 4)!; var listRow = old.GetControlFromPosition(0, 6)!;
        var stack = new ContentStack();
        foreach (var label in new[] { state, player, count }) { Responsive.Readable(label); stack.AddRow(label); }
        foreach (var row in new[] { actionRow, keyRow, listRow })
        {
            row.AutoSize = true; if (row is FlowLayoutPanel flow) { flow.AutoSizeMode = AutoSizeMode.GrowAndShrink; flow.WrapContents = true; }
            foreach (var button in row.Controls.OfType<Button>()) Responsive.Button(button);
        }
        stack.AddRow(actionRow); stack.AddRow(keyRow); result.AutoSize = false; result.MinimumSize = Size.Empty; stack.AddRow(result);
        int resultRow = stack.RowCount - 1;
        void SizeResult() { stack.RowStyles[resultRow].SizeType = SizeType.Absolute; stack.RowStyles[resultRow].Height = result.Font.Height * 5 + 16; }
        result.FontChanged += (_, _) => SizeResult(); DpiChangedAfterParent += (_, _) => SizeResult(); SizeResult();
        stack.AddRow(listRow); grid.Height = 340; grid.MinimumSize = new(0, 260); grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
        foreach (DataGridViewColumn column in grid.Columns) column.Width = column.Index is 1 or 2 ? 220 : 180;
        stack.AddRow(grid); Responsive.Mount(this, stack);
    }
}
