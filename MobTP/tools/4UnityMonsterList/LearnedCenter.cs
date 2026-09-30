namespace UnityMonsterList;

record LearnedCenterInfo(string State,Position? Center,int CompletedReturns,double? MobDistanceXZ,double? PlayerDistanceXZ,string Detail);

// Session-local observation only. These tolerances qualify evidence, not a leash/gameplay radius.
sealed class LearnedCenter
{
    public const double MaxGapMs=2000,MinReturnMs=500,GoalTolerance=0.1,ArrivalXZ=1,ArrivalY=3;
    Position? center,goal;
    int confirmations,samples;
    byte? lastFlag;
    double? lastTime;
    double started;
    bool active;
    string detail="Tam bir dönüş bekleniyor";
    public void Reset(string reason)
    {center=null;goal=null;confirmations=0;active=false;samples=0;lastFlag=null;lastTime=null;detail=reason;}
    public LearnedCenterInfo View(Position? mob,Position? player)=>new(center is null?"UNKNOWN":confirmations>=2?"RECONFIRMED":"LEARNED",center,confirmations,center is null?null:MobCapture.Distance(mob,center,true),center is null?null:Reader.DiagnosticDistance(player,center),detail);
    static bool Finite(Position? p)=>p is not null&&float.IsFinite(p.X)&&float.IsFinite(p.Y)&&float.IsFinite(p.Z);
    public LearnedCenterInfo Observe(double time,bool valid,byte? flag,Position? target,uint? follow,byte? followType,Position? mob,Position? player)
    {
        if(!valid || !double.IsFinite(time) || flag is not (0 or 1) || !Finite(target) || !Finite(mob))
        {Reset("Kimlik/veri doğrulaması koptu; merkez silindi");return View(mob,player);}
        if(lastTime is double previous && (time<=previous || time-previous>MaxGapMs))Reset("Gözlem aralığı koptu; yeniden öğrenilecek");
        if(flag==1)
        {
            if(center is not null && MobCapture.Distance(center,target)>GoalTolerance)
            {center=null;confirmations=0;detail="Farklı dönüş hedefi görüldü; eski merkez silindi";}
            if(lastFlag==0)
            {active=true;goal=target;started=time;samples=0;detail=center is null?"Dönüş gözleniyor; tamamlanması bekleniyor":"Öğrenilmiş merkez yeni dönüşle kontrol ediliyor";}
            if(active)
            {
                if(follow!=0 || followType!=0 || MobCapture.Distance(goal,target)>GoalTolerance)
                {active=false;goal=null;detail="Dönüş hedefi/takip durumu tutarsız; döngü sayılmadı";}
                else samples++;
            }
            else if(lastFlag is null)detail="Kayıt dönüş ortasında başladı; bu döngü öğrenme için sayılmayacak";
        }
        else if(lastFlag==1)
        {
            bool completed=active&&samples>=3&&time-started>=MinReturnMs&&follow==0&&followType==0&&MobCapture.Distance(goal,target)<=GoalTolerance&&MobCapture.Distance(mob,goal,true)<=ArrivalXZ&&Math.Abs(mob!.Y-goal!.Y)<=ArrivalY;
            if(completed)
            {
                if(center is null){center=goal;confirmations=1;detail="Bir tamamlanmış dönüşten öğrenildi; kalıcı Home alanı değildir";}
                else{confirmations++;detail=$"Aynı merkez {confirmations} tamamlanmış dönüşte gözlendi";}
            }
            else detail="Dönüş bitiş koşulları karşılanmadı; döngü sayılmadı";
            active=false;goal=null;
        }
        lastFlag=flag;lastTime=time;
        return View(mob,player);
    }
}
