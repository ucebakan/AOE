namespace UnityTools;

static class ShutdownTests
{
    internal static async Task Run(Action<bool, string> check)
    {
        var calls = new List<int>(); int active = 0, peak = 0;
        var release = new TaskCompletionSource();
        var running = ShutdownSequence.RunAsync(async page =>
        {
            calls.Add(page); peak = Math.Max(peak, ++active);
            if (page == 8) await release.Task;
            await Task.Delay(10); active--; return ShutdownResult.Complete;
        });
        await Task.Delay(30);
        check(!running.IsCompleted && calls.SequenceEqual(new[] { 8 }), "Multikill completes before the next shutdown starts; UI can yield");
        release.SetResult(); check(await running is null && peak == 1, "shutdown never overlaps restores");
        check(calls.SequenceEqual(new[] { 8, 1, 6, 4, 2, 3, 5 }), "shutdown order is fast tools first, AOE last");
        calls.Clear();
        var failed = await ShutdownSequence.RunAsync(page => { calls.Add(page); return Task.FromResult(page == 2 ? ShutdownResult.Failed : ShutdownResult.Complete); });
        check(failed == 2 && calls.Last() == 5, "finished failure retains blocked tool and permits remaining sequential cleanup");
        calls.Clear();
        failed = await ShutdownSequence.RunAsync(page => { calls.Add(page); return Task.FromResult(ShutdownResult.Pending); });
        check(failed == 8 && calls.SequenceEqual(new[] { 8 }), "pending timeout cannot start another restore concurrently");
        calls.Clear();
        failed = await ShutdownSequence.RunAsync(page => { calls.Add(page); throw new IOException("Fixture failure"); });
        check(failed == 8 && calls.Count == 1, "unknown cleanup failure fails closed without an overlapping restore");

        using var viewport = new Panel(); viewport.CreateControl();
        using var workspace = new ToolWorkspace(viewport, true);
        foreach (int page in new[] { 5, 3, 4, 2, 1, 8 }) workspace.Ensure(page);
        var closed = new List<int>();
        foreach (var item in workspace.Forms) { int page = item.Key; item.Value.FormClosed += (_, _) => closed.Add(page); }
        workspace.Quiesce();
        check(workspace.Forms.Values.All(f => !f.Enabled), "quiesce disables loaded module controls before restoration");
        var first = workspace.CloseAsync(); bool inFlight = !first.IsCompleted; var second = workspace.CloseAsync();
        check(!inFlight || ReferenceEquals(first, second), "duplicate close shares the active shutdown sequence");
        check(await first is null && workspace.Forms.Count == 0 && workspace.Aoe is null, "sequential actual workspace close releases every module");
        check(closed.SequenceEqual(new[] { 8, 1, 4, 2, 3 }), "actual embedded forms close in defined order independent of opening order");
    }
}
