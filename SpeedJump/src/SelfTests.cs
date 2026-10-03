using System.Collections.Concurrent;
using System.Text.Json;

namespace SpeedJump;

static class SelfTests
{
    public static int Run(string output)
    {
        string root=Files.Root;var passed=new List<string>();
        Files.Root=Path.Combine(Path.GetTempPath(),"4UnitySpeedJump-tests-"+Guid.NewGuid().ToString("N"));
        void Check(bool condition,string name){if(!condition)throw new Exception(name);passed.Add(name);}
        void Reject(Action action,string name){try{action();}catch{passed.Add(name);return;}throw new Exception("Unexpected acceptance: "+name);}
        try
        {
            using var b=new Binary(@"C:\Games\4Unity\TClient.exe");
            var p=Profiles.Load(b);Check(p.Sha256==b.Sha,"current SHA");
            Check(p.JumpWriterRva>0 && p.JumpFieldOffset>=0x100 && p.SpeedFieldOffset>=0x100 && p.OwnerToPlayerOffset>=0x100,"derived offsets");
            Check(p.CtclientgameVtableRva>0 && p.CtclientcharVtableRva>0 && p.CtclientgameVtableRva!=p.CtclientcharVtableRva,"RTTI-derived vtables");
            Check(p.Signatures.Count==5,"five unique code signatures");
            int scans=Profiles.ScanCount;Profiles.Load(b);Check(Profiles.ScanCount==scans,"same SHA profile has zero AOB scans");
            string json=File.ReadAllText(Path.Combine(Files.Profiles,b.Sha+".json"));
            Check(!json.Contains("module_base")&&!json.Contains("owner_address")&&!json.Contains("pid")&&!json.Contains("player_address"),"profile contains no session addresses");
            p.SpeedFieldOffset++;Reject(()=>Profiles.Validate(b,p),"reject corrupt field offset");p.SpeedFieldOffset--;
            p.JumpWriterOriginalBytes="9090909090909090";Reject(()=>Profiles.Validate(b,p),"reject corrupt original bytes");
            p=Profiles.Load(b);
            Reject(()=>b.FindUnique("CC CC CC"),"ambiguous AOB fail closed");
            byte[] changed=b.Data.ToArray();
            var sec=b.Pe.PEHeaders.SectionHeaders.Single(s=>p.Signatures["pair_write"].Rva>=s.VirtualAddress && p.Signatures["pair_write"].Rva<s.VirtualAddress+s.SizeOfRawData);
            changed[sec.PointerToRawData+p.Signatures["pair_write"].Rva-sec.VirtualAddress+27]^=4;
            using(var bad=new Binary(changed))Reject(()=>Profiles.Resolve(bad),"new SHA unequal compare/store displacement rejected");

            var fake=new FakeTarget(p);using(var engine=new Engine(()=>fake))
            {
                engine.Poll();engine.Toggle(false);engine.Toggle(true);Thread.Sleep(110);
                Check(engine.View.Speed&&engine.View.Jump,"independent concurrent toggles");
                Check(fake.SpeedWrites>=2 && fake.JumpWrites>=2,"periodic pins run");
                engine.Toggle(true);int stopped=fake.JumpWrites;Thread.Sleep(65);
                Check(fake.JumpWrites==stopped && !fake.Patched,"jump stop joins before restore");
                Check(engine.View.Speed,"jump OFF preserves Speed");
                engine.Toggle(false);stopped=fake.SpeedWrites;Thread.Sleep(65);Check(fake.SpeedWrites==stopped,"Speed OFF stops without fixed restore write");
                Check(Profiles.ScanCount==scans+1,"toggles perform no AOB scan");
                engine.Toggle(true);
            }
            Check(!fake.Patched && fake.Disposed,"app exit restores patch and disposes session");
            var badTarget=new FakeTarget(p){FailPin=true};using(var engine=new Engine(()=>badTarget)){engine.Poll();engine.Toggle(true);Check(!engine.View.Jump&&!badTarget.Patched,"pin failure rolls back jump patch");}
            var dead=new FakeTarget(p);using(var engine=new Engine(()=>dead)){engine.Poll();engine.Toggle(false);dead.Alive=false;Thread.Sleep(60);engine.Poll();Check(!engine.View.Speed&&!engine.View.Ready,"process exit stops writers and clears session");}
            using(var loop=new PinLoop()){loop.Start(()=>{});Reject(()=>loop.Start(()=>{}),"duplicate writer start rejected");}
            Files.Save(Path.GetFullPath(output),new{status="PASS",tests=passed.Count,passed,live_game_writes=0,limits="Memory effects simulated; no in-game causal test, no process patch, no future-build compatibility claim."});return 0;
        }
        catch(Exception ex){Files.Save(Path.GetFullPath(output),new{status="FAIL",passed,error=ex.ToString(),live_game_writes=0});return 1;}
        finally{Files.Root=root;}
    }
    sealed class FakeTarget(BuildProfile profile):ITarget
    {
        public BuildProfile Profile=>profile;
        public bool Alive{get;set;}=true;
        public bool Patched,Disposed,FailPin;
        public int SpeedWrites,JumpWrites;
        public void Health(){if(!Alive)throw new IOException("dead");}
        public void Pin(bool jump){Health();if(FailPin)throw new IOException("pin failure");if(jump){if(!Patched)throw new Exception("write after restore");Interlocked.Increment(ref JumpWrites);}else Interlocked.Increment(ref SpeedWrites);}
        public void EnablePatch(){Health();if(Patched)throw new IOException("duplicate patch");Patched=true;}
        public void RestorePatch(){Patched=false;}
        public void Dispose(){Disposed=true;}
    }
}
