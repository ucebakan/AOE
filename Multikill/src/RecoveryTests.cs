using System.Reflection.PortableExecutable;

namespace Multikill;

static class RecoveryTests
{
    internal static Signature Run(Action<bool, string> check, Action<Action, string> reject)
    {
        byte[] disk = File.ReadAllBytes(Profile.GamePath);
        using var pe = new PEReader(new MemoryStream(disk));
        int Raw(int rva) { var s = pe.PEHeaders.SectionHeaders.Single(s => rva >= s.VirtualAddress && rva < s.VirtualAddress + s.SizeOfRawData); return s.PointerToRawData + rva - s.VirtualAddress; }
        var baseline = BuildRecovery.Resolve(disk);
        check(baseline.PatchRva == Profile.PatchRva, "semantic fingerprint distinguishes type 2 from similar type 7 path");
        var changed = (byte[])disk.Clone(); changed[^1] ^= 1;
        var resolved = Profile.Resolve(changed);
        check(resolved.Sha256 != Profile.Known.Sha256 && resolved.PatchRva == Profile.PatchRva, "new SHA automatically resolves unchanged semantics");
        int old = Raw(Profile.Known.SignatureRva), movedRva = Profile.Known.SignatureRva - 0x100, moved = Raw(movedRva);
        var relocation = (byte[])disk.Clone(); byte[] body = Profile.Pattern;
        // Three rejection branches plus allocator call retain their absolute targets.
        foreach (int displacement in new[] { 18, 32, 56, 66 }) BitConverter.GetBytes(BitConverter.ToInt32(body, displacement) + 0x100).CopyTo(body, displacement);
        Array.Fill(relocation, (byte)0xCC, old, Profile.Pattern.Length); body.CopyTo(relocation, moved);
        var relocated = Profile.Resolve(relocation);
        check(relocated.SignatureRva == movedRva && relocated.PatchRva == movedRva + 16, "relocated function fragment resolves new RVA and branch displacement");
        byte[] modified = (byte[])relocation.Clone(); modified[moved + 12] = 0x50; // range field offset
        BitConverter.GetBytes(0x780).CopyTo(modified, moved + 24); // actor ID field
        check(Profile.Resolve(modified).SignatureRva == movedRva, "semantic recovery tolerates field displacement changes");
        modified = (byte[])disk.Clone(); modified[old + 78] = 7; reject(() => Profile.Resolve(modified), "type 7 impostor is rejected");
        modified = (byte[])disk.Clone(); modified[old + 16] = 0x90; reject(() => Profile.Resolve(modified), "changed range predicate is rejected");
        modified = (byte[])disk.Clone(); modified[old + 32] ^= 1; reject(() => Profile.Resolve(modified), "mismatched rejection control-flow target is rejected");
        modified = (byte[])disk.Clone(); body.CopyTo(modified, moved); reject(() => Profile.Resolve(modified), "two valid semantic candidates block recovery");
        modified = (byte[])disk.Clone(); Array.Fill(modified, (byte)0xCC, old, Profile.Pattern.Length); reject(() => Profile.Resolve(modified), "missing semantic candidate blocks recovery");
        byte[] nopImage = (byte[])relocation.Clone(); var withNop = body.Take(7).Concat(new byte[] { 0x90 }).Concat(body.Skip(7).Take(72)).ToArray();
        // Inserting before all relative instructions moves their IP by one.
        foreach (int displacement in new[] { 19, 33, 57, 67 }) BitConverter.GetBytes(BitConverter.ToInt32(withNop, displacement) - 1).CopyTo(withNop, displacement);
        withNop.CopyTo(nopImage, moved);
        var nopProfile = Profile.Resolve(nopImage);
        check(nopProfile.PatchOffset == 17, "NOP insertion resolves instruction boundary and new patch offset");
        var live = nopProfile.Pattern; Convert.FromHexString(nopProfile.Patched).CopyTo(live, nopProfile.PatchOffset);
        Profile.ValidateLive(live, true, nopProfile); live[0] ^= 1;
        reject(() => Profile.ValidateLive(live, true, nopProfile), "recovered profile still requires exact live bytes");
        return nopProfile;
    }
}
