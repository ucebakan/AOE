using System.Runtime.InteropServices;
using System.Text.Json;
using Iced.Intel;
using UnityTools.Controls;

namespace UnityTools.Collection;

static class Tests
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int NativeTest();
    internal static int Report(string path)
    {
        try { File.WriteAllText(path, JsonSerializer.Serialize(new { status="PASS", checks=Run(), game_memory_writes=0, feature_activation=0 }, new JsonSerializerOptions { WriteIndented=true })); return 0; }
        catch(Exception ex) { File.WriteAllText(path, JsonSerializer.Serialize(new { status="FAIL", error=ex.ToString(), game_memory_writes=0, feature_activation=0 })); return 1; }
    }
    internal static string[] Run()
    {
        var checks=new List<string>();
        void Check(bool ok,string text) { if(!ok)throw new IOException(text);checks.Add(text); }
        void Reject(Action action,string text) { bool rejected=false;try{action();}catch{rejected=true;}Check(rejected,text); }
        Check(Marshal.SizeOf<CodeGuard>()==136&&Marshal.SizeOf<NativeLayout>()==764&&Marshal.SizeOf<NativeRequest>()==96&&Marshal.SizeOf<NativeBatch>()==6952,"Managed bridge ABI matches native layout");
        Check(NativeModules.Function<NativeTest>("UnityCollection.dll","BatchSize")()==6952,"Native batch ABI 6952 bytes");
        Check(NativeModules.Function<NativeTest>("UnityCollection.dll","TestBridge")()==1,"Native 64 group, duplicate, partial failure, mixed session, host identity, cancellation and deadline guards");
        Check(NativeModules.Function<NativeTest>("UnityCollection.dll","TestGuards")()==1,"Native recovered fields, live fingerprint, stale membership, parent relationship and real 9909 buff guards");
        Check(NativeModules.Function<NativeTest>("UnityCollection.dll","TestCleanup")()==1,"Native cancellation retains mapping until in-flight callback finishes");
        var disk=File.ReadAllBytes(Multikill.Profile.GamePath);
        using var b=new PlayerXYZ.Binary(disk);
        var p=Profiles.Load(disk,true);int scans=Profiles.ScanCount;
        Profiles.Load(disk);Check(Profiles.Cached&&Profiles.ScanCount==scans,"Same SHA validated profile reused without scan");
        Check(p.SessionOffset==0x2358&&p.ActionOffset==0x7dc&&p.Mob.RegistryOffset==0x1160&&p.MaintainOffset==0x1248,"Current build sender, world, death and registry fields derived semantically");
        Reject(()=>Profiles.Validate(b,p with { ActionOffset=p.ActionOffset+1 }),"Corrupt profile field rejected");
        var path=Path.Combine(Profiles.DirectoryPath,p.Sha+".json");File.WriteAllText(path,JsonSerializer.Serialize(p with { SessionOffset=123 }));Profiles.Load(disk);
        Check(Profiles.ScanCount==scans+1,"Altered cached profile recovered through fresh unique scan");
        int Raw(int rva){var s=b.Pe.PEHeaders.SectionHeaders.Single(s=>rva>=s.VirtualAddress&&rva<s.VirtualAddress+s.SizeOfRawData);return s.PointerToRawData+rva-s.VirtualAddress;}
        int sender=p.Codes["sender"].Rva;
        var section=b.Pe.PEHeaders.SectionHeaders.Single(s=>sender>=s.VirtualAddress&&sender<s.VirtualAddress+s.SizeOfRawData);
        int cave=section.VirtualAddress+Math.Min(section.VirtualSize,section.SizeOfRawData)-1024;
        var copy=disk.ToArray();p.Codes["sender"].Bytes.CopyTo(copy,Raw(cave));
        using(var duplicate=new PlayerXYZ.Binary(copy))Reject(()=>Profiles.Resolve(duplicate),"Ambiguous sender AOB rejected");
        copy=disk.ToArray();copy[Raw(sender)]^=1;using(var missing=new PlayerXYZ.Binary(copy))Reject(()=>Profiles.Resolve(missing),"Missing sender AOB has no old RVA fallback");
        copy=disk.ToArray();p.Codes["sender"].Bytes.CopyTo(copy,Raw(cave));
        using(var image=new RecoveryImage(disk))
        {
            foreach(var call in image.Decode(sender,p.Codes["sender"].Bytes.Length).Where(i=>i.Mnemonic==Mnemonic.Call))
            {
                int moved=cave+(int)call.IP-sender;BitConverter.GetBytes(checked((int)call.NearBranchTarget-(moved+call.Length))).CopyTo(copy,Raw(moved+1));
            }
            var callSite=image.Decode(p.Anchors["dispatch"].Rva,20).Single(i=>i.Mnemonic==Mnemonic.Call);
            BitConverter.GetBytes(cave-((int)callSite.IP+callSite.Length)).CopyTo(copy,Raw((int)callSite.IP+1));
        }
        copy[Raw(sender)]^=1;
        var recovered=Profiles.Load(copy);Check(recovered.Codes["sender"].Rva==cave&&recovered.Sha!=p.Sha&&!Profiles.Cached,"New SHA relocates unique sender and revalidates manual call link");
        copy[Raw(cave)+24]^=1;using(var bad=new PlayerXYZ.Binary(copy))Reject(()=>Profiles.Resolve(bad),"Changed native sender schema rejected");
        var observer=new DeathObserver();
        Row R(uint id,bool dead,long actor=0)=>new(id,actor==0?1000+id:actor,2000+id,dead);
        observer.Observe([R(99,true)]);Check(observer.TakeBatch().Count==0,"Existing corpses skipped on activation");
        observer.Observe(Enumerable.Range(1,150).Select(i=>R((uint)i,false)));observer.Observe(Enumerable.Range(1,150).Select(i=>R((uint)i,true)));
        Check(new[]{observer.TakeBatch().Count,observer.TakeBatch().Count,observer.TakeBatch().Count}.SequenceEqual(new[]{64,64,22}),"150 deaths sent as 64/64/22 without one-mob wait");
        observer.Observe(Enumerable.Range(1,150).Select(i=>R((uint)i,true)));Check(!observer.HasPending,"Repeated dead snapshots never duplicate loot");
        observer.Observe([R(1,false)]);observer.Observe([R(1,true)]);observer.Observe([R(1,true,9999)]);Check(!observer.HasPending,"Recycled actor invalidates pending death");
        observer.Observe([R(2,false)]);observer.Observe([R(2,true)]);observer.Observe([]);Check(!observer.HasPending,"Despawn invalidates pending death");
        observer.Observe([R(3,false)]);observer.Observe([R(3,true)]);observer.Observe([R(3,false)]);observer.Observe([R(3,true)]);Check(observer.TakeBatch().Count==1,"Respawn creates one new death generation");
        Task.Run(() => ControllerTests(Check)).GetAwaiter().GetResult();
        return checks.ToArray();
    }
    sealed class FakeEngine : IEngine
    {
        internal volatile bool Dead,Disposed,Cancelled,AllowCleanup=true;
        internal readonly List<int> Groups=[];
        internal readonly ManualResetEventSlim Sent=new(false);
        public string Identity=>"fixture";
        public List<Row> ReadWorld()=>Enumerable.Range(1,150).Select(i=>new Row((uint)i,1000+i,2000+i,Dead)).ToList();
        public DispatchResult Dispatch(IReadOnlyList<Row> rows){lock(Groups)Groups.Add(rows.Count);if(Groups.Sum()==150)Sent.Set();return new(rows.Count,0,false);}
        public void Cancel()=>Cancelled=true;
        public bool Cleanup()=>AllowCleanup;
        public void Dispose()=>Disposed=true;
    }
    static async Task ControllerTests(Action<bool,string> check)
    {
        bool blocked=OperationGate.Blocked,continuous=OperationGate.StopContinuous;
        try
        {
            OperationGate.Blocked=false;
            var e=new FakeEngine();using(var controller=new Controller(_=>e))
            {
                await controller.PrepareAsync();check(controller.Ready&&!controller.Active&&e.Groups.Count==0,"Prepare/startup validation cannot activate automation");
                await controller.ToggleAsync();e.Dead=true;
                check(await Task.Run(()=>e.Sent.Wait(5000)),"Automatic deaths dispatch without target selection");
                check(e.Groups.SequenceEqual(new[]{64,64,22}),"Controller drains batch backlog immediately");
                await Task.Delay(100);check(e.Groups.Sum()==150,"Worker deduplicates recurring dead registry");
                check(await controller.StopAsync()&&!controller.Active&&e.Disposed,"Button stop cancels and disposes session after clean callback");
            }
            e=new FakeEngine();using(var controller=new Controller(_=>e))
            {
                await controller.PrepareAsync();await controller.ToggleAsync();OperationGate.StopContinuous=true;
                for(int i=0;i<100&&controller.Active;i++)await Task.Delay(10);
                check(!controller.Active&&e.Cancelled&&e.Disposed&&e.Groups.Count==0,"SafeMode cancels continuous automation without future sends");
                OperationGate.Blocked=false;
            }
            e=new FakeEngine {AllowCleanup=false};using(var controller=new Controller(_=>e))
            {
                await controller.PrepareAsync();await controller.ToggleAsync();controller.Quiesce();
                await Task.Delay(100);check(controller.Active&&!controller.Ready&&!e.Disposed&&e.Groups.Count==0,"Pending native cleanup retains session and blocks new sends");
                e.AllowCleanup=true;check(await controller.StopAsync()&&e.Disposed&&!controller.Active,"Pending cleanup may finish before serialized shutdown advances");
            }
        }
        finally { OperationGate.Blocked=blocked;OperationGate.StopContinuous=continuous; }
    }
}
