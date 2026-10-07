using System.Text.RegularExpressions;

namespace UnityPuzzleTest;

record LineBox(string Text, RectangleF Bounds);
record LocateResult(bool HeaderSeen, Rectangle Bounds, string Detail);

static class PuzzleLocator
{
    public static LocateResult Locate(LineBox[] lines, Size client)
    {
        static string Upper(string text) => text.ToUpperInvariant();
        var headers = lines.Where(l => Regex.IsMatch(Upper(l.Text), @"MACRO\s+PROTECTION\s+PUZZLE")).ToArray();
        if (headers.Length == 0) return new(false, Rectangle.Empty, "Bağlı · doğrulama kutusu bekleniyor");
        if (headers.Length != 1) return new(true, Rectangle.Empty, "Birden fazla kutu başlığı okundu; seçim bekletiliyor");
        var header = headers[0];
        float cx = header.Bounds.Left + header.Bounds.Width / 2;
        bool Aligned(LineBox l) => Math.Abs(l.Bounds.Left + l.Bounds.Width / 2 - cx) < header.Bounds.Width * .45;
        var targets = lines.Where(l => l.Bounds.Top > header.Bounds.Bottom && Aligned(l) &&
            Regex.IsMatch(Upper(l.Text), OcrReader.TargetPattern)).ToArray();
        var steps = lines.Where(l => l.Bounds.Top > header.Bounds.Bottom && Aligned(l) &&
            Regex.IsMatch(Upper(l.Text), @"STEP\s+[1-4]\s+OF\s+4\b")).ToArray();
        if (targets.Length != 1 || steps.Length != 1 || steps[0].Bounds.Top <= targets[0].Bounds.Bottom)
            return new(true, Rectangle.Empty, "Kutu başlığı bulundu; hedef ve adımın konumu henüz okunamadı");
        float targetCy = targets[0].Bounds.Top + targets[0].Bounds.Height / 2;
        float stepCy = steps[0].Bounds.Top + steps[0].Bounds.Height / 2;
        float gap = stepCy - targetCy;
        if (gap < 20 || gap > client.Height * .25) return new(true, Rectangle.Empty, "Kutu düzeni doğrulanamadı; seçim bekletiliyor");
        int height = (int)Math.Round(gap * 7.35), width = (int)Math.Round(height * .92);
        int top = (int)Math.Floor(header.Bounds.Top - header.Bounds.Height * .85);
        var candidate = new Rectangle((int)Math.Round(cx - width / 2f), top, width, height);
        var bounds = Rectangle.Intersect(candidate, new Rectangle(Point.Empty, client));
        if (bounds.Width < 200 || bounds.Height < 220 || bounds.Width < candidate.Width * .95 || bounds.Height < candidate.Height * .95)
            return new(true, Rectangle.Empty, "Kutunun tamamı görünür değil; oyun penceresini ekrana sığdırın");
        return new(true, bounds, "Doğrulama kutusu bulundu · seçenekler okunuyor");
    }
}
