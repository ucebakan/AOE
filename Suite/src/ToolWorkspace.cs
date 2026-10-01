namespace UnityTools;

sealed class ToolWorkspace : IDisposable
{
    readonly bool preview;
    readonly Panel viewport;
    internal readonly Dictionary<int, Control> Pages = new();
    internal readonly Dictionary<int, Form> Forms = new();
    internal readonly CounterOverlay Counter = new();
    internal readonly UnityTools.Controls.CoordinateDraft Coordinates = new();
    internal NativeTool? Aoe;
    internal ToolWorkspace(Panel viewport, bool preview) { this.viewport = viewport; this.preview = preview; }
    internal void Ensure(int index)
    {
        if (Pages.ContainsKey(index)) return;
        var page = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Palette.Background, Visible = false };
        if (index == 5)
        {
            Aoe = new NativeTool(preview); page.Controls.Add(Aoe);
            page.Resize += (_, _) => FitAoe();
        }
        else
        {
            Form form;
            switch (index)
            {
                case 1: var xyz = new PlayerXYZ.MainForm(preview); xyz.SuiteBindCoordinates(Coordinates); xyz.ConfigureSuiteLayout(); form = xyz; break;
                case 2: var speed = new SpeedJump.MainForm(preview); speed.ConfigureSuiteLayout(); form = speed; break;
                case 3: var invisible = new InvisibleAggro.MainForm(preview); invisible.ConfigureSuiteLayout(); form = invisible; break;
                case 8: form = new Multikill.MainForm(preview); break;
                case 4: var mob = new MobTP.MainForm(preview); mob.ConfigureSuiteLayout(); form = mob; break;
                default: throw new ArgumentOutOfRangeException(nameof(index));
            }
            form.TopLevel = false; form.FormBorderStyle = FormBorderStyle.None; form.ShowInTaskbar = false;
            form.Dock = DockStyle.Fill; Palette.Apply(form); page.Controls.Add(form); Forms[index] = form;
            form.Show();
        }
        Pages[index] = page; viewport.Controls.Add(page);
        page.CreateControl(); FitAoe();
    }
    internal void Show(int index)
    {
        if (index != 0) Ensure(index);
        foreach (var page in Pages) page.Value.Visible = page.Key == index;
        if (index != 0) Pages[index].BringToFront();
        FitAoe();
    }
    internal void FitAoe()
    {
        if (Aoe is { IsDisposed: false, ToolWindow: not 0 } && Pages.TryGetValue(5, out var page))
            Aoe.Fit(Math.Max(300, page.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 2), viewport.DeviceDpi);
    }
    internal async Task<int?> CloseAsync()
    {
        int? blocked = null;
        // Start every cleanup even when another module refuses to close.
        try { if (Aoe is not null && !Aoe.TryClose()) blocked = 5; else Remove(5); }
        catch { blocked = 5; }
        Counter.Dispose();
        var pending = Forms.ToArray().Select(async item =>
        {
            try
            {
                if (!item.Value.IsDisposed)
                {
                    item.Value.Close(); var deadline = DateTime.UtcNow.AddSeconds(15);
                    while (!item.Value.IsDisposed && DateTime.UtcNow < deadline) await Task.Delay(40);
                    if (!item.Value.IsDisposed) return (int?)item.Key;
                }
                Remove(item.Key); return null;
            }
            catch { return (int?)item.Key; }
        }).ToArray();
        var failures = await Task.WhenAll(pending);
        return blocked ?? failures.FirstOrDefault(f => f.HasValue);
    }
    void Remove(int index) { Forms.Remove(index); if (Pages.Remove(index, out var page)) page.Dispose(); if (index == 5) Aoe = null; }
    public void Dispose() { Counter.Dispose(); }
}
