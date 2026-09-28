using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace SpeedJump;

static class Native
{
    [DllImport("kernel32.dll", SetLastError = true)] public static extern IntPtr OpenProcess(uint access, bool inherit, int pid);
    [DllImport("kernel32.dll", SetLastError = true)] public static extern nuint VirtualQueryEx(IntPtr process, IntPtr address, out MemoryInfo info, nuint size);
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

[StructLayout(LayoutKind.Sequential)]
struct MemoryInfo
{
    public long BaseAddress, AllocationBase;
    public uint AllocationProtect, Alignment1;
    public ulong RegionSize;
    public uint State, Protect, Type, Alignment2;
}

sealed class PausedThreads : IDisposable
{
    readonly List<IntPtr> handles = [];
    public PausedThreads(int pid, long patchAddress)
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
                                if (rip > patchAddress && rip < patchAddress + 8)
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
