using System.Text.Json;

namespace UnityMonsterList;

static class LearnedCenterTests
{
    public static void Run()
    {
        int passed=0;Position goal=new(100,5,200),near=new(100.2f,5,200),far=new(130,5,200),player=new(103,5,204);
        void Check(bool value){if(!value)throw new Exception("Learned center test failed");passed++;}
        LearnedCenterInfo Feed(LearnedCenter l,double t,byte f,Position? target=null,Position? mob=null,bool valid=true,uint follow=0)=>l.Observe(t,valid,f,target??goal,follow,0,mob??near,player);
        void Cycle(LearnedCenter l,double t,Position? target=null)
        {Feed(l,t,0,target);Feed(l,t+100,1,target,far);Feed(l,t+350,1,target,far);Feed(l,t+650,1,target,near);Feed(l,t+800,0,target,near);}
        var learner=new LearnedCenter();Check(learner.View(near,player).Center is null);
        Cycle(learner,0);var first=learner.View(near,player);Check(first.State=="LEARNED"&&first.CompletedReturns==1&&first.Center==goal&&first.PlayerDistanceXZ==5);
        Feed(learner,900,0,new(120,5,230),far,follow:77);Check(learner.View(far,player).Center==goal);
        Cycle(learner,1000);Check(learner.View(near,player).State=="RECONFIRMED"&&learner.View(near,player).CompletedReturns==2);
        Feed(learner,1900,0);Check(learner.View(near,player).CompletedReturns==2);
        Feed(learner,2000,1,new(200,5,300));Check(learner.View(near,player).Center is null);
        var mid=new LearnedCenter();Feed(mid,0,1);Feed(mid,600,1);Feed(mid,900,0);Check(mid.View(near,player).Center is null);
        var changed=new LearnedCenter();Feed(changed,0,0);Feed(changed,100,1);Feed(changed,400,1,new(101,5,201));Feed(changed,800,0);Check(changed.View(near,player).Center is null);
        var followBad=new LearnedCenter();Feed(followBad,0,0);Feed(followBad,100,1,follow:7);Feed(followBad,500,1);Feed(followBad,900,0);Check(followBad.View(near,player).Center is null);
        var arrival=new LearnedCenter();Feed(arrival,0,0);Feed(arrival,100,1);Feed(arrival,400,1);Feed(arrival,700,1);Feed(arrival,900,0,mob:far);Check(arrival.View(near,player).Center is null);
        var broken=new LearnedCenter();Cycle(broken,0);Feed(broken,900,0,valid:false);Check(broken.View(near,player).Center is null);
        var gap=new LearnedCenter();Cycle(gap,0);Feed(gap,4000,0);Check(gap.View(near,player).Center is null);
        var time=new LearnedCenter();Cycle(time,0);Feed(time,700,0);Check(time.View(near,player).Center is null);
        var nan=new LearnedCenter();Cycle(nan,0);Feed(nan,900,0,new(float.NaN,5,200));Check(nan.View(near,player).Center is null);
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"learned-center-tests.json"),JsonSerializer.Serialize(new{passed,synthetic=true,live_game_tested=false}));
    }
    public static void Replay(string directory)
    {
        var learner=new LearnedCenter();Identity? expected=null;var changes=new List<object>();string? previous=null;int count=0;
        foreach(string line in File.ReadLines(Path.Combine(directory,"timeline.jsonl")))
        {
            var s=JsonSerializer.Deserialize<CaptureSample>(line)!;var r=s.Research;expected??=s.Before.Identity;
            bool valid=s.Valid&&expected is not null&&MobCapture.Accept(s.Before,s.After,expected);
            var result=learner.Observe(s.ElapsedMs,valid,r?.ReturnFlagCandidate,r?.GoalCandidate,r?.FollowIdCandidate,r?.FollowTypeCandidate,s.A,s.PlayerA);
            string key=$"{result.State}:{result.CompletedReturns}";
            if(key!=previous){changes.Add(new{s.Index,s.ElapsedMs,result});previous=key;}
            count++;
        }
        File.WriteAllText(Path.Combine(directory,"learned-center-replay.json"),JsonSerializer.Serialize(new{offline=true,samples=count,changes,final=learner.View(null,null)},new JsonSerializerOptions{WriteIndented=true}));
    }
}
