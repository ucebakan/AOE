using System.Text.Json;

namespace UnityMonsterList;

static class HomeAnalysis
{
    public static void Analyze(string directory)
    {
        var samples=new List<CaptureSample>();
        using(var reader=new StreamReader(new FileStream(Path.Combine(directory,"timeline.jsonl"),FileMode.Open,FileAccess.Read,FileShare.ReadWrite)))
        {string? line;while((line=reader.ReadLine()) is not null){var s=JsonSerializer.Deserialize<CaptureSample>(line);if(s?.Valid==true && s.Research?.ActorBytesBase64 is not null)samples.Add(s);}}
        if(samples.Count==0)return;
        if(samples.Any(s=>s.LinkedMemory is not null))LinkedAnalysis.Analyze(samples,directory);
        // Bound memory: evaluate each offset across samples without retaining raw blocks for the full capture.
        var firstRaw=Convert.FromBase64String(samples[0].Research!.ActorBytesBase64!);
        Position end=samples[^1].A!;
        var candidates=new Dictionary<int,(Position Initial,double MaxShift)>();
        for(int offset=0;offset<=0x1320-12;offset+=4)
        {
            if(offset<0x7C&&offset+12>0x70 || offset<0xBC&&offset+12>0xB0)continue;
            var p=Vector(firstRaw,offset);
            if(p is null || p==new Position(0,0,0))continue;
            // Search heuristic only; endpoint may not be Home. Not a radius estimate.
            if(MobCapture.Distance(p,end,true)<=10 && Math.Abs(p.Y-end.Y)<=10)candidates[offset]=(p,0);
        }
        var transitions=new List<object>();byte? previous=null;
        foreach(var sample in samples)
        {
            var r=sample.Research!;var raw=Convert.FromBase64String(r.ActorBytesBase64!);
            if(raw.Length!=0x1320)throw new IOException("Raw actor block size mismatch");
            foreach(int offset in candidates.Keys.ToArray())
            {
                var initial=candidates[offset];var p=Vector(raw,offset);double? shift=MobCapture.Distance(p,initial.Initial);
                if(shift is null || shift>0.05)candidates.Remove(offset);
                else candidates[offset]=(initial.Initial,Math.Max(initial.MaxShift,shift.Value));
            }
            if(r.ReturnFlagCandidate!=previous){transitions.Add(new{sample.Index,sample.ElapsedMs,raw_flag=r.ReturnFlagCandidate,r.GoalCandidate,r.AToGoalXZ});previous=r.ReturnFlagCandidate;}
        }
        var report=new
        {
            status="CANDIDATES_ONLY_NOT_HOME_PROOF",valid_samples=samples.Count,
            DIRECT_OBSERVATION=new{first_goal=samples[0].Research!.GoalCandidate,last_goal=samples[^1].Research!.GoalCandidate,return_flag_transitions=transitions,end_position=end,
                stable_near_endpoint_candidates=candidates.OrderBy(k=>k.Key).Select(k=>new{offset=$"0x{k.Key:X}",xyz=k.Value.Initial,max_3d_shift=k.Value.MaxShift,end_distance_xz=MobCapture.Distance(k.Value.Initial,end,true)})},
            INFERENCE=new[]{"+12D8 target and +12FC return flag are source-inferred roles, pending runtime correlation."},
            UNTESTED=new[]{"Persistent Home field","Repeat-cycle return-center agreement","Leash radius including 50","Server acceptance"},
            search_limits="Only 4-byte-aligned inline float XYZ within Actor+0..131F; excludes A/B overlap. Max 0.05 3D change, within 10 XZ and 10 Y of endpoint. Heuristic cutoffs are not gameplay radii. Empty result does not prove absence; pointer-owned, encoded, other-layout or other-region data not searched. Endpoint not assumed Home."
        };
        string temp=Path.Combine(directory,"home-analysis.pending.json");File.WriteAllText(temp,JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true}));File.Move(temp,Path.Combine(directory,"home-analysis.json"),true);
    }
    static Position? Vector(byte[] raw,int offset)
    {
        if(raw.Length!=0x1320)throw new IOException("Raw actor block size mismatch");
        var p=new Position(BitConverter.ToSingle(raw,offset),BitConverter.ToSingle(raw,offset+4),BitConverter.ToSingle(raw,offset+8));
        return float.IsFinite(p.X)&&float.IsFinite(p.Y)&&float.IsFinite(p.Z)&&Math.Abs(p.X)<=1e6&&Math.Abs(p.Y)<=1e6&&Math.Abs(p.Z)<=1e6?p:null;
    }
}
