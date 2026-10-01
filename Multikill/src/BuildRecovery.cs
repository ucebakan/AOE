using Iced.Intel;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;

namespace Multikill;

// Bounded semantic fingerprint of the proven type-2 candidate admission path.
// Relocation, branch displacements, register allocation and harmless NOPs may
// change; the dataflow, vector bound and descriptor type must still agree.
static class BuildRecovery
{
    static readonly Mnemonic[] Flow = [Mnemonic.Shufps, Mnemonic.Sqrtps, Mnemonic.Movss, Mnemonic.Comiss, Mnemonic.Jbe,
        Mnemonic.Mov, Mnemonic.Test, Mnemonic.Je, Mnemonic.Mov, Mnemonic.Sub, Mnemonic.Sar, Mnemonic.Cmp,
        Mnemonic.Jae, Mnemonic.Mov, Mnemonic.Call, Mnemonic.Mov, Mnemonic.Mov, Mnemonic.Mov];
    internal static Signature Resolve(byte[] image, int? candidateRva = null)
    {
        using var pe = new PEReader(new MemoryStream(image)); var h = pe.PEHeaders;
        if ((int)h.CoffHeader.Machine != 0x8664 || h.PEHeader is null || h.PEHeader.Magic != PEMagic.PE32Plus) throw new IOException("Recovery: x64 PE doğrulanamadı.");
        var code = h.SectionHeaders.Where(s => ((uint)s.SectionCharacteristics & 0x20000000) != 0).ToArray();
        var directory = h.PEHeader.ExceptionTableDirectory;
        if (directory.Size <= 0 || directory.Size % 12 != 0) throw new IOException("Recovery: x64 fonksiyon sınırları yok.");
        byte[] unwind = pe.GetSectionData(directory.RelativeVirtualAddress).GetContent(0, directory.Size).ToArray();
        var functions = new List<(uint Start, uint End)>();
        for (int p = 0; p < unwind.Length; p += 12) functions.Add((BitConverter.ToUInt32(unwind, p), BitConverter.ToUInt32(unwind, p + 4)));
        bool Executable(ulong address) => code.Any(s => address >= (ulong)s.VirtualAddress && address < (ulong)s.VirtualAddress + (ulong)Math.Min(s.VirtualSize, s.SizeOfRawData));
        var matches = new List<Signature>(); string sha = Convert.ToHexString(SHA256.HashData(image));
        foreach (var section in code)
        {
            int length = Math.Min(section.SizeOfRawData, section.VirtualSize);
            if (section.PointerToRawData < 0 || length < 0 || (long)section.PointerToRawData + length > image.Length) throw new IOException("Recovery: PE section sınırları geçersiz.");
            int first = candidateRva is int target ? target - section.VirtualAddress : 0;
            if (first < 0 || first + 4 >= length) continue;
            int limit = candidateRva.HasValue ? first + 1 : length - 4;
            for (int offset = first; offset < limit; offset++)
            {
                int raw = section.PointerToRawData + offset;
                // Masked AOB anchor: SHUFPS register, register, 0.
                if (image[raw] != 0x0F || image[raw + 1] != 0xC6 || image[raw + 3] != 0 || (image[raw + 2] & 0xC0) != 0xC0) continue;
                int rva = section.VirtualAddress + offset;
                byte[] window = image.AsSpan(raw, Math.Min(192, length - offset)).ToArray();
                var decoder = Decoder.Create(64, new ByteArrayCodeReader(window)); decoder.IP = (ulong)rva;
                var instructions = new List<Instruction>(); int nops = 0;
                while (instructions.Count < Flow.Length && decoder.IP < (ulong)rva + (ulong)window.Length)
                {
                    decoder.Decode(out var instruction);
                    if (instruction.Mnemonic == Mnemonic.Nop && ++nops <= 8) continue;
                    if (instruction.Mnemonic != Flow[instructions.Count]) break;
                    instructions.Add(instruction);
                }
                if (instructions.Count != Flow.Length || !Matches(instructions, Executable)) continue;
                if (!functions.Any(f => f.Start <= (uint)rva && instructions[^1].NextIP < f.End && instructions[4].NearBranchTarget >= f.Start && instructions[4].NearBranchTarget < f.End)) continue;
                var branch = instructions[4]; int patch = checked((int)branch.IP - rva), size = checked((int)instructions[^1].NextIP - rva);
                if (patch + 6 > size || window[patch] != 0x0F || window[patch + 1] != 0x86 || branch.Length != 6) continue;
                byte[] original = window.AsSpan(patch, 6).ToArray(), patched = (byte[])original.Clone(); patched[1] = 0x84;
                matches.Add(new(sha, h.PEHeader.SizeOfImage, h.CoffHeader.TimeDateStamp, rva, patch,
                    Convert.ToHexString(window.AsSpan(0, size)), Convert.ToHexString(original), Convert.ToHexString(patched)));
            }
        }
        if (matches.Count != 1) throw new IOException($"Patch Recovery: {matches.Count} doğrulanmış semantik aday bulundu. Tekil eşleşme yok; yeni analiz gerekiyor. İşlev açılmadı.");
        return matches[0];
    }
    static bool Reg(Instruction i, int op, Register r) => i.GetOpKind(op) == OpKind.Register && i.GetOpRegister(op) == r;
    static bool Mem(Instruction i, int op, Register b) => i.GetOpKind(op) == OpKind.Memory && i.MemoryBase == b && i.MemoryIndex == Register.None;
    static bool Immediate(Instruction i, int op, ulong value) => i.GetOpKind(op) is OpKind.Immediate8 or OpKind.Immediate8to32 or OpKind.Immediate8to64 or OpKind.Immediate32 or OpKind.Immediate32to64 or OpKind.Immediate64 && i.GetImmediate(op) == value;
    static bool Matches(List<Instruction> i, Func<ulong, bool> executable)
    {
        Register distanceSquared = i[0].Op0Register, distance = i[1].Op0Register, range = i[2].Op0Register;
        if (!Reg(i[0], 1, distanceSquared) || i[0].Immediate8 != 0 || !Reg(i[1], 1, distanceSquared) ||
            i[2].Op1Kind != OpKind.Memory || i[2].MemoryBase == Register.None || i[2].MemoryIndex != Register.None || i[2].MemoryDisplacement64 is 0 or > 0x10000 ||
            !Reg(i[3], 0, range) || !Reg(i[3], 1, distance)) return false;
        ulong reject = i[4].NearBranchTarget;
        if (!executable(reject) || reject <= i[^1].NextIP || reject - i[4].IP > 4096 || i[7].NearBranchTarget != reject || i[12].NearBranchTarget != reject) return false;
        Register id = i[5].Op0Register;
        if (i[5].Op0Kind != OpKind.Register || i[5].Op1Kind != OpKind.Memory || i[5].MemorySize != MemorySize.UInt32 || i[5].MemoryBase == Register.None || i[5].MemoryIndex != Register.None ||
            i[5].MemoryDisplacement64 is < 0x100 or > 0x10000 || !Reg(i[6], 0, id) || !Reg(i[6], 1, id)) return false;
        Register count = i[8].Op0Register;
        if (i[8].Op0Kind != OpKind.Register || !Mem(i[8], 1, Register.RSP) || !Reg(i[9], 0, count) || !Mem(i[9], 1, Register.RSP) ||
            i[8].MemoryDisplacement64 != i[9].MemoryDisplacement64 + 8 || !Reg(i[10], 0, count) || i[10].Immediate8 != 3 ||
            !Reg(i[11], 0, count) || !Immediate(i[11], 1, 0x40)) return false;
        // Windows x64 allocation argument, return value, then { uint id; byte type=2 }.
        return Reg(i[13], 0, Register.ECX) && Immediate(i[13], 1, 8) && i[14].Op0Kind == OpKind.NearBranch64 && executable(i[14].NearBranchTarget) &&
            i[15].Op0Kind == OpKind.Register && Reg(i[15], 1, Register.RAX) &&
            Mem(i[16], 0, Register.RAX) && i[16].MemorySize == MemorySize.UInt32 && i[16].MemoryDisplacement64 == 0 && Reg(i[16], 1, id) &&
            Mem(i[17], 0, Register.RAX) && i[17].MemorySize == MemorySize.UInt8 && i[17].MemoryDisplacement64 == 4 && i[17].Op1Kind == OpKind.Immediate8 && i[17].Immediate8 == 2;
    }
}
