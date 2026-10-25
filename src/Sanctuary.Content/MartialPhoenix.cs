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
internal sealed record PhoenixDrop(long Id,long Cast,uint Owner,int Map,int Rank,int X,int Y,int Z,int Frame,DateTime NextUtc,string Fault=null);
internal sealed record PhoenixFlight(long Id,long Cast,uint Owner,int Map,int Rank,bool Cold,double X,double Y,int Z,int Frame,int Frames,
    int VelocityFp8,int Minimum256,int Maximum256,int ColdFrames,int Pierce,SlowMissilePayload Slow,ulong Seed,uint TargetSeed,int Packed,
    Point2D Direction,Point2D[] Path,int Next,SourceStepTrace Steps,DateTime NextUtc,string Fault=null);
internal sealed record PhoenixSave(PhoenixDrop[] Drops,PhoenixFlight[] Flights,WeaponFlightLease[] Leases,long NextId,long NextCast);
public sealed class PhoenixProfiles:GenericPersistence
{
    private PhoenixSave _snapshot;
    public PhoenixProfiles():base("SanctuaryMartialPhoenix",162){}
    public override void Serialize(){_snapshot=MartialPhoenix.Snapshot();base.Serialize();}
    public override void Serialize(IGenericWriter writer){writer.Write(0);writer.Write(JsonSerializer.Serialize(_snapshot));}
    public override void Deserialize(IGenericReader reader){if(reader.ReadInt()!=0){throw new InvalidDataException("Unknown Phoenix version");}MartialPhoenix.Restore(JsonSerializer.Deserialize<PhoenixSave>(reader.ReadString()));}
}
public static class MartialPhoenix
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(MartialPhoenix));
    private static readonly Dictionary<long,PhoenixDrop> Drops=new();
    private static readonly Dictionary<long,PhoenixFlight> Flights=new();
    private static readonly Dictionary<(long,uint),WeaponFlightLease> Leases=new();
    private static long _nextId,_nextCast;
    internal static Func<int> SeedLow=()=>unchecked((int)Random.Shared.NextInt64(1L<<32));
    internal static Func<int,int> DamageRoll=Utility.Random;
    private static readonly Point2D[] ChainDirections=[new(30,0),new(16,24),new(-11,27),new(-29,5),new(-21,-21),new(5,-29),new(27,-11)];
    public static void Configure()=>_ = new PhoenixProfiles();
    public static void Initialize()=>Timer.StartTimer(TimeSpan.FromMilliseconds(40),TimeSpan.FromMilliseconds(40),()=>Tick(TimedEffects.Now));
    private static PlayerMobile Owner(uint serial,int map)
        =>World.FindMobile((Serial)serial) is PlayerMobile{Alive:true,Deleted:false} p && ClassCreation.Profiles.TryGet(p.Serial,out var profile) && profile.Class==DiabloClass.Assassin
            && p.Map!=null && (p.Map==Map.Internal || p.Map.MapID==map)?p:null;
    private static bool Active(PlayerMobile p,bool? actions)=>p.Map!=Map.Internal && (actions??p.NetState is{Running:true});
    private static void Tables()
    {
        int F(string key,string field)=>NecromancerSummons.Source(key,field);
        var skill=SkillMechanics.For(CombatAbility.PhoenixStrike);
        if(skill.Number("prgstack")!=0 || skill.Number("prgdam")!=0 || skill.Parameter(2)!=6 || skill.Parameter(5)!=16 || skill.Parameter(6)!=10)
        {throw new NotSupportedException("Unmapped Phoenix progressive skill route");}
        if(F("royalstrikemeteorcenter","Range")!=60 || F("royalstrikemeteorcenter","pSrvHitFunc")!=4 || F("royalstrikemeteorcenter","AlwaysExplode")!=1
            || F("royalstrikemeteor","pSrvHitFunc")!=14 || F("royalstrikemeteor","sHitPar1")!=6 || F("royalstrikemeteor","sHitPar2")!=1
            || F("royalstrikemeteorfire","pSrvDoFunc")!=1 || F("royalstrikemeteorfire","pSrvDmgFunc")!=0 || F("royalstrikemeteorfire","CollideType")!=8 || F("royalstrikemeteorfire","Size")!=2)
        {throw new NotSupportedException("Unmapped Phoenix meteor table route");}
        foreach(string key in new[]{"royalstrikechainlightning","royalstrikechaosice"})
        {
            bool cold=key=="royalstrikechaosice";
            if(F(key,"pSrvDoFunc")!=(cold?35:1) || F(key,"pSrvDmgFunc")!=(cold?2:0) || F(key,"pSrvHitFunc")!=0 || F(key,"Range")!=(cold?40:60)
                || F(key,"Size")!=(cold?1:2) || F(key,"Vel")!=(cold?16:30) || F(key,"CollideKill")!=(cold?1:0) || F(key,"LastCollide")!=(cold?1:0)
                || F(key,"NextHit")!=1 || F(key,"NextDelay")!=4 || F(key,"ApplyMastery")!=1 || F(key,"SrcDamage")!=0 || F(key,"ToHit")!=0 || F(key,"CanSlow")!=1 || F(key,"VelLev")!=0 || F(key,"Accel")!=0 || F(key,"MaxVel")!=(cold?16:30) || F(key,"ResultFlags")!=4
                || cold && (F(key,"Param1")!=3 || F(key,"dParam1")!=100))
            {throw new NotSupportedException("Unmapped Phoenix child table route");}
        }
    }
    internal static long AfterParent(PlayerMobile owner,Mobile target,Point3D fallback,Map map,MartialCharge charge,DateTime at)
    {
        if(charge is not{Ability:CombatAbility.PhoenixStrike,Count:>=1 and <=3} || owner is not{Alive:true,Deleted:false} || owner.Map!=map || map==null || map==Map.Internal){return 0;}
        var center=target is{Deleted:false} && target.Map==map?target.Location:fallback;
        if(SpellHelper.IsTown(center,owner)){return 0;}Tables();int rank=Math.Max(charge.InitialRank,SkillMechanics.Rank(owner,CombatAbility.PhoenixStrike));long cast=checked(++_nextCast);
        if(charge.Count==1)
        {long id=checked(++_nextId);Drops.Add(id,new(id,cast,owner.Serial.Value,map.MapID,rank,center.X,center.Y,center.Z,0,at.AddMilliseconds(40)));Effects.SendLocationEffect(center,map,0x3709,60,10,0x489,0);return cast;}
        ulong stream=SourceRandom.InitLow(SeedLow());bool cold=charge.Count==3;int count=cold?16:7;
        for(int i=0;i<count;i++)
        {
            Point2D aim;uint targetSeed=0,childSeed=0;int packed=0;Point2D[] path=[];
            if(cold)
            {int x=(int)(SourceRandom.Roll(ref stream)%40)-20,y=(int)(SourceRandom.Roll(ref stream)%40)-20;if(x==0 && y==0){x=20;}aim=new(x,y);targetSeed=(uint)stream;packed=unchecked((ushort)x+((int)(ushort)y<<16));}
            else{aim=ChainDirections[i];childSeed=SourceRandom.Roll(ref stream);}
            var values=SkillMechanics.Values(owner,CombatAbility.PhoenixStrike,rank);string key=cold?"royalstrikechaosice":"royalstrikechainlightning",element=cold?"cold":"ltng";
            var damage=MartialPhoenixPackets.Capture(key,values,NamedElements.MasteryPercent(owner,element));var slow=AmazonSlowMissiles.Capture(owner,true);
            int speed=SlowMissilePayload.VelocityFp8(NecromancerSummons.Source(key,"Vel"),0,rank,3,0,true,slow.VelocityPercent);
            ulong seed=SourceRandom.InitLow(unchecked((int)targetSeed));
            if(!cold){var chain=MartialPhoenixPaths.Chain(unchecked((int)childSeed),MartialPhoenixPaths.Direction(aim.X,aim.Y),60,center.X*2,center.Y*2);path=chain.Points;seed=chain.Seed;}
            long id=checked(++_nextId);var f=new PhoenixFlight(id,cast,owner.Serial.Value,map.MapID,rank,cold,center.X,center.Y,center.Z,0,cold?40:60,speed,
                slow.Damage256(damage.Minimum256),slow.Damage256(damage.Maximum256),damage.ColdFrames,ClassGear.Bonus(owner,"pierce-"+element),slow,seed,targetSeed,packed,
                MissileVectors.Direction(aim.X,aim.Y),path,0,new([new(center.X*2,center.Y*2)]),at.AddMilliseconds(40));Validate(f);Flights.Add(id,f);
        }
        return cast;
    }
    internal static void MeteorImpact(PlayerMobile owner,PhoenixDrop drop,DateTime at)
    {
        var values=SkillMechanics.Values(owner,CombatAbility.PhoenixStrike,drop.Rank);var packet=MartialPhoenixPackets.Capture("royalstrikemeteor",values,NamedElements.MasteryPercent(owner,"fire"));
        int damage=WeaponMissileDamage.RollValue(packet.Minimum256,packet.Maximum256,0,DamageRoll);var center=new Point3D(drop.X,drop.Y,drop.Z);
        foreach(var target in Map.Maps[drop.Map].GetMobilesInRange<BaseCreature>(center,4))
        {if(FistsFireRelease.InRadius(center,target,6) && ClassCombat.IsHostile(owner,target) && !SpellHelper.IsTown(target.Location,target)){SkillCombat.FireImpact(owner,target,damage);CombatDamage.Finish(target,owner);}}
        if(owner.Deleted || !owner.Alive || owner.Map.MapID!=drop.Map){return;}
        foreach(var offset in MartialPhoenixPackets.MeteorOffsets())
        {
            double x=drop.X+offset.X/2.0,y=drop.Y+offset.Y/2.0;var point=new Point3D((int)Math.Round(x),(int)Math.Round(y),drop.Z);
            if(x<0 || y<0 || x>=owner.Map.Width || y>=owner.Map.Height || SpellHelper.IsTown(point,owner)){continue;}
            values=SkillMechanics.Values(owner,CombatAbility.PhoenixStrike,drop.Rank);var fire=MartialPhoenixPackets.Capture("royalstrikemeteorfire",values,NamedElements.MasteryPercent(owner,"fire"));
            FireGround.AddPhoenix(owner,drop.Rank,x,y,drop.Z,fire.Minimum256,fire.Maximum256,at);
        }
        Effects.SendLocationEffect(center,owner.Map,0x36BD,10,10,0x489,0); // Identified meteor impact placeholder.
    }
    internal static bool Ready(long cast,uint target,DateTime at)=>!Leases.TryGetValue((cast,target),out var lease) || lease.UntilUtc<=at;
    internal static BaseCreature Recipient(PlayerMobile owner,PhoenixFlight f,SourceMissileCell cell)
    {
        foreach(var target in Map.Maps[f.Map].GetMobilesInRange<BaseCreature>(new Point3D((cell.X+1)/2,(cell.Y+1)/2,f.Z),2))
        {
            if(Math.Abs(target.Z-f.Z)>=16 || !ClassCombat.IsHostile(owner,target) || !Ready(f.Cast,target.Serial.Value,f.NextUtc) || SpellHelper.IsTown(target.Location,target)){continue;}
            int size=CreatureProfiles.For(target).Traits.Size??throw new NotSupportedException("Phoenix collider lacks mapped source size");
            if(CombatHitboxes.SourceTouches(f.Cold?1:2,size,cell.X-target.X*2,cell.Y-target.Y*2)){return target;}
        }
        return null;
    }
    internal static PhoenixFlight Impact(PlayerMobile owner,BaseCreature target,PhoenixFlight f)
    {
        Leases[(f.Cast,target.Serial.Value)]=new(f.Cast,target.Serial.Value,f.NextUtc.AddMilliseconds(160));
        if(ClassAvoidance.Missile(target)){return f;}ulong seed=f.Seed;int damage=WeaponMissileDamage.RollValue(f.Minimum256,f.Maximum256,0,n=>(int)SourceRandom.Limited(ref seed,n));
        owner.DoHarmful(target,true);var reduced=NamedElements.Resolve256(owner,target,damage,f.Cold?"cold":"ltng",enhance:false,pierceOverride:f.Pierce);
        if(reduced.Absorbed256>0){SkillHealth.Heal(target,reduced.Absorbed256);}
        if(f.Cold){long tick=Core.TickCount+(long)(f.NextUtc-TimedEffects.Now).TotalMilliseconds;FreezeStates.Apply(owner,target,f.ColdFrames,tick,f.Pierce);} // damage2 clears ordinary chill.
        int loss=SkillHealth.Damage(owner,target,reduced.Damage256);CreatureHitRecovery.Apply(target,owner,loss);CombatDamage.Finish(target,owner);return f with{Seed=seed};
    }
    internal static void Tick(DateTime at,bool? allowActions=null)
    {
        var queue=new PriorityQueue<(long Id,bool Drop,DateTime Next),(DateTime,long)>();
        foreach(var d in Drops.Values){if(d.NextUtc<=at){queue.Enqueue((d.Id,true,d.NextUtc),(d.NextUtc,-d.Id));}}
        foreach(var f in Flights.Values){if(f.NextUtc<=at){queue.Enqueue((f.Id,false,f.NextUtc),(f.NextUtc,-f.Id));}}
        while(queue.TryDequeue(out var due,out _))
        {
            if(due.Drop)
            {
                if(!Drops.TryGetValue(due.Id,out var d) || d.NextUtc!=due.Next){continue;}
                var owner=Owner(d.Owner,d.Map);if(owner==null){Drops.Remove(d.Id);continue;}
                var next=d with{Frame=d.Frame+1,NextUtc=d.NextUtc.AddMilliseconds(40)};
                if(next.Frame>=60){Drops.Remove(d.Id);if(Active(owner,allowActions) && d.Fault==null){try{MeteorImpact(owner,d,d.NextUtc);}catch(Exception e){Log.Error(e,"Phoenix meteor suspended: {Id}",d.Id);}}}
                else{Drops[d.Id]=next;if(next.NextUtc<=at){queue.Enqueue((d.Id,true,next.NextUtc),(next.NextUtc,-d.Id));}}
            }
            else
            {
                if(!Flights.TryGetValue(due.Id,out var f) || f.NextUtc!=due.Next){continue;}
                try{Advance(f,allowActions);}catch(Exception e){if(f.Frame+1>=f.Frames){Flights.Remove(f.Id);}else{Flights[f.Id]=f with{Fault=e.Message,Frame=f.Frame+1,NextUtc=f.NextUtc.AddMilliseconds(40)};}Log.Error(e,"Phoenix missile suspended: {Id}",f.Id);}
                if(Flights.TryGetValue(f.Id,out var next) && next.NextUtc<=at){queue.Enqueue((f.Id,false,next.NextUtc),(next.NextUtc,-f.Id));}
            }
        }
        var casts=Flights.Values.Select(x=>x.Cast).ToHashSet();foreach(var key in Leases.Where(x=>x.Value.UntilUtc<=at || !casts.Contains(x.Key.Item1)).Select(x=>x.Key).ToArray()){Leases.Remove(key);}
    }
    private static void Advance(PhoenixFlight f,bool? actions)
    {
        var owner=Owner(f.Owner,f.Map);if(owner==null){Flights.Remove(f.Id);return;}
        var next=f with{Frame=f.Frame+1,NextUtc=f.NextUtc.AddMilliseconds(40)};
        if(!Active(owner,actions) || f.Fault!=null){if(next.Frame>=f.Frames){Flights.Remove(f.Id);}else{Flights[f.Id]=next;}return;}
        double x=f.X,y=f.Y;int index=f.Next;
        if(f.Cold)
        {
            if(MartialPhoenixPaths.Turn(f.Frame,3))
            {var curve=MartialPhoenixPaths.Curve(unchecked((int)f.TargetSeed),f.Packed);int cx=(int)Math.Floor(f.X*2+0.5),cy=(int)Math.Floor(f.Y*2+0.5);int dx=(int)Math.Round((cx+curve.X-f.X*2)*65536),dy=(int)Math.Round((cy+curve.Y-f.Y*2)*65536);next=next with{TargetSeed=curve.TargetSeed,Packed=curve.Packed,Seed=curve.Seed,Direction=MissileVectors.Direction(dx,dy)};}
            int magnitude=f.VelocityFp8<<4;double vx=(magnitude*next.Direction.X>>12)/131072.0,vy=(magnitude*next.Direction.Y>>12)/131072.0;x+=vx;y+=vy;
        }
        else
        {
            double remaining=SkillGeometry.Tiles(f.VelocityFp8/4096.0);
            while(remaining>0 && index<f.Path.Length)
            {double dx=f.Path[index].X/2.0-x,dy=f.Path[index].Y/2.0-y,length=Math.Sqrt(dx*dx+dy*dy);if(length<=remaining){x+=dx;y+=dy;remaining-=length;index++;}else{x+=dx*remaining/length;y+=dy*remaining/length;remaining=0;}}
        }
        var map=Map.Maps[f.Map];if(next.Frame>=f.Frames || !f.Cold && index>=f.Path.Length || x<0 || y<0 || x>=map.Width || y>=map.Height){Flights.Remove(f.Id);return;}
        next=next with{X=x,Y=y,Next=index,Steps=SourceMissileSteps.Advance(f.X,f.Y,x-f.X,y-f.Y,f.Steps)};
        foreach(var cell in next.Steps.Cells)
        {
            if(cell.X<=0 || cell.Y<=0 || cell.X>=map.Width*2 || cell.Y>=map.Height*2){Flights.Remove(f.Id);return;}
            if(Recipient(owner,next with{NextUtc=f.NextUtc},cell) is{} target)
            {next=Impact(owner,target,next with{NextUtc=f.NextUtc}) with{NextUtc=next.NextUtc};if(f.Cold){Flights.Remove(f.Id);return;}break;}
            if(!map.LineOfSight(new Point3D((int)Math.Round(f.X),(int)Math.Round(f.Y),f.Z+12),new Point3D((cell.X+1)/2,(cell.Y+1)/2,f.Z+12))){Flights.Remove(f.Id);return;}
        }
        Flights[f.Id]=next;
        if(next.Frame%3==0){Effects.SendLocationEffect(new Point3D((int)Math.Round(x),(int)Math.Round(y),f.Z),map,f.Cold?0x374A:0x3818,3,10,f.Cold?0x480:0x482,0);} // Identified travel placeholder.
    }
    private static void Validate(PhoenixFlight f)
    {
        if(f==null || f.Id<=0 || f.Cast<=0 || !((Serial)f.Owner).IsMobile || f.Map<0 || f.Map>=Map.Maps.Length || Map.Maps[f.Map]==null || Map.Maps[f.Map]==Map.Internal || f.Rank is <1 or >255
            || f.Frames!=(f.Cold?40:60) || f.Frame<0 || f.Frame>=f.Frames || f.NextUtc.Kind!=DateTimeKind.Utc || !double.IsFinite(f.X+f.Y) || f.X<0 || f.Y<0 || f.X>=Map.Maps[f.Map].Width || f.Y>=Map.Maps[f.Map].Height
            || f.Z is <-128 or >127 || f.VelocityFp8<0 || f.Minimum256<0 || f.Maximum256<f.Minimum256 || f.Path==null || f.Next<0 || f.Next>f.Path.Length || f.Cold && f.Path.Length!=0 || !f.Cold && f.Path.Length!=60
            || f.ColdFrames<0 || !f.Cold && f.ColdFrames!=0 || f.Slow.VelocityPercent is <0 or >100 || f.Slow.DamagePercent is <0 or >100 || f.Steps?.Cells is not{Length:>=1 and <=10})
        {throw new InvalidDataException("Invalid saved Phoenix flight");}
        if(f.Path.Any(x=>x.X is <0 or >65535 || x.Y is <0 or >65535)){throw new InvalidDataException("Invalid Phoenix path point");}
    }
    internal static PhoenixSave Snapshot()=>new(Drops.Values.OrderBy(x=>x.Id).ToArray(),Flights.Values.OrderBy(x=>x.Id).ToArray(),Leases.Values.OrderBy(x=>x.Cast).ThenBy(x=>x.Target).ToArray(),_nextId,_nextCast);
    internal static void Restore(PhoenixSave save)
    {
        if(save?.Drops==null || save.Flights==null || save.Leases==null || save.NextId<0 || save.NextCast<0){throw new InvalidDataException("Missing Phoenix state");}
        var drops=new Dictionary<long,PhoenixDrop>();var flights=new Dictionary<long,PhoenixFlight>();var leases=new Dictionary<(long,uint),WeaponFlightLease>();
        foreach(var d in save.Drops){if(d.Id<=0 || d.Id>save.NextId || d.Cast<=0 || d.Cast>save.NextCast || d.Frame is <0 or >=60 || d.Rank is <1 or >255 || d.Map<0 || d.Map>=Map.Maps.Length || Map.Maps[d.Map]==null || Map.Maps[d.Map]==Map.Internal || d.X<=0 || d.Y<=0 || d.X>=Map.Maps[d.Map].Width || d.Y>=Map.Maps[d.Map].Height || !((Serial)d.Owner).IsMobile || d.NextUtc.Kind!=DateTimeKind.Utc || !drops.TryAdd(d.Id,d)){throw new InvalidDataException("Invalid Phoenix warning");}}
        foreach(var f in save.Flights){Validate(f);if(f.Id>save.NextId || f.Cast>save.NextCast || drops.ContainsKey(f.Id) || !flights.TryAdd(f.Id,f)){throw new InvalidDataException("Duplicate Phoenix flight");}}
        foreach(var l in save.Leases){if(l.Cast<=0 || l.Cast>save.NextCast || !((Serial)l.Target).IsMobile || l.UntilUtc.Kind!=DateTimeKind.Utc || !leases.TryAdd((l.Cast,l.Target),l)){throw new InvalidDataException("Invalid Phoenix lease");}}
        Drops.Clear();foreach(var d in drops){Drops.Add(d.Key,d.Value);}Flights.Clear();foreach(var f in flights){Flights.Add(f.Key,f.Value);}Leases.Clear();foreach(var l in leases){Leases.Add(l.Key,l.Value);}_nextId=save.NextId;_nextCast=save.NextCast;
    }
}
