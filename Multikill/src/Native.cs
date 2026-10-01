using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace Multikill;

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
