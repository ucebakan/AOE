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
    Rectangle? lastClip;
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
        Resize += (_, _) => ResizeTool(); LocationChanged += (_, _) => UpdateClip(); ResizeTool();
    }
    internal void Fit(int availableWidth, int displayDpi)
    {
        if (ToolWindow == 0) return;
        dpi(ToolWindow, displayDpi); Width = Math.Max(1, availableWidth); ResizeTool();
        Height = Math.Max(1, height(ToolWindow));
        UpdateClip();
    }
    void ResizeTool()
    {
        if (ToolWindow == 0) return;
        GetClientRect(ToolWindow, out var current);
        if (current.Right != ClientSize.Width || current.Bottom != ClientSize.Height)
            MoveWindow(ToolWindow, 0, 0, ClientSize.Width, ClientSize.Height, true);
    }
    void UpdateClip()
    {
        if (ToolWindow == 0 || Parent is null) return;
        var visible = Rectangle.Intersect(Bounds, Parent.ClientRectangle);
        visible.Offset(-Left, -Top);
        if (lastClip == visible) return;
        nint region = CreateRectRgn(visible.Left, visible.Top, visible.Right, visible.Bottom);
        if (SetWindowRgn(ToolWindow, region, true) == 0) { DeleteObject(region); return; }
        // Windows owns the region after a successful SetWindowRgn.
        lastClip = visible;
    }
    internal void Redraw() { UpdateClip(); RedrawWindow(Handle, 0, 0, 0x0001 | 0x0004 | 0x0080 | 0x0100); }
    protected override CreateParams CreateParams
    {
        get { var parameters = base.CreateParams; parameters.Style |= 0x02000000 | 0x04000000; return parameters; }
    }
    internal void StopForSafety() { if (ToolWindow != 0) stop(ToolWindow); }
    internal bool TryClose() => CloseStep() == ShutdownResult.Complete;
    internal ShutdownResult CloseStep()
    {
        if (ToolWindow == 0) return ShutdownResult.Complete;
        int result = close(ToolWindow);
        if (result <= 0) return result < 0 ? ShutdownResult.Pending : ShutdownResult.Failed;
        ToolWindow = 0; return ShutdownResult.Complete;
    }
    protected override void Dispose(bool disposing) { if (disposing && ToolWindow != 0) TryClose(); base.Dispose(disposing); }
    [DllImport("user32.dll")] static extern bool MoveWindow(nint h, int x, int y, int w, int height, bool repaint);
    [StructLayout(LayoutKind.Sequential)] struct Rect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] static extern bool GetClientRect(nint h, out Rect rectangle);
    [DllImport("user32.dll")] static extern bool RedrawWindow(nint h, nint rectangle, nint region, uint flags);
    [DllImport("user32.dll")] static extern int SetWindowRgn(nint h, nint region, bool redraw);
    [DllImport("gdi32.dll")] static extern nint CreateRectRgn(int left, int top, int right, int bottom);
    [DllImport("gdi32.dll")] static extern bool DeleteObject(nint value);
}
