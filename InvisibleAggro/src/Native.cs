using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace InvisibleAggro;

static class Native
{
    [DllImport("kernel32.dll", SetLastError = true)] public static extern IntPtr OpenProcess(uint access, bool inherit, int pid);
    [DllImport("kernel32.dll", SetLastError = true)] public static extern bool CloseHandle(IntPtr handle);
    [DllImport("kernel32.dll", SetLastError = true)] public static extern bool ReadProcessMemory(IntPtr process, IntPtr address, byte[] bytes, nuint size, out nuint read);
    [DllImport("kernel32.dll", SetLastError = true)] public static extern bool WriteProcessMemory(IntPtr process, IntPtr address, byte[] bytes, nuint size, out nuint written);
    [DllImport("kernel32.dll", SetLastError = true)] public static extern bool VirtualProtectEx(IntPtr process, IntPtr address, nuint size, uint protect, out uint old);
    [DllImport("kernel32.dll", SetLastError = true)] public static extern bool FlushInstructionCache(IntPtr process, IntPtr address, nuint size);
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)] public static extern bool QueryFullProcessImageName(IntPtr process, uint flags, StringBuilder path, ref uint size);
    [DllImport("kernel32.dll", SetLastError = true)] public static extern bool GetProcessTimes(IntPtr process, out long created, out long exit, out long kernel, out long user);
    [DllImport("kernel32.dll", SetLastError = true)] public static extern uint WaitForSingleObject(IntPtr handle, uint milliseconds);
    [DllImport("kernel32.dll", SetLastError = true)] public static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint pid);
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)] public static extern bool Module32First(IntPtr snapshot, ref ModuleEntry entry);
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)] public static extern bool Module32Next(IntPtr snapshot, ref ModuleEntry entry);
    [DllImport("kernel32.dll", SetLastError = true)] public static extern bool Thread32First(IntPtr snapshot, ref ThreadEntry entry);
    [DllImport("kernel32.dll", SetLastError = true)] public static extern bool Thread32Next(IntPtr snapshot, ref ThreadEntry entry);
    [DllImport("kernel32.dll", SetLastError = true)] public static extern IntPtr OpenThread(uint access, bool inherit, uint tid);
    [DllImport("kernel32.dll", SetLastError = true)] public static extern uint SuspendThread(IntPtr thread);
    [DllImport("kernel32.dll", SetLastError = true)] public static extern uint ResumeThread(IntPtr thread);
    [DllImport("kernel32.dll", SetLastError = true)] public static extern bool GetThreadContext(IntPtr thread, IntPtr context);
    [DllImport("kernel32.dll", SetLastError = true)] public static extern bool GetExitCodeThread(IntPtr thread, out uint code);
    [DllImport("kernel32.dll", SetLastError = true)] public static extern uint GetProcessIdOfThread(IntPtr thread);
    public static Exception Error(string operation) => new Win32Exception(Marshal.GetLastWin32Error(), operation + " başarısız (Win32 " + Marshal.GetLastWin32Error() + ").");
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
struct ModuleEntry
{
    public uint Size, ModuleId, Pid, GlobalUsage, ProcessUsage;
    public IntPtr Base;
    public uint BaseSize;
    public IntPtr Module;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string Name;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string Path;
}
[StructLayout(LayoutKind.Sequential)]
struct ThreadEntry { public uint Size, Usage, Id, Owner; public int BasePriority, DeltaPriority; public uint Flags; }

sealed class GameSession : IMemory, IDisposable
{
    public int Pid { get; private set; }
    public long Created { get; private set; }
    public long Base { get; private set; }
    public long Player { get; private set; }
    public uint PlayerId { get; private set; }
    public byte[] Disk { get; private set; } = [];
    IntPtr readHandle, writeHandle;
    byte[][] codeBaseline = [];
    readonly Dictionary<long, uint> pendingProtections = [];
    public bool Exited => readHandle == IntPtr.Zero || Native.WaitForSingleObject(readHandle, 0) == 0;

    public static GameSession Connect()
    {
        var session = new GameSession();
        try
        {
            session.Disk = Profile.VerifyDisk();
            var processes = Process.GetProcessesByName("TClient");
            try
            {
                if (processes.Length != 1) throw new InvalidOperationException(processes.Length == 0 ? "Oyun bekleniyor." : "Birden fazla TClient açık.");
                session.Pid = processes[0].Id;
            }
            finally { foreach (var p in processes) p.Dispose(); }
            session.readHandle = Native.OpenProcess(0x101010, false, session.Pid); // synchronize + limited query + read
            if (session.readHandle == IntPtr.Zero) throw Native.Error("Oyun belleğine erişim");
            var path = new StringBuilder(32768); uint length = (uint)path.Capacity;
            if (!Native.QueryFullProcessImageName(session.readHandle, 0, path, ref length)) throw Native.Error("Oyun yolu");
            if (!string.Equals(path.ToString(), Profile.GamePath, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Farklı oyun yolu.");
            if (!Native.GetProcessTimes(session.readHandle, out long created, out _, out _, out _)) throw Native.Error("Oturum kimliği");
            session.Created = created;
            IntPtr snapshot = Native.CreateToolhelp32Snapshot(0x18, (uint)session.Pid);
            if (snapshot == new IntPtr(-1)) throw Native.Error("Modül listesi");
            try
            {
                var e = new ModuleEntry { Size = (uint)Marshal.SizeOf<ModuleEntry>() };
                int matches = 0;
                if (Native.Module32First(snapshot, ref e)) do
                {
                    if (!string.Equals(e.Path, Profile.GamePath, StringComparison.OrdinalIgnoreCase)) continue;
                    if (e.BaseSize != Profile.ImageSize) throw new InvalidOperationException("Modül boyutu değişmiş.");
                    session.Base = e.Base.ToInt64(); matches++;
                } while (Native.Module32Next(snapshot, ref e));
                if (matches != 1) throw new InvalidOperationException("Oyun modülü doğrulanamadı.");
            }
            finally { Native.CloseHandle(snapshot); }
            using(var pe=new System.Reflection.PortableExecutable.PEReader(new MemoryStream(session.Disk)))
            {
                int nt=BitConverter.ToInt32(session.Read(session.Base+0x3C,4));
                if(nt<64||nt>4096||BitConverter.ToInt32(session.Read(session.Base+nt,4))!=0x4550||
                    BitConverter.ToInt32(session.Read(session.Base+nt+8,4))!=pe.PEHeaders.CoffHeader.TimeDateStamp)
                    throw new InvalidOperationException("Loaded game build differs from the recovered disk profile.");
            }
            long context = session.Pointer(session.Base + Profile.Context);
            session.Player = session.Pointer(context + Profile.Player);
            session.PlayerId = BitConverter.ToUInt32(session.Read(session.Player + Profile.ActorId, 4));
            session.codeBaseline = Profile.Signatures.Select(s => Profile.At(session.Disk, s.Rva, s.Mask.Length)).ToArray();
            session.ValidatePlayer();
            return session;
        }
        catch { session.Dispose(); throw; }
    }

    public long Pointer(long address)
    {
        long pointer = BitConverter.ToInt64(Read(address, 8));
        if (pointer < 0x10000 || pointer > 0x7FFFFFFFFFFF) throw new InvalidOperationException("Oyuncu henüz hazır değil.");
        return pointer;
    }
    public void ValidatePlayer()
    {
        if (Exited) throw new InvalidOperationException("Oyun oturumu kapandı.");
        long context = Pointer(Base + Profile.Context);
        if (Pointer(context + Profile.Player) != Player || Pointer(Player) != Base + Profile.Vtable ||
            Read(Player + Profile.ActorType, 1)[0] != 1 || PlayerId == 0 || BitConverter.ToUInt32(Read(Player + Profile.ActorId, 4)) != PlayerId)
            throw new InvalidOperationException("Oyuncu değişti veya doğrulanamadı.");
        long col = Pointer(Base + Profile.Vtable - 8);
        byte[] locator = Read(col, 24);
        if (BitConverter.ToUInt32(locator, 0) != 1 || Base + BitConverter.ToUInt32(locator, 20) != col)
            throw new InvalidOperationException("RTTI kaydı doğrulanamadı.");
        long descriptor = Base + BitConverter.ToUInt32(locator, 12);
        string name = Encoding.ASCII.GetString(Read(descriptor + 16, 32)).Split('\0')[0];
        if (name != ".?AVCTClientChar@@") throw new InvalidOperationException("CTClientChar doğrulanamadı.");
        byte[] coordinates = Read(Player + 0xB0, 16);
        for (int i = 0; i < 3; i++)
        {
            float value = BitConverter.ToSingle(coordinates, i * 4);
            if (!float.IsFinite(value) || Math.Abs(value) > 1_000_000) throw new InvalidOperationException("Oyuncu koordinatları geçersiz.");
        }
        float w = BitConverter.ToSingle(coordinates, 12);
        if (!float.IsFinite(w) || Math.Abs(w - 1) > 0.01) throw new InvalidOperationException("Oyuncu matrisi doğrulanamadı.");
    }
    public bool PlayerStillCurrent()
    {
        long context = BitConverter.ToInt64(Read(Base + Profile.Context, 8));
        if (context < 0x10000 || context > 0x7FFFFFFFFFFF) return false;
        long player = BitConverter.ToInt64(Read(context + Profile.Player, 8));
        if (player != Player) return false;
        return BitConverter.ToInt64(Read(Player, 8)) == Base + Profile.Vtable &&
            Read(Player + Profile.ActorType, 1)[0] == 1 && BitConverter.ToUInt32(Read(Player + Profile.ActorId, 4)) == PlayerId;
    }
    public void ValidateCode(IReadOnlyList<OwnedCell> owned)
    {
        int signatureIndex = 0;
        foreach (var sig in Profile.Signatures)
        {
            int count = sig.Mask.Length;
            byte[] bytes = Read(Base + sig.Rva, count);
            foreach (var c in owned.Where(c => c.Code))
                if (c.Address >= Base + sig.Rva && c.Address + c.Original.Length <= Base + sig.Rva + count)
                {
                    int index = checked((int)(c.Address - Base - sig.Rva));
                    if (!bytes.AsSpan(index, c.Expected.Length).SequenceEqual(c.Expected)) throw new InvalidOperationException("Aktif writer değişmiş.");
                    c.Original.CopyTo(bytes, index);
                }
            if (!bytes.SequenceEqual(codeBaseline[signatureIndex++])) throw new InvalidOperationException("Canlı kod, doğrulanan sürümle uyuşmuyor.");
        }
    }
    public byte[] Read(long address, int count)
    {
        byte[] bytes = new byte[count];
        if (!Native.ReadProcessMemory(readHandle, (IntPtr)address, bytes, (nuint)count, out nuint n) || n != (nuint)count)
            throw Native.Error("Bellek okuma");
        return bytes;
    }
    public IDisposable BeginWrite()
    {
        if (writeHandle != IntPtr.Zero) throw new InvalidOperationException("Yazma işlemi zaten açık.");
        writeHandle = Native.OpenProcess(0x1038, false, Pid);
        if (writeHandle == IntPtr.Zero) throw Native.Error("Yazma erişimi");
        if (!Native.GetProcessTimes(writeHandle, out long created, out _, out _, out _) || created != Created)
        {
            Native.CloseHandle(writeHandle); writeHandle = IntPtr.Zero;
            throw new InvalidOperationException("Oyun oturumu değişti.");
        }
        return new Cleanup(() => { Native.CloseHandle(writeHandle); writeHandle = IntPtr.Zero; });
    }
    public void Write(long address, byte[] bytes, bool code)
    {
        if (writeHandle == IntPtr.Zero) throw new InvalidOperationException("Yazma erişimi açık değil.");
        uint old = 0;
        if (code && !Native.VirtualProtectEx(writeHandle, (IntPtr)address, (nuint)bytes.Length, 0x40, out old)) throw Native.Error("Kod koruması");
        long page = address & ~0xFFFL;
        if (code && !pendingProtections.ContainsKey(page)) pendingProtections[page] = old;
        Exception? error = null;
        try
        {
            if (!Native.WriteProcessMemory(writeHandle, (IntPtr)address, bytes, (nuint)bytes.Length, out nuint n) || n != (nuint)bytes.Length)
                throw Native.Error("Bellek yazma");
            if (code && !Native.FlushInstructionCache(writeHandle, (IntPtr)address, (nuint)bytes.Length)) throw Native.Error("Instruction cache");
        }
        catch (Exception ex) { error = ex; }
        finally
        {
            if (code)
            {
                if (!Native.VirtualProtectEx(writeHandle, (IntPtr)address, (nuint)bytes.Length, pendingProtections[page], out _))
                    error = new IOException("Kod sayfası koruması geri alınamadı.", error);
                else pendingProtections.Remove(page);
            }
        }
        if (error is not null) throw error;
    }
    public void Dispose()
    {
        if (writeHandle != IntPtr.Zero) Native.CloseHandle(writeHandle);
        if (readHandle != IntPtr.Zero) Native.CloseHandle(readHandle);
        writeHandle = readHandle = IntPtr.Zero;
    }
}

sealed class Cleanup(Action action) : IDisposable { public void Dispose() => action(); }

sealed class PausedThreads : IDisposable
{
    readonly List<IntPtr> handles = [];
    public PausedThreads(int pid, long imageBase)
    {
        try
        {
            var seen = new HashSet<uint>();
            for (int pass = 0; pass < 3; pass++)
            {
                IntPtr snapshot = Native.CreateToolhelp32Snapshot(4, 0);
                if (snapshot == new IntPtr(-1)) throw Native.Error("Thread listesi");
                int added = 0;
                try
                {
                    var e = new ThreadEntry { Size = (uint)Marshal.SizeOf<ThreadEntry>() };
                    if (!Native.Thread32First(snapshot, ref e)) throw Native.Error("Thread listesi");
                    do
                    {
                        if (e.Owner != pid || seen.Contains(e.Id)) continue;
                        IntPtr h = Native.OpenThread(0x42 | 0x08, false, e.Id);
                        if (h == IntPtr.Zero) throw Native.Error("Thread erişimi");
                        bool suspended = false;
                        try
                        {
                            if (Native.GetProcessIdOfThread(h) != (uint)pid) throw new InvalidOperationException("Thread oturumu değişti.");
                            if (Native.SuspendThread(h) == uint.MaxValue) throw Native.Error("Thread duraklatma");
                            suspended = true;
                            handles.Add(h); seen.Add(e.Id); added++;
                            IntPtr raw = Marshal.AllocHGlobal(0x4D0 + 16);
                            try
                            {
                                var ctx = new IntPtr((raw.ToInt64() + 15) & ~15L);
                                Marshal.Copy(new byte[0x4D0], 0, ctx, 0x4D0);
                                Marshal.WriteInt32(ctx, 0x30, 0x100001);
                                if (!Native.GetThreadContext(h, ctx)) throw Native.Error("Thread instruction kontrolü");
                                long rip = Marshal.ReadInt64(ctx, 0xF8);
                                if ((rip > imageBase + Profile.Writer1 && rip < imageBase + Profile.Writer1 + 6) ||
                                    (rip > imageBase + Profile.Writer2 && rip < imageBase + Profile.Writer2 + 7))
                                    throw new InvalidOperationException("Writer çalışıyor; düğmeye tekrar basın.");
                            }
                            finally { Marshal.FreeHGlobal(raw); }
                        }
                        finally { if (!suspended) Native.CloseHandle(h); }
                    } while (Native.Thread32Next(snapshot, ref e));
                }
                finally { Native.CloseHandle(snapshot); }
                if (added == 0 && handles.Count > 0) return;
            }
            throw new InvalidOperationException("Thread listesi kararlı değil; işlem yapılmadı.");
        }
        catch { Dispose(); throw; }
    }
    public void Dispose()
    {
        bool failed = false;
        foreach (var h in handles.AsEnumerable().Reverse())
        {
            uint resumed = Native.ResumeThread(h);
            if (resumed == uint.MaxValue && (!Native.GetExitCodeThread(h, out uint code) || code == 259)) failed = true;
            Native.CloseHandle(h);
        }
        handles.Clear();
        if (failed) throw new IOException("Bir oyun thread'i devam ettirilemedi.");
    }
}
