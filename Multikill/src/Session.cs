using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace Multikill;

sealed class Session : IPatchSession
{
    nint handle;
    public Signature Build { get; private set; } = Profile.Known;
    public int Pid { get; private set; }
    public long Created { get; private set; }
    public long Base { get; private set; }
    public bool Exited => Native.WaitForSingleObject(handle, 0) == 0;
    public static IPatchSession Connect(string? cacheDirectory = null, bool forceScan = false)
    {
        var build = Profile.Resolve(File.ReadAllBytes(Profile.GamePath), cacheDirectory, forceScan); var result = new Session { Build = build };
        try
        {
            var processes = Process.GetProcessesByName("TClient");
            try { if (processes.Length != 1) throw new IOException("Tek bir TClient oturumu gerekli."); result.Pid = processes[0].Id; }
            finally { foreach (var p in processes) p.Dispose(); }
            result.handle = Native.OpenProcess(0x101010, false, result.Pid);
            if (result.handle == 0) throw Native.Error("Salt okunur erişim");
            var path = new StringBuilder(32768); uint size = (uint)path.Capacity;
            if (!Native.QueryFullProcessImageName(result.handle, 0, path, ref size) || !string.Equals(path.ToString(), Profile.GamePath, StringComparison.OrdinalIgnoreCase)) throw new IOException("Oyun yolu doğrulanamadı.");
            if (!Native.GetProcessTimes(result.handle, out long created, out _, out _, out _)) throw Native.Error("Oturum kimliği"); result.Created = created;
            nint snapshot = Native.CreateToolhelp32Snapshot(0x18, (uint)result.Pid);
            if (snapshot == -1) throw Native.Error("Modül listesi");
            try
            {
                var e = new ModuleEntry { Size = (uint)Marshal.SizeOf<ModuleEntry>() }; int count = 0;
                if (Native.Module32First(snapshot, ref e)) do
                {
                    if (!string.Equals(e.Path, Profile.GamePath, StringComparison.OrdinalIgnoreCase)) continue;
                    if (e.BaseSize != result.Build.ImageSize) throw new IOException("Modül boyutu uyuşmuyor.");
                    result.Base = e.Base.ToInt64(); count++;
                } while (Native.Module32Next(snapshot, ref e));
                if (count != 1) throw new IOException("TClient modülü doğrulanamadı.");
            }
            finally { Native.CloseHandle(snapshot); }
            result.ValidateIdentity(); return result;
        }
        catch { result.Dispose(); throw; }
    }
    byte[] Read(long address, int count)
    {
        var bytes = new byte[count];
        if (!Native.ReadProcessMemory(handle, (nint)address, bytes, (nuint)count, out var got) || got != (nuint)count) throw Native.Error("Bellek okuma");
        return bytes;
    }
    public byte[] ReadSite() => Read(Base + Build.SignatureRva, Build.Pattern.Length);
    public void ValidateIdentity()
    {
        if (Exited || !Native.GetProcessTimes(handle, out long created, out _, out _, out _) || created != Created) throw new IOException("Oyun oturumu değişti.");
        var dos = Read(Base, 64); int pe = BitConverter.ToInt32(dos, 60);
        if (dos[0] != 'M' || dos[1] != 'Z' || pe < 64 || pe > 4096) throw new IOException("Canlı DOS başlığı uyuşmuyor.");
        var h = Read(Base + pe, 88);
        if (BitConverter.ToUInt32(h) != 0x4550 || BitConverter.ToUInt16(h, 4) != 0x8664 || BitConverter.ToInt32(h, 8) != Build.Timestamp || BitConverter.ToUInt16(h, 24) != 0x20B || BitConverter.ToInt32(h, 80) != Build.ImageSize) throw new IOException("Canlı PE profili uyuşmuyor.");
        Protection();
    }
    public uint Protection()
    {
        if (VirtualQueryEx(handle, (nint)(Base + Build.PatchRva), out var region, (nuint)Marshal.SizeOf<Region>()) == 0 || region.State != 0x1000 || region.Type != 0x1000000 || region.AllocationBase != (nint)Base || region.Protect is not (0x10 or 0x20 or 0x40 or 0x80)) throw new IOException("Patch adresi geçerli bir oyun kod bölgesi değil.");
        return region.Protect;
    }
    nint OpenWrite()
    {
        ValidateIdentity(); nint writer = Native.OpenProcess(0x1038, false, Pid);
        if (writer == 0) throw Native.Error("Yazma erişimi");
        if (!Native.GetProcessTimes(writer, out long created, out _, out _, out _) || created != Created) { Native.CloseHandle(writer); throw new IOException("Yazma oturumu değişti."); }
        return writer;
    }
    public void WriteOpcode(byte value, uint restoreProtection)
    {
        nint writer = OpenWrite(); nint address = (nint)(Base + Build.PatchRva + 1);
        try
        {
            // Only the single opcode byte changes. The six-byte instruction and all
            // surrounding bytes must match the validated opposite state before writing.
            Profile.ValidateLive(ReadSite(), value == 0x86, Build);
            if (!Native.VirtualProtectEx(writer, address, 1, 0x40, out _)) throw Native.Error("Kod koruması");
            try
            {
                if (!Native.WriteProcessMemory(writer, address, [value], 1, out var written) || written != 1) throw Native.Error("Opcode yazma");
                if (!Native.FlushInstructionCache(writer, address, 1)) throw Native.Error("Instruction cache");
            }
            finally { if (!Native.VirtualProtectEx(writer, address, 1, restoreProtection, out _)) throw Native.Error("Kod korumasını geri alma"); }
        }
        finally { Native.CloseHandle(writer); }
    }
    public void RestoreProtection(uint protection)
    {
        if (Protection() == protection) return;
        nint writer = OpenWrite();
        try { if (!Native.VirtualProtectEx(writer, (nint)(Base + Build.PatchRva), 6, protection, out _)) throw Native.Error("Sayfa korumasını kurtarma"); }
        finally { Native.CloseHandle(writer); }
    }
    public void Dispose() { if (handle != 0) Native.CloseHandle(handle); handle = 0; }
    [StructLayout(LayoutKind.Sequential)] struct Region { public nint BaseAddress, AllocationBase; public uint AllocationProtect; public ushort PartitionId; public nuint RegionSize; public uint State, Protect, Type; }
    [DllImport("kernel32.dll", SetLastError = true)] static extern nuint VirtualQueryEx(nint process, nint address, out Region region, nuint length);
}
