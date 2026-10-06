using System.Runtime.InteropServices;

namespace UnityTools;

static class WindowLayer
{
    internal static bool PlaceTopmost(Form form, bool focus = false)
    {
        if (form.IsDisposed || !form.IsHandleCreated) return false;
        form.TopMost = true;
        // Apply after the native Show pass; its initial positioning can otherwise
        // replace the Z order requested during construction/Shown.
        bool placed = SetWindowPos(form.Handle, (nint)(-1), 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010);
        if (focus) { form.BringToFront(); form.Activate(); }
        return placed;
    }
    [DllImport("user32.dll", SetLastError = true)]
    static extern bool SetWindowPos(nint window, nint after, int x, int y, int width, int height, uint flags);
}
