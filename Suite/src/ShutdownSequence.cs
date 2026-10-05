namespace UnityTools;

enum ShutdownResult { Complete, Failed, Pending }

// A pending asynchronous restore must finish before the next restore can start.
// A completed failure may be retried later while the remaining tools are stopped.
static class ShutdownSequence
{
    internal static readonly int[] Order = [10, 8, 9, 1, 6, 4, 2, 3, 5];
    internal static async Task<int?> RunAsync(Func<int, Task<ShutdownResult>> close, Action<int>? progress = null)
    {
        int? failed = null;
        foreach (int page in Order)
        {
            progress?.Invoke(page);
            ShutdownResult result;
            try { result = await close(page); }
            catch { return page; } // Unknown completion: never overlap a second restore.
            if (result == ShutdownResult.Pending) return page;
            if (result == ShutdownResult.Failed) failed ??= page;
        }
        return failed;
    }
}
