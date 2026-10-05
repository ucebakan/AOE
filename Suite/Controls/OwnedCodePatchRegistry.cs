namespace UnityTools.Controls;

public sealed record OwnedCodePatchQuery(int Pid, long Created, long ModuleBase, string Sha,
    int Rva, byte[] Original, byte[] Patched);

// Process-local owners supply a fresh read-only proof. Nothing is cached across
// a game session, and a journal on disk alone is never sufficient ownership.
public static class OwnedCodePatchRegistry
{
    static readonly object sync = new();
    static readonly Dictionary<object, Func<OwnedCodePatchQuery, bool>> owners = new();
    public static void Register(object owner, Func<OwnedCodePatchQuery, bool> validate)
    { lock (sync) owners[owner] = validate; }
    public static void Remove(object owner) { lock (sync) owners.Remove(owner); }
    public static bool Validate(OwnedCodePatchQuery query)
    {
        Func<OwnedCodePatchQuery, bool>[] checks;
        lock (sync) checks = owners.Values.ToArray();
        int accepted = 0;
        foreach (var check in checks) { try { if (check(query)) accepted++; } catch { return false; } }
        return accepted == 1;
    }
}
