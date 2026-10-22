// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Server;
using Server.Mobiles;
using Server.Spells;

namespace Sanctuary;

public sealed record HammerFlight(long Id,uint Owner,int Map,int OriginX,int OriginY,int Z,int Rank,int Frame,int Point,
    int XFp16,int YFp16,int VelocityQ8,int AccelerationQ8,int AccelerationCounter,int Min256,int Max256,uint LastCollision,DateTime UntilUtc,DateTime NextUtc);
public sealed class BlessedHammerProfiles:GenericPersistence
{
    public BlessedHammerProfiles():base("SanctuaryBlessedHammers",125){}
    public override void Serialize(IGenericWriter writer){writer.Write(0);writer.Write(JsonSerializer.Serialize(BlessedHammers.Snapshot()));}
    public override void Deserialize(IGenericReader reader)
    {
        if(reader.ReadInt()!=0){throw new InvalidDataException("Unknown Blessed Hammer version");}
        BlessedHammers.Restore(JsonSerializer.Deserialize<HammerFlight[]>(reader.ReadString()));
    }
}
public static class BlessedHammers
{
    private static readonly Dictionary<long,HammerFlight> _flights=new();
    private static readonly List<long> _keys=new();
    private static readonly Point2D[] _path=Waypoints();
    private static long _next;
    internal static Func<int,int> Roll=static range=>Utility.Random(range);
    public static BlessedHammerProfiles Profiles{get;private set;}
    public static int Frames=>NecromancerSummons.Source("blessedhammer","Range");
    public static void Configure()=>Profiles=new();
    public static void Initialize()=>Timer.StartTimer(TimeSpan.Zero,TimeSpan.FromMilliseconds(40),Tick);
    public static Point2D[] Waypoints()
    {
        var points=new Point2D[77];int previousX=0,previousY=0,angle=0,distance=0,index=0;
        while(index<points.Length)
        {
            angle+=16;distance+=9600;
            // Original Fog LUT is imported, not available in the reconstruction. Explicit single-precision trig projection.
            float c=(float)Math.Cos((angle&511)*Math.PI/256),s=(float)Math.Sin((angle&511)*Math.PI/256);
            int x=(int)Math.Floor((32768+(long)(c*(float)distance))/65536.0),y=(int)Math.Floor((32768+(long)(s*(float)distance))/65536.0);
            if(x==previousX && y==previousY){continue;}
            points[index++]=new(x,y);previousX=x;previousY=y;
        }
        return points;
    }
    public static (int Min,int Max) Damage(PlayerMobile owner,SkillValues values)
    {
        int concentration=PaladinOffense.HammerBonus(owner);
        int Scale(int value)
        {
            long enhanced=(long)Math.Clamp(NamedElements.Enhance(owner,value*(100+CombatPotions.Bonus(owner,"magical-penetration"))/100.0,"mag"),0,int.MaxValue);
            return (int)Math.Clamp(enhanced*(100+concentration)/100,0,int.MaxValue);
        }
        return(Scale(values.Damage256(false)),Scale(values.Damage256(true)));
    }
    public static void Cast(PlayerMobile owner,SkillValues values)
    {
        var damage=Damage(owner,values);long id=++_next;var now=TimedEffects.Now;
        _flights.Add(id,new(id,owner.Serial.Value,owner.Map.MapID,owner.X,owner.Y,owner.Z,values.Rank,0,0,0,0,
            (NecromancerSummons.Source("blessedhammer","Vel")<<8)*75/100,NecromancerSummons.Source("blessedhammer","Accel"),0,
            damage.Min,damage.Max,0,now.AddMilliseconds(Frames*40),now));
    }
    public static HammerFlight Move(HammerFlight flight)
    {
        if(flight.Point>=_path.Length){return flight;}
        int velocity=flight.VelocityQ8,acceleration=flight.AccelerationQ8,counter=flight.AccelerationCounter;
        if(acceleration!=0 && ++counter>=5)
        {
            velocity+=acceleration;int maximum=NecromancerSummons.Source("blessedhammer","MaxVel")<<8;
            if(velocity>maximum){velocity=maximum;acceleration=0;}else{velocity=Math.Max(0,velocity);}counter=0;
        }
        int dx=_path[flight.Point].X*65536-flight.XFp16,dy=_path[flight.Point].Y*65536-flight.YFp16;
        var direction=MissileVectors.Direction(dx,dy);int magnitude=(1024*velocity)>>6;
        int vx=(int)((long)magnitude*direction.X>>12),vy=(int)((long)magnitude*direction.Y>>12);
        int maximumStep=Math.Max(Math.Abs(vx),Math.Abs(vy)),point=flight.Point;
        // The source snaps one waypoint per update and discards unused distance; do not integrate through several points.
        if(Math.Abs(dx)<=maximumStep && Math.Abs(dy)<=maximumStep){vx=dx;vy=dy;point++;}
        return flight with{XFp16=flight.XFp16+vx,YFp16=flight.YFp16+vy,Point=point,VelocityQ8=velocity,AccelerationQ8=acceleration,AccelerationCounter=counter};
    }
    internal static int Impact(PlayerMobile owner,Mobile target,HammerFlight flight)
    {
        if(!ClassCombat.IsHostile(owner,target)){return 0;}
        int range=flight.Max256-flight.Min256;
        // SrvDmg05 rerolls the magic value populated by FillDamageParams; retain both exclusive-upper draws.
        if(range>0){Roll(range);}
        int damage=flight.Min256+(range>0?Roll(range):0);
        long total=damage;
        if(GemCombat.Undead(target)){total+=(long)damage*NecromancerSummons.Source("blessedhammer","dParam1")/100;}
        if(NamedCombat.Demon(target)){total+=(long)damage*NecromancerSummons.Source("blessedhammer","dParam2")/100;}
        SorceressEffects.MissileHit(owner,target,"blessedhammer");
        // Holy=0: species damage bonus does not bypass magic resistance/immunity and supplies no weapon leech/proc.
        return SkillCombat.HolyImpact(owner,target,(int)Math.Clamp(total,0,int.MaxValue),false);
    }
    private static void Tick()
    {
        var now=TimedEffects.Now;_keys.Clear();_keys.AddRange(_flights.Keys);
        foreach(long id in _keys)
        {
            if(!_flights.TryGetValue(id,out var flight)){continue;}
            var owner=World.FindMobile((Serial)flight.Owner) as PlayerMobile;
            if(owner is not{Alive:true,Deleted:false} || !ClassCreation.Profiles.TryGet(owner.Serial,out var profile) || profile.Class!=DiabloClass.Paladin
                || owner.Map!=Map.Internal && (owner.Map.MapID!=flight.Map || SpellHelper.IsTown(owner.Location,owner)))
            {_flights.Remove(id);continue;}
            if(owner.Map==Map.Internal){if(flight.UntilUtc<=now){_flights.Remove(id);}continue;}
            while(_flights.TryGetValue(id,out flight) && flight.NextUtc<=now && flight.NextUtc<flight.UntilUtc){Step(owner,flight);}
            if(_flights.TryGetValue(id,out flight) && flight.UntilUtc<=now){_flights.Remove(id);}
        }
    }
    private static void Step(PlayerMobile owner,HammerFlight before)
    {
        int frame=before.Frame+1;if(frame>=Frames){_flights.Remove(before.Id);return;}
        var flight=Move(before) with{Frame=frame,NextUtc=before.NextUtc.AddMilliseconds(40)};
        double x0=before.OriginX+before.XFp16/131072.0,y0=before.OriginY+before.YFp16/131072.0;
        double x=flight.OriginX+flight.XFp16/131072.0,y=flight.OriginY+flight.YFp16/131072.0;
        var from=new Point3D((int)Math.Round(x0),(int)Math.Round(y0),flight.Z+12);var to=new Point3D((int)Math.Round(x),(int)Math.Round(y),flight.Z+12);
        if(x<0 || y<0 || x>=owner.Map.Width || y>=owner.Map.Height || !owner.Map.LineOfSight(from,to))
        {_flights.Remove(flight.Id);return;}
        Mobile first=null;double nearest=double.MaxValue;
        foreach(var target in owner.Map.GetMobilesInRange<Mobile>(new Point3D(to.X,to.Y,flight.Z),2))
        {
            if(target.Serial.Value!=flight.LastCollision && ClassCombat.IsHostile(owner,target) && !SpellHelper.IsTown(target.Location,target)
                && CombatHitboxes.Intersect(x0,y0,x,y,flight.Z,target.Location,out double entry)
                && (entry<nearest || entry==nearest && target.Serial.Value<(first?.Serial.Value ?? uint.MaxValue)))
            {first=target;nearest=entry;}
        }
        if(first!=null){flight=flight with{LastCollision=first.Serial.Value};}
        _flights[flight.Id]=flight;
        Effects.SendLocationEffect(new(to.X,to.Y,flight.Z),owner.Map,0x36E4,3,10,0x481,0); // Placeholder hammer/trail.
        if(first!=null){Impact(owner,first,flight);}
    }
    public static HammerFlight[] Snapshot()=>new List<HammerFlight>(_flights.Values).ToArray();
    public static void Restore(HammerFlight[] saved)
    {
        if(saved==null){throw new InvalidDataException("Missing Blessed Hammers");}
        _flights.Clear();_next=0;
        foreach(var flight in saved)
        {
            if(flight.Id<=0 || flight.Owner==0 || flight.Rank<1 || flight.Frame<0 || flight.Frame>=Frames || flight.Point<0 || flight.Point>77
                || flight.VelocityQ8<0 || flight.VelocityQ8>7680 || flight.AccelerationQ8<0 || flight.AccelerationCounter is <0 or >4
                || flight.Min256<0 || flight.Max256<flight.Min256 || !_flights.TryAdd(flight.Id,flight))
            {throw new InvalidDataException("Invalid Blessed Hammer");}
            _next=Math.Max(_next,flight.Id);
        }
    }
}
