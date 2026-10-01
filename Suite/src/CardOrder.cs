using System.Text.Json;

namespace UnityTools;

sealed class CardOrder(string path)
{
    internal int[] Load(IEnumerable<int> defaults)
    {
        var ids = defaults.ToArray();
        try { var saved = JsonSerializer.Deserialize<int[]>(File.ReadAllText(path)) ?? []; return saved.Where(ids.Contains).Distinct().Concat(ids.Where(id => !saved.Contains(id))).ToArray(); }
        catch { return ids; }
    }
    internal void Save(IEnumerable<int> ids)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(ids.ToArray())); File.Move(path + ".tmp", path, true);
    }
}
