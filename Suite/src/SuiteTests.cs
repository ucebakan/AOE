using System.Runtime.InteropServices;
using System.Text.Json;
using PlayerXYZ;

namespace UnityTools;

static class SuiteTests
{
    internal static IEnumerable<Control> Descendants(Control root)
    { foreach (Control c in root.Controls) { yield return c; foreach (var child in Descendants(c)) yield return child; } }
    public static int Run(string output)
    {
        Directory.CreateDirectory(output); var results = new List<string>(); int exit = 0;
        void Check(bool condition, string name) { if (!condition) throw new InvalidOperationException(name); results.Add(name); }
        int notifications = 0;
        using var shell = new SuiteForm(true, notify: _ => notifications++);
        shell.Shown += (_, _) => shell.BeginInvoke(async () =>
        {
            try
            {
                // Let the first Show/Shown pass finish applying native window styles.
                await Task.Delay(80);
                Check(shell.SelectedPage == SuiteForm.ButtonsPage && shell.Width < shell.Height, "starts as a narrow vertical Buttons window");
                using (var focusTarget = new Form { Text = "UI focus fixture" })
                {
                    focusTarget.Show(); focusTarget.Activate(); await Task.Delay(80);
                    Check(shell.TopMost && Above(shell.Handle, focusTarget.Handle), $"main window remains above a normal window receiving focus (shellTopMost={shell.TopMost}, shellVisible={IsWindowVisible(shell.Handle)}, shellStyle={GetWindowLongPtr(shell.Handle,-20):X}, targetStyle={GetWindowLongPtr(focusTarget.Handle,-20):X}, targetOwner={GetWindow(focusTarget.Handle,4):X})");
                    focusTarget.Close();
                }
                var mainTabs = Descendants(shell).OfType<NavButton>().ToArray();
                Check(mainTabs[0].Text=="Butonlar"&&!mainTabs.Any(b=>b.Text=="Genel bakış")&&!Descendants(shell).OfType<OverviewPanel>().Any(), "overview is removed and Buttons is the main page");
                Check(shell.Buttons.Actions.Count == 12 && !Descendants(shell.Buttons).Any(c => c is TextBoxBase or NumericUpDown), "compact tab contains twelve names and command buttons without settings fields");
                Save(shell, Path.Combine(output, "11-buttons.png"));
                var buttonBackend = new TestBackend { Ready = true }; int buttonNotices = 0;
                using (var compact = new SuiteForm(true, buttonBackend, _ => buttonNotices++))
                {
                    compact.Show(); await Task.Delay(60);
                    var speed = compact.Buttons.Actions[Feature.Speed];
                    speed.PerformClick(); await Task.Delay(40);
                    Check(speed.Text == "Kapat" && compact.Actions.Read(Feature.Speed).Active, "compact toggle activates real dispatcher");
                    speed.PerformClick(); await Task.Delay(40);
                    Check(speed.Text == "Aç" && !compact.Actions.Read(Feature.Speed).Active, "compact toggle closes the same backend feature");
                    compact.Buttons.Actions[Feature.Salesman].PerformClick(); await Task.Delay(40);
                    Check(buttonBackend.Applied[^1] == Feature.Salesman && compact.Buttons.Actions[Feature.Salesman].Text == "Aç" && !compact.Actions.Read(Feature.Salesman).Active, "Salesman compact button remains a one-shot open command");
                    compact.Buttons.Actions[Feature.Collection].PerformClick(); await Task.Delay(40);
                    Check(compact.Actions.Read(Feature.Collection).Active && compact.Buttons.Actions[Feature.Collection].Text == "Kapat", "Collection compact button starts automatic collection backend");
                    compact.Buttons.Actions[Feature.Collection].PerformClick(); await Task.Delay(40);
                    buttonBackend.Hold = new(); speed.PerformClick(); await Task.Delay(40);
                    Check(compact.Buttons.Actions.Values.All(b => !b.Enabled), "pending action disables all compact commands");
                    buttonBackend.Hold.SetResult(); await Task.Delay(40); buttonBackend.Hold = null;
                    speed.PerformClick(); await Task.Delay(40);
                    buttonBackend.Ready = false; int applied = buttonBackend.Applied.Count;
                    compact.Buttons.Actions[Feature.Collection].PerformClick(); await Task.Delay(40);
                    Check(buttonNotices == 1 && compact.SelectedPage == 10 && buttonBackend.Applied.Count == applied, "compact validation failure opens correct settings without queuing activation");
                    compact.SelectPage(SuiteForm.ButtonsPage);
                    using (var foreground = new Form())
                    {
                        foreground.Show(); foreground.Activate(); await Task.Delay(40);
                        Check(Above(compact.Handle, foreground.Handle), "compact window remains above another window receiving focus");
                        foreground.Close();
                    }
                    compact.Close(); await Task.Delay(60);
                }
                shell.SelectPage(0);
                foreach (string value in new[] { "0", "-123.5", "12,5", "-0,25", ".5", "123456" }) Check(CoordinateTextBox.TryValue(value, out _), "accepted number: " + value);
                foreach (string value in new[] { "abc", "12a", "1e3", "NaN", "Infinity", "1,2.3", "1..2", "--1", "1-2", "1 2", "１２", "+1", "", "-", "." }) Check(!CoordinateTextBox.TryValue(value, out _), "rejected number: " + value);
                using (var input = new CoordinateTextBox())
                {
                    input.Text = "12.5"; input.SelectAll(); input.SelectedText = "bad paste"; Check(input.Text == "12.5", "invalid paste rejected");
                    input.SelectAll(); input.SelectedText = "-9,25"; Check(input.Text == "-9,25", "valid paste accepted");
                }
                var fake = new TestBackend(); var dispatcher = new FeatureActions(fake);
                foreach (Feature feature in Enum.GetValues<Feature>())
                {
                    var blocked = await dispatcher.ExecuteAsync(feature); Check(blocked.RequiresSetup && fake.Applied.Count == 0, "unvalidated action blocked: " + feature);
                }
                fake.Ready = true;
                foreach (Feature feature in Enum.GetValues<Feature>())
                {
                    int before = fake.Applied.Count; var done = await dispatcher.ExecuteAsync(feature);
                    Check(!done.RequiresSetup && fake.Applied.Count == before + 1 && fake.Applied[^1] == feature, "validated action dispatch: " + feature);
                }
                await dispatcher.ExecuteAsync(Feature.Speed); Check(!dispatcher.Read(Feature.Speed).Active, "toggle reflects backend OFF state");
                Check(!dispatcher.Read(Feature.Coordinates).Active && !dispatcher.Read(Feature.MobTP).Active, "one-shot actions are not persistent toggles");
                fake.Hold = new(); var pending = dispatcher.ExecuteAsync(Feature.Jump); int calls = fake.Applied.Count;
                await dispatcher.ExecuteAsync(Feature.Jump); Check(fake.Applied.Count == calls, "double click cannot dispatch while action is pending");
                fake.Hold.SetResult(); await pending; fake.Hold = null;

                await ShutdownTests.Run(Check);
                await SafeModeTests.Run(Check);
                await StartupScanTests.Run(Check);
                var startup = new StartupScan();
                shell.SelectPage(SuiteForm.ButtonsPage);
                using (var modal = new ScanDialog(startup, (progress, token) => startup.RunAsync(new StartupScanTests.Probe(), progress, token)))
                {
                    bool ownerBlocked = false, aboveOwner = false, topmost = false;
                    modal.Shown += (_, _) => modal.BeginInvoke(() =>
                    {
                        ownerBlocked = modal.Modal && GetWindow(modal.Handle, 4) == shell.Handle && !IsWindowEnabled(shell.Handle);
                        topmost = modal.TopMost;
                        aboveOwner = Above(modal.Handle, shell.Handle);
                        Save(modal, Path.Combine(output, "startup-modal-front.png"));
                        modal.Close();
                    });
                    modal.ShowDialog(shell);
                    Check(ownerBlocked && topmost && aboveOwner, $"startup modal appears above topmost Buttons window and blocks owner input (blocked={ownerBlocked}, topmost={topmost}, above={aboveOwner})");
                    Check(IsWindowEnabled(shell.Handle) && shell.TopMost && shell.SelectedPage == SuiteForm.ButtonsPage, "closing startup modal restores compact owner without changing topmost preference");
                    Check(!startup.Busy && startup.Results.Count == 0, "opening startup modal never starts scan automatically");
                }
                using (var dialog = new ScanDialog(startup, (progress, token) => startup.RunAsync(new StartupScanTests.Probe(), progress, token)))
                {
                    dialog.Show(shell); await Task.Delay(60);
                    Check(dialog.TopMost && Above(dialog.Handle, shell.Handle), "scan status window also opens above its topmost owner");
                    Check(!startup.Busy && startup.Results.Count == 0, "startup popup waits for explicit scan choice");
                    Save(dialog, Path.Combine(output, "startup-prompt.png"));
                    Descendants(dialog).OfType<Button>().Single(b => b.Text == "Taramayı başlat").PerformClick();
                    while (startup.Busy) await Task.Delay(10);
                    foreach (var size in new[] { new Size(440, 380), new Size(680, 650), new Size(1000, 800) })
                    {
                        dialog.Size = size; await Task.Delay(40);
                        Check(LayoutValid(dialog, out string scanError), "scan popup responsive " + size + ": " + scanError);
                        var closeButton = Descendants(dialog).OfType<Button>().Single(b => b.Text == "Kapat");
                        Check(dialog.ClientRectangle.Contains(dialog.PointToClient(closeButton.PointToScreen(Point.Empty))), "scan close button remains visible " + size);
                    }
                    dialog.Size = new(680, 650); Save(dialog, Path.Combine(output, "startup-results.png"));
                    float previous = 1;
                    foreach (float scale in new[] { 1.25f, 1.5f, 2f })
                    {
                        dialog.Scale(new SizeF(scale / previous, scale / previous)); previous = scale; dialog.Size = new(900, 800); await Task.Delay(30);
                        Check(LayoutValid(dialog, out string error), "scan popup DPI " + scale + ": " + error);
                    }
                    dialog.Close();
                }
                shell.SelectPage(0);
                await Task.Delay(100); Save(shell, Path.Combine(output, "00-home.png"));
                Check(shell.Buttons.Actions.Count == 12, "main page exposes twelve actual action buttons including Collection");
                shell.Buttons.Actions[Feature.Speed].PerformClick(); await Task.Delay(150);
                Check(notifications == 1 && shell.SelectedPage == 2, "main action shows validation notice and opens correct page");
                Check(!shell.Actions.Read(Feature.Speed).Active, "failed validation never displays an active toggle");
                shell.SelectPage(SuiteForm.ButtonsPage);
                shell.Buttons.Actions[Feature.Collection].PerformClick(); await Task.Delay(100);
                Check(shell.SelectedPage == 10 && !shell.Actions.Read(Feature.Collection).Active, "Collection main button validates before activation and opens its settings on failure");
                using (var nativeReader = new NativeCountReader()) Check(true, "independent SafeMode native reader lifecycle available without overlay");
                foreach (int i in new[] { 1, 2, 3, 4, 5, 8, 9, 10 })
                {
                    shell.SelectPage(i); await Task.Delay(80); Check(shell.SelectedPage == i, "navigation " + SuiteForm.Names[i]);
                    if (i != 5) Check(!shell.Workspace.Forms[i].TopLevel && shell.Workspace.Forms[i].Parent != null, "managed module remains embedded " + i);
                    else
                    {
                        var tool = shell.Workspace.Aoe!; NativeModules.GetWindowThreadProcessId(tool.ToolWindow, out uint pid);
                        Check(pid == Environment.ProcessId && NativeModules.GetParent(tool.ToolWindow) == tool.Handle, "AOE shares host process and parent");
                        nint tabs = FindWindowEx(tool.ToolWindow, 0, "SysTabControl32", null); Check(tabs != 0 && !IsWindowVisible(tabs), "AOE research and profiles navigation hidden");
                        Check(!tool.Ready && !tool.Active && !tool.Toggle(), "AOE cannot arm without validated session");
                        foreach (string caption in new[] { "Auto Attach to TClient.exe", "Auto Arm after attach", "Advanced / Diagnostics", "COPY DIAGNOSTICS", "FREEZE" })
                            Check(FindWindowEx(tool.ToolWindow, 0, "Button", caption) == 0, "AOE removed control is absent: " + caption);
                    }
                    Save(shell, Path.Combine(output, $"0{i}-panel.png"));
                }
                shell.SelectPage(1); var xyz = shell.Workspace.Forms[1]; var inputs = Descendants(xyz).OfType<CoordinateTextBox>().ToArray();
                await AoeLayoutTests.Run(shell, Check, output); shell.SelectPage(1);
                Check(inputs.Length == 3, "three numeric coordinate fields retained"); inputs[0].Text = "-125,5";
                shell.SelectPage(0); shell.SelectPage(1); Check(inputs[0].Text == "-125,5", "coordinates retained across overview switches");

                shell.SelectPage(4); var mob = (MobTP.MainForm)shell.Workspace.Forms[4];
                var homeRange = Descendants(mob).OfType<NumericUpDown>().Single(c => c.Name == "HomeRange");
                Check(homeRange.Value == 50 && homeRange.Maximum > 50, "MobTP default Home range remains editable above 50");
                MobTP.Tests.RenderRangeFixture(mob);
                var tpButton = Descendants(mob).OfType<Button>().Single(b => b.Text == "UYGUN MOBLARI YANIMA GETİR");
                Check(!tpButton.Enabled, "MobTP preview excludes Home beyond selected 50");
                homeRange.Value = 100;
                Check(tpButton.Enabled && Descendants(mob).OfType<Label>().Any(l => l.Text.Contains("Home ≤ 100: 1")), "MobTP custom range refreshes count and eligibility immediately");
                shell.SelectPage(0); shell.SelectPage(4);
                Check(homeRange.Value == 100, "MobTP custom range retained across overview switches");
                Save(shell, Path.Combine(output, "04-panel.png"));
                homeRange.Value = 10; Check(!tpButton.Enabled, "MobTP smaller range refreshes eligibility immediately");
                homeRange.Value = 100;

                await Xyz.XyzTests.Run(shell, Check, output);
                shell.SelectPage(0); await shell.ExecuteFeatureAsync(Feature.Counter); nint overlay = shell.Workspace.Counter.Window;
                Check(overlay != 0 && NativeModules.GetParent(overlay) == 0, "counter is an independent top-level window");
                long style = GetWindowLongPtr(overlay, -20).ToInt64(); Check((style & 8) != 0 && (style & 0x80) != 0, "counter is topmost tool window");
                GetWindowRect(overlay, out var rect); Check(rect.Right - rect.Left <= 450 && rect.Bottom - rect.Top <= 100, "counter retains compact dimensions");
                SaveWindow(overlay, Path.Combine(output, "06-counter-overlay.png"));
                shell.WindowState = FormWindowState.Minimized; await Task.Delay(70); Check(IsWindowVisible(overlay), "counter remains visible while main app is minimized");
                shell.WindowState = FormWindowState.Normal; SendMessage(overlay, 0x10, 0, 0); Check(!shell.Workspace.Counter.Active, "external overlay close clears action state");
                await shell.ExecuteFeatureAsync(Feature.Counter); Check(shell.Workspace.Counter.Active, "counter reopens after external close");
                await shell.ExecuteFeatureAsync(Feature.Counter); Check(!shell.Workspace.Counter.Active, "overview button closes counter");

                foreach (var size in new[] { new Size(320, 600), new Size(396, 799), new Size(640, 600), new Size(820, 720), new Size(1240, 900), new Size(1600, 1000) })
                {
                    shell.Size = size;
                    foreach (int i in new[] { 0, 1, 2, 3, 4, 5, 8, 9, 10, SuiteForm.ButtonsPage }) { shell.SelectPage(i); await Task.Delay(45); Check(LayoutValid(shell, out string error), $"layout {size.Width}x{size.Height} page {i}: {error}"); }
                    Save(shell, Path.Combine(output, $"buttons-{size.Width}.png"));
                    shell.SelectPage(0); Save(shell, Path.Combine(output, $"layout-{size.Width}.png"));
                }
                shell.WindowState = FormWindowState.Maximized; await Task.Delay(80); Check(LayoutValid(shell, out _), "maximized layout");
                shell.WindowState = FormWindowState.Normal; await Task.Delay(80); Check(LayoutValid(shell, out _), "restored layout");
                shell.SelectPage(SuiteForm.ButtonsPage); await Task.Delay(50); Save(shell, Path.Combine(output, "07-safemode.png"));
                using (var standalone = new Multikill.MainForm(true))
                {
                    standalone.Show();
                    foreach (var size in new[] { new Size(480, 420), new Size(820, 620) })
                    { standalone.Size = size; await Task.Delay(50); Check(LayoutValid(standalone, out string error), "standalone Multikill layout " + size + ": " + error); }
                    Save(standalone, Path.Combine(output, "multikill-standalone.png")); standalone.Close();
                }
                // Exercise DPI-sized native text/layout and synthetic managed scale changes without changing Windows settings.
                shell.SelectPage(5);
                foreach (int dpi in new[] { 96, 120, 144, 192 })
                {
                    using var scaledTool = new NativeTool(true);
                    scaledTool.Fit(900 * dpi / 96, dpi);
                    int measuredHeight = NativeModules.Function<NativeModules.WindowFunction>("UnityAoe.dll", "ToolContentHeight")(scaledTool.ToolWindow);
                    Check(measuredHeight > 0 && scaledTool.Height == measuredHeight, "AOE host matches measured content at DPI " + dpi);
                    Check(AoeLayoutTests.Valid(scaledTool, out string nativeError), "AOE native label/rectangle layout at DPI " + dpi + ": " + nativeError);
                }
                float previousScale = 1;
                foreach (float scale in new[] { 1.25f, 1.5f, 2f })
                {
                    shell.Scale(new SizeF(scale / previousScale, scale / previousScale)); previousScale = scale;
                    shell.Size = new(1240, 900);
                    foreach (int page in new[] { 0, 1, 2, 3, 4, 8, 9, 10, SuiteForm.ButtonsPage }) { shell.SelectPage(page); await Task.Delay(40); Check(LayoutValid(shell, out string scaleError), $"managed {scale:P0} resize simulation page {page}: " + scaleError); }
                    shell.Size = new((int)(396 * scale), 900); shell.SelectPage(SuiteForm.ButtonsPage); await Task.Delay(40);
                    Check(LayoutValid(shell, out string compactError), $"compact managed scale {scale:P0}: {compactError}");
                    Save(shell, Path.Combine(output, $"buttons-scale-{(int)(scale * 100)}.png"));
                    shell.Size = new(1240, 900);
                    shell.SelectPage(0); Save(shell, Path.Combine(output, $"layout-scale-{(int)(scale*100)}.png"));
                }
                await shell.ExecuteFeatureAsync(Feature.Counter);
                Check(await shell.CloseTools(), "all modules close cleanly"); Check(!shell.Workspace.Counter.Active && shell.Workspace.Forms.Count == 0 && shell.Workspace.Aoe is null, "cleanup releases modules and overlay");
            }
            catch (Exception ex) { exit = 1; results.Add("FAIL: " + ex); Save(shell, Path.Combine(output, "failure.png")); }
            finally
            {
                File.WriteAllText(Path.Combine(output, "suite-tests.json"), JsonSerializer.Serialize(new { status = exit == 0 ? "PASS" : "FAIL", count = results.Count, writesToGame = 0, checks = results }, new JsonSerializerOptions { WriteIndented = true }));
                shell.Close();
            }
        });
        Application.Run(shell); return exit;
    }
    static bool LayoutValid(Control root, out string error)
    {
        error = "readable / no overlap";
        foreach (var parent in Descendants(root).Prepend(root).Where(c => c.Visible))
        {
            var children = parent.Controls.Cast<Control>().Where(c => c.Visible && c.Width > 0 && c.Height > 0).ToArray();
            for (int i = 0; i < children.Length; i++) for (int j = i + 1; j < children.Length; j++)
            {
                // Form scrollbars and WinForms internal NumericUpDown edit/spin children are framework-owned.
                if (parent is UpDownBase or ComboBox) continue;
                var overlap = Rectangle.Intersect(children[i].Bounds, children[j].Bounds);
                if (overlap.Width > 2 && overlap.Height > 2) { error = $"overlap {parent.GetType().Name} {parent.Size}: {children[i].GetType().Name} '{children[i].Text}' {children[i].Bounds} / '{children[j].Text}' {children[j].Bounds}"; return false; }
            }
            if (parent is Label label && label.AutoSize && label.Text.Length > 0)
            {
                var needed = label.GetPreferredSize(new(label.Width, 0));
                if (needed.Height > label.Height + 2) { error = "clipped label: " + label.Text; return false; }
            }
            if (parent is Button button && button.AutoSize)
            {
                var needed = TextRenderer.MeasureText(button.Text, button.Font);
                if (needed.Width > button.ClientSize.Width + 2 || needed.Height > button.ClientSize.Height + 2) { error = "clipped button: " + button.Text; return false; }
            }
        }
        return true;
    }
    static void Save(Form form, string path) { form.Update(); using var image = new Bitmap(form.Width, form.Height); form.DrawToBitmap(image, new(Point.Empty, image.Size)); image.Save(path); }
    static bool Above(nint window, nint owner)
    {
        // GW_HWNDPREV walks toward the front of the native Z order.
        for (nint previous = GetWindow(owner, 3); previous != 0; previous = GetWindow(previous, 3))
            if (previous == window) return true;
        return false;
    }
    static void SaveWindow(nint window, string path) { GetWindowRect(window, out var r); using var bitmap = new Bitmap(r.Right-r.Left,r.Bottom-r.Top); using (var g = Graphics.FromImage(bitmap)) { var dc = g.GetHdc(); PrintWindow(window, dc, 0); g.ReleaseHdc(dc); } bitmap.Save(path); }
    sealed class TestBackend : IFeatureBackend
    {
        internal bool Ready; internal TaskCompletionSource? Hold; internal readonly List<Feature> Applied = new(); readonly HashSet<Feature> active = new();
        public FeatureState Read(Feature f) => new(Ready, active.Contains(f), "Fixture state");
        public async Task<FeatureState> PrepareAsync(Feature f) { if (Hold is not null) await Hold.Task; return Read(f); }
        public Task<string> ApplyAsync(Feature f) { Applied.Add(f); if (!FeatureActions.OneShot(f) && !active.Remove(f)) active.Add(f); return Task.FromResult("Fixture action applied"); }
    }
    [StructLayout(LayoutKind.Sequential)] struct Rect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] static extern bool GetWindowRect(nint h, out Rect rect);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(nint h);
    [DllImport("user32.dll")] static extern bool IsWindowEnabled(nint h);
    [DllImport("user32.dll")] static extern nint GetWindow(nint h, uint command);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] static extern nint GetWindowLongPtr(nint h, int index);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern nint FindWindowEx(nint parent, nint after, string? type, string? text);
    [DllImport("user32.dll")] static extern nint SendMessage(nint h, uint message, nint w, nint l);
    [DllImport("user32.dll")] static extern bool PrintWindow(nint h, nint dc, uint flags);
}
