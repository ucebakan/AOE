using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace UnityMonsterList;

record Identity(ulong Context, ulong Head, ulong Node, uint Key, ulong Actor, uint ActorId, byte Type, ulong Vtable, string Rtti);
record IdentityCheck(bool Membership, Identity? Identity, string Status);
record EventMark(DateTimeOffset Timestamp, double ElapsedMs, string ObservedEvent, string Notes);
record Candidate(int Offset, Position? XYZ, string Status, double? DistanceA3D, double? DistanceB3D, double? DistanceAXZ, double? DistanceBXZ);
record ResearchState(Position? GoalCandidate, byte? ReturnFlagCandidate, uint? FollowIdCandidate, byte? FollowTypeCandidate,
    byte? ActionRaw, byte? ModeRaw, double? AToGoalXZ, double? BToGoalXZ, string? ActorBytesBase64, string Status);
record CaptureSample(long Index, DateTimeOffset Timestamp, double ElapsedMs, double ReadDurationMs,
    double? PreviousIntervalMs, IdentityCheck Before, IdentityCheck? After, bool Valid,
    Position? A, Position? B, double? DistanceAB3D, Position? DeltaA, Position? DeltaB,
    double? ChangeA3D, double? ChangeB3D, Candidate[] HomeCandidates, EventMark[] Events, string Status, ResearchState? Research=null, LearnedCenterInfo? LearnedCenter=null, Position? PlayerA=null, LinkedSnapshot? LinkedMemory=null, HomeState? Home=null);

// Bounded reads only. The selected key, actor, node, owner, and RTTI never rebind.
sealed class MobIdentity(Func<ulong,int,byte[]> read, ulong moduleBase, uint moduleSize,
    ulong context, ulong head, uint key, ulong actor, ulong node)
{
    public Identity? Expected { get; private set; }
    ulong Q(ulong a)=>BitConverter.ToUInt64(read(a,8));
    uint D(ulong a)=>BitConverter.ToUInt32(read(a,4));
    byte B(ulong a)=>read(a,1)[0];
    static void Require(bool ok,string message){if(!ok)throw new IOException(message);}
    bool Image(ulong p, uint n=1)=>p>=moduleBase && n<=moduleSize && p-moduleBase<=moduleSize-n;
    public IdentityCheck Check()
    {
        try
        {
            Require(Q(moduleBase+0xE6E9C0)==context,"Owner binding changed");
            Require(Q(context+0x1120)==head,"Registry binding changed");
            Require(B(head+0x19)==1,"Invalid sentinel");
            ulong current=Q(head+8),parent=head;
            var visited=new HashSet<ulong>();long lower=-1,upper=(long)uint.MaxValue+1;
            bool found=false;
            for(int i=0;i<128 && current!=head;i++)
            {
                Require(Rpm.P(current)&&visited.Add(current),"Invalid/cyclic registry path");
                Require(B(current+0x19)==0 && Q(current+8)==parent,"Invalid registry parent/nil");
                uint k=D(current+0x20);
                Require(k>lower && k<upper,"Registry ordering invalid");
                if(k==key){Require(current==node && Q(current+0x28)==actor,"Selected member rebound");found=true;break;}
                parent=current;
                if(key<k){upper=k;current=Q(current);}else{lower=k;current=Q(current+0x10);}
            }
            Require(found,"Selected key not found in bounded registry lookup");
            uint id=D(actor+0x768);byte type=B(actor+0x7E1);ulong vt=Q(actor);
            Require(id==key && type==2,"Actor ID/type mismatch");
            Require(vt>=moduleBase+8 && Image(vt,8),"Vtable outside image");
            ulong col=Q(vt-8);Require(Image(col,24),"RTTI locator outside image");
            Require(D(col)==1 && D(col+4)==0 && D(col+8)==0,"Unexpected x64 RTTI locator");
            Require(moduleBase+D(col+20)==col,"RTTI self RVA mismatch");
            ulong td=moduleBase+D(col+12);Require(Image(td,16),"Type descriptor outside image");
            var name=new List<byte>();
            for(uint i=0;i<128;i++){Require(Image(td+16+i),"RTTI name outside image");byte b=B(td+16+i);if(b==0)break;name.Add(b);}
            string rtti=Encoding.ASCII.GetString(name.ToArray());
            Require(rtti==".?AVCTClientMonster@@","RTTI is not CTClientMonster: "+rtti);
            var identity=new Identity(context,head,node,key,actor,id,type,vt,rtti);
            Require(Expected is null || identity==Expected,"Identity continuity lost");
            return new(true,identity,"MATCH");
        }
        catch(Exception ex){return new(false,null,ex.Message);}
    }
    public Identity Pin(){var c=Check();if(!c.Membership || c.Identity is null)throw new IOException(c.Status);return Expected=c.Identity;}
}

sealed class MobCapture : IDisposable
{
    const string Sha="9CD77CD0C305359A3048D60C3C2FD837B907E2E61BBF8B828BFCC8E5270E5C28";
    readonly Process process;
    readonly IntPtr handle;
    readonly MobIdentity identity;
    readonly int[] offsets;
    readonly bool research;
    readonly double homeRadius;
    readonly LearnedCenter learner=new();
    readonly PlayerXYZ.BuildProfile playerProfile;
    readonly long moduleBase;
    readonly Stopwatch clock=Stopwatch.StartNew();
    readonly StreamWriter jsonl,csv,eventLog;
    readonly List<EventMark> pending=[];
    readonly object gate=new();
    readonly long startTicks;
    Position? previousA,previousB;
    double? previousTime;
    long count;
    bool stopped,finalized;
    public string DirectoryPath {get;}
    public string Status {get;private set;}="CAPTURING";
    public LearnedCenterInfo? LastCenter {get;private set;}
    public HomeState? LastHome {get;private set;}
    public static readonly string[] Events=["T0_IDLE","T1_AGGRO_OBSERVED","T2_FOLLOWING_OBSERVED","T3_FAR_POINT_OBSERVED","T4_AGGRO_LOST_OBSERVED","T5_RETURNING_OBSERVED","T6_IDLE_AGAIN_OBSERVED"];
    static readonly JsonSerializerOptions Json=new(){WriteIndented=true};
    public MobCapture(Snapshot snapshot,Monster selected,int[] candidates,bool homeResearch=true,double radius=50)
    {
        if(!double.IsFinite(radius)||radius<=0)throw new ArgumentOutOfRangeException(nameof(radius));
        offsets=candidates;research=homeResearch;homeRadius=radius;
        process=Process.GetProcessById(snapshot.Pid??throw new IOException("PID missing"));
        StreamWriter? j=null,c=null,e=null;
        try
        {
            startTicks=process.StartTime.ToUniversalTime().Ticks;
            if(process.StartTime.ToUniversalTime()>snapshot.Timestamp.UtcDateTime)throw new IOException("Process restarted since selection");
            handle=N.OpenProcess(0x1010,false,process.Id);
            if(handle==IntPtr.Zero)throw new IOException("Read-only OpenProcess failed");
            string path=Reader.PathOf(handle);
            if(!string.Equals(Path.GetFullPath(path),@"C:\Games\4Unity\TClient.exe",StringComparison.OrdinalIgnoreCase))throw new IOException("Unexpected client path");
            if(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)))!=Sha)throw new IOException("Unknown build");
            ulong mb=(ulong)Reader.Module(process.Id,path);
            moduleBase=(long)mb;playerProfile=PlayerSource.Load(path);
            uint imageSize=(uint)process.MainModule!.ModuleMemorySize;
            if(Parse(snapshot.ModuleBase!)!=mb)throw new IOException("Module changed since selection");
            identity=new MobIdentity(Read,mb,imageSize,Parse(snapshot.Context!),Parse(snapshot.Tree!),selected.EntityId,selected.ActorPtr,selected.NodePtr);
            Identity expected=identity.Pin();
            DirectoryPath=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"4UnityMonsterList","Captures",DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff")+"-"+Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(DirectoryPath);
            File.WriteAllText(Path.Combine(DirectoryPath,"home-profile.json"),JsonSerializer.Serialize(new{source="Actor+12D8/12DC/12E0",role="User-selected operational Home",radius_xz=homeRadius,radius_source="User setting, not measured leash",behavior="Live message-fed XYZ; can change. No fallback to learned center. Read-only player distance filter; not teleport permission or server acceptance."},Json));
            File.WriteAllText(Path.Combine(DirectoryPath,"linked-memory-rules.json"),JsonSerializer.Serialize(new{schema_version=2,coverage="All eligible 8-byte aligned actor slots every sample; no rotation",actor_size=LinkedMemory.ActorSize,allocation_evidence_rva="0x44CDA: mov ecx, 0x1320; allocation then constructor 0x8422E0",pointer_alignment=8,depth=1,prefix_bytes=LinkedMemory.PrefixSize,max_queries=LinkedMemory.MaxQueries,max_blocks=LinkedMemory.MaxBlocks,filter="Committed private readable non-executable memory; guard pages excluded",scope="Pointer-looking qwords are candidates, not verified owned objects. No recursive traversal. Link and region checked before/after; actor identity checked around entire sample. ABA not excluded. Raw linked data in timeline JSON only."},Json));
            File.WriteAllText(Path.Combine(DirectoryPath,"metadata.json"),JsonSerializer.Serialize(new{schema_version=1,read_only=true,pid=process.Id,process_start_utc=process.StartTime.ToUniversalTime(),sha256=Sha,module_base=$"0x{mb:X}",expected_identity=expected,a_offset="0x70",b_offset="0xB0",home_candidate_offsets=offsets.Select(x=>$"0x{x:X}"),requested_interval_ms=100,identity_scope="Selected key lookup, RTTI and binding checks; not a full red-black-tree audit. Reads are not atomic; between-read ABA reuse cannot be excluded."},Json));
            File.WriteAllText(Path.Combine(DirectoryPath,"research-metadata.json"),JsonSerializer.Serialize(new{enabled=research,goal_candidate_offset="0x12D8",return_flag_candidate_offset="0x12FC",follow_id_candidate_offset="0x12F0",follow_type_candidate_offset="0x12F4",actor_raw_start=0,actor_raw_length=0x1320,scope="ActorBytesBase64 contains inline actor bytes only. Separate LinkedMemory contains bounded first-level pointer candidates; see linked-memory-rules.json. Goal is mutable movement target candidate, not proven persistent Home. Return flag semantics source-inferred; no automatic observed events.",evidence_rvas=new[]{"0x842391","0x6F987","0x51846","0x7A16AC"}},Json));
            File.WriteAllText(Path.Combine(DirectoryPath,"learned-center-rules.json"),JsonSerializer.Serialize(new{scope="This capture/session only. No center restored from disk. Identity failure clears center.",max_gap_ms=LearnedCenter.MaxGapMs,min_return_ms=LearnedCenter.MinReturnMs,min_flag_one_samples=3,goal_tolerance_3d=LearnedCenter.GoalTolerance,arrival_xz=LearnedCenter.ArrivalXZ,arrival_y=LearnedCenter.ArrivalY,notice="Evidence qualification tolerances, NOT a leash radius. Requires observed 0→1→0 and zero follow ID/type throughout return."},Json));
            jsonl=j=new(Path.Combine(DirectoryPath,"timeline.jsonl")){AutoFlush=true};
            csv=c=new(Path.Combine(DirectoryPath,"timeline.csv")){AutoFlush=true};
            eventLog=e=new(Path.Combine(DirectoryPath,"events.jsonl")){AutoFlush=true};
            csv.WriteLine(Header(offsets));
        }
        catch{j?.Dispose();c?.Dispose();e?.Dispose();if(handle!=IntPtr.Zero)N.CloseHandle(handle);process.Dispose();throw;}
    }
    static ulong Parse(string s)=>Convert.ToUInt64(s.Replace("0x",""),16);
    byte[] Read(ulong address,int length)
    {
        if(!Rpm.P(address)||length<=0 || address>(ulong)long.MaxValue-(uint)length)throw new IOException("Invalid read range");
        return new Rpm(handle).Bytes((long)address,length)??throw new IOException($"Unreadable memory 0x{address:X}");
    }
    IdentityCheck Check()
    {
        try{if(process.HasExited || process.StartTime.ToUniversalTime().Ticks!=startTicks)return new(false,null,"Process session lost");return identity.Check();}
        catch(Exception ex){return new(false,null,ex.Message);}
    }
    Position? XYZ(int offset)
    {
        try{byte[] b=Read(identity.Expected!.Actor+(uint)offset,12);var p=new Position(BitConverter.ToSingle(b,0),BitConverter.ToSingle(b,4),BitConverter.ToSingle(b,8));return float.IsFinite(p.X)&&float.IsFinite(p.Y)&&float.IsFinite(p.Z)&&Math.Abs(p.X)<=1_000_000&&Math.Abs(p.Y)<=1_000_000&&Math.Abs(p.Z)<=1_000_000?p:null;}catch(IOException){return null;}
    }
    public void Mark(string label,string notes)
    {
        lock(gate){if(stopped)throw new InvalidOperationException("Capture stopped");if(!Events.Contains(label))throw new ArgumentException("Unknown event");var mark=new EventMark(DateTimeOffset.UtcNow,clock.Elapsed.TotalMilliseconds,label,notes);eventLog.WriteLine(JsonSerializer.Serialize(mark));pending.Add(mark);}
    }
    public CaptureSample? Sample()
    {
        lock(gate)
        {
            if(stopped)return null;
            double t=clock.Elapsed.TotalMilliseconds;var utc=DateTimeOffset.UtcNow;
            var before=Check();Position? a=null,b=null,playerA=null;Candidate[] homes=[];IdentityCheck? after=null;ResearchState? researchState=null;LinkedSnapshot? linked=null;
            if(before.Membership)
            {
                a=XYZ(0x70);b=XYZ(0xB0);
                homes=offsets.Select(o=>{var p=XYZ(o);return new Candidate(o,p,p is null?"UNREADABLE_OR_NONFINITE":"CANDIDATE_ONLY",Distance(a,p),Distance(b,p),Distance(a,p,true),Distance(b,p,true));}).ToArray();
                if(research)
                {
                    try{var raw=Read(identity.Expected!.Actor,LinkedMemory.ActorSize);researchState=DecodeResearch(raw,a,b);linked=LinkedMemory.Capture(raw,identity.Expected.Actor,Read,address=>LinkedMemory.Query(handle,address));}
                    catch(IOException ex){researchState=new(null,null,null,null,null,null,null,null,null,"UNREADABLE: "+ex.Message);}
                }
                var player=Reader.ValidatedPlayer(new Rpm(handle),identity.Expected!.Context,moduleBase,playerProfile);
                if(player is not null)playerA=new(player.X,player.Y,player.Z);
                after=Check();
            }
            bool valid=Accept(before,after,identity.Expected!);
            bool researchOk=!research || researchState?.ActorBytesBase64 is not null;
            string status=!valid?"CAPTURE TERMINATED: "+(!before.Membership?before.Status:after?.Status):a is null || b is null?"CAPTURE TERMINATED: A/B unreadable or nonfinite":!researchOk?"CAPTURE TERMINATED: research region unreadable":"VALID_OBSERVATION";
            var learned=learner.Observe(t,valid&&a is not null&&b is not null&&researchOk,researchState?.ReturnFlagCandidate,researchState?.GoalCandidate,researchState?.FollowIdCandidate,researchState?.FollowTypeCandidate,a,playerA);
            LastCenter=learned;
            LastHome=HomeState.Read(valid&&researchOk,researchState?.GoalCandidate,a,playerA,homeRadius);
            var sample=new CaptureSample(count++,utc,t,clock.Elapsed.TotalMilliseconds-t,previousTime is null?null:t-previousTime,before,after,valid&&a is not null&&b is not null&&researchOk,a,b,Distance(a,b),valid?Delta(a,previousA):null,valid?Delta(b,previousB):null,valid?Distance(a,previousA):null,valid?Distance(b,previousB):null,homes,pending.ToArray(),status,researchState,learned,playerA,linked);
            sample=sample with{Home=LastHome};
            pending.Clear();jsonl.WriteLine(JsonSerializer.Serialize(sample));csv.WriteLine(CsvRow(sample,identity.Expected!,offsets));
            previousTime=t;previousA=a;previousB=b;
            if(!sample.Valid)Stop(status);else if(count>=36000)Stop("CAPTURE STOPPED: 36000 sample limit");
            return sample;
        }
    }
    internal static bool Accept(IdentityCheck before,IdentityCheck? after,Identity expected)=>before.Membership && after?.Membership==true && before.Identity==expected && after.Identity==expected;
    internal static ResearchState DecodeResearch(byte[] raw,Position? a,Position? b)
    {
        if(raw.Length!=0x1320)throw new IOException("Unexpected actor capture length");
        var goal=new Position(BitConverter.ToSingle(raw,0x12D8),BitConverter.ToSingle(raw,0x12DC),BitConverter.ToSingle(raw,0x12E0));
        bool finite=float.IsFinite(goal.X)&&float.IsFinite(goal.Y)&&float.IsFinite(goal.Z);
        return new(finite?goal:null,raw[0x12FC],BitConverter.ToUInt32(raw,0x12F0),raw[0x12F4],raw[0x7D4],raw[0x7E0],finite?Distance(a,goal,true):null,finite?Distance(b,goal,true):null,Convert.ToBase64String(raw),finite?"SOURCE_INFERRED_TARGET_AND_STATE; HOME_UNPROVEN":"GOAL_NONFINITE; HOME_UNPROVEN");
    }
    internal static double? Distance(Position? a,Position? b,bool xz=false)=>a is null||b is null?null:Math.Sqrt(Math.Pow((double)a.X-b.X,2)+(xz?0:Math.Pow((double)a.Y-b.Y,2))+Math.Pow((double)a.Z-b.Z,2));
    internal static Position? Delta(Position? a,Position? b)=>a is null||b is null?null:new(a.X-b.X,a.Y-b.Y,a.Z-b.Z);
    public void Stop(string reason="CAPTURE STOPPED: user")
    {
        lock(gate)
        {
            if(finalized)return;
            if(!stopped){stopped=true;Status=reason;}
            // Release writer handles before reopening; a failed finalization remains retryable.
            try{jsonl.Dispose();}finally{try{csv.Dispose();}finally{eventLog.Dispose();}}
            CaptureFiles.FinalizeCapture(DirectoryPath,Status);
            finalized=true;
        }
    }
    public void Dispose(){try{Stop();}finally{jsonl.Dispose();csv.Dispose();eventLog.Dispose();N.CloseHandle(handle);process.Dispose();}}
    internal static int[] ParseOffsets(string input)
    {
        var parts=input.Split([',',';',' '],StringSplitOptions.RemoveEmptyEntries);
        if(parts.Length>8)throw new ArgumentException("En fazla 8 Home adayı girilebilir.");
        var values=parts.Select(s=>int.Parse(s.StartsWith("0x",StringComparison.OrdinalIgnoreCase)?s[2..]:s,NumberStyles.HexNumber,CultureInfo.InvariantCulture)).ToArray();
        if(values.Any(x=>x<0 || x>LinkedMemory.ActorSize-12 || x%4!=0 || (x<0x7C&&x+12>0x70) || (x<0xBC&&x+12>0xB0)) || values.Distinct().Count()!=values.Length)throw new ArgumentException("Adaylar benzersiz, 4-byte hizalı, 0..1314 hex aralığında ve A/B ile çakışmayan offsetler olmalı. Monster nesnesinin boyutu 0x1320 byte.");
        return values;
    }
    internal static string Cell(object? value)=>"\""+(value is IFormattable f?f.ToString(null,CultureInfo.InvariantCulture):value?.ToString()??"").Replace("\"","\"\"")+"\"";
    static IEnumerable<object?> Components(Position? p)=>new object?[]{p?.X,p?.Y,p?.Z};
    static string Header(int[] offsets)=>string.Join(',',new[]{"sample_index","timestamp_utc","elapsed_ms","read_duration_ms","previous_interval_ms","registry_key","expected_actor","expected_node","actor_id_before","type_before","rtti_before","membership_before","actor_id_after","type_after","rtti_after","membership_after","valid","status","A_x","A_y","A_z","B_x","B_y","B_z","AB_distance_3d","deltaA_x","deltaA_y","deltaA_z","deltaB_x","deltaB_y","deltaB_z","A_change_3d","B_change_3d"}.Concat(offsets.SelectMany(o=>new[]{"x","y","z","status","A_distance_3d","B_distance_3d","A_distance_xz","B_distance_xz"}.Select(s=>$"home_candidate_{o:X}_{s}"))).Concat(new[]{"observed_events_json","research_state_json","learned_center_json","player_A_x","player_A_y","player_A_z","home_json"}));
    static string CsvRow(CaptureSample s,Identity expected,int[] offsets)
    {
        var cells=new List<object?>{s.Index,s.Timestamp.ToString("O"),s.ElapsedMs,s.ReadDurationMs,s.PreviousIntervalMs,expected.Key,$"0x{expected.Actor:X}",$"0x{expected.Node:X}",s.Before.Identity?.ActorId,s.Before.Identity?.Type,s.Before.Identity?.Rtti,s.Before.Membership,s.After?.Identity?.ActorId,s.After?.Identity?.Type,s.After?.Identity?.Rtti,s.After?.Membership,s.Valid,s.Status};
        cells.AddRange(Components(s.A));cells.AddRange(Components(s.B));cells.Add(s.DistanceAB3D);cells.AddRange(Components(s.DeltaA));cells.AddRange(Components(s.DeltaB));cells.Add(s.ChangeA3D);cells.Add(s.ChangeB3D);
        foreach(int o in offsets){var h=s.HomeCandidates.FirstOrDefault(x=>x.Offset==o);cells.AddRange(Components(h?.XYZ));cells.AddRange(new object?[]{h?.Status,h?.DistanceA3D,h?.DistanceB3D,h?.DistanceAXZ,h?.DistanceBXZ});}
        cells.Add(JsonSerializer.Serialize(s.Events));cells.Add(JsonSerializer.Serialize(s.Research is null?null:s.Research with{ActorBytesBase64=null}));cells.Add(JsonSerializer.Serialize(s.LearnedCenter));cells.AddRange(Components(s.PlayerA));cells.Add(JsonSerializer.Serialize(s.Home));return string.Join(',',cells.Select(Cell));
    }
}
