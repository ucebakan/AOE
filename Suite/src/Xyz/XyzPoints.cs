using System.Text.Json;

namespace UnityTools.Xyz;

sealed record SavedPoint(string Name, float X, float Y, float Z)
{
    internal float[] Values => [X, Y, Z];
}
sealed record PointDocument(int Version, List<SavedPoint> Points);

// Lists contain coordinates only. Session pointers and build profiles stay in their existing stores.
static class PointStore
{
    internal static void Validate(IReadOnlyList<SavedPoint> points)
    {
        if (points.Count > 10000) throw new IOException("Bir listede en fazla 10.000 nokta olabilir.");
        if (points.Any(p => p is null || string.IsNullOrWhiteSpace(p.Name) || p.Name.Length > 80 || p.Values.Any(v => !float.IsFinite(v))))
            throw new IOException("Nokta adı veya X / Y / Z değerleri geçersiz.");
    }
    internal static List<SavedPoint> Load(string path)
    {
        if (new FileInfo(path).Length > 4 * 1024 * 1024) throw new IOException("Nokta listesi çok büyük.");
        var document = JsonSerializer.Deserialize<PointDocument>(File.ReadAllText(path)) ?? throw new IOException("Liste okunamadı.");
        if (document.Version != 1 || document.Points is null) throw new IOException("Desteklenmeyen nokta listesi.");
        Validate(document.Points); return document.Points;
    }
    internal static void Save(string path, IReadOnlyList<SavedPoint> points)
    {
        Validate(points);
        path = Path.GetFullPath(path); Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(new PointDocument(1, points.ToList()), new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temporary, path, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
