namespace UnityTools;
static class SafeModeTests
{
    internal sealed class Count : ICountReader
    {
        public ulong? Value; public TaskCompletionSource<ulong?>? Pending;
        public Task<ulong?> ReadAsync() => Pending?.Task ?? Task.FromResult(Value);
        public void Dispose() { }
    }
    internal static async Task Run(Action<bool, string> check)
    {
        var count = new Count { Value = 0 }; int restores = 0;
        using (var safe = new SafeMode(() => count, () => { restores++; return Task.FromResult(true); }))
        {
            await safe.ToggleAsync(); check(safe.Enabled && !safe.Blocked && restores == 0, "SafeMode zero count permits operations without cleanup");
            count.Value = 1; await safe.SampleAsync(); check(safe.Blocked && restores == 1 && UnityTools.Controls.OperationGate.Blocked, "SafeMode threshold one triggers cleanup and gates hotkeys");
            count.Value = 8; await safe.SampleAsync(); check(safe.Blocked && restores == 1, "higher counts stay blocked without duplicate cleanup");
            count.Value = 0; await safe.SampleAsync(); check(!safe.Blocked && restores == 1, "return to zero releases gate without automatic feature enable");
            count.Value = null; await safe.SampleAsync(); check(safe.Blocked && restores == 2, "missing counter fails closed instead of treating missing as zero");
            await safe.ToggleAsync(); check(!safe.Enabled && !safe.Blocked, "explicit SafeMode disable after successful cleanup");
        }
        count = new Count { Value = 1 };
        using (var safe = new SafeMode(() => count, () => Task.FromResult(false)))
        {
            await safe.ToggleAsync(); check(safe.Blocked, "cleanup failure keeps operations blocked");
            count.Value = 0; await safe.SampleAsync(); check(safe.Blocked, "zero cannot release an unresolved cleanup");
            await safe.ToggleAsync(); check(safe.Enabled && safe.Blocked, "disable cannot conceal failed cleanup");
            await safe.PauseAsync(); await safe.SampleAsync(); safe.Resume();
            check(safe.Enabled && safe.Blocked, "failed application close preserves enabled monitor and unresolved cleanup");
        }
        count = new Count { Pending = new() }; restores = 0;
        using (var safe = new SafeMode(() => count, () => { restores++; return Task.FromResult(true); }))
        {
            var start = safe.ToggleAsync(); check(safe.Blocked && safe.Busy, "initial asynchronous counter validation blocks entry");
            check(!UnityTools.Controls.OperationGate.StopContinuous, "initial validation does not stop existing writers before a counter result");
            var duplicate = safe.SampleAsync(); count.Pending.SetResult(1); await Task.WhenAll(start, duplicate);
            check(restores == 1, "overlapping timer samples share one cleanup");
        }
        count = new Count { Value = 0 };
        using (var shell = new SuiteForm(true, notify: _ => { }, countReader: count))
        {
            shell.Show(); shell.SelectPage(2); await shell.ExecuteFeatureAsync(Feature.Counter);
            await shell.ExecuteFeatureAsync(Feature.SafeMode); count.Value = 1; await shell.Safety.SampleAsync();
            check(shell.Workspace.Forms.Count == 0 && !shell.Workspace.Counter.Active && shell.SelectedPage == 0, "SafeMode actual shell closes tools and overlay but leaves shell open");
            var blocked = await shell.ExecuteFeatureAsync(Feature.Speed); check(!blocked.RequiresSetup && shell.Workspace.Forms.Count == 0, "blocked overview cannot reopen a tool");
            shell.SelectPage(4); check(shell.SelectedPage == 0, "blocked settings cannot enable embedded actions");
            count.Value = 0; await shell.Safety.SampleAsync(); shell.SelectPage(2); check(shell.SelectedPage == 2, "zero permits manual navigation again");
            await shell.CloseTools(); shell.Close();
        }
    }
}
