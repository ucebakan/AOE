using Iced.Intel;
using System.Reflection.PortableExecutable;

namespace UnityTools.Controls;

// Disk-only recovery helpers. All results are RVAs/operands, never session pointers.
public sealed class RecoveryImage : IDisposable
{
    readonly byte[] image;
    readonly PEReader pe;
    public long ImageBase => checked((long)pe.PEHeaders.PEHeader!.ImageBase);
    public RecoveryImage(byte[] image)
    {
        this.image=image; pe=new(new MemoryStream(image));
        if ((int)pe.PEHeaders.CoffHeader.Machine!=0x8664 || pe.PEHeaders.PEHeader?.Magic!=PEMagic.PE32Plus)
            throw new IOException("Recovery: x64 PE gerekli.");
    }
    public byte[] At(int rva,int count)
    {
        var s=pe.PEHeaders.SectionHeaders.Single(s=>rva>=s.VirtualAddress&&(long)rva+count<=(long)s.VirtualAddress+s.SizeOfRawData);
        return image.AsSpan(s.PointerToRawData+rva-s.VirtualAddress,count).ToArray();
    }
    public int I32(int rva)=>BitConverter.ToInt32(At(rva,4));
    public long I64(int rva)=>BitConverter.ToInt64(At(rva,8));
    public bool Executable(int rva)=>pe.PEHeaders.SectionHeaders.Any(s=>((uint)s.SectionCharacteristics&0x20000000)!=0&&rva>=s.VirtualAddress&&(long)rva<(long)s.VirtualAddress+Math.Min(s.VirtualSize,s.SizeOfRawData));
    public int[] Find(string pattern)
    {
        byte?[] p=pattern.Split(' ',StringSplitOptions.RemoveEmptyEntries).Select(t=>t=="??"?(byte?)null:Convert.ToByte(t,16)).ToArray();
        var hits=new List<int>();
        foreach(var s in pe.PEHeaders.SectionHeaders.Where(s=>((uint)s.SectionCharacteristics&0x20000000)!=0))
            for(int i=0;i<=Math.Min(s.SizeOfRawData,s.VirtualSize)-p.Length;i++)
            {
                bool ok=true;for(int k=0;k<p.Length;k++)if(p[k] is byte b&&image[s.PointerToRawData+i+k]!=b){ok=false;break;}
                if(ok)hits.Add(s.VirtualAddress+i);
            }
        return hits.ToArray();
    }
    public static int Unique(IEnumerable<int> values,string name)
    {var a=values.Distinct().ToArray();if(a.Length!=1)throw new IOException($"Patch Recovery / {name}: {a.Length} doğrulanmış aday; tekil eşleşme gerekli.");return a[0];}
    public int FindUnique(string pattern,string name)=>Unique(Find(pattern),name);
    public (int Start,int End) Function(int site)
    {
        var d=pe.PEHeaders.PEHeader!.ExceptionTableDirectory;
        if(d.Size==0||d.Size%12!=0)throw new IOException("Recovery: x64 fonksiyon sınırları eksik.");
        var data=At(d.RelativeVirtualAddress,d.Size);
        for(int i=0;i<data.Length;i+=12){int a=BitConverter.ToInt32(data,i),b=BitConverter.ToInt32(data,i+4);if(a<=site&&site<b)return(a,b);}
        throw new IOException("Recovery: fonksiyon sınırı bulunamadı.");
    }
    public Instruction[] Decode(int start,int length)
    {
        var dec=Decoder.Create(64,new ByteArrayCodeReader(At(start,length)));dec.IP=(ulong)start;
        var list=new List<Instruction>();while(dec.IP<(ulong)start+(ulong)length){dec.Decode(out var i);if(i.IsInvalid)throw new IOException("Recovery: geçersiz talimat.");list.Add(i);}
        return list.ToArray();
    }
    public Instruction[] FunctionInstructions(int site){var f=Function(site);return Decode(f.Start,f.End-f.Start);}
    public int MethodSlot(int table,int site)
    {
        int entry=FunctionRoot(site);var slots=new List<int>();
        for(int slot=0;slot<0x1000;slot+=8){long target=I64(table+slot)-ImageBase;if(target<0||target>int.MaxValue||!Executable((int)target))break;if(target==entry)slots.Add(slot);}
        return Unique(slots,"class method linkage");
    }
    public int Call(int site){var i=Decode(site,5).Single();if(i.Mnemonic!=Mnemonic.Call||i.Op0Kind!=OpKind.NearBranch64||!Executable((int)i.NearBranchTarget))throw new IOException("Recovery: call hedefi geçersiz.");return checked((int)i.NearBranchTarget);}
    public static bool Mem(Instruction i,int op,Register b)=>i.GetOpKind(op)==OpKind.Memory&&i.MemoryBase==b&&i.MemoryIndex==Register.None;
    public static bool Reg(Instruction i,int op,Register r)=>i.GetOpKind(op)==OpKind.Register&&i.GetOpRegister(op)==r;
    public static bool Field(ulong value,int alignment=1)=>value>=0x100&&value<=0x10000&&value%(uint)alignment==0;
    public int FunctionRoot(int site)
    {
        var d=pe.PEHeaders.PEHeader!.ExceptionTableDirectory;var data=At(d.RelativeVirtualAddress,d.Size);
        int entry=Function(site).Start, unwind=0;
        for(int p=0;p<data.Length;p+=12)if(BitConverter.ToInt32(data,p)==entry){unwind=BitConverter.ToInt32(data,p+8);break;}
        var seen=new HashSet<int>();
        while(unwind!=0)
        {
            if(!seen.Add(unwind)||seen.Count>32)throw new IOException("Recovery: geçersiz chained unwind.");
            byte[] h=At(unwind,4);if((h[0]>>3&4)==0)break;
            int chain=unwind+((4+h[2]*2+3)&~3);entry=I32(chain);unwind=I32(chain+8);
            if(!Executable(entry))throw new IOException("Recovery: unwind hedefi geçersiz.");
        }
        return entry;
    }
    public void Dispose()=>pe.Dispose();
}
