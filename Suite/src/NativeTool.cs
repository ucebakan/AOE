using System.Runtime.InteropServices;

namespace UnityTools;

static class NativeModules
{
    internal static readonly Dictionary<string, string> Paths = new();
    static readonly Dictionary<string, nint> loaded = new();
    internal static T Function<T>(string file, string name) where T : Delegate
    {
        if (!loaded.TryGetValue(file, out var module)) loaded[file] = module = NativeLibrary.Load(Paths[file]);
        return Marshal.GetDelegateForFunctionPointer<T>(NativeLibrary.GetExport(module, name));
    }
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int WindowFunction(nint window);
    [DllImport("user32.dll")] internal static extern bool IsWindow(nint window);
    [DllImport("user32.dll")] internal static extern nint GetParent(nint window);
    [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(nint window, out uint pid);
}

sealed class NativeTool : Panel
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
    delegate nint CreateDelegate(nint parent, [MarshalAs(UnmanagedType.LPWStr)] string root, int preview);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate void DpiDelegate(nint window, int dpi);
    readonly NativeModules.WindowFunction close, state, toggle, height, stop;
    readonly DpiDelegate dpi;
    public nint ToolWindow { get; private set; }
    internal bool Ready => (state(ToolWindow) & 1) != 0;
    internal bool Active => (state(ToolWindow) & 2) != 0;
    internal bool Toggle() => toggle(ToolWindow) != 0;
    public NativeTool(bool preview)
    {
        const string file = "UnityAoe.dll"; BackColor = Palette.Surface; Size = new(800, 760);
        stop = NativeModules.Function<NativeModules.WindowFunction>(file, "StopForSafety");
        close = NativeModules.Function<NativeModules.WindowFunction>(file, "CloseTool");
        state = NativeModules.Function<NativeModules.WindowFunction>(file, "ToolState");
        toggle = NativeModules.Function<NativeModules.WindowFunction>(file, "ToggleTool");
        height = NativeModules.Function<NativeModules.WindowFunction>(file, "ToolContentHeight");
        dpi = NativeModules.Function<DpiDelegate>(file, "ToolDpi");
        ToolWindow = NativeModules.Function<CreateDelegate>(file, "CreateTool")(Handle, Path.Combine(Program.DataRoot, "AOE"), preview ? 1 : 0);
        if (ToolWindow == 0) throw new InvalidOperationException("AOE paneli oluşturulamadı.");
        Resize += (_, _) => ResizeTool(); ResizeTool();
    }
    internal void Fit(int availableWidth, int displayDpi)
    {
        if (ToolWindow == 0) return;
        dpi(ToolWindow, displayDpi); Width = Math.Max(1, availableWidth); ResizeTool();
        Height = Math.Max(1, height(ToolWindow));
    }
    void ResizeTool() { if (ToolWindow != 0) MoveWindow(ToolWindow, 0, 0, ClientSize.Width, ClientSize.Height, true); }
    internal void StopForSafety() { if (ToolWindow != 0) stop(ToolWindow); }
    internal bool TryClose()
    {
        if (ToolWindow == 0) return true;
        if (close(ToolWindow) == 0) return false;
        ToolWindow = 0; return true;
    }
    protected override void Dispose(bool disposing) { if (disposing && ToolWindow != 0) TryClose(); base.Dispose(disposing); }
    [DllImport("user32.dll")] static extern bool MoveWindow(nint h, int x, int y, int w, int height, bool repaint);
}
