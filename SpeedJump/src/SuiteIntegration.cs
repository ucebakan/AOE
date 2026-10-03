using UnityTools.Controls;

namespace SpeedJump;

sealed partial class MainForm
{
    internal bool SuiteClosing => closing;
    internal void SuiteQuiesce() { timer.Stop(); Enabled = false; }
    internal bool SuiteReady => engine.View.Ready && !busy && !closing;
    internal bool SuiteActive(bool isJump) => isJump ? engine.View.Jump : engine.View.Speed;
    internal string SuiteMessage => engine.View.Message;
    internal async Task SuiteRefreshAsync() { while (busy && !closing) await Task.Delay(30); if (!closing) { await Work(engine.Poll); if (Enabled) timer.Start(); } }
    internal Task SuiteToggleAsync(bool isJump) => Work(() => engine.Toggle(isJump));
    internal void ConfigureSuiteLayout()
    {
        var stack = new ContentStack(); stack.AddRow(Responsive.Text("Speed / Jump", 17, true));
        stack.AddRow(Responsive.Text("İki özellik bağımsız çalışır. Genel bakıştaki düğmeler de aynı durumu yönetir."));
        Responsive.Button(speed); Responsive.Button(jump); stack.AddRow(Responsive.Flow(speed, jump));
        Responsive.Readable(status); stack.AddRow(status); Responsive.Mount(this, stack);
    }
}
