using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace UnityTools;
static class RecoveryTests
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl,CharSet=CharSet.Ansi)]delegate int Counter([MarshalAs(UnmanagedType.LPStr)]string sha,[Out]byte[] message,int capacity);
    internal static int Run(string output)
    {
        var checks=new List<object>();int failed=0;
        void Test(string name,Func<object> run){try{checks.Add(new{name,status="PASS",detail=run()});}catch(Exception ex){failed++;checks.Add(new{name,status="FAIL",error=ex.ToString()});}}
        byte[] disk=File.ReadAllBytes(Multikill.Profile.GamePath);string sha=Convert.ToHexString(SHA256.HashData(disk));
        void Require(bool ok,string message){if(!ok)throw new Exception(message);}
        void Reject(Action action){bool rejected=false;try{action();}catch{rejected=true;}Require(rejected,"Invalid recovery candidate was accepted");}
        Test("XYZ automatic recovery and SHA cache",()=>{using var b=new PlayerXYZ.Binary(disk);var p=PlayerXYZ.Profiles.Load(b);int scans=PlayerXYZ.Profiles.ScanCount;PlayerXYZ.Profiles.Load(b);Require(scans==PlayerXYZ.Profiles.ScanCount,"Same SHA rescanned");return new{p.RootRva,p.OwnerToPlayerOffset,p.CoordinateA,p.CoordinateB,p.CtclientcharVtableRva};});
        Test("Speed/Jump automatic recovery and SHA cache",()=>{using var b=new SpeedJump.Binary(disk);var p=SpeedJump.Profiles.Load(b);int scans=SpeedJump.Profiles.ScanCount;SpeedJump.Profiles.Load(b);Require(scans==SpeedJump.Profiles.ScanCount,"Same SHA rescanned");return new{p.RootRva,p.OwnerToPlayerOffset,p.SpeedFieldOffset,p.JumpFieldOffset,p.JumpWriterRva};});
        Test("Recovered root rebuilds current pointers under ASLR",()=>{
            using var b=new PlayerXYZ.Binary(disk);var p=PlayerXYZ.Profiles.Load(b);int getter=p.Signatures["root"].Rva;
            foreach(long module in new[]{0x140000000L,0x7FF700000000L}){
                long owner=module+0x20000000,player=owner+0x20000;
                var memory=new Dictionary<long,byte[]>{[module+getter]=b.At(getter,8),[module+p.RootRva]=BitConverter.GetBytes(owner),[owner]=BitConverter.GetBytes(module+p.CtclientgameVtableRva),[owner+p.OwnerToPlayerOffset]=BitConverter.GetBytes(player),[player]=BitConverter.GetBytes(module+p.CtclientcharVtableRva)};
                (long,long) Resolve()=>UnityTools.Controls.KnownPlayerPath.Resolve(module,(address,length)=>memory[address],p.CtclientgameVtableRva,p.CtclientcharVtableRva,p.OwnerToPlayerOffset,getter,p.RootRva);
                Require(Resolve()==(owner,player),"Current profile did not relocate");
                memory[module+p.RootRva]=new byte[8];Reject(()=>Resolve());
                memory[module+p.RootRva]=BitConverter.GetBytes(owner);memory[module+getter][3]^=1;Reject(()=>Resolve());
            }return "Both bases rebuilt; missing root and changed live getter rejected";
        });
        Test("Invisible/Aggro automatic fields and exact writer bytes",()=>{InvisibleAggro.Profile.DirectoryPath=Path.Combine(Program.DataRoot,"Invisible","profiles");InvisibleAggro.Profile.VerifyDisk(force:true);Require(InvisibleAggro.Profile.ActorType+1==InvisibleAggro.Profile.Stealth,"Type/stealth mismatch");return new{InvisibleAggro.Profile.Context,InvisibleAggro.Profile.Player,InvisibleAggro.Profile.ActorId,InvisibleAggro.Profile.ActorType,InvisibleAggro.Profile.Stealth,InvisibleAggro.Profile.Visual};});
        Test("MobTP automatic recovery and SHA cache",()=>{using var b=new global::PlayerXYZ.Binary(disk);var bundle=MobTP.ProfileStore.Load(Multikill.Profile.GamePath);int scans=MobTP.ProfileStore.ScanCount;MobTP.ProfileStore.Load(Multikill.Profile.GamePath);Require(scans==MobTP.ProfileStore.ScanCount,"Same SHA rescanned");var p=bundle.Profile;return new{p.RootRva,p.ActorIdOffset,p.ActorTypeOffset,p.HomeOffset,p.ActorSize};});
        Test("AOE automatic profile, loader and live gate",()=>{AoeProfileRecovery.Ensure();var path=Path.Combine(Program.DataRoot,"AOE","profiles",sha+".json");using var json=JsonDocument.Parse(File.ReadAllText(path));Require(json.RootElement.GetProperty("liveValidationRequired").GetBoolean(),"Live validation removed");AoeProfileRecovery.Ensure();return path;});
        Test("Counter/Exit automatic disk recovery",()=>{byte[] message=new byte[2048];int ok=NativeModules.Function<Counter>("UnityCounter.dll","ResolveCounterDisk")(sha,message,message.Length);string detail=Encoding.UTF8.GetString(message).TrimEnd('\0');Require(ok==1,detail);return detail;});
        Test("Counter/Exit click uses current recovered profile",()=>{byte[] message=new byte[2048];int ok=NativeModules.Function<Counter>("UnityCounter.dll","RunCounterExitFixtures")(sha,message,message.Length);string detail=Encoding.UTF8.GetString(message).TrimEnd('\0');Require(ok==1,detail);return detail;});
        Test("Multikill semantic recovery",()=>Multikill.BuildRecovery.Resolve(disk));
        Test("Salesman direct opening and patch recovery",()=>Salesman.Tests.Run());
        Test("Collection grouped automation and patch recovery",()=>Collection.Tests.Run());
        Test("AOE/Multikill same-session ownership and native bridge",()=>Multikill.CompatibilityTests.Run(AoePatchCompatibility.TestProof));
        Test("Changed SHA extracts new fields; inconsistent layout rejected",()=>{
            using var b=new PlayerXYZ.Binary(disk);var p=PlayerXYZ.Profiles.Resolve(b);var data=disk.ToArray();
            int Raw(int rva){var s=b.Pe.PEHeaders.SectionHeaders.Single(s=>rva>=s.VirtualAddress&&rva<s.VirtualAddress+s.SizeOfRawData);return s.PointerToRawData+rva-s.VirtualAddress;}
            int a=p.Signatures["coords_a"].Rva,c=p.Signatures["coords_b"].Rva,o=p.Signatures["owner"].Rva;
            foreach(int n in new[]{4,10,16})data[Raw(a+n)]+=4;
            foreach(int n in new[]{10,30,57})BitConverter.GetBytes(b.I32(c+n)+16).CopyTo(data,Raw(c+n));
            foreach(int n in new[]{21,37})BitConverter.GetBytes(b.I32(o+n)+8).CopyTo(data,Raw(o+n));
            using(var changed=new PlayerXYZ.Binary(data)){var q=PlayerXYZ.Profiles.Resolve(changed);Require(q.OwnerToPlayerOffset==p.OwnerToPlayerOffset+8&&q.CoordinateB[0]==p.CoordinateB[0]+16,"Operands not re-derived");}
            data[Raw(a+10)]++;using(var broken=new PlayerXYZ.Binary(data))Reject(()=>PlayerXYZ.Profiles.Resolve(broken));
            p.RootRva+=8;Reject(()=>PlayerXYZ.Profiles.Validate(b,p));return "Changed offsets recovered; inconsistent triplet and root rejected";});
        Test("Missing and duplicate anchor rejection",()=>{using var b=new PlayerXYZ.Binary(disk);var p=PlayerXYZ.Profiles.Resolve(b);var root=p.Signatures["root"];var s=b.Pe.PEHeaders.SectionHeaders.Single(s=>root.Rva>=s.VirtualAddress&&root.Rva<s.VirtualAddress+s.SizeOfRawData);int pos=s.PointerToRawData+root.Rva-s.VirtualAddress;var data=disk.ToArray();data[pos]^=1;using(var missing=new PlayerXYZ.Binary(data))Reject(()=>PlayerXYZ.Profiles.Resolve(missing));data=disk.ToArray();b.At(root.Rva,PlayerXYZ.Binary.Pattern(root.Pattern).Length).CopyTo(data,s.PointerToRawData+s.SizeOfRawData-512);using(var duplicate=new PlayerXYZ.Binary(data))Reject(()=>PlayerXYZ.Profiles.Resolve(duplicate));return "Missing and ambiguous roots rejected";});
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);File.WriteAllText(output,JsonSerializer.Serialize(new{status=failed==0?"PASS":"FAIL",sha,failed,checks,game_memory_writes=0,feature_activation=0},new JsonSerializerOptions{WriteIndented=true}));return failed==0?0:1;
    }
}
