using System.Text.Json;

namespace UnityMonsterList;

static class LinkedMemoryTests
{
    public static void Run()
    {
        int passed=0;
        void Test(string name,Action f){try{f();passed++;}catch(Exception ex){throw new Exception(name,ex);}}
        void Assert(bool b){if(!b)throw new Exception("Assertion failed");}
        const ulong actor=0x20000000,target=0x30000000;
        var raw=new byte[LinkedMemory.ActorSize];BitConverter.GetBytes(target).CopyTo(raw,0x120);
        var region=new ReadableRegion(target,0x200,target,0x1000,0x04,0x20000);
        byte[] Read(ulong a,int n)=>a==actor+0x120?BitConverter.GetBytes(target):a==target?new byte[n]:throw new IOException("unexpected read");
        LinkedSnapshot Capture(Func<ulong,int,byte[]>? read=null,Func<ulong,ReadableRegion?>? query=null)=>LinkedMemory.Capture(raw,actor,read??Read,query??(_=>region));
        Test("bounded capture",()=>{var s=Capture();Assert(s.Blocks.Length==1&&s.Blocks[0].Length==512&&s.Blocks[0].BytesBase64 is not null&&!s.Truncated);});
        Test("region boundary",()=>Assert(Capture(query:_=>region with{Size=20}).Blocks[0].Length==20));
        Test("guard and executable excluded",()=>{Assert(Capture(query:_=>region with{Protect=0x104}).Blocks.Length==0);Assert(Capture(query:_=>region with{Protect=0x20}).Blocks.Length==0);});
        Test("uncommitted/image excluded",()=>{Assert(Capture(query:_=>region with{State=0x2000}).Blocks.Length==0);Assert(Capture(query:_=>region with{Type=0x1000000}).Blocks.Length==0);});
        Test("link changes before read",()=>Assert(Capture(read:(a,n)=>BitConverter.GetBytes(target+8)).Blocks[0].Status=="LINK_CHANGED_BEFORE"));
        Test("link changes after read",()=>{int links=0;var s=Capture(read:(a,n)=>a==target?new byte[n]:BitConverter.GetBytes(++links==1?target:target+8));Assert(s.Blocks[0].Status=="LINK_CHANGED_AFTER"&&s.Blocks[0].BytesBase64 is null);});
        Test("region changes",()=>{int q=0;var s=Capture(query:_=>++q==1?region:region with{AllocationBase=target+8});Assert(s.Blocks[0].Status=="REGION_CHANGED"&&s.Blocks[0].BytesBase64 is null);});
        Test("unreadable target",()=>Assert(Capture(read:(a,n)=>a==target?throw new IOException():Read(a,n)).Blocks[0].BytesBase64 is null));
        Test("read budgets reported",()=>{var many=new byte[LinkedMemory.ActorSize];for(int i=0;i<200;i++)BitConverter.GetBytes(target+(ulong)i*0x1000).CopyTo(many,i*8);var s=LinkedMemory.Capture(many,actor,Read,_=>null);Assert(s.Queries==200&&!s.Truncated&&s.EligibleSlots==200&&s.Slots!.Length==200);});
        Test("no actor overflow",()=>{bool rejected=false;try{MobCapture.ParseOffsets("8AFC");}catch(ArgumentException){rejected=true;}Assert(rejected);Assert(MobCapture.ParseOffsets("1314")[0]==0x1314);});
        Test("all 612 readable slots include final actor field",()=>
        {
            var full=new byte[LinkedMemory.ActorSize];
            for(int off=0;off<full.Length;off+=8)BitConverter.GetBytes(target+(ulong)off*0x1000).CopyTo(full,off);
            byte[] ReadFull(ulong a,int n)=>a>=actor&&a<actor+(ulong)full.Length?full.AsSpan((int)(a-actor),n).ToArray():new byte[n];
            var s=LinkedMemory.Capture(full,actor,ReadFull,a=>new(a,512,a,0x1000,4,0x20000));
            Assert(!s.Truncated&&s.Queries==612&&s.EligibleSlots==612&&s.Blocks.Length==612&&s.Slots!.Length==612);
            Assert(s.Blocks[^1].SourceOffset==0x1318&&s.Blocks.All(b=>b.BytesBase64 is not null));
            Assert(s.Blocks.Sum(b=>b.Length)==612*512);
        });
        Test("early filtered slots do not starve late pointer",()=>
        {
            var full=new byte[LinkedMemory.ActorSize];
            for(int off=0;off<full.Length;off+=8)BitConverter.GetBytes(target+(ulong)off*0x1000).CopyTo(full,off);
            ulong last=BitConverter.ToUInt64(full,0x1318);
            byte[] ReadFull(ulong a,int n)=>a==actor+0x1318?BitConverter.GetBytes(last):new byte[n];
            var s=LinkedMemory.Capture(full,actor,ReadFull,a=>a==last?new(a,512,a,0x1000,4,0x20000):null);
            Assert(s.Queries==612&&!s.Truncated&&s.Blocks.Single().SourceOffset==0x1318);
            Assert(s.Slots!.Count(x=>x.Status=="QUERY_FAILED")==611);
        });
        Test("stable candidate vs missing or rebound",()=>
        {
            string dir=Path.Combine(Path.GetTempPath(),"UnityLinkedTest-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
            var bytes=new byte[128];BitConverter.GetBytes(100f).CopyTo(bytes,21);BitConverter.GetBytes(5f).CopyTo(bytes,25);BitConverter.GetBytes(200f).CopyTo(bytes,29);
            var block=new LinkedBlock(0x1318,target,target,bytes.Length,Convert.ToBase64String(bytes),"STABLE_LINK_CANDIDATE_ONLY");
            var linked=new LinkedSnapshot([block],1,1,false,[new(0x1318,target,"STABLE_LINK_CANDIDATE_ONLY")]);
            var check=new IdentityCheck(true,null,"SYNTHETIC");
            var learned=new LearnedCenterInfo("RECONFIRMED",new(100,5,200),2,0,0,"SYNTHETIC");
            var sample=new CaptureSample(0,DateTimeOffset.UtcNow,0,1,null,check,check,true,new(100,5,200),new(100,5,200),0,null,null,null,null,[],[],"SYNTHETIC",LearnedCenter:learned,LinkedMemory:linked);
            int Count(){using var d=JsonDocument.Parse(File.ReadAllText(Path.Combine(dir,"linked-home-analysis.json")));return d.RootElement.GetProperty("candidates").GetArrayLength();}
            LinkedAnalysis.Analyze([sample,sample with{Index=1}],dir);Assert(Count()==1);
            using(var report=JsonDocument.Parse(File.ReadAllText(Path.Combine(dir,"linked-home-analysis.json"))))
            {
                Assert(report.RootElement.GetProperty("candidates")[0].GetProperty("source_offset").GetString()=="0x1318");
                Assert(report.RootElement.GetProperty("coverage").GetProperty("samples_with_all_eligible_slots_queried").GetInt32()==2);
            }
            LinkedAnalysis.Analyze([sample,sample with{Index=1,LinkedMemory=null}],dir);Assert(Count()==0);
            LinkedAnalysis.Analyze([sample,sample with{Index=1,LinkedMemory=linked with{Blocks=[block with{Address=target+8}]}}],dir);Assert(Count()==0);
            LinkedAnalysis.Analyze([sample with{LinkedMemory=linked with{Slots=null,Truncated=true}}],dir);
            using(var report=JsonDocument.Parse(File.ReadAllText(Path.Combine(dir,"linked-home-analysis.json"))))
                Assert(report.RootElement.GetProperty("coverage").GetProperty("samples_with_all_eligible_slots_queried").GetInt32()==0);
        });
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"linked-self-test.json"),JsonSerializer.Serialize(new{passed,synthetic=true,live_game_tested=false}));
    }
}
