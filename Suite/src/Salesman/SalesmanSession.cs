using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using PlayerXYZ;

namespace UnityTools.Salesman;

sealed record World(long Owner, long Player, long Session, long Shop, int Visible, ushort Context, long Cash);
sealed class Session : IDisposable
{
    internal readonly Build Profile;
    internal readonly int Pid;
    internal readonly long Created, Base;
    internal bool IsFixture { get; private set; }
    readonly nint handle;
    internal bool Alive => Native.WaitForSingleObject(handle, 0) == 0x102;
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int Layout(out ulong module, out uint rva);
    internal static Session Fixture(int pid, long creation, long module, bool readOnly = false)
    {
        string testRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "4UnityTools-tests")) + Path.DirectorySeparatorChar;
        if (!Path.GetFullPath(Program.DataRoot).StartsWith(testRoot, StringComparison.OrdinalIgnoreCase)) throw new IOException("Fixture yalnız test dizininde kullanılabilir.");
        using var process = Process.GetProcessById(pid);
        if (!string.Equals(process.MainModule?.FileName, Environment.ProcessPath, StringComparison.OrdinalIgnoreCase) || process.StartTime.ToUniversalTime().ToFileTimeUtc() != creation) throw new IOException("Yalnız bu aracın kendi fixture süreci kullanılabilir.");
        string dll = NativeModules.Paths["UnitySalesman.dll"];
        var modules = process.Modules.Cast<ProcessModule>().Where(m => string.Equals(m.FileName, dll, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (modules.Length != 1 || modules[0].BaseAddress.ToInt64() != module) throw new IOException("Fixture DLL kimliği değişmiş.");
        if (NativeModules.Function<Layout>("UnitySalesman.dll", "SalesmanFixtureLayout")(out _, out uint rva) != 1) throw new IOException("Fixture kod alanı bulunamadı.");
        var profile = new Build { Sha = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(dll))), CloseSite = (int)rva, CloseBytes = Convert.FromHexString("4533C0B20B488BCBE8B00000004533C0B20C488BCBE8A3000000") };
        return new Session(pid, creation, module, profile, readOnly) { IsFixture = true };
    }
    internal void FixtureProtectOnly()
    {
        if (!IsFixture) throw new IOException("Bu işlem yalnız ayrı fixture içindir.");
        ValidateClose(false); if (!Native.VirtualProtectEx(handle, (nint)(Base + Profile.CloseSite), 1, 0x40, out _)) throw Native.Error("Fixture protect");
    }
    internal static Session Connect(bool readOnly = true, int? expectedPid = null, long? expectedCreation = null, long? expectedBase = null, string? expectedSha = null)
    {
        var candidates = Process.GetProcessesByName("TClient");
        try
        {
            var selected = candidates.Where(p => expectedPid is null || p.Id == expectedPid).Where(p =>
            {
                try { return string.Equals(p.MainModule?.FileName, Multikill.Profile.GamePath, StringComparison.OrdinalIgnoreCase); } catch { return false; }
            }).ToArray();
            if (selected.Length != 1) throw new IOException("Tek bir doğrulanmış 4Unity oturumu gerekli.");
            using var binary = new Binary(Multikill.Profile.GamePath);
            var profile = Profiles.Load(binary.Data); var process = selected[0];
            long created = process.StartTime.ToUniversalTime().ToFileTimeUtc(), module = process.MainModule!.BaseAddress.ToInt64();
            if ((expectedCreation is long c && c != created) || (expectedBase is long b && b != module) || (expectedSha is string sha && sha != profile.Sha)) throw new IOException("Salesman oyun oturumu / SHA değişmiş.");
            var session = new Session(process.Id, created, module, profile, readOnly);
            try
            {
                if (!Native.GetProcessTimes(session.handle, out long liveCreated, out _, out _, out _) || liveCreated != created) throw new IOException("PID yeniden kullanılmış.");
                var path = new StringBuilder(32768); uint length = (uint)path.Capacity;
                if (!Native.QueryFullProcessImageName(session.handle, 0, path, ref length) || !string.Equals(path.ToString(), Multikill.Profile.GamePath, StringComparison.OrdinalIgnoreCase)) throw new IOException("Salesman hedef yolu uyuşmuyor.");
                if (!IsWow64Process2(session.handle, out ushort machine, out ushort native) || machine != 0 || native != 0x8664) throw new IOException("Native x64 oyun gerekli.");
                int pe = session.I32(module + 0x3c);
                if (!session.Read(module + pe, 4).SequenceEqual(new byte[] { 0x50, 0x45, 0, 0 }) || session.I32(module + pe + 8) != profile.Timestamp || session.I32(module + pe + 24 + 56) != profile.ImageSize) throw new IOException("Canlı PE kimliği uyuşmuyor.");
                return session;
            }
            catch { session.Dispose(); throw; }
        }
        finally { foreach (var p in candidates) p.Dispose(); }
    }
    Session(int pid, long created, long module, Build profile, bool readOnly)
    {
        Pid = pid; Created = created; Base = module; Profile = profile;
        handle = Native.OpenProcess(readOnly ? 0x100410u : 0x100438u, false, pid);
        if (handle == 0) throw Native.Error("Salesman OpenProcess");
    }
    internal byte[] Read(long address, int size)
    {
        if (!Alive || size < 1 || size > 4096) throw new IOException("Salesman oturumu kapandı veya okuma boyutu geçersiz.");
        var result = new byte[size]; if (!Native.ReadProcessMemory(handle, (nint)address, result, (nuint)size, out nuint n) || n != (nuint)size) throw Native.Error("Salesman read"); return result;
    }
    internal long Pointer(long address) => BitConverter.ToInt64(Read(address, 8));
    internal int I32(long address) => BitConverter.ToInt32(Read(address, 4));
    internal World Snapshot()
    {
        var p = Profile; long owner = Pointer(Base + p.Root);
        if (Pointer(owner) != Base + p.OwnerVtable) throw new IOException("Salesman owner doğrulanamadı.");
        long player = Pointer(owner + p.PlayerOffset), session = Pointer(owner + p.SessionOffset), shop = Pointer(owner + p.ShopOffset);
        if (player == 0 || session == 0 || Pointer(player) != Base + p.PlayerVtable || Pointer(shop) != Base + p.ShopVtable) throw new IOException("Oyun dünyasına gir; Salesman canlı yolu bekleniyor.");
        int visible = I32(shop + p.VisibleOffset); if (visible is not 0 and not 1) throw new IOException("Shop visibility alanı uyuşmuyor.");
        var result = new World(owner, player, session, shop, visible, BitConverter.ToUInt16(Read(shop + p.ContextOffset, 2)), Pointer(shop + p.CashOffset));
        if (Pointer(Base + p.Root) != owner || Pointer(owner + p.PlayerOffset) != player || Pointer(owner + p.SessionOffset) != session || Pointer(owner + p.ShopOffset) != shop) throw new IOException("Salesman dünya yolu okuma sırasında değişti."); return result;
    }
    internal void ValidateCode(bool owned)
    {
        if (!Read(Base + Profile.Sender, Profile.SenderBytes.Length).SequenceEqual(Profile.SenderBytes) || !Read(Base + Profile.Finder, Profile.FinderBytes.Length).SequenceEqual(Profile.FinderBytes)) throw new IOException("Salesman canlı sender / NPC kodu değişmiş.");
        ValidateClose(owned);
    }
    internal static byte[] Patched(Build p) { var bytes = p.CloseBytes.ToArray(); bytes[4] = 0x0c; return bytes; }
    internal void ValidateClose(bool owned)
    {
        var live = Read(Base + Profile.CloseSite, Profile.CloseBytes.Length);
        if (!live.SequenceEqual(Profile.CloseBytes) && !(owned && live.SequenceEqual(Patched(Profile)))) throw new IOException("Salesman kapatma kodunda sahip olunmayan değişiklik var.");
        if (Native.VirtualQueryEx(handle, (nint)(Base + Profile.CloseSite), out var page, (nuint)Marshal.SizeOf<MemoryInfo>()) == 0 || page.Type != 0x1000000 || page.AllocationBase != Base || page.State != 0x1000 || (page.Protect != 0x20 && !(owned && page.Protect == 0x40))) throw new IOException("Salesman patch kod sayfası doğrulanamadı.");
    }
    internal void SetClose(bool active)
    {
        ValidateClose(true); long address = Base + Profile.CloseSite + 4; byte value = active ? (byte)0x0c : (byte)0x0b;
        if (!Native.VirtualProtectEx(handle, (nint)address, 1, 0x40, out _)) throw Native.Error("Salesman protect");
        bool wrote = true, flushed = true;
        try
        {
            if (Read(address, 1)[0] != value)
            { wrote = Native.WriteProcessMemory(handle, (nint)address, [value], 1, out nuint n) && n == 1; flushed = Native.FlushInstructionCache(handle, (nint)address, 1); }
        }
        finally { if (!Native.VirtualProtectEx(handle, (nint)address, 1, 0x20, out _)) throw Native.Error("Salesman RX restore"); }
        if (!wrote || !flushed || Read(address, 1)[0] != value) throw new IOException("Salesman patch/geri alma doğrulanamadı.");
        ValidateClose(active); Log(active ? "PATCH_ACTIVE close_frame=12" : "RESTORE_VERIFIED");
    }
    internal void ValidateResource()
    {
        string archive = Path.Combine(Path.GetDirectoryName(Multikill.Profile.GamePath)!, "FileMerger.unity");
        if (DiskFingerprint.Read(archive) != Profiles.ArchiveSha) throw new IOException("Salesman kaynak paketi değişmiş; NPC kimliği yeniden analiz edilmeli.");
        long head = Pointer(Base + Profile.NpcRoot), node = Pointer(head + 8), candidate = head; int steps = 0;
        while (Read(node + 0x19, 1)[0] == 0)
        {
            if (++steps > 256) throw new IOException("NPC tree sınırı aşıldı.");
            if (BitConverter.ToUInt16(Read(node + 0x20, 2)) >= Profiles.Npc) { candidate = node; node = Pointer(node); } else node = Pointer(node + 0x10);
        }
        if (candidate == head || BitConverter.ToUInt16(Read(candidate + 0x20, 2)) != Profiles.Npc || Read(Pointer(candidate + 0x28) + Profile.NpcTypeOffset, 1)[0] != 2 || Pointer(Base + Profile.NpcRoot) != head) throw new IOException("Salesman NPC resource kaydı doğrulanamadı.");
    }
    internal void Log(string message) => Diagnostics.Write($"PID={Pid} created={Created} SHA={Profile.Sha} {message}");
    public void Dispose() => Native.CloseHandle(handle);
    [DllImport("kernel32.dll", SetLastError = true)] static extern bool IsWow64Process2(nint process, out ushort machine, out ushort native);
}

static class Diagnostics
{
    static readonly object sync = new();
    internal static void Write(string text)
    {
        lock (sync) { try { string dir = Path.Combine(Program.DataRoot, "Salesman", "logs"); Directory.CreateDirectory(dir); File.AppendAllText(Path.Combine(dir, "salesman.log"), $"{DateTimeOffset.Now:O} {text}\n"); } catch { } }
    }
}

[StructLayout(LayoutKind.Sequential, Pack = 8)]
struct OpenRequest
{
    public uint Version, Status, Error, Pid;
    public ulong Created, Base, Owner, Player, Session, Shop, Deadline;
    public uint Root, OwnerVtable, PlayerVtable, ShopVtable, PlayerOffset, SessionOffset, ShopOffset, CashOffset;
    public uint Sender, SenderSize, Finder, FinderSize, NpcTypeOffset;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 128)] public byte[] SenderBytes;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 128)] public byte[] FinderBytes;
    internal static OpenRequest From(Session s, World w)
    {
        var p = s.Profile; var sender = new byte[128]; var finder = new byte[128]; p.SenderBytes.CopyTo(sender, 0); p.FinderBytes.CopyTo(finder, 0);
        return new() { Version = 1, Pid = (uint)s.Pid, Created = (ulong)s.Created, Base = (ulong)s.Base, Owner = (ulong)w.Owner, Player = (ulong)w.Player, Session = (ulong)w.Session, Shop = (ulong)w.Shop, Deadline = GetTickCount64() + 5000,
            Root = (uint)p.Root, OwnerVtable = (uint)p.OwnerVtable, PlayerVtable = (uint)p.PlayerVtable, ShopVtable = (uint)p.ShopVtable, PlayerOffset = (uint)p.PlayerOffset, SessionOffset = (uint)p.SessionOffset, ShopOffset = (uint)p.ShopOffset, CashOffset = (uint)p.CashOffset,
            Sender = (uint)p.Sender, SenderSize = (uint)p.SenderBytes.Length, Finder = (uint)p.Finder, FinderSize = (uint)p.FinderBytes.Length, NpcTypeOffset = (uint)p.NpcTypeOffset, SenderBytes = sender, FinderBytes = finder };
    }
    [DllImport("kernel32.dll")] static extern ulong GetTickCount64();
}
