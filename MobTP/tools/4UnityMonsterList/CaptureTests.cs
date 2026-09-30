using System.Text;
using System.Text.Json;

namespace UnityMonsterList;

static class CaptureTests
{
    public static void Run()
    {
        int passed=0;
        void Test(string name,Action action){action();passed++;}
        void Assert(bool value){if(!value)throw new Exception("Assertion failed");}
        Test("live Home radius uses XZ and includes boundary",()=>{var h=HomeState.Read(true,new(100,5,200),new(101,5,200),new(130,900,240),50);Assert(h.PlayerWithinRadius==true&&h.PlayerDistanceXZ==50);});
        Test("live Home outside radius",()=>Assert(HomeState.Read(true,new(100,5,200),null,new(151,5,200),50).PlayerWithinRadius==false));
        Test("invalid Home and zero player remain unknown",()=>{Assert(HomeState.Read(false,new(100,5,200),null,new(100,5,200),50).XYZ is null);Assert(HomeState.Read(true,new(100,5,200),null,new(0,0,0),50).PlayerWithinRadius is null);});
        Test("live Home updates without learned fallback",()=>{var a=HomeState.Read(true,new(100,5,200),null,new(110,5,200),50);var b=HomeState.Read(true,new(200,5,200),null,new(110,5,200),50);Assert(a.PlayerWithinRadius==true&&b.PlayerWithinRadius==false&&b.XYZ!.X==200);});
        var memory=new Dictionary<ulong,byte>();
        void Put(ulong p,byte[] b){for(int i=0;i<b.Length;i++)memory[p+(uint)i]=b[i];}
        void Q(ulong p,ulong v)=>Put(p,BitConverter.GetBytes(v));
        void D(ulong p,uint v)=>Put(p,BitConverter.GetBytes(v));
        void B(ulong p,byte v)=>memory[p]=v;
        byte[] Read(ulong p,int n)=>Enumerable.Range(0,n).Select(i=>memory.TryGetValue(p+(uint)i,out byte v)?v:throw new IOException("missing page")).ToArray();
        const ulong mb=0x10000000,ctx=0x20000000,head=0x21000000,node=0x22000000,actor=0x23000000,vt=mb+0x300,col=mb+0x400,td=mb+0x500;
        Q(mb+0xE6E9C0,ctx);Q(ctx+0x1120,head);B(head+0x19,1);Q(head+8,node);
        Q(node,head);Q(node+8,head);Q(node+0x10,head);B(node+0x19,0);D(node+0x20,42);Q(node+0x28,actor);
        D(actor+0x768,42);B(actor+0x7E1,2);Q(actor,vt);Q(vt-8,col);D(col,1);D(col+4,0);D(col+8,0);D(col+12,0x500);D(col+20,0x400);Put(td+16,Encoding.ASCII.GetBytes(".?AVCTClientMonster@@\0"));
        var identity=new MobIdentity(Read,mb,0x1000000,ctx,head,42,actor,node);
        var expected=identity.Pin();var good=identity.Check();
        Test("valid identity",()=>Assert(MobCapture.Accept(good,identity.Check(),expected)));
        Test("same key rebound",()=>{Q(node+0x28,actor+0x1000);Assert(!identity.Check().Membership);Q(node+0x28,actor);});
        Test("actor type changed",()=>{B(actor+0x7E1,1);Assert(!identity.Check().Membership);B(actor+0x7E1,2);});
        Test("actor ID changed",()=>{D(actor+0x768,43);Assert(!identity.Check().Membership);D(actor+0x768,42);});
        Test("wrong RTTI",()=>{B(td+16,(byte)'X');Assert(!identity.Check().Membership);B(td+16,(byte)'.');});
        Test("owner changed",()=>{Q(mb+0xE6E9C0,ctx+8);Assert(!identity.Check().Membership);Q(mb+0xE6E9C0,ctx);});
        Test("missing member",()=>{Q(head+8,head);Assert(!identity.Check().Membership);Q(head+8,node);});
        Test("cycle",()=>{D(node+0x20,41);Q(node+0x10,node);Assert(!identity.Check().Membership);D(node+0x20,42);Q(node+0x10,head);});
        Test("unreadable identity",()=>{memory.Remove(actor+0x7E1);Assert(!identity.Check().Membership);B(actor+0x7E1,2);});
        Test("after mismatch invalidates sample",()=>Assert(!MobCapture.Accept(good,new(false,null,"lost"),expected)));
        Test("missing after invalidates sample",()=>Assert(!MobCapture.Accept(good,null,expected)));
        Test("different after identity invalidates sample",()=>Assert(!MobCapture.Accept(good,new(true,expected with{Actor=actor+8},"MATCH"),expected)));
        Test("candidate offsets not invented",()=>Assert(MobCapture.ParseOffsets("").Length==0));
        Test("hex offsets",()=>Assert(MobCapture.ParseOffsets("0x100, 200").SequenceEqual(new[]{256,512})));
        Test("A overlap rejected",()=>{bool failed=false;try{MobCapture.ParseOffsets("6C");}catch(ArgumentException){failed=true;}Assert(failed);});
        Test("distance and delta",()=>{var a=new Position(3,4,0);var b=new Position(0,0,0);Assert(MobCapture.Distance(a,b)==5&&MobCapture.Distance(a,b,true)==3&&MobCapture.Delta(a,b)==a&&MobCapture.Distance(a,null)==null);});
        Test("CSV quotes and newlines",()=>Assert(MobCapture.Cell("a,\"b\"\nc")=="\"a,\"\"b\"\"\nc\""));
        Test("manual event preserved",()=>{var e=new EventMark(DateTimeOffset.UtcNow,12.5,"T0_IDLE","user note");Assert(JsonSerializer.Deserialize<EventMark>(JsonSerializer.Serialize(e))==e);});
        Test("research buffer size rejection",()=>{bool rejected=false;try{MobCapture.DecodeResearch(new byte[12],null,null);}catch(IOException){rejected=true;}Assert(rejected);});
        Test("research and stable candidate analysis",()=>
        {
            var raw=new byte[0x1320];
            void Vec(int o,float x,float y,float z){BitConverter.GetBytes(x).CopyTo(raw,o);BitConverter.GetBytes(y).CopyTo(raw,o+4);BitConverter.GetBytes(z).CopyTo(raw,o+8);}
            Vec(0x100,100,5,200);Vec(0x12D8,110,5,200);raw[0x12FC]=0;
            var research=MobCapture.DecodeResearch(raw,new(105,5,200),new(105,5,200));
            Assert(research.GoalCandidate==new Position(110,5,200)&&research.ReturnFlagCandidate==0&&research.AToGoalXZ==5);
            var sample=new CaptureSample(0,DateTimeOffset.UtcNow,0,1,null,good,good,true,new(105,5,200),new(105,5,200),0,null,null,null,null,[],[],"SYNTHETIC",research);
            string dir=Path.Combine(Path.GetTempPath(),"4UnityHomeTest-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
            Vec(0x12D8,100,5,200);raw[0x12FC]=1;
            var second=sample with{Index=1,ElapsedMs=100,A=new(100,5,200),Research=MobCapture.DecodeResearch(raw,new(100,5,200),new(100,5,200))};
            File.WriteAllLines(Path.Combine(dir,"timeline.jsonl"),new[]{JsonSerializer.Serialize(sample),JsonSerializer.Serialize(second)});
            HomeAnalysis.Analyze(dir);
            using var report=JsonDocument.Parse(File.ReadAllText(Path.Combine(dir,"home-analysis.json")));
            var observed=report.RootElement.GetProperty("DIRECT_OBSERVATION");
            var offsets=observed.GetProperty("stable_near_endpoint_candidates").EnumerateArray().Select(x=>x.GetProperty("offset").GetString()).ToArray();
            Assert(offsets.Contains("0x100")&&!offsets.Contains("0x12D8")&&observed.GetProperty("return_flag_transitions").GetArrayLength()==2);
        });
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"capture-self-test.json"),JsonSerializer.Serialize(new{passed,synthetic=true,live_game_tested=false}));
    }
}
