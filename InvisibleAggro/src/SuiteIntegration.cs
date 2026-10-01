using UnityTools.Controls;

namespace InvisibleAggro;

sealed partial class MainForm
{
    internal bool SuiteReady => engine.View.Ready && !busy && !closing;
    internal bool SuiteActive(bool isAggro) => engine.View.Mode == (isAggro ? StateMode.Aggro : StateMode.Invisible);
    internal string SuiteMessage => engine.View.Message;
    internal async Task SuiteRefreshAsync() { while (busy && !closing) await Task.Delay(30); if (!closing) { await Work(engine.Poll); timer.Start(); } }
    internal Task SuiteToggleAsync(bool isAggro) => Work(() => engine.Toggle(isAggro ? StateMode.Aggro : StateMode.Invisible));
    internal void ConfigureSuiteLayout()
    {
        var stack = new ContentStack(); stack.AddRow(Responsive.Text("Invisible / Aggro", 17, true));
        stack.AddRow(Responsive.Text("Bir modu açmak diğer modun yerini alır. Aktif düğmeye tekrar basmak modu kapatır."));
        Responsive.Button(invisible); Responsive.Button(aggro); Responsive.Button(approve);
        stack.AddRow(Responsive.Flow(invisible, aggro)); Responsive.Readable(status); stack.AddRow(status);
        stack.AddRow(Responsive.Flow(approve)); Responsive.Mount(this, stack);
    }
}
