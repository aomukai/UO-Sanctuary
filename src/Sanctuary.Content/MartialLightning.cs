// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Logging;
using Server.Mobiles;
using Server.Spells;

namespace Sanctuary;

internal sealed record ThunderPacket(int Count,int Rank,int Inline256,int NovaMin256,int NovaMax256,int BoltMin256,int BoltMax256,int Pierce);
internal readonly record struct ThunderSpawn(int X,int Y,bool Bolt,uint Seed,int Flags,int Velocity,int Frames,int TargetX);
internal sealed record ThunderFlight(long Id,long Cast,uint Owner,int Map,int Rank,bool Bolt,int OriginX,int OriginY,int Z,
    double X,double Y,double Vx,double Vy,Point2D[] Path,int Next,int Frame,int Frames,int VelocityFp8,int Min256,int Max256,
    int Pierce,SlowMissilePayload Slow,ulong Seed,SourceStepTrace Steps,uint LastCollision,DateTime NextUtc,string Fault=null)
{
    public int TargetX{get;init;}
    public Point2D Direction{get;init;}
    public int Acceleration{get;init;}
    public int AccelerationCounter{get;init;}
    public int MaximumVelocityFp8{get;init;}
    public string MotionAdapter{get;init;}="native-shared-martial-lightning-v1";
}
internal sealed record ThunderSave(ThunderFlight[] Flights,WeaponFlightLease[] Leases,long NextId,long NextCast);
public sealed class ThunderProfiles:GenericPersistence
{
    private ThunderSave _snapshot;
    public ThunderProfiles():base("SanctuaryMartialLightning",160){}
    public override void Serialize(){_snapshot=MartialLightning.Snapshot();base.Serialize();}
    public override void Serialize(IGenericWriter writer){writer.Write(0);writer.Write(JsonSerializer.Serialize(_snapshot));}
    public override void Deserialize(IGenericReader reader)
    {if(reader.ReadInt()!=0){throw new InvalidDataException("Unknown martial lightning version");}MartialLightning.Restore(JsonSerializer.Deserialize<ThunderSave>(reader.ReadString()));}
}

// Shared missile stepping/collision adapters retain their documented native
// geometry/path limits. Emission, seed ownership and child setup are source-compared.
public static class MartialLightning
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(MartialLightning));
    private static readonly Dictionary<long,ThunderFlight> Flights=new();
    private static readonly Dictionary<(long,uint),WeaponFlightLease> Leases=new();
    private static long _nextId,_nextCast;
    internal static Func<int,int> DamageRoll=Utility.Random;
    internal static Func<int> SeedLow=()=>unchecked((int)Random.Shared.NextInt64(1L<<32));
    private static readonly Point2D[] Directions=
    [
        new(30,0),new(29,2),new(29,5),new(28,8),new(27,11),new(26,14),new(24,16),new(23,19),
        new(21,21),new(19,23),new(16,24),new(14,26),new(11,27),new(8,28),new(5,29),new(2,29),
        new(0,30),new(-2,29),new(-5,29),new(-8,28),new(-11,27),new(-14,26),new(-16,24),new(-19,23),
        new(-21,21),new(-23,19),new(-24,16),new(-26,14),new(-27,11),new(-28,8),new(-29,5),new(-29,2),
        new(-30,0),new(-29,-2),new(-29,-5),new(-28,-8),new(-27,-11),new(-26,-14),new(-24,-16),new(-23,-19),
        new(-21,-21),new(-19,-23),new(-16,-24),new(-14,-26),new(-11,-27),new(-8,-28),new(-5,-29),new(-2,-29),
        new(0,-30),new(2,-29),new(5,-29),new(8,-28),new(11,-27),new(14,-26),new(16,-24),new(19,-23),
        new(21,-21),new(23,-19),new(24,-16),new(26,-14),new(27,-11),new(28,-8),new(29,-5),new(29,-2)
    ];
    public static void Configure()=>_ = new ThunderProfiles();
    public static void Initialize()=>Timer.StartTimer(TimeSpan.FromMilliseconds(40),TimeSpan.FromMilliseconds(40),()=>Tick(TimedEffects.Now));
    internal static ThunderSpawn[] Pattern(int count,int childLow)
    {
        if(count is <1 or >3){throw new NotSupportedException("Unmapped Thunder charge count");}
        var result=new List<ThunderSpawn>();
        if(count>=2){foreach(var d in Directions){result.Add(new(d.X,d.Y,false,0,7,24,13,0));}}
        if(count==3)
        {
            ulong seed=SourceRandom.InitLow(childLow);
            for(int i=0;i<64;i+=4){var d=Directions[i];result.Add(new(d.X,d.Y,true,SourceRandom.Roll(ref seed),3,0,77,7));}
        }
        return result.ToArray();
    }
    internal static ThunderPacket Capture(PlayerMobile owner,MartialCharge charge)
    {
        if(charge is not{Ability:CombatAbility.ClawsOfThunder,Count:>=1 and <=3}){return null;}
        int rank=Math.Max(charge.InitialRank,SkillMechanics.Rank(owner,CombatAbility.ClawsOfThunder));
        return Capture(SkillMechanics.Values(owner,CombatAbility.ClawsOfThunder,rank),charge.Count,NamedElements.MasteryPercent(owner,"ltng"),ClassGear.Bonus(owner,"pierce-ltng"),DamageRoll);
    }
    internal static ThunderPacket Capture(SkillValues values,int count,int mastery,int pierce,Func<int,int> roll)
    {
        if(values.Ability!=CombatAbility.ClawsOfThunder || values.Rank is <1 or >255 || count is <1 or >3 || values.Eval("calc1")!=0)
        {throw new NotSupportedException("Unmapped Thunder packet context");}
        int Enhance(int damage)=>checked((int)((long)damage*Math.Max(0,100+mastery)/100));
        int Child(string missile,bool maximum)=>Enhance(FireGround.MissileDamage256(missile,values.Rank,NecromancerSummons.Formula(missile,"EDmgSymPerCalc",values),maximum));
        return new(count,values.Rank,WeaponMissileDamage.RollValue(Enhance(values.Damage256(false)),Enhance(values.Damage256(true)),0,roll),
            Child("clawsofthundernova",false),Child("clawsofthundernova",true),Child("clawsofthunderbolt",false),Child("clawsofthunderbolt",true),pierce);
    }
    private static void Tables()
    {
        foreach(string key in new[]{"clawsofthundernova","clawsofthunderbolt"})
        {
            int F(string name)=>NecromancerSummons.Source(key,name);
            if(F("Size")!=1 || F("CollideType")!=3 || F("LastCollide")!=1 || F("NextHit")!=1 || F("NextDelay")!=4
                || F("ApplyMastery")!=1 || F("pSrvHitFunc")!=0 || F("pSrvDmgFunc")!=0 || F("SrcDamage")!=0 || F("ToHit")!=0 || F("ResultFlags")!=4
                || F("VelLev")!=0 || F("Accel")!=(key=="clawsofthunderbolt"?0:-1000) || F("MaxVel")!=(key=="clawsofthunderbolt"?12:24) || F("pSrvDoFunc")!=1
                || F("Range")!=(key=="clawsofthunderbolt"?98:13) || F("Vel")!=(key=="clawsofthunderbolt"?12:24)
                || F("CollideKill")!=(key=="clawsofthunderbolt"?1:0))
            {throw new NotSupportedException("Unmapped Thunder child missile table route");}
        }
    }
    internal static long AfterParent(PlayerMobile owner,Point3D center,Map map,ThunderPacket packet,DateTime at)
    {
        if(packet==null || packet.Count<2 || owner is not{Alive:true,Deleted:false} || owner.Map!=map || map==null || map==Map.Internal || SpellHelper.IsTown(center,owner)){return 0;}
        Tables();long cast=checked(++_nextCast);
        // Nova never draws the owner seed. The bolt callback consumes exactly
        // one owner low draw; its child initializer owns the independent stream.
        foreach(var spawn in Pattern(packet.Count,packet.Count==3?SeedLow():0))
        {
            string key=spawn.Bolt?"clawsofthunderbolt":"clawsofthundernova";
            var slow=AmazonSlowMissiles.Capture(owner,NecromancerSummons.Source(key,"CanSlow")!=0);
            int speed=SlowMissilePayload.VelocityFp8(NecromancerSummons.Source(key,"Vel"),0,packet.Rank,spawn.Flags,spawn.Velocity,
                NecromancerSummons.Source(key,"CanSlow")!=0,slow.VelocityPercent);
            var velocity=MissileVectors.VelocityFp8(spawn.X,spawn.Y,speed);
            Point2D[] path=spawn.Bolt?ChargedBolts.Path(spawn.Seed,ChargedBolts.Heading(spawn.X,spawn.Y),spawn.Frames):[];
            ulong seed=SourceRandom.InitLow(unchecked((int)spawn.Seed));
            // Existing charged-path adapter consumes one child roll per waypoint.
            for(int i=1;i<path.Length;i++){SourceRandom.Roll(ref seed);}
            long id=checked(++_nextId);
            var flight=new ThunderFlight(id,cast,owner.Serial.Value,map.MapID,packet.Rank,spawn.Bolt,center.X,center.Y,center.Z,
                center.X,center.Y,velocity.X,velocity.Y,path,1,0,spawn.Frames,speed,
                slow.Damage256(spawn.Bolt?packet.BoltMin256:packet.NovaMin256),slow.Damage256(spawn.Bolt?packet.BoltMax256:packet.NovaMax256),
                packet.Pierce,slow,seed,new([new(center.X*2,center.Y*2)]),owner.Serial.Value,at.AddMilliseconds(40));
            flight=flight with{TargetX=spawn.TargetX,Direction=MissileVectors.Direction(spawn.X,spawn.Y),Acceleration=NecromancerSummons.Source(key,"Accel"),MaximumVelocityFp8=NecromancerSummons.Source(key,"MaxVel")<<8};
            Validate(flight);Flights.Add(id,flight);
        }
        return cast;
    }
    internal static bool Ready(long cast,uint target,DateTime at)=>!Leases.TryGetValue((cast,target),out var lease) || lease.UntilUtc<=at;
    internal static BaseCreature Recipient(PlayerMobile owner,Map map,ThunderFlight s,SourceMissileCell cell)
    {
        foreach(var target in map.GetMobilesInRange<BaseCreature>(new Point3D((cell.X+1)/2,(cell.Y+1)/2,s.Z),2))
        {
            if(target.Serial.Value==s.LastCollision || Math.Abs(target.Z-s.Z)>=16 || !ClassCombat.IsHostile(owner,target)
                || !Ready(s.Cast,target.Serial.Value,s.NextUtc) || SpellHelper.IsTown(target.Location,target)){continue;}
            int size=CreatureProfiles.For(target).Traits.Size??throw new NotSupportedException("Thunder collider lacks mapped source size");
            if(CombatHitboxes.SourceTouches(1,size,cell.X-target.X*2,cell.Y-target.Y*2)){return target;}
        }
        return null;
    }
    internal static (int Loss,ulong Seed) Impact(PlayerMobile owner,BaseCreature target,ThunderFlight s)
    {
        Leases[(s.Cast,target.Serial.Value)]=new(s.Cast,target.Serial.Value,s.NextUtc.AddMilliseconds(160));
        if(ClassAvoidance.Missile(target)){return(0,s.Seed);}
        SorceressEffects.MissileHit(owner,target,s.Bolt?"clawsofthunderbolt":"clawsofthundernova");
        ulong seed=s.Seed;int damage=WeaponMissileDamage.RollValue(s.Min256,s.Max256,0,n=>(int)SourceRandom.Limited(ref seed,n));
        owner.DoHarmful(target,true);var reduced=NamedElements.Resolve256(owner,target,damage,"ltng",enhance:false,pierceOverride:s.Pierce);
        if(reduced.Absorbed256>0){SkillHealth.Heal(target,reduced.Absorbed256);}
        int loss=SkillHealth.Damage(owner,target,reduced.Damage256);CreatureHitRecovery.Apply(target,owner,loss);CombatDamage.Finish(target,owner);return(loss,seed);
    }
    internal static void Tick(DateTime at,bool? allowActions=null)
    {
        var queue=new PriorityQueue<ThunderFlight,(DateTime,long)>();
        foreach(var s in Flights.Values){if(s.NextUtc<=at){queue.Enqueue(s,(s.NextUtc,-s.Id));}}
        while(queue.TryDequeue(out var due,out _))
        {
            if(!Flights.TryGetValue(due.Id,out var s) || s.NextUtc!=due.NextUtc){continue;}
            try{Advance(s,allowActions);}
            catch(Exception e){Flights[s.Id]=s with{Fault=e.Message,Frame=s.Frame+1,NextUtc=s.NextUtc.AddMilliseconds(40)};if(s.Frame+1>=s.Frames){Flights.Remove(s.Id);}Log.Error(e,"Thunder missile suspended: {Id}",s.Id);}
            if(Flights.TryGetValue(s.Id,out var next) && next.NextUtc<=at){queue.Enqueue(next,(next.NextUtc,-next.Id));}
        }
        var active=Flights.Values.Select(x=>x.Cast).ToHashSet();
        foreach(var key in Leases.Where(x=>x.Value.UntilUtc<=at || !active.Contains(x.Key.Item1)).Select(x=>x.Key).ToArray()){Leases.Remove(key);}
    }
    internal static (int Velocity,int Acceleration,int Counter,int X,int Y) VelocityStep(int velocity,int acceleration,int counter,int maximum,int basis,int dx,int dy)
    {
        if(basis<=0){basis=1024;}
        if(acceleration!=0 && ++counter>=5)
        {
            velocity=unchecked(velocity+acceleration);
            if(velocity>maximum){velocity=maximum;acceleration=0;}
            else if(velocity<0){velocity=0;}
            counter=0;
        }
        int magnitude=unchecked(basis*velocity)>>6;
        return(velocity,acceleration,counter,unchecked(magnitude*dx)>>12,unchecked(magnitude*dy)>>12);
    }
    private static void Advance(ThunderFlight s,bool? allowActions)
    {
        int frame=s.Frame+1;var next=s with{Frame=frame,NextUtc=s.NextUtc.AddMilliseconds(40)};
        var owner=World.FindMobile((Serial)s.Owner) as PlayerMobile;
        if(owner is not{Alive:true,Deleted:false} || ClassCreation.Profiles.TryGet(owner.Serial,out var profile)!=true || profile.Class!=DiabloClass.Assassin
            || owner.Map==null || owner.Map!=Map.Internal && owner.Map.MapID!=s.Map){Flights.Remove(s.Id);return;}
        bool active=owner.Map!=Map.Internal && (allowActions??owner.NetState is{Running:true});
        if(!active || s.Fault!=null){if(frame>=s.Frames){Flights.Remove(s.Id);}else{Flights[s.Id]=next;}return;}
        var map=Map.Maps[s.Map];var velocity=VelocityStep(s.VelocityFp8,s.Acceleration,s.AccelerationCounter,s.MaximumVelocityFp8,0,s.Direction.X,s.Direction.Y);
        next=next with{VelocityFp8=velocity.Velocity,Acceleration=velocity.Acceleration,AccelerationCounter=velocity.Counter,Vx=velocity.X/131072.0,Vy=velocity.Y/131072.0};
        double x=s.X+next.Vx,y=s.Y+next.Vy;int pathNext=s.Next;
        if(s.Bolt){(x,y,pathNext)=ChargedBolts.Advance(s.Path,s.OriginX,s.OriginY,s.X,s.Y,s.Next,SkillGeometry.Tiles(next.VelocityFp8/4096.0));}
        if(frame>=s.Frames || s.Bolt && pathNext>=s.Path.Length || x<0 || y<0 || x>=map.Width || y>=map.Height){Flights.Remove(s.Id);return;}
        next=next with{X=x,Y=y,Next=pathNext,Steps=SourceMissileSteps.Advance(s.X,s.Y,x-s.X,y-s.Y,s.Steps)};
        foreach(var cell in next.Steps.Cells)
        {
            if(cell.X<=0 || cell.Y<=0 || cell.X>=map.Width*2 || cell.Y>=map.Height*2){Flights.Remove(s.Id);return;}
            var target=Recipient(owner,map,next with{NextUtc=s.NextUtc},cell);
            if(target!=null)
            {
                var hit=Impact(owner,target,next with{NextUtc=s.NextUtc});next=next with{Seed=hit.Seed};
                if(s.Bolt){Flights.Remove(s.Id);return;}
                next=next with{LastCollision=target.Serial.Value};
            }
            else if(!map.LineOfSight(new Point3D((int)Math.Round(s.X),(int)Math.Round(s.Y),s.Z+12),new Point3D((cell.X+1)/2,(cell.Y+1)/2,s.Z+12)))
            {Flights.Remove(s.Id);return;}
        }
        Flights[s.Id]=next;
        if(frame%2==0){Effects.SendLocationEffect(new Point3D((int)Math.Round(x),(int)Math.Round(y),s.Z),map,0x3818,3,10,0x482,0);} // Identified lightning trail placeholder.
    }
    private static void Validate(ThunderFlight s)
    {
        if(s==null || s.Id<=0 || s.Cast<=0 || !((Serial)s.Owner).IsMobile || s.Map<0 || s.Map>=Map.Maps.Length || Map.Maps[s.Map]==null || Map.Maps[s.Map]==Map.Internal
            || s.Rank is <1 or >255 || s.Frame<0 || s.Frame>=s.Frames || s.Frames!=(s.Bolt?77:13) || s.NextUtc.Kind!=DateTimeKind.Utc
            || !double.IsFinite(s.X+s.Y+s.Vx+s.Vy) || s.X<0 || s.Y<0 || s.X>=Map.Maps[s.Map].Width || s.Y>=Map.Maps[s.Map].Height
            || s.TargetX!=(s.Bolt?7:0) || s.Acceleration!=(s.Bolt?0:-1000) || s.AccelerationCounter is <0 or >4 || s.MaximumVelocityFp8!=(s.Bolt?3072:6144)
            || Math.Abs(s.Direction.X)>4096 || Math.Abs(s.Direction.Y)>4096 || s.Direction==new Point2D(0,0)
            || s.OriginX<=0 || s.OriginY<=0 || s.Z is <-128 or >127 || s.VelocityFp8<0 || s.Min256<0 || s.Max256<s.Min256
            || s.Slow.VelocityPercent is <0 or >100 || s.Slow.DamagePercent is <0 or >100 || s.Steps?.Cells is not{Length:>=1 and <=10}
            || s.Path==null || s.Bolt && (s.Path.Length!=39 || s.Path[0]!=new Point2D(0,0) || s.Next<1 || s.Next>s.Path.Length)
            || !s.Bolt && (s.Path.Length!=0 || s.Next!=1) || s.MotionAdapter!="native-shared-martial-lightning-v1")
        {throw new InvalidDataException("Invalid saved Thunder missile");}
        if(s.Steps.Cells.Any(x=>x.X is <0 or >65535 || x.Y is <0 or >65535)){throw new InvalidDataException("Invalid Thunder source cell");}
        for(int i=1;i<s.Path.Length;i++){int dx=Math.Abs(s.Path[i].X-s.Path[i-1].X),dy=Math.Abs(s.Path[i].Y-s.Path[i-1].Y);if(dx>1 || dy>1 || dx+dy==0){throw new InvalidDataException("Invalid Thunder path step");}}
    }
    internal static ThunderSave Snapshot()=>new(Flights.Values.OrderBy(x=>x.Id).ToArray(),Leases.Values.OrderBy(x=>x.Cast).ThenBy(x=>x.Target).ToArray(),_nextId,_nextCast);
    internal static void Restore(ThunderSave save)
    {
        if(save?.Flights==null || save.Leases==null || save.NextId<0 || save.NextCast<0){throw new InvalidDataException("Missing Thunder state");}
        var flights=new Dictionary<long,ThunderFlight>();var leases=new Dictionary<(long,uint),WeaponFlightLease>();
        foreach(var s in save.Flights){Validate(s);if(s.Id>save.NextId || s.Cast>save.NextCast || !flights.TryAdd(s.Id,s)){throw new InvalidDataException("Duplicate Thunder missile");}}
        foreach(var s in save.Leases){if(s.Cast<=0 || s.Cast>save.NextCast || !((Serial)s.Target).IsMobile || s.UntilUtc.Kind!=DateTimeKind.Utc || !leases.TryAdd((s.Cast,s.Target),s)){throw new InvalidDataException("Invalid Thunder hit lease");}}
        Flights.Clear();foreach(var s in flights){Flights.Add(s.Key,s.Value);}Leases.Clear();foreach(var s in leases){Leases.Add(s.Key,s.Value);}_nextId=save.NextId;_nextCast=save.NextCast;
    }
}
