using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using UnityMonsterList;

namespace MobTP;

record MobView(Monster Mob,Identity Identity,Position A,Position B,Position? Home,double? PlayerHomeXZ);
record World(DateTimeOffset Time,Snapshot? Source,PlayerInfo? Player,List<MobView> Mobs,string Status);
record WriteResult(bool Success,int Bytes,int Error);
record MoveResult(string Status,WriteResult? BWrite=null,WriteResult? AWrite=null,Position? ReadA=null,Position? ReadB=null);
record PlannedMob(MobView Mob,Position Target,bool Eligible);

static class Placement
{
    public static PlannedMob[] Plan(World world,double spread)
    {
        if(world.Player is null)return [];
        var player=Player(world.Player);
        var candidates=world.Mobs.Where(m=>Usable(m.Home)&&MobCapture.Distance(player,m.Home,true)<=HomeRadius).OrderBy(m=>m.Mob.EntityId).ToArray();
        return candidates.Select((m,i)=>{var t=Ring(player,i,candidates.Length,spread);return new PlannedMob(m,t,InBase(player,m.Home,t));}).ToArray();
    }
    public const double HomeRadius=50;
    public static bool Usable(Position? p)=>p is not null && p!=new Position(0,0,0) && float.IsFinite(p.X)&&float.IsFinite(p.Y)&&float.IsFinite(p.Z)&&Math.Abs(p.X)<1e6&&Math.Abs(p.Y)<1e6&&Math.Abs(p.Z)<1e6;
    public static Position Player(PlayerInfo p)=>new(p.X,p.Y,p.Z);
    public static Position Ring(Position player,int index,int count,double spread)
    {
        if(count<1||index<0||index>=count||!double.IsFinite(spread)||spread<1||spread>10)throw new ArgumentOutOfRangeException(nameof(spread));
        double angle=2*Math.PI*index/count;
        return new((float)(player.X+spread*Math.Cos(angle)),player.Y,(float)(player.Z+spread*Math.Sin(angle)));
    }
    public static bool InBase(Position? player,Position? home,Position? target)=>Usable(player)&&Usable(home)&&Usable(target)&&
        MobCapture.Distance(player,home,true)<=HomeRadius&&MobCapture.Distance(target,home,true)<=HomeRadius&&MobCapture.Distance(player,target,true)>=0.9;
    public static MoveResult Move(Position target,Func<bool> guard,Func<int,Position,WriteResult> write,Func<int,Position?> read)
    {
        if(!Usable(target)||!guard())return new("STALE_BEFORE_WRITE");
        var b=write(0xB0,target);
        if(!b.Success||b.Bytes!=12)return new("B_WRITE_FAILED_BATCH_STOP",b);
        if(!guard())return new("CHANGED_AFTER_B_BATCH_STOP",b);
        var a=write(0x70,target);
        if(!a.Success||a.Bytes!=12)return new("A_WRITE_FAILED_BATCH_STOP",b,a);
        if(!guard())return new("CHANGED_AFTER_WRITES_BATCH_STOP",b,a);
        var ra=read(0x70);var rb=read(0xB0);
        bool match=ra is not null&&rb is not null&&MobCapture.Distance(ra,target)<=0.01&&MobCapture.Distance(rb,target)<=0.01;
        return new(match?"READBACK_MATCH":"READBACK_MISMATCH_BATCH_STOP",b,a,ra,rb);
    }
}

sealed class Session : IDisposable
{
    readonly Process process;
    readonly IntPtr handle;
    readonly ProfileBundle bundle;
    readonly ulong mb,context,head;
    readonly long start;
    public int Pid => process.Id;
    public MobProfile Profile=>bundle.Profile;
    public string ProfileStatus=>bundle.Origin+" · "+Profile.Player.Sha256[..12];
    public Snapshot Source {get;}
    public Session(Snapshot? source=null,bool write=false)
    {
        if(write&&source is null)throw new IOException("Write session requires fresh snapshot");
        process=Process.GetProcessById(source?.Pid??SelectPid());
        try
        {
            start=process.StartTime.ToUniversalTime().Ticks;
            if(source is not null&&process.StartTime.ToUniversalTime()>source.Timestamp.UtcDateTime)throw new IOException("Oyun yeniden başladı.");
            handle=N.OpenProcess(write?0x1038u:0x1010u,false,process.Id);
            if(handle==IntPtr.Zero)throw new IOException("Oyuna erişilemedi; yönetici olarak açın. Kod: "+Marshal.GetLastWin32Error());
            string path=Reader.PathOf(handle);
            if(!string.Equals(Path.GetFullPath(path),@"C:\Games\4Unity\TClient.exe",StringComparison.OrdinalIgnoreCase))throw new IOException("Beklenmeyen oyun yolu.");
            bundle=ProfileStore.Load(path);mb=(ulong)Reader.Module(process.Id,path);
            if(process.MainModule!.ModuleMemorySize!=Profile.Player.ImageSize)throw new IOException("Loaded image size mismatch");
            bundle.ValidateLive(Read,mb);
            context=Q(mb+(uint)Profile.RootRva);head=Q(context+(uint)Profile.RegistryOffset);
            if(!Rpm.P(context)||!Rpm.P(head)||Q(context)!=mb+(uint)Profile.Player.CtclientgameVtableRva)throw new IOException("Oyuncu/harita henüz hazır değil.");
            if(source is not null&&(source.Sha256!=Profile.Player.Sha256||Parse(source.ModuleBase!)!=mb||Parse(source.Context!)!=context||Parse(source.Tree!)!=head))throw new IOException("Snapshot oturumu/registry değişti.");
            var player=Player()??throw new IOException("Oyuncu koordinatı doğrulanamadı.");
            Source=new(DateTimeOffset.Now,"REGISTRY_CANDIDATE",ProfileStatus,process.Id,Profile.Player.Sha256,$"0x{mb:X}",$"0x{context:X}",$"0x{head:X}",[],"PROFILE_RESOLVED",player,"MobTP profili doğrulandı");
        }
        catch{Dispose();throw;}
    }
    static int SelectPid()
    {
        var choices=new List<int>();
        foreach(var p in Process.GetProcessesByName("TClient"))using(p)if(p.MainWindowHandle!=IntPtr.Zero)choices.Add(p.Id);
        if(choices.Count!=1)throw new IOException(choices.Count==0?"4Unity bekleniyor.":"Birden fazla oyun istemcisi açık.");
        return choices[0];
    }
    static ulong Parse(string s)=>Convert.ToUInt64(s.Replace("0x",""),16);
    public byte[] Read(ulong a,int n)=>Rpm.P(a)?new Rpm(handle).Bytes((long)a,n)??throw new IOException("Bellek okunamadı."):throw new IOException("Invalid pointer");
    ulong Q(ulong a)=>BitConverter.ToUInt64(Read(a,8));
    uint D(ulong a)=>BitConverter.ToUInt32(Read(a,4));
    byte B(ulong a)=>Read(a,1)[0];
    public bool Alive()=>!process.HasExited&&process.StartTime.ToUniversalTime().Ticks==start&&Q(mb+(uint)Profile.RootRva)==context&&Q(context+(uint)Profile.RegistryOffset)==head&&Q(context)==mb+(uint)Profile.Player.CtclientgameVtableRva;
    public PlayerInfo? Player()
    {
        if(!Alive())return null;
        bundle.ValidateLive(Read,mb);
        ulong actor=Q(context+(uint)Profile.Player.OwnerToPlayerOffset);
        if(!Rpm.P(actor)||Q(actor)!=mb+(uint)Profile.Player.CtclientcharVtableRva)return null;
        uint id=D(actor+(uint)Profile.ActorIdOffset);
        if(id==0||B(actor+(uint)Profile.ActorTypeOffset)!=1)return null;
        var a=XYZ(actor,Profile.Player.CoordinateA[0]);var b=XYZ(actor,Profile.Player.CoordinateB[0]);
        if(a is null||Q(context+(uint)Profile.Player.OwnerToPlayerOffset)!=actor||D(actor+(uint)Profile.ActorIdOffset)!=id)return null;
        return new(id,actor,a.X,a.Y,a.Z,"SHA/AOB/live RTTI",b);
    }
    public List<Monster> Enumerate()
    {
        if(!Alive()||B(head+0x19)!=1)throw new IOException("Registry sentinel invalid");
        ulong root=Q(head+8),expectedCount=Q(context+(uint)Profile.RegistryOffset+8);
        if(expectedCount>4096)throw new IOException("Registry limit");
        var stack=new Stack<(ulong Node,ulong Parent,long Low,long High)>();stack.Push((root,head,-1,(long)uint.MaxValue+1));
        var seen=new HashSet<ulong>();var result=new List<Monster>();
        while(stack.Count>0)
        {
            var (node,parent,low,high)=stack.Pop();if(node==head)continue;
            if(!Rpm.P(node)||!seen.Add(node)||seen.Count>4096||B(node+0x19)!=0||Q(node+8)!=parent)throw new IOException("Registry geçici olarak tutarsız.");
            uint key=D(node+0x20);ulong actor=Q(node+0x28);
            if(key<=low||key>=high)throw new IOException("Registry sırası geçersiz");
            var a=XYZ(actor,Profile.Player.CoordinateA[0]);var b=XYZ(actor,Profile.Player.CoordinateB[0]);
            result.Add(new(key,2,actor,node,a?.X??0,a?.Y??0,a?.Z??0,a is not null,null,b));
            stack.Push((Q(node),node,low,key));stack.Push((Q(node+0x10),node,key,high));
        }
        if((ulong)seen.Count!=expectedCount||Q(head+8)!=root||Q(context+(uint)Profile.RegistryOffset+8)!=expectedCount||!Alive())throw new IOException("Registry değişti; yeniden okunacak.");
        return result.OrderBy(x=>x.EntityId).ToList();
    }
    public sealed class Binding(Session s,Monster m)
    {
        Identity? expected;
        public Identity Pin(){var c=Check();if(!c.Membership||c.Identity is null)throw new IOException(c.Status);return expected=c.Identity;}
        public IdentityCheck Check()
        {
            try
            {
                if(!s.Alive()||s.B(s.head+0x19)!=1)throw new IOException("Session changed");
                ulong node=s.Q(s.head+8),parent=s.head;var seen=new HashSet<ulong>();long low=-1,high=(long)uint.MaxValue+1;
                bool found=false;
                while(node!=s.head&&seen.Count<128)
                {
                    if(!Rpm.P(node)||!seen.Add(node)||s.B(node+0x19)!=0||s.Q(node+8)!=parent)break;
                    uint key=s.D(node+0x20);if(key<=low||key>=high)break;
                    if(key==m.EntityId){found=node==m.NodePtr&&s.Q(node+0x28)==m.ActorPtr;break;}
                    parent=node;if(m.EntityId<key){high=key;node=s.Q(node);}else{low=key;node=s.Q(node+0x10);}
                }
                if(!found||s.D(m.ActorPtr+(uint)s.Profile.ActorIdOffset)!=m.EntityId||s.B(m.ActorPtr+(uint)s.Profile.ActorTypeOffset)!=2||s.Q(m.ActorPtr)!=s.mb+(uint)s.Profile.MonsterVtableRva)throw new IOException("Mob binding changed");
                var id=new Identity(s.context,s.head,m.NodePtr,m.EntityId,m.ActorPtr,m.EntityId,2,s.mb+(uint)s.Profile.MonsterVtableRva,".?AVCTClientMonster@@");
                if(expected is not null&&id!=expected)throw new IOException("Mob continuity changed");
                return new(true,id,"MATCH");
            }
            catch(Exception ex){return new(false,null,ex.Message);}
        }
    }
    public Binding Identity(Monster m)=>new(this,m);
    public Position? XYZ(ulong actor,int offset)
    {
        try{var b=Read(actor+(uint)offset,12);var p=new Position(BitConverter.ToSingle(b),BitConverter.ToSingle(b,4),BitConverter.ToSingle(b,8));return Placement.Usable(p)?p:null;}catch(IOException){return null;}
    }
    public MobView? Inspect(Monster m,PlayerInfo player)
    {
        try
        {
            var id=Identity(m);var expected=id.Pin();
            var a=XYZ(m.ActorPtr,Profile.Player.CoordinateA[0]);var b=XYZ(m.ActorPtr,Profile.Player.CoordinateB[0]);var home=XYZ(m.ActorPtr,Profile.HomeOffset);var after=id.Check();
            if(a is null||b is null||!after.Membership||after.Identity!=expected||!Alive())return null;
            return new(m,expected,a,b,home,MobCapture.Distance(Placement.Player(player),home,true));
        }
        catch(IOException){return null;}
    }
    // Logical channel identifiers stay stable; resolved physical offsets come only from the profile.
    int CoordinateOffset(int channel)=>channel switch{0x70=>Profile.Player.CoordinateA[0],0xB0=>Profile.Player.CoordinateB[0],_=>throw new ArgumentException("Invalid coordinate channel")};
    public Position? ReadCoordinate(ulong actor,int channel)=>XYZ(actor,CoordinateOffset(channel));
    public WriteResult Write(ulong actor,int channel,Position p)
    {
        int offset=CoordinateOffset(channel);
        if(!Placement.Usable(p))throw new ArgumentException("Invalid coordinate write");
        byte[] bytes=new byte[12];BitConverter.GetBytes(p.X).CopyTo(bytes,0);BitConverter.GetBytes(p.Y).CopyTo(bytes,4);BitConverter.GetBytes(p.Z).CopyTo(bytes,8);
        bool ok=WriteProcessMemory(handle,(IntPtr)(long)(actor+(uint)offset),bytes,12,out nuint got);
        return new(ok,(int)got,ok?0:Marshal.GetLastWin32Error());
    }
    [DllImport("kernel32.dll",SetLastError=true)]static extern bool WriteProcessMemory(IntPtr handle,IntPtr address,byte[] bytes,nuint length,out nuint written);
    public void Dispose(){if(handle!=IntPtr.Zero)N.CloseHandle(handle);process.Dispose();}
}

static class Engine
{
    public static string LogRoot=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"MobTP","Logs");
    static void CheckClientCount()
    {
        int visible=0;
        foreach(var p in Process.GetProcessesByName("TClient"))using(p){if(p.MainWindowHandle!=IntPtr.Zero)visible++;}
        if(visible>1)throw new IOException("Birden fazla oyun istemcisi açık. MobTP için tek istemci bırakın.");
    }
    public static World Capture()
    {
        try
        {
            CheckClientCount();using var session=new Session();var s=session.Source;var player=session.Player()??throw new IOException("Oyuncu hazır değil.");
            var mobs=new List<MobView>();
            foreach(var m in session.Enumerate()){var v=session.Inspect(m,player);if(v is not null)mobs.Add(v);}
            if(!session.Alive())throw new IOException("Harita/oturum değişiyor.");
            return new(DateTimeOffset.Now,s,player,mobs,$"Hazır · {session.ProfileStatus}");
        }
        catch(Exception ex){return new(DateTimeOffset.Now,null,null,[],ex.Message);}
    }
    public static (string Message,string Log) Teleport(double spread,CancellationToken cancel,int? hotkeyPid=null)
    {
        Directory.CreateDirectory(LogRoot);string path=Path.Combine(LogRoot,$"tp-{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}-{Guid.NewGuid():N}.jsonl");
        using var log=new StreamWriter(path){AutoFlush=true};
        void Save(object value)=>log.WriteLine(JsonSerializer.Serialize(value));
        int matched=0,skipped=0;string finish="BATCH_FINISHED";
        try
        {
            // Fresh read-only snapshot; no cached map members enter a batch.
            var world=Capture();if(world.Source is null||world.Player is null)throw new IOException(world.Status);
            if(hotkeyPid is not null&&(world.Source.Pid!=hotkeyPid||!HotkeyRegistration.GameForeground(hotkeyPid)))throw new IOException("Oyun odağı değişti; kısayol işlemi iptal edildi.");
            var player=world.Player;var origin=Placement.Player(player);
            var plan=Placement.Plan(world,spread);var eligible=plan.Where(p=>p.Eligible).ToArray();
            Save(new{kind="batch",world.Time,pid=world.Source.Pid,world.Source.Sha256,player,home_radius=50,spread,count=eligible.Length,home_count=plan.Length,registry_count=world.Mobs.Count,trigger=hotkeyPid is null?"button":"game_hotkey",policy="Resolved B then A; Home untouched; fresh guards; readback is not server acceptance"});
            if(eligible.Length==0){Save(new{kind="summary",matched=0,skipped=0,status="NO_ELIGIBLE_MOBS"});return("Home sınırı içinde uygun mob yok.",path);}
            cancel.ThrowIfCancellationRequested();
            using var session=new Session(world.Source,true);
            Save(new{kind="resolved_layout",session.Profile.RootRva,session.Profile.RegistryOffset,session.Profile.HomeOffset,session.Profile.ActorIdOffset,session.Profile.ActorTypeOffset,session.Profile.Player.CoordinateA,session.Profile.Player.CoordinateB});
            for(int i=0;i<eligible.Length;i++)
            {
                cancel.ThrowIfCancellationRequested();var old=eligible[i].Mob;
                var freshPlayer=session.Player();
                if(freshPlayer is null||freshPlayer.ActorPtr!=player.ActorPtr||freshPlayer.EntityId!=player.EntityId||MobCapture.Distance(origin,Placement.Player(freshPlayer))>0.25)throw new IOException("Oyuncu/harita değişti; işlem durdu.");
                var current=session.Inspect(old.Mob,freshPlayer);
                if(current is null||current.Identity!=old.Identity){Save(new{kind="skip",id=old.Mob.EntityId,status="STALE_MEMBER"});skipped++;continue;}
                var target=eligible[i].Target;
                if(!Placement.InBase(Placement.Player(freshPlayer),current.Home,target)){Save(new{kind="skip",id=old.Mob.EntityId,status="OUTSIDE_BASE_OR_HOME_UNAVAILABLE",current.Home,target});skipped++;continue;}
                var identity=session.Identity(old.Mob);var expected=identity.Pin();
                if(expected!=old.Identity){skipped++;continue;}
                bool Guard()
                {
                    if(UnityTools.Controls.OperationGate.Blocked||cancel.IsCancellationRequested||hotkeyPid is not null&&!HotkeyRegistration.GameForeground(hotkeyPid)||!session.Alive())return false;
                    var id=identity.Check();if(!id.Membership||id.Identity!=expected)return false;
                    var p=session.Player();var h=session.XYZ(old.Mob.ActorPtr,session.Profile.HomeOffset);
                    return p is not null&&p.ActorPtr==player.ActorPtr&&p.EntityId==player.EntityId&&MobCapture.Distance(origin,Placement.Player(p))<=0.25&&h==current.Home&&Placement.InBase(Placement.Player(p),h,target);
                }
                Save(new{kind="intent",id=old.Mob.EntityId,old.Identity,current.A,current.B,current.Home,target});
                var result=Placement.Move(target,Guard,(offset,value)=>session.Write(old.Mob.ActorPtr,offset,value),offset=>session.ReadCoordinate(old.Mob.ActorPtr,offset));
                Save(new{kind="result",id=old.Mob.EntityId,result});
                if(result.Status=="READBACK_MATCH")matched++;
                else if(result.BWrite is not null){finish=result.Status;break;}
                else skipped++;
            }
        }
        catch(Exception ex){finish=ex is OperationCanceledException?"İşlem iptal edildi":ex.Message;Save(new{kind="error",message=finish});}
        Save(new{kind="summary",matched,skipped,status=finish});
        return($"{matched} mob yazımı okuma ile eşleşti · {skipped} atlandı. {(finish=="BATCH_FINISHED"?"":finish)}",path);
    }
}
