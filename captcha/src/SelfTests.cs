using System.Diagnostics;
using System.Text.Json;

namespace UnityPuzzleTest;

static class SelfTests
{
    static int checks;
    static void Check(bool condition, string message)
    {
        checks++; if (!condition) throw new InvalidOperationException(message);
    }
    static Reading Sample(int step = 1, string target = "SUN") => new(ReadingKind.Ready, target, step, 3,
        [new("CROWN", new(110, 340)), new("STAR", new(280, 340)), new("SUN", new(110, 400)), new("LEAF", new(280, 400))], "");
    static Option? Stable(PuzzleFlow flow, Reading reading, double at) { flow.Observe(reading, at); return flow.Observe(reading, at + .6); }
    public static async Task Run(string output)
    {
        var flow = new PuzzleFlow(); flow.Reset();
        Check(flow.Observe(Sample(), 0) == null && flow.Blocked, "first reading must pause without clicking");
        Check(flow.Observe(Sample(), .6)?.Word == "SUN", "two stable frames must select the unique target");
        for (int i = 0; i < 8; i++) Check(flow.Observe(Sample(), 1 + i * .6) == null, "same step was clicked again");
        Check(flow.LastClicked == 1, "repeated step changed progress");
        Check(Stable(flow, Sample(2, "STAR"), 6)?.Word == "STAR", "next step not selected");
        Check(Stable(flow, Sample(3), 7.5)?.Word == "SUN", "step 3 not selected");
        Check(Stable(flow, Sample(4), 9)?.Word == "SUN", "step 4 not selected");
        Check(!flow.Complete && flow.Blocked, "last click alone must not complete the session");
        flow.Observe(Reading.Absent, 10); flow.Observe(Reading.Absent, 10.6);
        Check(!flow.Complete, "two absence frames must not complete");
        flow.Observe(Reading.Absent, 11.2);
        Check(flow.Complete && !flow.Blocked, "three absence frames must confirm completion");
        flow.Observe(Reading.Unknown("new uncertain panel"), 11.8);
        Check(flow.Blocked && !flow.Complete, "a new uncertain panel must block a completed session again");
        Check(Stable(flow, Sample(), 12)?.Word == "SUN" && flow.LastClicked == 1, "new session did not reset correctly");
        flow.Reset(); Stable(flow, Sample(), 0); flow.Observe(Sample(), 14);
        Check(flow.Faulted && flow.Blocked, "stalled step must fault without retry");
        Check(flow.Observe(Sample(2), 15) == null, "faulted session emitted input");
        flow.Reset(); Stable(flow, Sample(), 0); flow.Observe(Sample(3), 1);
        Check(flow.Faulted, "skipped step must fault");
        flow.Reset(); Stable(flow, Sample(), 0); flow.Observe(Reading.Absent, 1); flow.Observe(Reading.Absent, 1.6); flow.Observe(Reading.Absent, 2.2);
        Check(flow.Faulted && !flow.Complete, "early disappearance must not count as success");
        flow.Reset();
        Check(Stable(flow, Sample() with { Attempts = 0 }, 0) == null, "exhausted attempts must never click");
        Check(Stable(flow, Sample() with { Options = [new("SUN", new(1, 1)), new("SUN", new(2, 2)), new("STAR", new(3, 3)), new("LEAF", new(4, 4))] }, 2) == null, "ambiguous target must never click");
        Check(Stable(flow, Sample() with { Target = "MOON" }, 4) == null, "missing match must never click");
        flow.Reset(); flow.Observe(Sample(), 0); flow.Observe(Sample(1, "STAR"), .6);
        Check(flow.LastClicked == 0, "unstable target must not click");
        flow.Observe(Reading.Unknown("uncertain"), 1); Check(!flow.Complete && flow.Blocked, "uncertain reading must pause");
        string[] lines = ["Macro protection puzzle", "Target: SUN", "Step 1 of 4", "3 attempts - 90 seconds"];
        WordBox[] words = [new("CROWN", new(70, 330, 70, 18)), new("STAR", new(260, 330, 50, 18)), new("SUN", new(90, 390, 40, 18)), new("LEAF", new(260, 390, 50, 18))];
        Check(OcrReader.Parse(lines, words, new(408, 444)).Valid, "parser rejected valid grid");
        Check(OcrReader.Parse(lines.Select(s => s.Replace("Target", "Tarqet")).ToArray(), words, new(408, 444)).Valid, "known label font variant rejected");
        Check(!OcrReader.Parse(lines.Select(s => s.Replace("SUN", "SVM")).ToArray(), words, new(408, 444)).Valid, "symbol typo must not use fuzzy matching");
        Check(OcrReader.Parse(lines.Skip(1).ToArray(), words, new(408, 444)).Kind == ReadingKind.Uncertain, "missing header must not be absence");
        Check(OcrReader.Parse(["background"], [], new(408, 444)).Kind == ReadingKind.Absent, "background should be absent");
        Check(!OcrReader.Parse(lines, words.Take(3).ToArray(), new(408, 444)).Valid, "incomplete grid accepted");
        Check(!OcrReader.Parse(lines, words.Select(w => w with { Bounds = new(70, 330, 70, 18) }).ToArray(), new(408, 444)).Valid, "overlapping grid accepted");
        var reader = new OcrReader(); using var demo = new DemoForm();
        var readings = new List<Reading>(); var sequence = new[] { 2, 1, 1, 1 };
        for (int i = 0; i < 4; i++)
        {
            using var frame = demo.Snapshot(); frame.Save(Path.Combine(output, $"fixture-step-{i + 1}.png"));
            var reading = await reader.ReadAsync(frame).ConfigureAwait(false); readings.Add(reading);
            Check(reading.Valid && reading.Step == i + 1, $"OCR fixture step {i + 1} failed: {reading.Detail}");
            demo.Press(sequence[i]);
        }
        using (var closed = demo.Snapshot()) Check((await reader.ReadAsync(closed).ConfigureAwait(false)).Kind == ReadingKind.Absent, "completed fixture was not absent");
        Check(demo.Accepted == 4 && demo.Attempts == 3, "fixture sequence failed");
        File.WriteAllText(Path.Combine(output, "self-tests.json"), JsonSerializer.Serialize(new { pass = true, checks, language = reader.Language, live_game_accessed = false, readings }, new JsonSerializerOptions { WriteIndented = true }));
    }
    public static void RunWindow(string output)
    {
        Exception? failure = null;
        using var fixture = new DemoForm();
        fixture.Shown += async (_, _) =>
        {
            try
            {
                using var process = Process.GetCurrentProcess();
                var target = new WindowChoice(fixture.Handle, Environment.ProcessId, fixture.Text, process.MainModule!.FileName!, process.StartTime.ToUniversalTime().Ticks);
                var reader = new OcrReader(); var flow = new PuzzleFlow(); flow.Reset();
                Native.SetForegroundWindow(fixture.Handle); await Task.Delay(200);
                Check(!(target with { Created = target.Created + 1 }).IsReady(out _), "changed process identity accepted");
                Check(!target.IsReady(out _, new Size(fixture.ClientSize.Width + 1, fixture.ClientSize.Height)), "changed client geometry accepted");
                bool rejected = false;
                try { target.Click(new(-1, -1), fixture.PuzzleBounds, fixture.ClientSize); } catch (IOException) { rejected = true; }
                Check(rejected && fixture.Inputs == 0, "out-of-region click was sent");
                using (var cover = new Form { Text = "Yerel odak testi", ClientSize = new(150, 100), StartPosition = FormStartPosition.CenterScreen })
                {
                    cover.Show(); Native.SetForegroundWindow(cover.Handle); await Task.Delay(100);
                    Check(!target.IsReady(out _), "background target accepted");
                    cover.Close();
                }
                Native.SetForegroundWindow(fixture.Handle); await Task.Delay(100);
                fixture.WindowState = FormWindowState.Minimized; await Task.Delay(100);
                Check(!target.IsReady(out _), "minimized target accepted");
                fixture.WindowState = FormWindowState.Normal; fixture.Activate(); Native.SetForegroundWindow(fixture.Handle); await Task.Delay(500);
                for (int i = 1; i <= 4; i++)
                {
                    using var first = target.Capture(fixture.PuzzleBounds, fixture.ClientSize);
                    var a = await reader.ReadAsync(first);
                    Check(a.Valid && a.Step == i, "native capture OCR failed");
                    Check(flow.Observe(a, i * 2) == null, "first native frame emitted input");
                    using var second = target.Capture(fixture.PuzzleBounds, fixture.ClientSize);
                    var b = await reader.ReadAsync(second);
                    var choice = flow.Observe(b, i * 2 + .6);
                    if (choice == null)
                    {
                        File.WriteAllText(Path.Combine(output, "native-reading-debug.json"), JsonSerializer.Serialize(new { a, b, state = flow.Status }, new JsonSerializerOptions { WriteIndented = true }));
                    }
                    Check(choice != null, "stable native frames did not select");
                    target.Click(new(fixture.PuzzleBounds.X + choice!.Center.X, fixture.PuzzleBounds.Y + choice.Center.Y), fixture.PuzzleBounds, fixture.ClientSize);
                    await Task.Delay(160);
                    Check(fixture.Accepted == i, "posted click did not advance fixture");
                }
                for (int i = 0; i < 3; i++)
                {
                    using var frame = target.Capture(fixture.PuzzleBounds, fixture.ClientSize);
                    flow.Observe(await reader.ReadAsync(frame), 10 + i * .6);
                }
                Check(flow.Complete && !flow.Blocked, "native full sequence did not complete");
                Check(fixture.Inputs == 4 && fixture.Attempts == 3, "native sequence produced wrong or extra input");
                File.WriteAllText(Path.Combine(output, "window-tests.json"), JsonSerializer.Serialize(new { pass = true, checks, inputs = fixture.Inputs,
                    accepted = fixture.Accepted, target_pid = Environment.ProcessId, live_game_accessed = false, complete = flow.Complete }, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception e) { failure = e; }
            finally { fixture.Close(); }
        };
        Application.Run(fixture);
        if (failure != null) throw failure;
    }
}
