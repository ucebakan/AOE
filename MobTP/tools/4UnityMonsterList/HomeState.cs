namespace UnityMonsterList;

// User-selected operational Home source. The underlying message-fed field can change.
record HomeState(Position? XYZ,double RadiusXZ,double? MobDistanceXZ,double? PlayerDistanceXZ,bool? PlayerWithinRadius,string Status)
{
    public static HomeState Read(bool valid,Position? xyz,Position? mob,Position? player,double radius)
    {
        if(!double.IsFinite(radius)||radius<=0)throw new ArgumentOutOfRangeException(nameof(radius));
        bool Usable(Position? p)=>p is not null && p!=new Position(0,0,0) && float.IsFinite(p.X)&&float.IsFinite(p.Y)&&float.IsFinite(p.Z);
        if(!valid||!Usable(xyz))return new(null,radius,null,null,null,"UNAVAILABLE");
        double? md=Usable(mob)?MobCapture.Distance(mob,xyz,true):null;
        double? pd=Usable(player)?MobCapture.Distance(player,xyz,true):null;
        return new(xyz,radius,md,pd,pd is null?null:pd<=radius,"LIVE_FIELD_12D8");
    }
}
