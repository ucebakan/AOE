using System.Runtime.InteropServices;
using System.Text;

namespace UnityTools;

static class AoeLayoutTests
{
    internal static async Task Run(SuiteForm shell, Action<bool, string> check, string output)
    {
        shell.Size = new(396, 799); shell.SelectPage(5); await Task.Delay(80);
        var page = (ScrollableControl)shell.Workspace.Pages[5]; var tool = shell.Workspace.Aoe!;
        int events = 0; ScrollEventHandler onScroll = (_, _) => events++;
        page.Scroll += onScroll;
        try
        {
            CheckSkillStatus(tool, shell.DeviceDpi, check);
            var before = Children(tool.ToolWindow);
            check(Valid(tool, out string error), "AOE narrow native text/rectangles: " + error);
            check((GetWindowLongPtr(tool.Handle, -16).ToInt64() & 0x02000000) != 0, "AOE managed host clips native child paint");
            foreach (int position in new[] { 200, 600, 1000, 300, 0 })
            {
                page.AutoScrollPosition = new(0, position);
                // Use a real line-up/top scroll message after positioning. End-scroll
                // alone does not raise WinForms' Scroll event.
                SendMessage(page.Handle, 0x0115, position == 0 ? 6 : 0, 0);
                await Task.Delay(80);
                check(position == 0 || page.DisplayRectangle.Height <= page.ClientSize.Height || page.AutoScrollPosition.Y < 0, "AOE viewport actually moves while scrolling " + position);
                check(tool.Top == page.AutoScrollPosition.Y, "AOE host follows scroll offset " + position);
                var expectedClip = Rectangle.Intersect(tool.Bounds, page.ClientRectangle);
                expectedClip.Offset(-tool.Left, -tool.Top);
                nint region = CreateRectRgn(0, 0, 0, 0);
                try
                {
                    int kind = GetWindowRgn(tool.ToolWindow, region); GetRgnBox(region, out var clipped);
                    check(kind != 0 && new Rectangle(clipped.Left, clipped.Top, clipped.Right-clipped.Left, clipped.Bottom-clipped.Top) == expectedClip,
                        "AOE native clipping matches visible viewport " + position);
                }
                finally { DeleteObject(region); }
                check(Children(tool.ToolWindow).SequenceEqual(before), "AOE native child geometry remains stable while scrolling " + position);
                check(Valid(tool, out error), "AOE native layout after scroll " + position + ": " + error);
                if (position == 600)
                {
                    Save(shell, Path.Combine(output, "aoe-scrolled.png"));
                    shell.Activate(); await Task.Delay(40);
                    using var screen = new Bitmap(shell.Width, shell.Height);
                    using (var graphics = Graphics.FromImage(screen)) graphics.CopyFromScreen(shell.Location, Point.Empty, screen.Size);
                    screen.Save(Path.Combine(output, "aoe-scrolled-screen.png"));
                }
            }
            check(events > 0, "AOE scrolling exercises host repaint handler");
            Save(shell, Path.Combine(output, "aoe-narrow-header.png"));
        }
        finally { page.Scroll -= onScroll; page.AutoScrollPosition = Point.Empty; }
    }

    static void CheckSkillStatus(NativeTool tool, int originalDpi, Action<bool, string> check)
    {
        var status = Children(tool.ToolWindow).First(c => c.Text.StartsWith("Bağlantı:"));
        int originalWidth = tool.Width;
        try
        {
            foreach (int dpi in new[] { 96, 120, 144, 192 })
            foreach (string skill in new[] { "Archer / Rain of Arrows", "Mage / Ice Rain", "Priest / Shadow Thunderstorm" })
            {
                string text = "Bağlantı: Hazır · " + skill + "\r\nInitial Nx: Başlangıç çağrısı bekleniyor";
                SetWindowText(status.Window, text);
                // Production UpdatePlay calls LayoutSuite after changing this
                // label. Reproduce that layout notification in the preview.
                SendMessage(tool.ToolWindow, 0x0005, 0, 0);
                tool.Fit(originalWidth, dpi);
                check(Valid(tool, out string error), "AOE family status wraps without overlap at DPI " + dpi + " " + skill + ": " + error);
            }
        }
        finally { SetWindowText(status.Window, status.Text); SendMessage(tool.ToolWindow, 0x0005, 0, 0); tool.Fit(originalWidth, originalDpi); }
    }

    internal static bool Valid(NativeTool tool, out string error)
    {
        error = "measured header, readable text, no child overlap";
        int header = NativeModules.Function<NativeModules.WindowFunction>("UnityAoe.dll", "ToolHeaderHeight")(tool.ToolWindow);
        var children = Children(tool.ToolWindow);
        foreach (var child in children)
        {
            if (child.Bounds.Left < 0 || child.Bounds.Right > tool.ClientSize.Width || child.Bounds.Bottom > tool.ClientSize.Height)
            { error = "native control exceeds measured content: " + child.Text; return false; }
            if (child.Bounds.Top < header) { error = "header overlaps " + child.Text; return false; }
            if (child.Class == "Static" && child.Text.Length > 0)
            {
                nint dc = GetDC(child.Window), font = SendMessage(child.Window, 0x0031, 0, 0), previous = SelectObject(dc, font);
                var measure = new Rect { Right = child.Bounds.Width };
                DrawText(dc, child.Text, -1, ref measure, 0x0410 | 0x0800);
                SelectObject(dc, previous); ReleaseDC(child.Window, dc);
                if (measure.Bottom > child.Bounds.Height) { error = "clipped native label " + child.Text; return false; }
            }
        }
        for (int i = 0; i < children.Count; i++) for (int j = i + 1; j < children.Count; j++)
        {
            // The numeric spin control deliberately sits over the edit's right edge.
            if (children[i].Class == "msctls_updown32" || children[j].Class == "msctls_updown32") continue;
            var overlap = Rectangle.Intersect(children[i].Bounds, children[j].Bounds);
            if (overlap.Width > 2 && overlap.Height > 2) { error = children[i].Text + " overlaps " + children[j].Text; return false; }
        }
        return true;
    }

    record Child(nint Window, string Class, string Text, Rectangle Bounds);
    static List<Child> Children(nint root)
    {
        var result = new List<Child>(); GetWindowRect(root, out var origin);
        EnumChildWindows(root, (window, _) =>
        {
            if (NativeModules.GetParent(window) != root || (GetWindowLongPtr(window, -16).ToInt64() & 0x10000000) == 0) return true;
            var type = new StringBuilder(64); GetClassName(window, type, type.Capacity);
            var text = new StringBuilder(GetWindowTextLength(window) + 1); GetWindowText(window, text, text.Capacity);
            GetWindowRect(window, out var rectangle);
            result.Add(new(window, type.ToString(), text.ToString(), new(rectangle.Left-origin.Left, rectangle.Top-origin.Top, rectangle.Right-rectangle.Left, rectangle.Bottom-rectangle.Top)));
            return true;
        }, 0);
        return result;
    }
    static void Save(Form form, string path) { using var image = new Bitmap(form.Width, form.Height); form.DrawToBitmap(image, new(Point.Empty, image.Size)); image.Save(path); }
    [DllImport("user32.dll", EntryPoint="SetWindowTextW", CharSet=CharSet.Unicode)] static extern bool SetWindowText(nint h, string text);
    delegate bool EnumProc(nint window, nint state);
    [StructLayout(LayoutKind.Sequential)] struct Rect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] static extern bool GetWindowRect(nint h, out Rect rectangle);
    [DllImport("user32.dll")] static extern bool EnumChildWindows(nint h, EnumProc callback, nint state);
    [DllImport("user32.dll", EntryPoint="GetWindowLongPtrW")] static extern nint GetWindowLongPtr(nint h, int index);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] static extern int GetClassName(nint h, StringBuilder text, int count);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] static extern int GetWindowText(nint h, StringBuilder text, int count);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] static extern int GetWindowTextLength(nint h);
    [DllImport("user32.dll")] static extern nint SendMessage(nint h, uint message, nint w, nint l);
    [DllImport("user32.dll")] static extern nint GetDC(nint h);
    [DllImport("user32.dll")] static extern int ReleaseDC(nint h, nint dc);
    [DllImport("gdi32.dll")] static extern nint SelectObject(nint dc, nint item);
    [DllImport("gdi32.dll")] static extern nint CreateRectRgn(int left, int top, int right, int bottom);
    [DllImport("gdi32.dll")] static extern int GetRgnBox(nint region, out Rect rectangle);
    [DllImport("gdi32.dll")] static extern bool DeleteObject(nint value);
    [DllImport("user32.dll")] static extern int GetWindowRgn(nint h, nint region);
    [DllImport("user32.dll", EntryPoint="DrawTextW", CharSet=CharSet.Unicode)] static extern int DrawText(nint dc, string text, int count, ref Rect rectangle, uint flags);
}
