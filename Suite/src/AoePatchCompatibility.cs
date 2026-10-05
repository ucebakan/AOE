using System.Runtime.InteropServices;
using UnityTools.Controls;

namespace UnityTools;

static class AoePatchCompatibility
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    delegate int Validate(uint pid, ulong created, ulong moduleBase,
        [MarshalAs(UnmanagedType.LPStr)] string sha, ulong rva, nint original, nint patched, nuint count);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate void Configure(Validate callback);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    delegate int Probe(uint pid, ulong created, ulong moduleBase,[MarshalAs(UnmanagedType.LPStr)] string sha,
        ulong rva,[In] byte[] original,[In] byte[] patched,nuint count);
    static readonly Validate callback = Check;
    internal static void Initialize() => NativeModules.Function<Configure>("UnityAoe.dll", "SetOwnedPatchValidator")(callback);
    internal static bool TestProof(OwnedCodePatchQuery query) => NativeModules.Function<Probe>("UnityAoe.dll", "CheckOwnedMultikill")(
        (uint)query.Pid,(ulong)query.Created,(ulong)query.ModuleBase,query.Sha,(ulong)query.Rva,query.Original,query.Patched,6)==1;
    static int Check(uint pid, ulong created, ulong moduleBase, string sha, ulong rva, nint original, nint patched, nuint count)
    {
        try
        {
            if (count != 6 || original == 0 || patched == 0 || pid > int.MaxValue || created > long.MaxValue || moduleBase > long.MaxValue || rva > int.MaxValue) return 0;
            byte[] before = new byte[6], after = new byte[6]; Marshal.Copy(original, before, 0, 6); Marshal.Copy(patched, after, 0, 6);
            return OwnedCodePatchRegistry.Validate(new((int)pid, (long)created, (long)moduleBase, sha, (int)rva, before, after)) ? 1 : 0;
        }
        catch { return 0; } // Exceptions must never cross the native callback boundary.
    }
}
