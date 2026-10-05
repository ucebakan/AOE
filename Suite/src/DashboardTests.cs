using UnityTools.Controls;

namespace UnityTools;

static class DashboardTests
{
    internal static async Task Run(SuiteForm shell, Action<bool, string> check, string output)
    {
        shell.SelectPage(0);
        var card = shell.Overview.Cards.Single(c => c.Id == 1); var xyz = card.Coordinates!;
        check(xyz.Inputs.Length == 3 && !xyz.Valid, "overview contains three empty numeric coordinate inputs");
        string[] values = ["-125,5", "12.75", "0"];
        for (int i = 0; i < 3; i++) xyz.Inputs[i].Text = values[i];
        check(xyz.Valid && Enumerable.Range(0, 3).All(i => shell.Workspace.Coordinates[i] == values[i]), "overview coordinates update shared draft including comma decimals");
        xyz.Inputs[0].SelectAll(); xyz.Inputs[0].SelectedText = "bad";
        check(xyz.Inputs[0].Text == values[0], "overview rejects nonnumeric paste");
        shell.SelectPage(1); var pageInputs = SuiteTests.Descendants(shell.Workspace.Forms[1]).OfType<PlayerXYZ.CoordinateTextBox>().ToArray();
        check(pageInputs.Select(i => i.Text).SequenceEqual(values), "overview values reach existing XYZ operation inputs");
        pageInputs[2].Text = "42"; check(xyz.Inputs[2].Text == "42", "XYZ detail edits synchronize back to overview");
        shell.SelectPage(0); var reply = await shell.ExecuteFeatureAsync(Feature.Coordinates);
        check(reply.RequiresSetup && !shell.Actions.Read(Feature.Coordinates).Active, "overview teleport retains validation guard and never auto writes in preview");
        var overview = shell.Overview; var before = overview.Cards.Select(c => c.Id).ToArray();
        check(overview.DraggedCard(overview.DragData(8)) == 8 && overview.DraggedCard(new DataObject("unrelated")) is null, "drag data accepts own cards and rejects foreign payloads");
        check(overview.MoveCard(8, 0) && overview.Cards[0].Id == 8, "card can move from last to first");
        using (var reopened = new OverviewPanel(_ => { }, _ => { }, new CoordinateDraft(), Path.Combine(Program.DataRoot, "card-order.json")))
            check(reopened.Cards.Select(c => c.Id).SequenceEqual(overview.Cards.Select(c => c.Id)), "card order survives overview reconstruction");
        check(overview.MoveCard(8, 7) && overview.Cards.Select(c => c.Id).SequenceEqual(before), "card can move back without losing other order or new Salesman card");
        check(!overview.MoveCard(-1, 0) && !overview.MoveCard(1, 99), "invalid reorder cannot corrupt card list");
        string damaged = Path.Combine(output, "order-fixture.json"); File.WriteAllText(damaged, "[8,8,999,1]");
        check(new CardOrder(damaged).Load(before).SequenceEqual(new[] { 8, 1, 2, 3, 4, 5, 6, 7, 9, 10 }), "saved order tolerates duplicates unknown ids and new cards");
        File.WriteAllText(damaged, "broken"); check(new CardOrder(damaged).Load(before).SequenceEqual(before), "corrupt order recovers default arrangement");
        shell.SelectPage(0); overview.AutoScrollPosition = Point.Empty;
    }
}
