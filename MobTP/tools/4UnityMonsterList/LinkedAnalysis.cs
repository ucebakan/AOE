using System.Text.Json;

namespace UnityMonsterList;

static class LinkedAnalysis
{
    // Full-capture intersection: missing/rebound/unreadable links never count as constant.
    public static void Analyze(IReadOnlyList<CaptureSample> samples,string directory)
    {
        var final=samples.LastOrDefault()?.LearnedCenter;
        var center=final?.CompletedReturns>=2?final.Center:null;
        var candidates=new Dictionary<(int Source,ulong Address,ulong Allocation,int Offset),Position>();
        bool first=true;int covered=0,truncated=0;
        foreach(var sample in samples)
        {
            if(sample.LinkedMemory is not null)covered++;
            if(sample.LinkedMemory?.Truncated==true)truncated++;
            var current=new Dictionary<(int,ulong,ulong,int),Position>();
            if(center is not null)
            foreach(var block in sample.LinkedMemory?.Blocks??[])
            {
                if(block.Status!="STABLE_LINK_CANDIDATE_ONLY" || block.BytesBase64 is null)continue;
                var raw=Convert.FromBase64String(block.BytesBase64);
                if(raw.Length!=block.Length)continue;
                // Scan each byte alignment, but keep X/Y/Z as contiguous float32.
                for(int off=0;off<=raw.Length-12;off++)
                {
                    var p=new Position(BitConverter.ToSingle(raw,off),BitConverter.ToSingle(raw,off+4),BitConverter.ToSingle(raw,off+8));
                    if(!float.IsFinite(p.X)||!float.IsFinite(p.Y)||!float.IsFinite(p.Z))continue;
                    if(MobCapture.Distance(p,center)>0.5)continue;
                    current[(block.SourceOffset,block.Address,block.AllocationBase,off)]=p;
                }
            }
            if(first){candidates=current;first=false;}
            else foreach(var key in candidates.Keys.ToArray())
                if(!current.TryGetValue(key,out var p)||MobCapture.Distance(candidates[key],p)>0.05)candidates.Remove(key);
        }
        var report=new{status=center is null?"NEED_TWO_COMPLETED_RETURNS":"CANDIDATES_ONLY_NOT_HOME_PROOF",
            valid_samples=samples.Count,linked_samples=covered,truncated_samples=truncated,reference_center=center,
            coverage=new{samples_with_slot_accounting=samples.Count(s=>s.LinkedMemory?.Slots is not null),
                samples_with_all_eligible_slots_queried=samples.Count(s=>s.LinkedMemory is {Slots:not null} l && !l.Truncated && l.Queries==l.EligibleSlots && l.Slots.Length==l.EligibleSlots),
                scope="Every eligible aligned slot in Actor[0,1320); queried does not mean readable or owned. Target prefix only.",
                slots=samples.SelectMany(s=>s.LinkedMemory?.Slots??[]).GroupBy(s=>s.SourceOffset).OrderBy(g=>g.Key).Select(g=>new{source_offset=$"0x{g.Key:X}",eligible_samples=g.Count(),statuses=g.GroupBy(s=>s.Status).ToDictionary(k=>k.Key,k=>k.Count())})},
            candidates=candidates.Select(x=>new{source_offset=$"0x{x.Key.Source:X}",address=$"0x{x.Key.Address:X}",allocation_base=$"0x{x.Key.Allocation:X}",target_offset=$"0x{x.Key.Offset:X}",xyz=x.Value}),
            limits="First-level pointer candidates, first 0x200 bytes or region remainder only. Float32 contiguous XYZ, all byte alignments; within 0.5 of final twice-observed center and 0.05 of first value throughout every valid sample. Same source slot/address/allocation required. Tolerances are search filters, not leash. No ownership proof, no recursive/split/encoded search. Empty result is not absence proof."};
        string temp=Path.Combine(directory,"linked-home-analysis.pending.json");
        File.WriteAllText(temp,JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true}));
        File.Move(temp,Path.Combine(directory,"linked-home-analysis.json"),true);
    }
}
