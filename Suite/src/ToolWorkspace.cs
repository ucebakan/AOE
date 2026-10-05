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
                case 9: form = new Salesman.SalesmanForm(preview); break;
                case 10: form = new Collection.CollectionForm(preview); break;
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
    Task<int?>? shutdown;
    internal Task<int?> CloseAsync(Action<int>? progress = null)
    {
        if (shutdown is { IsCompleted: false }) return shutdown;
        return shutdown = ShutdownSequence.RunAsync(CloseOneAsync, progress);
    }
    internal void Quiesce()
    {
        foreach (var page in Pages.Values) page.Enabled = false;
        foreach (var form in Forms.Values)
        {
            switch (form)
            {
                case Multikill.MainForm f: f.SuiteQuiesce(); break;
                case Salesman.SalesmanForm f: f.SuiteQuiesce(); break;
                case Collection.CollectionForm f: f.SuiteQuiesce(); break;
                case PlayerXYZ.MainForm f: f.SuiteQuiesce(); break;
                case SpeedJump.MainForm f: f.SuiteQuiesce(); break;
                case InvisibleAggro.MainForm f: f.SuiteQuiesce(); break;
                case MobTP.MainForm f: f.SuiteQuiesce(); break;
            }
        }
        Aoe?.StopForSafety();
    }
    static bool IsClosing(Form form) => form switch
    {
        Multikill.MainForm f => f.SuiteClosing,
        PlayerXYZ.MainForm f => f.SuiteClosing,
        SpeedJump.MainForm f => f.SuiteClosing,
        InvisibleAggro.MainForm f => f.SuiteClosing,
        MobTP.MainForm f => f.SuiteClosing,
        _ => false
    };
    async Task<ShutdownResult> CloseOneAsync(int index)
    {
        var deadline = DateTime.UtcNow.AddSeconds(15);
        if (index == 6) { Counter.Dispose(); return ShutdownResult.Complete; }
        if (index == 5)
        {
            if (Aoe is null) return ShutdownResult.Complete;
            do
            {
                var result = Aoe.CloseStep();
                if (result == ShutdownResult.Complete) { Remove(5); return result; }
                if (result == ShutdownResult.Failed) return result;
                await Task.Delay(40);
            } while (DateTime.UtcNow < deadline);
            return ShutdownResult.Pending;
        }
        if (!Forms.TryGetValue(index, out var form)) return ShutdownResult.Complete;
        if (form is Salesman.SalesmanForm salesman)
        {
            if (!await salesman.SuiteStopAsync()) return ShutdownResult.Failed;
            salesman.Close(); Remove(index); return ShutdownResult.Complete;
        }
        if (form is Collection.CollectionForm collection)
        {
            if (!await collection.SuiteStopAsync()) return ShutdownResult.Pending;
            collection.Close(); Remove(index); return ShutdownResult.Complete;
        }
        if (!form.IsDisposed && !IsClosing(form)) form.Close();
        while (!form.IsDisposed)
        {
            if (!IsClosing(form)) { Quiesce(); return ShutdownResult.Failed; }
            if (DateTime.UtcNow >= deadline) return ShutdownResult.Pending;
            await Task.Delay(40);
        }
        Remove(index); return ShutdownResult.Complete;
    }
    void Remove(int index) { Forms.Remove(index); if (Pages.Remove(index, out var page)) page.Dispose(); if (index == 5) Aoe = null; }
    public void Dispose() { Counter.Dispose(); }
}
