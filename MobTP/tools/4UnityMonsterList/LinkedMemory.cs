using System.Runtime.InteropServices;

namespace UnityMonsterList;

record LinkedBlock(int SourceOffset, ulong Address, ulong AllocationBase, int Length, string? BytesBase64, string Status);
record LinkedSlot(int SourceOffset, ulong Address, string Status);
record LinkedSnapshot(LinkedBlock[] Blocks, int Queries, int EligibleSlots, bool Truncated, LinkedSlot[]? Slots=null);
record ReadableRegion(ulong Base, ulong Size, ulong AllocationBase, uint State, uint Protect, uint Type);

// First-level pointer candidates only. Readability and a stable pointer do not prove ownership.
static class LinkedMemory
{
    // The object contains exactly 612 aligned qwords. Cover every eligible slot on
    // every sample so the full-timeline stability analysis also covers late fields.
    public const int ActorSize=0x1320, PrefixSize=0x200, MaxQueries=ActorSize/8, MaxBlocks=ActorSize/8;
    public static LinkedSnapshot Capture(byte[] actorBytes,ulong actor,Func<ulong,int,byte[]> read,Func<ulong,ReadableRegion?> query)
    {
        if(actorBytes.Length!=ActorSize)throw new IOException("Actor size mismatch");
        var blocks=new List<LinkedBlock>();var slots=new List<LinkedSlot>();int queries=0,eligible=0;bool truncated=false;
        for(int off=0;off<=ActorSize-8;off+=8)
        {
            ulong address=BitConverter.ToUInt64(actorBytes,off);
            if(!Rpm.P(address) || address>=actor && address<actor+ActorSize)continue;
            eligible++;
            if(queries>=MaxQueries || blocks.Count>=MaxBlocks){truncated=true;slots.Add(new(off,address,"BUDGET_SKIPPED"));continue;}
            queries++;
            ReadableRegion? region=query(address);
            if(region is null || !Readable(region,address)){slots.Add(new(off,address,region is null?"QUERY_FAILED":"REGION_FILTERED"));continue;}
            int length=(int)Math.Min((ulong)PrefixSize,region.Size-(address-region.Base));
            if(length<12){slots.Add(new(off,address,"REGION_REMAINDER_TOO_SHORT"));continue;}
            string? raw=null;string status="UNREADABLE";
            try
            {
                if(BitConverter.ToUInt64(read(actor+(uint)off,8))!=address)status="LINK_CHANGED_BEFORE";
                else
                {
                    byte[] bytes=read(address,length);
                    var after=query(address);
                    bool sameRegion=after==region;
                    bool sameLink=BitConverter.ToUInt64(read(actor+(uint)off,8))==address;
                    status=!sameLink?"LINK_CHANGED_AFTER":!sameRegion?"REGION_CHANGED":"STABLE_LINK_CANDIDATE_ONLY";
                    if(bytes.Length!=length)status="SHORT_READ";
                    else if(sameLink && sameRegion)raw=Convert.ToBase64String(bytes);
                }
            }
            catch(IOException){status="UNREADABLE";}
            blocks.Add(new(off,address,region.AllocationBase,length,raw,status));
            slots.Add(new(off,address,status));
        }
        return new(blocks.ToArray(),queries,eligible,truncated,slots.ToArray());
    }
    internal static bool Readable(ReadableRegion r,ulong p)=>r.State==0x1000 && r.Type==0x20000 &&
        (r.Protect&0x100)==0 && (r.Protect&0xFF) is 0x02 or 0x04 or 0x08 &&
        p>=r.Base && p-r.Base<r.Size;
    public static ReadableRegion? Query(IntPtr handle,ulong address)
    {
        if(VirtualQueryEx(handle,(IntPtr)(long)address,out var r,(nuint)Marshal.SizeOf<MemoryInfo>())==0)return null;
        return new(r.BaseAddress,r.RegionSize,r.AllocationBase,r.State,r.Protect,r.Type);
    }
    [StructLayout(LayoutKind.Sequential)]
    struct MemoryInfo
    {
        public ulong BaseAddress,AllocationBase;
        public uint AllocationProtect;
        public ushort PartitionId;
        public ulong RegionSize;
        public uint State,Protect,Type;
    }
    [DllImport("kernel32.dll",SetLastError=true)]
    static extern nuint VirtualQueryEx(IntPtr process,IntPtr address,out MemoryInfo buffer,nuint length);
}
