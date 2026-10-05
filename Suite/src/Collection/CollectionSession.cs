using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using UnityTools.Controls;

namespace UnityTools.Collection;

record Row(uint Id, long Actor, long Node, bool Dead);
record World(long Owner, long Player, long Session, long Head, uint PlayerId);
record DispatchResult(int Sent, int Skipped, bool Cancelled);
interface IEngine : IDisposable
{
    void Begin() { }
    string Identity { get; }
    List<Row> ReadWorld();
    DispatchResult Dispatch(IReadOnlyList<Row> rows);
    void Cancel();
    bool Cleanup();
}

sealed class NativeBridge
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int Send(nint window, nint batch);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int Control();
    readonly Send send = NativeModules.Function<Send>("UnityCollection.dll", "CollectBatch");
    readonly Control cancel = NativeModules.Function<Control>("UnityCollection.dll", "CancelCollection");
    readonly Control cleanup = NativeModules.Function<Control>("UnityCollection.dll", "CleanupCollection");
    readonly Control begin = NativeModules.Function<Control>("UnityCollection.dll", "BeginCollection");
    internal void Begin() { if (begin() != 1) throw new IOException("Collection önceki çağrı temizliği bekleniyor."); }
    internal void Cancel() => cancel();
    internal bool Cleanup() => cleanup() == 1;
    internal DispatchResult Dispatch(nint window, NativeBatch batch)
    {
        nint memory = Marshal.AllocHGlobal(Marshal.SizeOf<NativeBatch>());
        try
        {
            Marshal.StructureToPtr(batch, memory, false);
            int clean = send(window, memory); var result = Marshal.PtrToStructure<NativeBatch>(memory);
            int sent = result.Requests.Take((int)result.Count).Count(r => r.Status == 1);
            int skipped = result.Requests.Take((int)result.Count).Count(r => r.Status == 2);
            Diagnostics.Write($"BATCH count={result.Count} completed={result.Completed} sent={sent} skipped={skipped} status={result.Status} error={result.Error} clean={clean} distance_cap=none");
            if (clean != 1 || result.Status is not (1 or 5)) throw new IOException($"Collection çağrısı durdu (kod {result.Error}); kısmi/belirsiz gruba tekrar yok.");
            return new(sent, skipped, result.Status == 5);
        }
        finally { Marshal.FreeHGlobal(memory); }
    }
}

sealed class Session : IEngine
{
    internal readonly Build Profile;
    internal readonly int Pid;
    internal readonly long Created, Base;
    readonly nint process, window;
    readonly FileStream fileLock;
    readonly NativeBridge? bridge;
    World? world;
    public string Identity => $"PID {Pid} · SHA {Profile.Sha[..12]}";
    Session(int pid, long created, long module, Build profile, nint handle, nint window, FileStream fileLock, NativeBridge? bridge)
    { Pid = pid; Created = created; Base = module; Profile = profile; process = handle; this.window = window; this.fileLock = fileLock; this.bridge = bridge; }
    internal static Session Connect(bool force = false, NativeBridge? bridge = null)
    {
        var fileLock = new FileStream(Multikill.Profile.GamePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        nint handle = 0;
        try
        {
            var build = Profiles.Load(File.ReadAllBytes(Multikill.Profile.GamePath), force);
            var candidates = Process.GetProcessesByName("TClient");
            try
            {
                var matches = candidates.Where(p => { try { return string.Equals(p.MainModule?.FileName, Multikill.Profile.GamePath, StringComparison.OrdinalIgnoreCase); } catch { return false; } }).ToArray();
                if (matches.Length != 1) throw new IOException("Tek bir doğrulanmış 4Unity oyun oturumu gerekli.");
                var p = matches[0]; long created = p.StartTime.ToUniversalTime().ToFileTimeUtc(), module = p.MainModule!.BaseAddress.ToInt64();
                handle = Win.OpenProcess(0x100410, false, p.Id); if (handle == 0) throw new IOException("Collection oyun belleği okunamadı; yönetici onayı gerekli.");
                var path = new StringBuilder(32768); uint length = (uint)path.Capacity;
                if (!Win.GetProcessTimes(handle, out long live, out _, out _, out _) || live != created || !Win.QueryFullProcessImageName(handle, 0, path, ref length) || !string.Equals(path.ToString(), Multikill.Profile.GamePath, StringComparison.OrdinalIgnoreCase)) throw new IOException("Collection PID / dosya yolu değişmiş.");
                if (p.MainWindowHandle == 0 || NativeModules.GetWindowThreadProcessId(p.MainWindowHandle, out uint id) == 0 || id != p.Id) throw new IOException("Collection oyun penceresi hazır değil.");
                var s = new Session(p.Id, created, module, build, handle, p.MainWindowHandle, fileLock, bridge); handle = 0;
                try { s.ValidateCode(); s.ReadWorld(); return s; } catch { s.Dispose(); throw; }
            }
            finally { foreach (var p in candidates) p.Dispose(); }
        }
        catch { if (handle != 0) Win.CloseHandle(handle); fileLock.Dispose(); throw; }
    }
    byte[] Read(long address, int size)
    {
        if (size <= 0 || size > 4096 || Win.WaitForSingleObject(process, 0) != 0x102) throw new IOException("Collection oyun oturumu kapandı.");
        byte[] bytes = new byte[size];
        if (!Win.ReadProcessMemory(process, (nint)address, bytes, (nuint)size, out nuint got) || got != (nuint)size) throw new IOException("Collection canlı bellek okunamadı.");
        return bytes;
    }
    long Q(long a) => BitConverter.ToInt64(Read(a, 8));
    uint D(long a) => BitConverter.ToUInt32(Read(a, 4));
    byte B(long a) => Read(a, 1)[0];
    internal void ValidateCode()
    {
        var p = Profile; int pe = checked((int)D(Base + 0x3c));
        if (pe is < 0x40 or > 0x1000 || D(Base + pe) != 0x4550 || D(Base + pe + 8) != (uint)p.Timestamp || D(Base + pe + 80) != p.ImageSize) throw new IOException("Collection canlı PE kimliği değişmiş.");
        foreach (var c in p.Codes.Values) if (!Read(Base + c.Rva, c.Bytes.Length).SequenceEqual(c.Bytes)) throw new IOException("Collection canlı kod fingerprint değişmiş.");
    }
    internal World Snapshot()
    {
        var p = Profile; var m = p.Mob;
        long owner = Q(Base + m.RootRva);
        if (owner == 0 || Q(owner) != Base + m.Player.CtclientgameVtableRva) throw new IOException("Collection owner hazır değil.");
        long player = Q(owner + m.Player.OwnerToPlayerOffset), session = Q(owner + p.SessionOffset), head = Q(owner + m.RegistryOffset);
        if (player == 0 || session == 0 || head == 0 || Q(player) != Base + m.Player.CtclientcharVtableRva || B(player + m.ActorTypeOffset) != 1) throw new IOException("Collection oyun dünyası hazır değil.");
        uint playerId = D(player + m.ActorIdOffset);
        if (playerId == 0 || B(player + p.ActionOffset) is 6 or 7 || B(player + p.GhostOffset) != 0 || D(player + p.HealthOffset) == 0) throw new IOException("Collection: karakter ölü / ghost veya kimlik geçersiz.");
        long parent = Q(player + p.ParentOffset); if (parent != 0 && Q(parent + p.ReverseParentOffset) == player) throw new IOException("Collection: binek üzerinde bu doğrulanmış yol kullanılmaz.");
        ValidateBuffs(player);
        var next = new World(owner, player, session, head, playerId);
        if (world is not null && next != world) throw new IOException("Collection harita / karakter oturumu değişti; otomasyon durdu.");
        if (Q(Base + m.RootRva) != owner || Q(owner + m.Player.OwnerToPlayerOffset) != player || Q(owner + p.SessionOffset) != session || Q(owner + m.RegistryOffset) != head) throw new IOException("Collection dünya yolu okuma sırasında değişti.");
        return world ??= next;
    }
    void ValidateBuffs(long player)
    {
        long head = Q(player + Profile.MaintainOffset), count = Q(player + Profile.MaintainOffset + 8);
        if (head == 0 || count is < 0 or > 64 || B(head + 0x19) != 1) throw new IOException("Collection buff ağacı doğrulanamadı.");
        var stack = new Stack<long>(); stack.Push(Q(head + 8)); var seen = new HashSet<long>();
        while (stack.Count > 0)
        {
            long node = stack.Pop(); if (node == head) continue;
            if (node == 0 || !seen.Add(node) || seen.Count > count || B(node + 0x19) != 0) throw new IOException("Collection buff ağacı değişmiş.");
            long record = Q(node + 0x28); if (record == 0 || Q(record) != Base + Profile.MaintainVtable) throw new IOException("Collection maintain RTTI değişmiş.");
            long skill = Q(record + 0x20); if (skill == 0) throw new IOException("Collection skill pointer geçersiz.");
            if (BitConverter.ToUInt16(Read(skill, 2)) == 9909) throw new IOException("Collection 9909 eşya buff'ı aktif; önce süresinin bitmesini bekle.");
            stack.Push(Q(node)); stack.Push(Q(node + 0x10));
        }
        if (seen.Count != count || Q(player + Profile.MaintainOffset) != head || Q(player + Profile.MaintainOffset + 8) != count) throw new IOException("Collection buff ağacı okuma sırasında değişti.");
    }
    public List<Row> ReadWorld()
    {
        ValidateCode();
        // Registry updates may race the read-only observer. Retry a bounded
        // snapshot without dispatching; stable corruption still stops the tool.
        for (int attempt = 0; ; attempt++)
        {
            var current = Snapshot();
            try { return Registry.Read(Profile, Base, current, Read); }
            catch (IOException) when (attempt < 2) { Thread.Sleep(1); }
        }
    }
    public DispatchResult Dispatch(IReadOnlyList<Row> rows)
    {
        OperationGate.Check(); OperationGate.CheckContinuous(); ValidateCode(); var current = Snapshot();
        if (bridge is null) throw new IOException("Salt okunur Collection taraması istek gönderemez.");
        var batch = NativeBatch.From(this, current, rows);
        return bridge.Dispatch(window, batch);
    }
    public void Cancel() => bridge?.Cancel();
    public void Begin() => bridge?.Begin();
    public bool Cleanup() => bridge?.Cleanup() ?? true;
    public void Dispose() { Win.CloseHandle(process); fileLock.Dispose(); }
}

static class Registry
{
    internal static List<Row> Read(Build p, long module, World w, Func<long, int, byte[]> read)
    {
        long Q(long a) => BitConverter.ToInt64(read(a, 8)); uint D(long a) => BitConverter.ToUInt32(read(a, 4)); byte B(long a) => read(a, 1)[0];
        long count = Q(w.Owner + p.Mob.RegistryOffset + 8), root = Q(w.Head + 8);
        if (count is < 0 or > 4096 || B(w.Head + 0x19) != 1) throw new IOException("Collection registry count / sentinel değişmiş.");
        var stack = new Stack<(long Node, long Parent, long Low, long High)>(); stack.Push((root, w.Head, -1, (long)uint.MaxValue + 1));
        var seen = new HashSet<long>(); var result = new List<Row>();
        while (stack.Count > 0)
        {
            var (node, parent, low, high) = stack.Pop(); if (node == w.Head) continue;
            if (node == 0 || !seen.Add(node) || seen.Count > count || B(node + 0x19) != 0 || Q(node + 8) != parent) throw new IOException("Collection registry parent / cycle değişmiş.");
            uint id = D(node + 0x20); long actor = Q(node + 0x28);
            if (id == 0 || id <= low || id >= high || actor == 0 || Q(actor) != module + p.Mob.MonsterVtableRva || D(actor + p.Mob.ActorIdOffset) != id || B(actor + p.Mob.ActorTypeOffset) != 2) throw new IOException("Collection mob kimliği değişmiş.");
            byte action = B(actor + p.ActionOffset); result.Add(new(id, actor, node, action is 6 or 7));
            stack.Push((Q(node), node, low, id)); stack.Push((Q(node + 0x10), node, id, high));
        }
        if (seen.Count != count || Q(w.Head + 8) != root || Q(w.Owner + p.Mob.RegistryOffset) != w.Head || Q(w.Owner + p.Mob.RegistryOffset + 8) != count) throw new IOException("Collection mob listesi okuma sırasında değişti.");
        return result;
    }
}

static class Diagnostics
{
    internal static void Write(string message) { try { string dir = Path.Combine(Program.DataRoot, "Collection", "logs"); Directory.CreateDirectory(dir); lock (typeof(Diagnostics)) File.AppendAllText(Path.Combine(dir, "collection.log"), $"{DateTimeOffset.Now:O} {message}\n"); } catch { } }
}

static class Win
{
    [DllImport("kernel32.dll", SetLastError = true)] internal static extern nint OpenProcess(uint access, bool inherit, int pid);
    [DllImport("kernel32.dll")] internal static extern bool CloseHandle(nint handle);
    [DllImport("kernel32.dll")] internal static extern uint WaitForSingleObject(nint handle, uint timeout);
    [DllImport("kernel32.dll", SetLastError = true)] internal static extern bool ReadProcessMemory(nint process, nint address, byte[] bytes, nuint count, out nuint read);
    [DllImport("kernel32.dll")] internal static extern bool GetProcessTimes(nint process, out long created, out long exit, out long kernel, out long user);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] internal static extern bool QueryFullProcessImageName(nint process, uint flags, StringBuilder path, ref uint size);
    [DllImport("kernel32.dll")] internal static extern ulong GetTickCount64();
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
struct CodeGuard
{
    public uint Rva, Size;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 128)] public byte[] Bytes;
    internal static CodeGuard From(Code c) { byte[] data = new byte[128]; c.Bytes.CopyTo(data, 0); return new() { Rva = (uint)c.Rva, Size = (uint)c.Bytes.Length, Bytes = data }; }
}
[StructLayout(LayoutKind.Sequential, Pack = 4)]
struct NativeLayout
{
    public uint Version, ImageSize, Timestamp, Root, OwnerVtable, PlayerVtable, MonsterVtable, MaintainVtable;
    public uint PlayerOffset, SessionOffset, RegistryOffset, ActorIdOffset, ActorTypeOffset, ActionOffset, HealthOffset, DeadStateOffset, GhostOffset, ParentOffset, ReverseParentOffset, MaintainOffset, SkillOffset;
    public CodeGuard Sender, Lookup, Death, Maintain, Parent;
    internal static NativeLayout From(Build p) => new() { Version = 1, ImageSize = (uint)p.ImageSize, Timestamp = (uint)p.Timestamp, Root = (uint)p.Mob.RootRva, OwnerVtable = (uint)p.Mob.Player.CtclientgameVtableRva, PlayerVtable = (uint)p.Mob.Player.CtclientcharVtableRva, MonsterVtable = (uint)p.Mob.MonsterVtableRva, MaintainVtable = (uint)p.MaintainVtable, PlayerOffset = (uint)p.Mob.Player.OwnerToPlayerOffset, SessionOffset = (uint)p.SessionOffset, RegistryOffset = (uint)p.Mob.RegistryOffset, ActorIdOffset = (uint)p.Mob.ActorIdOffset, ActorTypeOffset = (uint)p.Mob.ActorTypeOffset, ActionOffset = (uint)p.ActionOffset, HealthOffset = (uint)p.HealthOffset, DeadStateOffset = (uint)p.DeadStateOffset, GhostOffset = (uint)p.GhostOffset, ParentOffset = (uint)p.ParentOffset, ReverseParentOffset = (uint)p.ReverseParentOffset, MaintainOffset = (uint)p.MaintainOffset, SkillOffset = 0x20, Sender = CodeGuard.From(p.Codes["sender"]), Lookup = CodeGuard.From(p.Codes["lookup"]), Death = CodeGuard.From(p.Codes["death"]), Maintain = CodeGuard.From(p.Codes["maintain"]), Parent = CodeGuard.From(p.Codes["parent"]) };
}
[StructLayout(LayoutKind.Sequential, Pack = 8)]
struct NativeRequest
{
    public uint Version, Status, Error, Pid;
    public ulong Created, Base, Owner, Player, Target, Session, Deadline;
    public uint TargetId, Mode;
    public ulong Head, Node;
}
[StructLayout(LayoutKind.Sequential, Pack = 8)]
struct NativeBatch
{
    public uint Version, Status, Error, Count, Completed, Cancel, HostPid, Reserved;
    public ulong HostCreated;
    public NativeLayout Layout;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)] public NativeRequest[] Requests;
    internal static NativeBatch From(Session s, World w, IReadOnlyList<Row> rows)
    {
        if (rows.Count is < 1 or > 64 || rows.Select(r => r.Id).Distinct().Count() != rows.Count) throw new IOException("Collection grup boyutu / kimliği geçersiz.");
        using var host = Process.GetCurrentProcess(); var result = new NativeBatch { Version = 1, Count = (uint)rows.Count, HostPid = (uint)host.Id, HostCreated = (ulong)host.StartTime.ToUniversalTime().ToFileTimeUtc(), Layout = NativeLayout.From(s.Profile), Requests = new NativeRequest[64] };
        for (int i = 0; i < rows.Count; i++) result.Requests[i] = new() { Version = 1, Pid = (uint)s.Pid, Created = (ulong)s.Created, Base = (ulong)s.Base, Owner = (ulong)w.Owner, Player = (ulong)w.Player, Target = (ulong)rows[i].Actor, Session = (ulong)w.Session, Deadline = Win.GetTickCount64() + 5000, TargetId = rows[i].Id, Mode = 1, Head = (ulong)w.Head, Node = (ulong)rows[i].Node };
        return result;
    }
}
