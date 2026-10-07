using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace UnityPuzzleTest;

sealed record WindowChoice(IntPtr Handle, int Pid, string Title, string Path, long Created)
{
    public override string ToString() => $"{Title}  ·  PID {Pid}  ·  {System.IO.Path.GetFileName(Path)}";
    public static WindowChoice[] List()
    {
        var choices = new List<WindowChoice>();
        Native.EnumWindows((h, _) =>
        {
            if (!Native.IsWindowVisible(h) || Native.GetWindowTextLength(h) == 0) return true;
            Native.GetWindowThreadProcessId(h, out int pid);
            if (pid == Environment.ProcessId) return true;
            try
            {
                using var p = Process.GetProcessById(pid);
                string path = p.MainModule?.FileName ?? "";
                var text = new StringBuilder(512); Native.GetWindowText(h, text, text.Capacity);
                if (path.Length > 0) choices.Add(new(h, pid, text.ToString(), path, p.StartTime.ToUniversalTime().Ticks));
            }
            catch { }
            return true;
        }, IntPtr.Zero);
        return choices.OrderBy(c => c.Title).ToArray();
    }
    public bool IsReady(out string reason, Size? expected = null, Rectangle? region = null)
    {
        reason = "";
        if (!Native.IsWindow(Handle)) { reason = "Seçilen oyun penceresi kapandı; tekrar client seçin"; return false; }
        if (Native.IsIconic(Handle)) { reason = "Bekliyor: seçilen oyun penceresi küçültülmüş"; return false; }
        Native.GetWindowThreadProcessId(Handle, out int pid);
        try
        {
            using var p = Process.GetProcessById(pid);
            if (pid != Pid || p.StartTime.ToUniversalTime().Ticks != Created || !string.Equals(p.MainModule?.FileName, Path, StringComparison.OrdinalIgnoreCase))
                { reason = "Test uygulaması oturumu değişti; tekrar pencere seçin"; return false; }
        }
        catch { reason = "Test uygulamasına erişilemiyor"; return false; }
        if (Native.GetAncestor(Native.GetForegroundWindow(), 2) != Handle) { reason = "Bekliyor: seçilen test penceresini öne getirin"; return false; }
        if (!Native.GetClientRect(Handle, out var rect)) { reason = "Pencere boyutu okunamadı"; return false; }
        var size = new Size(rect.Right, rect.Bottom);
        if (expected != null && expected.Value != size) { reason = "Pencere boyutu değişti; alanı tekrar seçin"; return false; }
        if (region != null)
        {
            if (!new Rectangle(Point.Empty, size).Contains(region.Value)) { reason = "Seçilen alan pencere dışında"; return false; }
            var origin = new Native.POINT();
            if (!Native.ClientToScreen(Handle, ref origin)) { reason = "Ekran konumu okunamadı"; return false; }
            Rectangle screen = region.Value; screen.Offset(origin.X, origin.Y);
            if (!SystemInformation.VirtualScreen.Contains(screen)) { reason = "Test alanının tamamı görünür olmalı"; return false; }
            foreach (double x in new[] { .08, .5, .92 }) foreach (double y in new[] { .08, .5, .92 })
            {
                var at = new Native.POINT { X = screen.Left + (int)(screen.Width * x), Y = screen.Top + (int)(screen.Height * y) };
                IntPtr top = Native.WindowFromPoint(at);
                if (top != Handle && !Native.IsChild(Handle, top)) { reason = "Test alanının üstünde başka bir pencere var"; return false; }
            }
        }
        return true;
    }
    public Bitmap Capture(Rectangle? crop, Size? expected = null)
    {
        if (!IsReady(out string reason, expected, crop)) throw new IOException(reason);
        Native.GetClientRect(Handle, out var rect);
        var bounds = crop ?? new Rectangle(0, 0, rect.Right, rect.Bottom);
        var origin = new Native.POINT { X = bounds.Left, Y = bounds.Top };
        if (!Native.ClientToScreen(Handle, ref origin)) throw new Win32Exception();
        var bitmap = new Bitmap(bounds.Width, bounds.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        try { using var g = Graphics.FromImage(bitmap); g.CopyFromScreen(origin.X, origin.Y, 0, 0, bitmap.Size); return bitmap; }
        catch { bitmap.Dispose(); throw; }
    }
    public void Click(Point point, Rectangle region, Size expected)
    {
        if (!region.Contains(point)) throw new IOException("Tıklama test alanı dışında");
        if (!IsReady(out string reason, expected, region)) throw new IOException("Tıklama koşulları değişti: " + reason);
        if (point.X is < 0 or > 32767 || point.Y is < 0 or > 32767) throw new IOException("Tıklama konumu desteklenmiyor");
        IntPtr position = (IntPtr)((point.Y << 16) | point.X);
        if (!Native.PostMessage(Handle, 0x200, IntPtr.Zero, position)) throw new Win32Exception();
        bool down = Native.PostMessage(Handle, 0x201, (IntPtr)1, position);
        bool up = Native.PostMessage(Handle, 0x202, IntPtr.Zero, position);
        if (!down || !up) throw new IOException("Test penceresine tıklama iletilemedi");
    }
}

static class Native
{
    public delegate bool EnumProc(IntPtr h, IntPtr l);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc callback, IntPtr l);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr h);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowTextLength(IntPtr h);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, StringBuilder text, int size);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out int pid);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern IntPtr GetAncestor(IntPtr h, uint flags);
    [DllImport("user32.dll")] public static extern bool IsChild(IntPtr parent, IntPtr child);
    [DllImport("user32.dll")] public static extern IntPtr WindowFromPoint(POINT point);
    [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr h, out RECT rectangle);
    [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr h, ref POINT point);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] public static extern bool PostMessage(IntPtr h, uint message, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll", SetLastError = true)] public static extern bool RegisterHotKey(IntPtr h, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr h, int id);
}
