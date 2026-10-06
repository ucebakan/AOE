using System.Text.Json;

namespace UnityTools.Xyz;

static class XyzTests
{
    internal static int RunStandalone(string output)
    {
        output = Path.GetFullPath(output); Directory.CreateDirectory(output); var checks = new List<string>(); int exit = 0;
        using var dispatcher = new SuiteForm(true, notify: _ => { });
        dispatcher.Shown += (_, _) => dispatcher.BeginInvoke(async () =>
        {
            void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); checks.Add(message); }
            try
            {
                Check(dispatcher.Buttons.Actions[Feature.Coordinates].Text == "Aç", "compact Player XYZ button opens a window rather than applying coordinates");
                await Run(dispatcher, Check, output);
            }
            catch (Exception ex) { exit = 1; checks.Add("FAIL: " + ex); }
            finally
            {
                File.WriteAllText(Path.Combine(output, "xyz-tests.json"), JsonSerializer.Serialize(new { status = exit == 0 ? "PASS" : "FAIL", count = checks.Count, game_memory_writes = 0, checks }, new JsonSerializerOptions { WriteIndented = true }));
                dispatcher.Close();
            }
        });
        Application.Run(dispatcher); return exit;
    }
    sealed class FakeClient : IXyzClient
    {
        internal readonly List<float[]> Writes = new();
        internal bool Blocked;
        public Task<Reply> SendAsync(string command, float[]? values = null)
        {
            if (command == "teleport" && !Blocked) Writes.Add(values!);
            return Task.FromResult(new Reply(true, Blocked, !Blocked, Blocked ? "SafeMode engeli" : "Fixture · doğrulandı", [123.25f, -4.5f, 987.125f]));
        }
    }
    internal static async Task Run(Form dispatcher, Action<bool, string> check, string output)
    {
        string root = Path.Combine(output, "xyz-fixtures-" + Environment.ProcessId); Directory.CreateDirectory(root);
        var original = new[] { new SavedPoint("Farm giriş", -123.25f, 7.125f, 987.5f), new SavedPoint("İkinci nokta", 0.000001f, 0, 10000000) };
        string list = Path.Combine(root, "points.json"); PointStore.Save(list, original);
        check(PointStore.Load(list).SequenceEqual(original), "XYZ list round-trip preserves names order and float32 coordinates");
        string before = File.ReadAllText(list); bool invalid = false;
        try { PointStore.Save(list, [new("bad", float.NaN, 0, 0)]); } catch (IOException) { invalid = true; }
        check(invalid && File.ReadAllText(list) == before, "invalid point save leaves previous list intact");
        var fake = new FakeClient();
        using (var window = new XyzWindow(fake, Path.Combine(root, "window"), false))
        {
            window.Show(); await Task.Delay(50); await window.RefreshAsync();
            check(window.Width < window.Height && window.TopMost, "XYZ companion uses narrow vertical topmost layout");
            check(SuiteTests.Descendants(window).OfType<PlayerXYZ.CoordinateTextBox>().Count() == 3, "XYZ companion has exactly three coordinate boxes");
            check(window.Inputs.All(i=>i.Text.Length==0)&&window.LiveInputs.All(i=>i.ReadOnly)&&window.LiveInputs[0].Text=="123.25", "live coordinates use separate read-only boxes and targets start empty");
            await window.CopyLiveAsync();
            check(window.Inputs.Select(i => i.Text).SequenceEqual(new[] { "123.25", "-4.5", "987.125" }) && fake.Writes.Count == 0,
                "live-to-target copies all three axes without teleporting");
            window.PointName.Text = "İlk nokta"; window.AddPoint.PerformClick();
            window.SetTarget([-100, 1.2345678f, 200]); window.PointName.Text = "İkinci nokta"; window.AddPoint.PerformClick();
            check(PlayerXYZ.CoordinateTextBox.TryValue(window.Inputs[1].Text, out float exact) && exact == 1.2345678f, "XYZ target formatting preserves float32 precision without exponent notation");
            await window.RefreshAsync(); check(window.Inputs[0].Text == "-100", "live refresh never overwrites edited target coordinates");
            check(window.Points.Count == 2 && window.Grid.Rows.Count == 2 && fake.Writes.Count == 0, "adding ordered named points never writes game coordinates");
            string export = Path.Combine(root, "export.json"); window.Export(export);
            string malformed = Path.Combine(root, "broken.json"); File.WriteAllText(malformed, "{bad}"); bool rejected = false;
            try { window.Import(malformed); } catch (JsonException) { rejected = true; }
            check(rejected && window.Points.Count == 2, "malformed imported list preserves displayed points");
            window.Import(list); check(window.Points.SequenceEqual(original), "load replaces visible list only after validation");
            await window.TeleportPointAsync(1);
            check(fake.Writes.Count == 1 && fake.Writes[0].SequenceEqual(original[1].Values), "saved point dispatches exactly one selected XYZ tuple");
            fake.Blocked = true; await window.RefreshAsync(); await window.TeleportPointAsync(0);
            check(fake.Writes.Count == 1 && !window.Teleport.Enabled, "SafeMode rejects companion teleport without automatic replay"); fake.Blocked = false;
            window.Import(export);
            foreach (var size in new[] { new Size(320, 700), new Size(380, 760), new Size(740, 820) })
            {
                window.ClientSize = size; await Task.Delay(40); CheckLayout(window, check, "XYZ " + size.Width);
                Save(window, Path.Combine(output, "xyz-" + size.Width + ".png"));
            }
            window.WindowState = FormWindowState.Maximized; await Task.Delay(50); CheckLayout(window, check, "XYZ maximized");
            window.WindowState = FormWindowState.Normal;
            foreach (float scale in new[] { 1.25f, 1.5f, 2f })
            {
                using var scaled = new XyzWindow(fake, Path.Combine(root, "scaled"), false); scaled.Show();
                scaled.Import(export); scaled.Scale(new SizeF(scale, scale)); scaled.ClientSize = new((int)(380 * scale), (int)(760 * scale));
                await Task.Delay(50); CheckLayout(scaled, check, "XYZ scale " + scale);
                Save(scaled, Path.Combine(output, "xyz-scale-" + scale.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".png")); scaled.Close();
            }
            window.Close();
        }
        using(var reusable=new XyzWindow(fake,Path.Combine(root,"reuse"),false,reuseOnClose:true))
        {
            reusable.Show();await reusable.RefreshAsync();reusable.SetTarget([7,8,9]);reusable.Close();
            check(!reusable.IsDisposed&&!reusable.Visible,"closing cached XYZ hides the existing window");
            var elapsed=System.Diagnostics.Stopwatch.StartNew();reusable.Reopen();elapsed.Stop();
            check(reusable.Visible&&reusable.Inputs[0].Text=="7"&&elapsed.ElapsedMilliseconds<500,"reopening cached XYZ retains targets without starting a process");
            reusable.ClosePermanently();check(reusable.IsDisposed,"application shutdown can dispose cached XYZ window");
        }
        using (var reopened = new XyzWindow(fake, Path.Combine(root, "window"), false))
            check(reopened.Points.Count == 2 && reopened.Points[0].Name == "İlk nokta", "XYZ points return after companion restart without a running game");

        int dispatched = 0; float[]? received = null;
        using var bridge = new WindowBridge(dispatcher, request =>
        {
            if (request.Command == "teleport") { dispatched++; received = request.Values; }
            return Task.FromResult(new Reply(true, false, true, "Fixture · hazır", [10, 20, 30]));
        });
        var unauthorized = await bridge.DispatchAsync(new Request("wrong", "teleport", [1, 2, 3]));
        check(!unauthorized.Success && dispatched == 0, "XYZ pipe rejects requests without per-launch authentication");
        string report = Path.Combine(root, "child-report.json");
        await bridge.OpenAsync(report);
        var until = DateTime.UtcNow.AddSeconds(15);
        while (!File.Exists(report) && DateTime.UtcNow < until) await Task.Delay(50);
        check(File.Exists(report), "real XYZ companion process reports successful launch");
        using (var result = JsonDocument.Parse(File.ReadAllText(report)))
            check(result.RootElement.GetProperty("status").GetString() == "PASS" && result.RootElement.GetProperty("pid").GetInt32() != Environment.ProcessId,
                "XYZ opens in a separate process and saves a fixture list");
        check(bridge.Open && dispatched == 1 && received!.SequenceEqual(new float[] { 10, 20, 30 }), "companion remains open after a single point action");
        int pid = bridge.ChildId;
        File.WriteAllText(report+".hide","hide");until=DateTime.UtcNow.AddSeconds(3);
        while(!File.Exists(report+".hidden")&&DateTime.UtcNow<until)await Task.Delay(20);
        check(File.Exists(report+".hidden")&&bridge.Open,"user close hides real companion while keeping its process ready");
        var reopenedTimer=System.Diagnostics.Stopwatch.StartNew();await bridge.OpenAsync();
        until=DateTime.UtcNow.AddSeconds(3);while(!File.Exists(report+".reopened")&&DateTime.UtcNow<until)await Task.Delay(20);reopenedTimer.Stop();
        check(bridge.ChildId==pid&&File.Exists(report+".reopened")&&reopenedTimer.ElapsedMilliseconds<1000,"real XYZ companion reopens in under one second without a new process");
        File.WriteAllText(report + ".close", "close");
        until = DateTime.UtcNow.AddSeconds(5); while (bridge.Open && DateTime.UtcNow < until) await Task.Delay(40);
        check(!bridge.Open, "user close command ends only the XYZ companion process");
        check(dispatched == 1, "opening loading and closing companion never replay coordinate writes");
    }
    static void CheckLayout(XyzWindow window, Action<bool, string> check, string label)
    {
        window.PerformLayout(); Application.DoEvents();
        var controls = SuiteTests.Descendants(window).Where(c => c.Visible && c is Button or TextBoxBase or Label or DataGridView).ToArray();
        bool overlap = controls.Any(a => controls.Any(b => a != b && a.Parent == b.Parent && Rectangle.Intersect(a.Bounds, b.Bounds) is var r && r.Width > 1 && r.Height > 1));
        check(!overlap, label + " controls do not overlap");
        check(window.Grid.ClientSize.Width >= window.Grid.Columns[0].Width + window.Grid.Columns[4].Width, label + " point details and side button remain visible");
        check(window.Inputs.All(i => i.Width >= 45 && i.Height >= 20), label + " all three coordinate inputs remain usable");
    }
    static void Save(Form window, string path)
    { using var bitmap = new Bitmap(window.Width, window.Height); window.DrawToBitmap(bitmap, new(Point.Empty, bitmap.Size)); bitmap.Save(path); }
    internal static void ConfigureChildFixture(XyzWindow window, string report)
    {
        window.Shown += (_, _) => window.BeginInvoke(async () =>
        {
            try
            {
                await window.CopyLiveAsync(); window.PointName.Text = "IPC test"; window.AddCurrentPoint();
                window.Export(report + ".points.json"); await window.TeleportPointAsync(window.Points.Count - 1);
                File.WriteAllText(report, JsonSerializer.Serialize(new { status = "PASS", pid = Environment.ProcessId, game_memory_writes = 0, points = window.Points.Count }));
                var until = DateTime.UtcNow.AddSeconds(20);
                bool hidden=false;
                while (!File.Exists(report + ".close") && DateTime.UtcNow < until)
                {
                    if(!hidden&&File.Exists(report+".hide")){window.Close();hidden=true;File.WriteAllText(report+".hidden","hidden");}
                    if(hidden&&window.Visible)File.WriteAllText(report+".reopened","visible");
                    await Task.Delay(20);
                }
                window.ClosePermanently();
            }
            catch (Exception ex) { File.WriteAllText(report, JsonSerializer.Serialize(new { status = "FAIL", error = ex.ToString() })); window.ClosePermanently(); }
        });
    }
}
