using System.Drawing.Imaging;
using System.Text.RegularExpressions;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;

namespace UnityPuzzleTest;

record WordBox(string Text, RectangleF Bounds);
sealed class OcrReader
{
    readonly OcrEngine engine;
    public string Language => engine.RecognizerLanguage.LanguageTag;
    public string LastText { get; private set; } = "";
    public LineBox[] LastLines { get; private set; } = [];
    static readonly HashSet<string> Vocabulary = ["SUN", "STAR", "MOON", "LEAF", "CROWN", "GEM", "SHIELD", "SWORD"];
    internal const string TargetPattern = @"TAR[GQ]ET[^\p{L}\p{N}\r\n]*([A-Z]+)";
    public OcrReader()
    {
        engine = OcrEngine.TryCreateFromLanguage(new Language("en-US")) ??
            OcrEngine.TryCreateFromLanguage(new Language("tr-TR")) ?? OcrEngine.TryCreateFromUserProfileLanguages() ??
            throw new InvalidOperationException("Windows metin tanıma dili bulunamadı. Windows dil ayarlarından İngilizce veya Türkçe temel yazmayı ekleyin.");
    }
    public async Task<Reading> ReadAsync(Bitmap source)
    {
        using var image = Prepare(source);
        using var bytes = new MemoryStream(); image.Save(bytes, ImageFormat.Png);
        using var stream = new InMemoryRandomAccessStream();
        using (var writer = new DataWriter(stream.GetOutputStreamAt(0)))
        {
            writer.WriteBytes(bytes.ToArray()); await writer.StoreAsync().AsTask().ConfigureAwait(false);
            await writer.FlushAsync().AsTask().ConfigureAwait(false); writer.DetachStream();
        }
        stream.Seek(0);
        var decoder = await BitmapDecoder.CreateAsync(stream).AsTask().ConfigureAwait(false);
        using var software = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore).AsTask().ConfigureAwait(false);
        var result = await engine.RecognizeAsync(software).AsTask().ConfigureAwait(false);
        LastText = result.Text;
        float scale = image.Width / (float)source.Width;
        LastLines = result.Lines.Where(l => l.Words.Count > 0).Select(l =>
        {
            float left = (float)l.Words.Min(w => w.BoundingRect.X) / scale;
            float top = (float)l.Words.Min(w => w.BoundingRect.Y) / scale;
            float right = (float)l.Words.Max(w => w.BoundingRect.X + w.BoundingRect.Width) / scale;
            float bottom = (float)l.Words.Max(w => w.BoundingRect.Y + w.BoundingRect.Height) / scale;
            return new LineBox(l.Text, RectangleF.FromLTRB(left, top, right, bottom));
        }).ToArray();
        var words = result.Lines.SelectMany(l => l.Words).Select(w => new WordBox(w.Text,
            new((float)w.BoundingRect.X / scale, (float)w.BoundingRect.Y / scale,
                (float)w.BoundingRect.Width / scale, (float)w.BoundingRect.Height / scale))).ToArray();
        return Parse(result.Lines.Select(l => l.Text).ToArray(), words, source.Size);
    }
    public async Task<LocateResult> LocateAsync(Bitmap client)
    {
        await ReadAsync(client).ConfigureAwait(false);
        LocateResult result = PuzzleLocator.Locate(LastLines, client.Size);
        LastText = "";
        return result;
    }
    internal static Reading Parse(string[] lines, WordBox[] words, Size size)
    {
        string text = string.Join("\n", lines).ToUpperInvariant();
        // The label's stylized lowercase g is consistently read as q in the supplied frames.
        // Only this fixed label permits that variant; symbol names never use fuzzy matching.
        var targetMatch = Regex.Match(text, TargetPattern);
        var stepMatch = Regex.Match(text, @"STEP\s+([1-4])\s+OF\s+4\b");
        var attemptsMatch = Regex.Match(text, @"([0-9])\s+ATTEMPTS?\b");
        bool title = Regex.IsMatch(text, @"MACRO\s+PROTECTION\s+PUZZLE");
        string Normalize(string s) => Regex.Replace(s.ToUpperInvariant(), "[^A-Z]", "");
        var buttons = words.Where(w => w.Bounds.Top > size.Height * .64 && Vocabulary.Contains(Normalize(w.Text))).ToArray();
        if (!title && !targetMatch.Success && !stepMatch.Success && buttons.Length == 0) return Reading.Absent;
        if (!title || !targetMatch.Success || !stepMatch.Success || !attemptsMatch.Success || buttons.Length != 4)
            return Reading.Unknown("Başlık, hedef, adım veya dört seçenek birlikte okunamadı; tıklama yok");
        string target = targetMatch.Groups[1].Value;
        if (!Vocabulary.Contains(target)) return Reading.Unknown("Hedef tanınmadı; tıklama yok");
        int Slot(WordBox w) => (w.Bounds.Top + w.Bounds.Height / 2 > size.Height * .82 ? 2 : 0) + (w.Bounds.Left + w.Bounds.Width / 2 > size.Width / 2f ? 1 : 0);
        if (buttons.Select(Slot).Distinct().Count() != 4) return Reading.Unknown("2 × 2 seçenek düzeni doğrulanamadı; tıklama yok");
        var options = buttons.OrderBy(Slot).Select(w => new Option(Normalize(w.Text), new(
            (int)Math.Round(w.Bounds.Left + w.Bounds.Width / 2), (int)Math.Round(w.Bounds.Top + w.Bounds.Height / 2)))).ToArray();
        var r = new Reading(ReadingKind.Ready, target, int.Parse(stepMatch.Groups[1].Value), int.Parse(attemptsMatch.Groups[1].Value), options, "");
        return r.Valid ? r : Reading.Unknown("Tek bir kesin eşleşme veya kalan deneme doğrulanamadı; tıklama yok");
    }
    static Bitmap Prepare(Bitmap source)
    {
        double scale = Math.Min(2.5, Math.Min(OcrEngine.MaxImageDimension / (double)source.Width, OcrEngine.MaxImageDimension / (double)source.Height));
        var result = new Bitmap(Math.Max(1, (int)(source.Width * scale)), Math.Max(1, (int)(source.Height * scale)), PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(result);
        graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
        using var attributes = new ImageAttributes();
        attributes.SetColorMatrix(new ColorMatrix(new[] {
            new float[] { -.299f, -.299f, -.299f, 0, 0 }, new float[] { -.587f, -.587f, -.587f, 0, 0 },
            new float[] { -.114f, -.114f, -.114f, 0, 0 }, new float[] { 0, 0, 0, 1, 0 }, new float[] { 1, 1, 1, 0, 1 } }));
        graphics.DrawImage(source, new Rectangle(Point.Empty, result.Size), 0, 0, source.Width, source.Height, GraphicsUnit.Pixel, attributes);
        return result;
    }
}
