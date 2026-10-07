using System.Buffers.Binary;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace UnityPuzzleTest;

record MemoryState(bool Valid, bool Active, int Visible, int Mode, string Object, string Detail);

sealed class MemoryDetector : IDisposable
{
    internal const string BuildHash = "4DC9C526A895A10113C4CF23F2D199BFF283D2C7CE75F949DC49CEF15D27D622";
    internal const long TableRva = 0xDC94B8;
    readonly IntPtr handle;
    readonly WindowChoice window;
    readonly long moduleBase, moduleSize;
    long component;
    bool disposed;
    MemoryDetector(IntPtr handle, WindowChoice window, long moduleBase, long moduleSize)
    { this.handle = handle; this.window = window; this.moduleBase = moduleBase; this.moduleSize = moduleSize; }
    public static (MemoryDetector? Detector, string Detail) Connect(WindowChoice window, CancellationToken cancel)
    {
        if (!string.Equals(Path.GetFileName(window.Path), "TClient.exe", StringComparison.OrdinalIgnoreCase))
            return (null, "Bellek: bu pencere için istemci profili yok; görüntü izleniyor");
        if (Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(window.Path))) != BuildHash)
            return (null, "Bellek: istemci sürümü farklı; görüntü izleniyor");
        // Query-information + VM-read only. No VM-write, VM-operation, thread or debug access.
        IntPtr h = MemoryNative.OpenProcess(0x410, false, window.Pid);
        if (h == IntPtr.Zero) return (null, "Bellek: okuma erişimi açılamadı (Win32 " + Marshal.GetLastWin32Error() + "); görüntü izleniyor");
        MemoryDetector? detector = null;
        try
        {
            var path = new StringBuilder(32768); uint length = (uint)path.Capacity;
            if (!MemoryNative.QueryFullProcessImageName(h, 0, path, ref length) || !string.Equals(path.ToString(), window.Path, StringComparison.OrdinalIgnoreCase))
                throw new IOException("Bellek bağlantısında process yolu değişti");
            if (!MemoryNative.GetProcessTimes(h, out long created, out _, out _, out _) || DateTime.FromFileTimeUtc(created).Ticks != window.Created)
                throw new IOException("Bellek bağlantısında process oturumu değişti");
            using var process = Process.GetProcessById(window.Pid);
            var module = process.MainModule ?? throw new IOException("Ana modül okunamadı");
            if (!string.Equals(module.FileName, window.Path, StringComparison.OrdinalIgnoreCase)) throw new IOException("Ana modül yolu değişti");
            detector = new(h, window, module.BaseAddress.ToInt64(), module.ModuleMemorySize);
            if (detector.ReadPointer(detector.moduleBase + TableRva + 0x30) != detector.moduleBase + 0x384080 ||
                detector.ReadPointer(detector.moduleBase + TableRva + 0x160) != detector.moduleBase + 0x3839D0)
                throw new IOException("Bellekteki UI profil imzası farklı");
            detector.Find(cancel);
            if (detector.component == 0) { detector.Dispose(); return (null, "Bellek: UI nesnesi sınırlı taramada bulunamadı; görüntü izleniyor"); }
            return (detector, "Bellek: pencerenin açık/kapalı durumu izleniyor");
        }
        catch (OperationCanceledException) { if (detector != null) detector.Dispose(); else MemoryNative.CloseHandle(h); throw; }
        catch (Exception e) { if (detector != null) detector.Dispose(); else MemoryNative.CloseHandle(h); return (null, "Bellek: " + e.Message + "; görüntü izleniyor"); }
    }
    byte[] Read(long address, int length)
    {
        var result = new byte[length];
        if (!MemoryNative.ReadProcessMemory(handle, (IntPtr)address, result, (nuint)length, out var count) || count != (nuint)length)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "UI durumu okunamadı");
        return result;
    }
    long ReadPointer(long address) => BinaryPrimitives.ReadInt64LittleEndian(Read(address, 8));
    internal static MemoryState Parse(byte[] bytes, long expectedTable, long address)
    {
        if (bytes.Length < 0x330 || BinaryPrimitives.ReadInt64LittleEndian(bytes) != expectedTable)
            return new(false, false, -1, -1, "", "UI nesne kimliği doğrulanamadı");
        int visible = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(0x19C));
        int mode = bytes[0x328];
        if (visible is not (0 or 1) || (mode > 11 && mode != 0xFA))
            return new(false, false, visible, mode, "", "UI alanları beklenen aralıkta değil");
        bool active = visible == 1 && mode == 0xFA;
        return new(true, active, visible, mode, "0x" + address.ToString("X"), active ? "Bellek: doğrulama açık" : "Bellek: doğrulama kapalı");
    }
    bool Candidate(long address)
    {
        try
        {
            byte[] state = Read(address, 0x330);
            if (!Parse(state, moduleBase + TableRva, address).Valid) return false;
            long title = BinaryPrimitives.ReadInt64LittleEndian(state.AsSpan(0x288));
            if (title < 0x10000) return false;
            long childTable = ReadPointer(title);
            return childTable >= moduleBase && childTable < moduleBase + moduleSize;
        }
        catch { return false; }
    }
    void Find(CancellationToken cancel)
    {
        const long budget = 256L * 1024 * 1024;
        long address = 0, scanned = 0;
        var watch = Stopwatch.StartNew(); var found = new HashSet<long>();
        byte[] signature = BitConverter.GetBytes(moduleBase + TableRva);
        for (int regions = 0; regions < 12000 && scanned < budget && watch.Elapsed.TotalSeconds < 6; regions++)
        {
            cancel.ThrowIfCancellationRequested();
            if (MemoryNative.VirtualQueryEx(handle, (IntPtr)address, out var page, (nuint)Marshal.SizeOf<MemoryNative.Info>()) == 0) break;
            long end = page.BaseAddress + checked((long)page.RegionSize);
            if (end <= address) break;
            // UI instances are private committed allocations; skip images, mappings and inaccessible pages.
            if (page.State == 0x1000 && page.Type == 0x20000 && (page.Protect & 0x101) == 0 && (page.Protect & 0xEE) != 0)
            {
                for (long at = page.BaseAddress; at < end && scanned < budget && watch.Elapsed.TotalSeconds < 6;)
                {
                    cancel.ThrowIfCancellationRequested();
                    int length = (int)Math.Min(256 * 1024, end - at);
                    byte[] bytes;
                    try { bytes = Read(at, length); } catch { at += length; continue; }
                    scanned += length;
                    int from = 0;
                    while (from <= bytes.Length - 8)
                    {
                        int offset = bytes.AsSpan(from).IndexOf(signature);
                        if (offset < 0) break;
                        offset += from; long possible = at + offset;
                        if (possible % 8 == 0 && Candidate(possible)) found.Add(possible);
                        if (found.Count > 1) throw new IOException("Birden fazla UI nesnesi bulundu; bellek durumu belirsiz");
                        from = offset + 8;
                    }
                    at += length == 256 * 1024 ? length - 8 : length;
                }
            }
            address = end;
        }
        if (found.Count == 1) component = found.Single();
    }
    public MemoryState Snapshot()
    {
        if (disposed) return new(false, false, -1, -1, "", "Bellek izleme kapalı");
        try
        {
            if (!MemoryNative.GetProcessTimes(handle, out long created, out _, out _, out _) || DateTime.FromFileTimeUtc(created).Ticks != window.Created || !Native.IsWindow(window.Handle))
                return new(false, false, -1, -1, "", "Process oturumu değişti");
            if (!Candidate(component)) return new(false, false, -1, -1, "", "UI nesnesi değişti; görüntü izleniyor");
            return Parse(Read(component, 0x330), moduleBase + TableRva, component);
        }
        catch (Exception e) { return new(false, false, -1, -1, "", e.Message); }
    }
    public void Dispose() { if (disposed) return; disposed = true; MemoryNative.CloseHandle(handle); }
}

static class MemoryNative
{
    [StructLayout(LayoutKind.Sequential)] public struct Info
    {
        public long BaseAddress, AllocationBase;
        public uint AllocationProtect, Padding;
        public ulong RegionSize;
        public uint State, Protect, Type, Padding2;
    }
    [DllImport("kernel32.dll", SetLastError = true)] public static extern IntPtr OpenProcess(uint access, bool inherit, int pid);
    [DllImport("kernel32.dll", SetLastError = true)] public static extern bool CloseHandle(IntPtr handle);
    [DllImport("kernel32.dll", SetLastError = true)] public static extern bool ReadProcessMemory(IntPtr process, IntPtr address, byte[] bytes, nuint length, out nuint read);
    [DllImport("kernel32.dll", SetLastError = true)] public static extern nuint VirtualQueryEx(IntPtr process, IntPtr address, out Info info, nuint size);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] public static extern bool QueryFullProcessImageName(IntPtr process, uint flags, StringBuilder path, ref uint length);
    [DllImport("kernel32.dll", SetLastError = true)] public static extern bool GetProcessTimes(IntPtr process, out long created, out long exit, out long kernel, out long user);
}
