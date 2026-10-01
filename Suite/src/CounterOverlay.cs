using System.Runtime.InteropServices;

namespace UnityTools;

sealed class CounterOverlay : IDisposable
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate nint CreateOverlayDelegate(int preview);
    nint window;
    internal nint Window => NativeModules.IsWindow(window) ? window : 0;
    internal bool Active => Window != 0;
    internal void Toggle(bool preview)
    {
        if (Active) { Dispose(); return; }
        window = NativeModules.Function<CreateOverlayDelegate>("UnityCounter.dll", "CreateOverlay")(preview ? 1 : 0);
        if (window == 0) throw new InvalidOperationException("Küçük sayaç penceresi açılamadı.");
    }
    public void Dispose()
    {
        if (Active) NativeModules.Function<NativeModules.WindowFunction>("UnityCounter.dll", "CloseTool")(window);
        window = 0;
    }
}
