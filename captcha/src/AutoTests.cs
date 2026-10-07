using System.Text.Json;

namespace UnityPuzzleTest;

static class AutoTests
{
    public static async Task Run(string output, string[] files)
    {
        int checks = 0;
        void Check(bool value, string message) { checks++; if (!value) throw new IOException(message); }
        LineBox[] lines = [new("Macro protection puzzle", new(400, 110, 215, 19)), new("Target: SUN", new(458, 210, 96, 15)), new("Step 1 of 4", new(465, 270, 80, 15))];
        Check(!PuzzleLocator.Locate([], new(1600, 900)).HeaderSeen, "background falsely identified as a puzzle");
        Check(PuzzleLocator.Locate(lines, new(1600, 900)).Bounds.Contains(new Point(507, 490)), "known layout not located");
        Check(PuzzleLocator.Locate([lines[0]], new(1600, 900)).Bounds.IsEmpty, "header-only layout must wait");
        Check(PuzzleLocator.Locate([lines[0], lines[0], lines[1], lines[2]], new(1600, 900)).Bounds.IsEmpty, "ambiguous header must wait");
        Check(PuzzleLocator.Locate([lines[0], lines[1], lines[2] with { Bounds = new(465, 150, 80, 15) }], new(1600, 900)).Bounds.IsEmpty, "reversed target/step positions accepted");
        Check(!PuzzleLocator.Locate([lines[0], lines[1] with { Text = "Target•. STAR" }, lines[2]], new(1600, 900)).Bounds.IsEmpty, "native OCR label punctuation rejected");
        var memory = new byte[0x330];
        System.Buffers.Binary.BinaryPrimitives.WriteInt64LittleEndian(memory, 0x140DC94B8);
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(memory.AsSpan(0x19C), 1); memory[0x328] = 0xFA;
        Check(MemoryDetector.Parse(memory, 0x140DC94B8, 0x20000000).Active, "visible macro mode must be active");
        memory[0x328] = 3;
        Check(!MemoryDetector.Parse(memory, 0x140DC94B8, 0x20000000).Active, "other security mode must not be a puzzle");
        memory[0x328] = 0xFA; System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(memory.AsSpan(0x19C), 0);
        Check(!MemoryDetector.Parse(memory, 0x140DC94B8, 0x20000000).Active, "hidden macro mode must not be active");
        Check(!MemoryDetector.Parse(memory, 0x140DC94B0, 0x20000000).Valid, "wrong vtable accepted");
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(memory.AsSpan(0x19C), 2);
        Check(!MemoryDetector.Parse(memory, 0x140DC94B8, 0x20000000).Valid, "invalid visibility accepted");
        Check(!MemoryDetector.Parse(new byte[8], 0x140DC94B8, 0x20000000).Valid, "truncated memory fields accepted");
        var reader = new OcrReader(); var results = new List<object>();
        using var fixture = new DemoForm();
        using (var whole = new Bitmap(fixture.ClientSize.Width, fixture.ClientSize.Height))
        {
            using (var g = Graphics.FromImage(whole)) fixture.PaintScene(g);
            var found = await reader.LocateAsync(whole).ConfigureAwait(false);
            Check(!found.Bounds.IsEmpty, "full fixture not auto-located: " + found.Detail);
            using var panel = whole.Clone(found.Bounds, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            var r = await reader.ReadAsync(panel).ConfigureAwait(false);
            Check(r.Valid && r.Step == 1 && r.Target == "SUN", "automatically cropped fixture was not readable");
            results.Add(new { fixture = true, found, reading = r });
        }
        foreach (string path in files)
        {
            using var reference = new Bitmap(path);
            foreach (double factor in new[] { 1.0, 1.25, .85 })
            {
                using var whole = new Bitmap(1600, 900);
                var destination = new Rectangle(510, 180, (int)(reference.Width * factor), (int)(reference.Height * factor));
                using (var g = Graphics.FromImage(whole)) { g.Clear(Color.FromArgb(30, 42, 32)); g.DrawImage(reference, destination); }
                var found = await reader.LocateAsync(whole).ConfigureAwait(false);
                Check(!found.Bounds.IsEmpty, $"reference {Path.GetFileName(path)} factor {factor} not located: {found.Detail}");
                using var panel = whole.Clone(found.Bounds, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                var reading = await reader.ReadAsync(panel).ConfigureAwait(false);
                if (!reading.Valid)
                {
                    panel.Save(Path.Combine(output, "auto-failure.png"));
                    File.WriteAllText(Path.Combine(output, "auto-failure.json"), JsonSerializer.Serialize(new { path, factor, found, reading, text = reader.LastText }, new JsonSerializerOptions { WriteIndented = true }));
                }
                Check(reading.Valid, $"auto-cropped reference {Path.GetFileName(path)} factor {factor} not readable");
                results.Add(new { file = Path.GetFileName(path), factor, found, reading });
            }
        }
        File.WriteAllText(Path.Combine(output, "auto-tests.json"), JsonSerializer.Serialize(new { pass = true, checks, game_accessed = false, input_sent = false, results }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
