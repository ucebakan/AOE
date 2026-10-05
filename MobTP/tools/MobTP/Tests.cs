using System.Text.Json;
using UnityMonsterList;

namespace MobTP;
static class Tests
{
    internal static void RenderRangeFixture(MainForm form)
    {
        var position=new Position(102,5,200);var home=new Position(180,5,200);
        var monster=new Monster(1,2,0x100000,0x200000,102,5,200,true,0,null);
        form.Render(new(DateTimeOffset.Now,null,new(99,0x400000,100,5,200,"synthetic"),
            [new(monster,new(1,2,3,1,monster.ActorPtr,1,2,4,"synthetic"),position,position,home,80)],"SENTETİK ÖNİZLEME · oyuna bağlı değil"));
    }
    public static void Profiles()
    {
        using var b=new PlayerXYZ.Binary(@"C:\Games\4Unity\TClient.exe");
        var p=ProfileStore.Resolve(b);
        if(b.Sha==PlayerXYZ.Profiles.KnownSha&&(p.RootRva!=0xE6E9C0||p.RegistryOffset!=0x1120||p.ActorIdOffset!=0x768||p.ActorTypeOffset!=0x7E1||p.ActorSize!=0x1320||p.HomeOffset!=0x12D8||p.MonsterVtableRva!=0xCE5578))throw new Exception("Current anchors mismatch");
        int passed=1;
        p.HomeOffset+=4;bool rejected=false;try{ProfileStore.Validate(b,p);}catch(IOException){rejected=true;}if(!rejected)throw new Exception("Tampered profile accepted");p.HomeOffset-=4;passed++;
        var bundle=ProfileStore.Load(@"C:\Games\4Unity\TClient.exe");int scans=ProfileStore.ScanCount;ProfileStore.Load(@"C:\Games\4Unity\TClient.exe");if(scans!=ProfileStore.ScanCount)throw new Exception("Repeated scans");passed++;
        if((b.Sha==PlayerXYZ.Profiles.KnownSha&&ProfileStore.Embedded(b) is null)||scans!=ProfileStore.ScanCount)throw new Exception("Embedded profile rescan");passed++;
        int Raw(int rva)=>b.Pe.PEHeaders.SectionHeaders.Where(s=>rva>=s.VirtualAddress&&rva<s.VirtualAddress+s.SizeOfRawData).Select(s=>s.PointerToRawData+rva-s.VirtualAddress).Single();
        var changed=b.Data.ToArray();changed[0x20]^=1;
        using(var future=new PlayerXYZ.Binary(changed)){var resolved=ProfileStore.Resolve(future);if(resolved.Player.Sha256==b.Sha||resolved.HomeOffset!=p.HomeOffset)throw new Exception("Changed SHA not resolved");}passed++;
        changed=b.Data.ToArray();changed[Raw(p.Signatures["root_a"].Rva)]^=1;
        using(var broken=new PlayerXYZ.Binary(changed)){rejected=false;try{ProfileStore.Resolve(broken);}catch(IOException){rejected=true;}if(!rejected)throw new Exception("Broken anchor accepted");}passed++;
        changed=b.Data.ToArray();int rootOperand=Raw(p.Signatures["root_b"].Rva+3);BitConverter.GetBytes(BitConverter.ToInt32(changed,rootOperand)+8).CopyTo(changed,rootOperand);
        using(var diverged=new PlayerXYZ.Binary(changed)){rejected=false;try{ProfileStore.Resolve(diverged);}catch(IOException){rejected=true;}if(!rejected)throw new Exception("Conflicting roots accepted");}passed++;
        changed=b.Data.ToArray();var section=b.Pe.PEHeaders.SectionHeaders.First(s=>((uint)s.SectionCharacteristics&0x20000000)!=0);
        var anchor=p.Signatures["root_a"];int anchorLength=PlayerXYZ.Binary.Pattern(anchor.Pattern).Length;b.At(anchor.Rva,anchorLength).CopyTo(changed,section.PointerToRawData+section.SizeOfRawData-512);
        using(var ambiguous=new PlayerXYZ.Binary(changed)){rejected=false;try{ProfileStore.Resolve(ambiguous);}catch(IOException){rejected=true;}if(!rejected)throw new Exception("Ambiguous anchor accepted");}passed++;
        byte[] Live(ulong address,int length)
        {
            int rva=checked((int)(address-(ulong)b.ImageBase));return rva<b.Pe.PEHeaders.PEHeader!.SizeOfHeaders?b.Data.AsSpan(rva,length).ToArray():b.At(rva,length);
        }
        var liveBundle=new ProfileBundle(p,b,"SYNTHETIC");liveBundle.ValidateLive(Live,(ulong)b.ImageBase);passed++;
        ulong relocated=(ulong)b.ImageBase+0x100000;var pointerSlots=new HashSet<int>();
        foreach(int vt in new[]{p.MonsterVtableRva,p.Player.CtclientcharVtableRva,p.Player.CtclientgameVtableRva})
        {
            pointerSlots.Add(vt-8);pointerSlots.Add(vt);
            if(vt!=p.Player.CtclientgameVtableRva){foreach(int slot in ProfileStore.CoordinateSlots(b,p))pointerSlots.Add(vt+slot);}
        }
        liveBundle.ValidateLive((address,length)=>{int rva=(int)(address-relocated);var bytes=Live((ulong)b.ImageBase+(uint)rva,length);return length==8&&pointerSlots.Contains(rva)?BitConverter.GetBytes(BitConverter.ToUInt64(bytes)+0x100000):bytes;},relocated);passed++;
        rejected=false;try{liveBundle.ValidateLive((address,length)=>{var bytes=Live(address,length);if(address==(ulong)b.ImageBase+(uint)p.Signatures["home"].Rva)bytes[0]^=1;return bytes;},(ulong)b.ImageBase);}catch(IOException){rejected=true;}if(!rejected)throw new Exception("Live code patch accepted");passed++;
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"profile-test.json"),JsonSerializer.Serialize(new{passed,sha=b.Sha,profile=p,live_tested=false},new JsonSerializerOptions{WriteIndented=true}));
    }
    public static void Run()
    {
        int passed=0;var names=new List<string>();
        void Assert(bool x){if(!x)throw new Exception("Assertion failed");}
        void Test(string name,Action f){try{f();passed++;names.Add(name);}catch(Exception ex){throw new Exception(name,ex);}}
        var player=new Position(100,5,200);var home=new Position(100,80,200);var target=new Position(102,5,200);
        Test("radius ignores height",()=>Assert(Placement.InBase(player,home,target,50)));
        Test("player outside is excluded",()=>Assert(!Placement.InBase(new(151,5,200),home,target,50)));
        Test("target outside is excluded",()=>Assert(!Placement.InBase(player,home,new(151,5,200),50)));
        Test("exactly selected range is included",()=>Assert(Placement.InBase(new(174,5,200),home,new(175,5,200),75)));
        Test("range above 50 is used by guards",()=>Assert(Placement.InBase(new(174,5,200),home,new(175,5,200),100)&&!Placement.InBase(new(174,5,200),home,new(175,5,200),50)));
        Test("smaller range and target edge are enforced",()=>Assert(Placement.InBase(new(109,5,200),home,new(110,5,200),10)&&!Placement.InBase(new(109,5,200),home,new(111,5,200),10)));
        Test("invalid ranges are rejected",()=>{foreach(double range in new[]{0,-1,double.NaN,double.PositiveInfinity})Assert(!Placement.InBase(player,home,target,range));});
        Test("no placement inside player",()=>Assert(!Placement.InBase(player,home,player,50)));
        Test("invalid and zero rejected",()=>Assert(!Placement.InBase(player,new(0,0,0),target,50)&&!Placement.Usable(new(float.NaN,5,200))));
        Test("old settings default and custom range roundtrip",()=>{var old=JsonSerializer.Deserialize<UserSettings>("{\"Spread\":3,\"DistanceDescending\":true}")!;Assert(old.HomeRange==50&&old.Spread==3&&old.DistanceDescending);var custom=old with{HomeRange=125.5m};Assert(JsonSerializer.Deserialize<UserSettings>(JsonSerializer.Serialize(custom))==custom);});
        Test("ring spacing",()=>{var pts=Enumerable.Range(0,20).Select(i=>Placement.Ring(player,i,20,2)).ToArray();Assert(pts.Distinct().Count()==20&&pts.All(p=>Math.Abs(MobCapture.Distance(p,player,true)!.Value-2)<0.001&&p.Y==5));});
        Test("invalid spread",()=>{bool fail=false;try{Placement.Ring(player,0,1,0);}catch(ArgumentOutOfRangeException){fail=true;}Assert(fail);});
        Test("assignable and invalid shortcut keys",()=>{Assert(new KeyBinding((int)Keys.F8,0).Valid&&new KeyBinding((int)Keys.K,2).Valid);Assert(!new KeyBinding((int)Keys.ControlKey,0).Valid&&!new KeyBinding((int)Keys.K,8).Valid);});
        Test("foreground requires a selected game",()=>Assert(!HotkeyRegistration.GameForeground(null)));
        Test("counter includes ring target filter",()=>
        {
            var m=new Monster(1,2,0x100000,0x200000,149,5,200,true,0,null);
            var v=new MobView(m,new(1,2,3,1,m.ActorPtr,1,2,4,"synthetic"),player,player,home,49);
            var w=new World(DateTimeOffset.Now,null,new(99,0x400000,149,5,200,"synthetic"),[v],"synthetic");
            var plan=Placement.Plan(w,2,50);Assert(plan.Length==1&&!plan[0].Eligible);Assert(Placement.Plan(w,1,50)[0].Eligible);
            Assert(Placement.Plan(w,2,100)[0].Eligible&&Placement.Plan(w,2,20).Length==0);
        });
        Test("hotkey registration conflict preserves old assignment",()=>
        {
            using var first=new Form();using var second=new Form();using var a=new HotkeyRegistration(first.Handle);using var b=new HotkeyRegistration(second.Handle);
            var one=new KeyBinding((int)Keys.F23,7);var two=new KeyBinding((int)Keys.F24,7);a.Assign(one);b.Assign(two);
            bool rejected=false;try{b.Assign(one);}catch(IOException){rejected=true;}Assert(rejected&&b.Binding==two);a.Dispose();b.Assign(one);Assert(b.Binding==one);
        });
        var writes=new List<int>();WriteResult Write(int o,Position p){writes.Add(o);return new(true,12,0);}
        Test("B then A and readback",()=>{writes.Clear();var r=Placement.Move(target,()=>true,Write,_=>target);Assert(r.Status=="READBACK_MATCH"&&writes.SequenceEqual(new[]{0xB0,0x70}));});
        Test("stale guard never writes",()=>{writes.Clear();var r=Placement.Move(target,()=>false,Write,_=>target);Assert(writes.Count==0&&r.BWrite is null);});
        Test("custom range guard stops A after Home changes",()=>{writes.Clear();var movingHome=home;var r=Placement.Move(target,()=>Placement.InBase(player,movingHome,target,10),(o,p)=>{var written=Write(o,p);movingHome=new(130,5,200);return written;},_=>target);Assert(writes.SequenceEqual(new[]{0xB0})&&r.Status=="CHANGED_AFTER_B_BATCH_STOP");});
        Test("partial B stops before A",()=>{int n=0;var r=Placement.Move(target,()=>true,(_,_)=>{n++;return new(false,4,299);},_=>target);Assert(n==1&&r.Status=="B_WRITE_FAILED_BATCH_STOP");});
        Test("rebind after B stops A",()=>{writes.Clear();int n=0;var r=Placement.Move(target,()=>++n==1,Write,_=>target);Assert(writes.Count==1&&r.Status=="CHANGED_AFTER_B_BATCH_STOP");});
        Test("failed A stops batch",()=>{int n=0;var r=Placement.Move(target,()=>true,(_,_)=>++n==1?new(true,12,0):new(false,0,5),_=>target);Assert(r.Status=="A_WRITE_FAILED_BATCH_STOP");});
        Test("changed after writes reported",()=>{int n=0;var r=Placement.Move(target,()=>++n<3,Write,_=>target);Assert(r.Status=="CHANGED_AFTER_WRITES_BATCH_STOP");});
        Test("overwritten readback is isolated to one mob",()=>{var r=Placement.Move(target,()=>true,Write,_=>player);Assert(r.Status=="READBACK_POSITION_CHANGED"&&!r.StopsBatch);});
        Test("unreadable readback stops batch",()=>{var r=Placement.Move(target,()=>true,Write,_=>null);Assert(r.Status=="READBACK_UNAVAILABLE_BATCH_STOP"&&r.StopsBatch);});
        Test("observed terrain height change preserves XZ result",()=>{var adjusted=target with{Y=target.Y+0.053255f};var r=Placement.Move(target,()=>true,Write,o=>o==0x70?target:adjusted);Assert(r.Status=="READBACK_XZ_MATCH_Y_CHANGED"&&!r.StopsBatch&&r.ReadB==adjusted);});
        Test("actor change during readback stops batch",()=>{int guards=0;var r=Placement.Move(target,()=>++guards<=3,Write,_=>target);Assert(r.Status=="CHANGED_DURING_READBACK_BATCH_STOP"&&r.StopsBatch);});
        Test("invalid coordinate readback stops batch",()=>{var r=Placement.Move(target,()=>true,Write,_=>new(float.NaN,5,200));Assert(r.Status=="READBACK_UNAVAILABLE_BATCH_STOP"&&r.StopsBatch);});
        Test("one position change does not prevent remaining nearby mobs",()=>
        {
            writes.Clear();int processed=0,height=0,changed=0,matched=0;
            foreach(var mode in new[]{0,1,2,2})
            {
                var r=Placement.Move(target,()=>true,Write,o=>mode==0?player:mode==1&&o==0xB0?target with{Y=target.Y+0.053255f}:target);
                processed++;if(r.Status=="READBACK_MATCH")matched++;if(r.Status=="READBACK_XZ_MATCH_Y_CHANGED")height++;if(r.Status=="READBACK_POSITION_CHANGED")changed++;if(r.StopsBatch)break;
            }
            Assert(processed==4&&changed==1&&height==1&&matched==2&&writes.Count==8);
        });
        Test("write and continuity failures still stop remaining mobs",()=>
        {
            foreach(var status in new[]{"B_WRITE_FAILED_BATCH_STOP","A_WRITE_FAILED_BATCH_STOP","CHANGED_AFTER_B_BATCH_STOP","CHANGED_AFTER_WRITES_BATCH_STOP","CHANGED_DURING_READBACK_BATCH_STOP","READBACK_UNAVAILABLE_BATCH_STOP"})Assert(new MoveResult(status).StopsBatch);
        });
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"self-test.json"),JsonSerializer.Serialize(new{passed,names,synthetic=true,live_game_writes=false},new JsonSerializerOptions{WriteIndented=true}));
    }
    public static void Preview()
    {
        using var form=new MainForm(true);form.Show();
        var player=new PlayerInfo(99,0x400000,100,5,200,"SYNTHETIC");var mobs=new List<MobView>();
        for(int i=0;i<12;i++)
        {
            uint key=(uint)(100+i);ulong actor=(ulong)(0x200000+i*0x2000);var a=new Position(102+i,5,202+i);var home=new Position(100+i*6,5,200);
            var m=new Monster(key,2,actor,actor+0x1000,a.X,a.Y,a.Z,true,5,a);
            mobs.Add(new(m,new(1,2,m.NodePtr,key,actor,key,2,3,".?AVCTClientMonster@@"),a,a,home,MobCapture.Distance(Placement.Player(player),home,true)));
        }
        form.Render(new(DateTimeOffset.Now,null,player,mobs,"SENTETİK ÖNİZLEME · oyuna bağlı değil"));form.ShowList();form.VerifyDistanceSorting();Application.DoEvents();
        using var bitmap=new Bitmap(form.Width,form.Height);form.DrawToBitmap(bitmap,new(0,0,bitmap.Width,bitmap.Height));bitmap.Save(Path.Combine(AppContext.BaseDirectory,"ui-preview.png"));
        form.Close();
    }
}
