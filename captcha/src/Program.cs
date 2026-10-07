using System.Text.Json;

namespace UnityPuzzleTest;

static class Program
{
    [STAThread] static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        string output = Path.Combine(AppContext.BaseDirectory, "verification");
        try
        {
            if (args.Contains("--self-test")) { Directory.CreateDirectory(output); SelfTests.Run(output).GetAwaiter().GetResult(); return; }
            if (args.Contains("--integration-test")) { Directory.CreateDirectory(output); SelfTests.RunWindow(output); return; }
            if (args.Contains("--inspect-targets"))
            {
                Directory.CreateDirectory(output);
                bool f9 = Native.RegisterHotKey(IntPtr.Zero, 91, 0x4000, 0x78);
                int f9Error = f9 ? 0 : System.Runtime.InteropServices.Marshal.GetLastWin32Error();
                if (f9) Native.UnregisterHotKey(IntPtr.Zero, 91);
                var targets = WindowChoice.List().Where(w => string.Equals(Path.GetFileName(w.Path), "TClient.exe", StringComparison.OrdinalIgnoreCase))
                    .Select(w => new { w.Pid, w.Title, w.Path, window = w.Handle.ToString("X"), minimized = Native.IsIconic(w.Handle) }).ToArray();
                File.WriteAllText(Path.Combine(output, "target-inspection.json"), JsonSerializer.Serialize(new { utc = DateTimeOffset.UtcNow, f9_available = f9, f9_error = f9Error, targets, screen_captured = false, input_sent = false }, new JsonSerializerOptions { WriteIndented = true }));
                return;
            }
            if (args.Contains("--memory-probe"))
            {
                Directory.CreateDirectory(output);
                using var process = System.Diagnostics.Process.GetProcessById(int.Parse(args[1]));
                var target = new WindowChoice(process.MainWindowHandle, process.Id, "Read-only diagnostic", Path.GetFullPath(args[2]), process.StartTime.ToUniversalTime().Ticks);
                var linked = MemoryDetector.Connect(target, CancellationToken.None);
                using var detector = linked.Detector;
                File.WriteAllText(Path.Combine(output, "memory-probe.json"), JsonSerializer.Serialize(new { pid = target.Pid, target.Path, access = "0x410", detail = linked.Detail,
                    state = detector?.Snapshot(), input_sent = false, writes_attempted = false }, new JsonSerializerOptions { WriteIndented = true }));
                return;
            }
            if (args.Contains("--auto-test")) { Directory.CreateDirectory(output); AutoTests.Run(output, args.Skip(1).ToArray()).GetAwaiter().GetResult(); return; }
            if (args.Contains("--automatic-session-test"))
            {
                Directory.CreateDirectory(output); Exception? failure = null;
                using var form = new MainForm();
                form.Shown += async (_, _) =>
                {
                    try { await form.VerifyAutomaticSession(output); }
                    catch (Exception e) { failure = e; }
                    finally { form.Close(); }
                };
                Application.Run(form); if (failure != null) throw failure; return;
            }
            if (args.Contains("--analyze-files"))
            {
                Directory.CreateDirectory(output); var reader = new OcrReader();
                var result = args.Skip(1).Select(path =>
                {
                    using var image = new Bitmap(path);
                    var reading = reader.ReadAsync(image).GetAwaiter().GetResult();
                    return new { file = path, raw_text = reader.LastText, reading, exact_match = reading.Valid ? reading.Options.Single(o => o.Word == reading.Target) : null };
                }).ToArray();
                File.WriteAllText(Path.Combine(output, "image-readings.json"), JsonSerializer.Serialize(new { language = reader.Language, input_sent = false, result }, new JsonSerializerOptions { WriteIndented = true }));
                if (result.Any(r => !r.reading.Valid)) Environment.ExitCode = 1;
                return;
            }
            if (args.Contains("--render-ui"))
            {
                Directory.CreateDirectory(output); using var form = new MainForm(true);
                using var bitmap = new Bitmap(form.ClientSize.Width, form.ClientSize.Height);
                using var graphics = Graphics.FromImage(bitmap); graphics.Clear(form.BackColor);
                foreach (Control control in form.Controls)
                {
                    using var part = new Bitmap(control.Width, control.Height);
                    if (control is ComboBox combo)
                    {
                        using var g = Graphics.FromImage(part); g.Clear(Color.White);
                        using var brush = new SolidBrush(Color.Black);
                        using var format = new StringFormat { LineAlignment = StringAlignment.Center };
                        g.DrawString(combo.Text, combo.Font, brush, new RectangleF(5, 1, part.Width - 24, part.Height - 2), format);
                        g.DrawString("▾", combo.Font, brush, new PointF(part.Width - 18, 2));
                    }
                    else control.DrawToBitmap(part, new(0, 0, part.Width, part.Height));
                    graphics.DrawImageUnscaled(part, control.Left, control.Top);
                }
                bitmap.Save(Path.Combine(output, "puzzle-test-ui.png")); return;
            }
            if (args.Length > 0) throw new ArgumentException("Bilinmeyen tanılama seçeneği: " + args[0]);
            using var mutex = new Mutex(true, @"Local\4UnityPuzzleTest", out bool owner);
            if (!owner) { MessageBox.Show("Puzzle Test zaten açık.", "4Unity Puzzle Test"); return; }
            try { Application.Run(new MainForm()); } finally { mutex.ReleaseMutex(); }
        }
        catch (Exception e)
        {
            if (args.Length > 0)
            {
                Directory.CreateDirectory(output); File.WriteAllText(Path.Combine(output, "failure.txt"), e.ToString()); Environment.ExitCode = 1;
            }
            else MessageBox.Show(e.Message, "4Unity Puzzle Test");
        }
    }
}
