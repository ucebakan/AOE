using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace UnityTools.Salesman;

// File ID distinguishes replacement; ChangeTime also detects a content edit
// followed by restoring LastWriteTime. The read lease prevents concurrent
// writes/replacement while checking the stamp or calculating the SHA.
static class DiskFingerprint
{
    sealed record Stamp(ulong Volume, Guid Id, long Length, long Created, long Written, long Changed);
    sealed record Proof(Stamp Stamp, string Sha);
    static readonly Dictionary<string, Proof> cache = new(StringComparer.OrdinalIgnoreCase);
    static readonly object sync = new();
    internal static int HashCount { get; private set; }
    internal static string Read(string path, bool force = false)
    {
        path = Path.GetFullPath(path);
        lock (sync)
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 128 * 1024, FileOptions.SequentialScan);
            var before = Identify(stream);
            if (!force && before is not null && cache.TryGetValue(path, out var proof) && proof.Stamp == before) return proof.Sha;
            cache.Remove(path);
            string sha = Convert.ToHexString(SHA256.HashData(stream)); HashCount++;
            var after = Identify(stream);
            if (before != after) throw new IOException("Salesman kaynak dosyası doğrulama sırasında değişti.");
            if (after is not null) cache[path] = new(after, sha);
            return sha;
        }
    }
    static Stamp? Identify(FileStream stream)
    {
        if (!GetFileInformationByHandleEx(stream.SafeFileHandle, 0, out Basic b, (uint)Marshal.SizeOf<Basic>()) ||
            !GetFileInformationByHandleEx(stream.SafeFileHandle, 18, out FileId id, (uint)Marshal.SizeOf<FileId>())) return null;
        return new(id.Volume, id.Id, stream.Length, b.Created, b.Written, b.Changed);
    }
    [StructLayout(LayoutKind.Sequential)] struct Basic { internal long Created, Accessed, Written, Changed; internal uint Attributes; }
    [StructLayout(LayoutKind.Sequential)] struct FileId { internal ulong Volume; internal Guid Id; }
    [DllImport("kernel32.dll", SetLastError = true)] static extern bool GetFileInformationByHandleEx(SafeFileHandle handle, int type, out Basic information, uint size);
    [DllImport("kernel32.dll", SetLastError = true)] static extern bool GetFileInformationByHandleEx(SafeFileHandle handle, int type, out FileId information, uint size);
}
