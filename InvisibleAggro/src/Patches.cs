namespace InvisibleAggro;

enum StateMode { Off, Invisible, Aggro }
interface IMemory
{
    byte[] Read(long address, int count);
    void Write(long address, byte[] bytes, bool code);
}
record Cell(long Address, byte[] Bytes, bool Code);
record OwnedCell(long Address, byte[] Original, byte[] Expected, bool Code);

// Caller holds the process threads stopped for the entire preflight/commit/rollback.
// A write may have partially succeeded even when its API reports failure.
sealed class PatchSet(IMemory memory)
{
    public List<OwnedCell> Owned { get; private set; } = [];
    public Action<List<OwnedCell>> SaveJournal { get; set; } = _ => { };
    public void Import(IEnumerable<OwnedCell> cells) => Owned = cells.ToList();

    public void Apply(IEnumerable<Cell> desired, bool restoring = false)
    {
        var requested = desired.ToDictionary(c => c.Address);
        var previous = Owned.ToList();
        var future = new List<OwnedCell>();
        var operations = new List<(Cell Target, byte[] Before)>();
        foreach (long address in Owned.Select(c => c.Address).Union(requested.Keys))
        {
            var old = Owned.SingleOrDefault(c => c.Address == address);
            requested.TryGetValue(address, out var target);
            int count = old?.Original.Length ?? target!.Bytes.Length;
            byte[] current = memory.Read(address, count);
            if (old is not null && !current.SequenceEqual(old.Expected))
            {
                if (current.SequenceEqual(old.Original))
                {
                    // Another natural writer may already have restored the value.
                }
                else if (restoring && !old.Code)
                {
                    // Do not overwrite a state field that the game has since changed.
                    continue;
                }
                else throw new InvalidOperationException($"Beklenmeyen değişiklik: 0x{address:X}; işlem yapılmadı.");
            }
            byte[] original = old?.Original ?? current;
            byte[] next = target?.Bytes ?? original;
            bool code = old?.Code ?? target!.Code;
            if (next.Length != count) throw new InvalidOperationException("Patch uzunluğu uyuşmuyor.");
            if (!next.SequenceEqual(original)) future.Add(new(address, original, next, code));
            if (!current.SequenceEqual(next)) operations.Add((new(address, next, code), current));
        }
        // Journal covers both previous ownership and the planned new writes before mutation.
        var recovery = previous.ToDictionary(c => c.Address);
        foreach (var c in future) recovery[c.Address] = c;
        SaveJournal(recovery.Values.ToList());
        var attempted = new List<(Cell Target, byte[] Before)>();
        try
        {
            foreach (var op in operations)
            {
                attempted.Add(op);
                memory.Write(op.Target.Address, op.Target.Bytes, op.Target.Code);
                if (!memory.Read(op.Target.Address, op.Target.Bytes.Length).SequenceEqual(op.Target.Bytes))
                    throw new IOException("Yazma doğrulaması başarısız.");
            }
            SaveJournal(future);
            Owned = future;
        }
        catch (Exception error)
        {
            var rollbackErrors = new List<string>();
            foreach (var op in attempted.AsEnumerable().Reverse())
                try
                {
                    memory.Write(op.Target.Address, op.Before, op.Target.Code);
                    if (!memory.Read(op.Target.Address, op.Before.Length).SequenceEqual(op.Before))
                        throw new IOException("Geri alma doğrulanamadı.");
                }
                catch (Exception ex) { rollbackErrors.Add(ex.Message); }
            if (rollbackErrors.Count == 0)
            {
                Owned = previous;
                SaveJournal(previous);
                throw new IOException(error.Message + " Değişiklikler geri alındı.", error);
            }
            Owned = recovery.Values.ToList();
            throw new IOException(error.Message + " Geri alma eksik: " + string.Join("; ", rollbackErrors), error);
        }
    }
}
