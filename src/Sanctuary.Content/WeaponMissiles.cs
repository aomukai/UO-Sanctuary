// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Server.Mobiles;
using Server.Spells;

namespace Sanctuary;

public sealed record WeaponMissileDamage(int Min256,int Max256,int Percent,int DemonPercent,int UndeadPercent,
    bool Critical,MissileSourceStats Elements,int FireMastery=0,int LightningMastery=0,int MagicMastery=0,int ColdMastery=0,int PoisonMastery=0)
{
    internal static int RollValue(int minimum,int maximum,int mastery,Func<int,int> random)
    {
        if(maximum<=0 || minimum<=0){return 0;}
        if(minimum>maximum){(minimum,maximum)=(maximum,minimum);}
        minimum+=(int)((long)minimum*mastery/100);maximum+=(int)((long)maximum*mastery/100);
        int span=Math.Max(0,maximum-minimum);int roll=random(span);
        return minimum+(span>0?Math.Clamp(roll,0,span-1):0);
    }
    internal GearDamage Roll(Mobile target,Func<int,int> random)=>Roll(target!=null,NamedCombat.Demon(target),GemCombat.Undead(target),random);
    internal GearDamage Roll(bool hasTarget,bool demon,bool undead,Func<int,int> random)
    {
        int physical=RollValue(Min256,Max256,0,random);
        int percent=Math.Max(-90,Percent+(demon?DemonPercent:0)+(undead?UndeadPercent:0));
        if(hasTarget){physical+=(int)((long)physical*percent/100);}if(Critical){physical*=2;}
        var e=Elements;
        int fire=RollValue(e.FireMin,e.FireMax,FireMastery,random),magic=RollValue(e.MagicMin,e.MagicMax,MagicMastery,random);
        int lightning=RollValue(e.LightningMin,e.LightningMax,LightningMastery,random),cold=RollValue(e.ColdMin,e.ColdMax,ColdMastery,random);
        int poison=RollValue(e.PoisonMin,e.PoisonMax,PoisonMastery,random);
        return new(physical/256.0,fire/256.0,cold/256.0,lightning/256.0,
            (long)poison*e.PoisonImpactFrames/256.0,e.PoisonImpactFrames*40,e.ColdFrames*40,magic/256.0);
    }
}
public sealed record WeaponFlight(long Id,long Cast,uint Owner,int Map,CombatAbility Ability,double X,double Y,int Z,
    double Vx,double Vy,int Frame,int Frames,int Activation,int NextDelay,int Pierces,uint LastCollision,uint Flags,
    WeaponMissileDamage Damage,DateTime NextUtc);
public sealed record WeaponFlightLease(long Cast,uint Target,DateTime UntilUtc);
public sealed record WeaponFlightSave(WeaponFlight[] Flights,WeaponFlightLease[] Leases,long NextId,long NextCast);

public sealed class WeaponMissileProfiles:GenericPersistence
{
    private WeaponFlightSave _snapshot;
    internal static int LoadedFlights;
    public WeaponMissileProfiles():base("SanctuaryWeaponMissiles",147){}
    public override void Serialize(){_snapshot=WeaponMissiles.Snapshot();base.Serialize();}
    public override void Serialize(IGenericWriter writer){writer.Write(0);writer.Write(JsonSerializer.Serialize(_snapshot));}
    public override void Deserialize(IGenericReader reader)
    {
        if(reader.ReadInt()!=0){throw new InvalidDataException("Unknown weapon missile version");}
        var saved=JsonSerializer.Deserialize<WeaponFlightSave>(reader.ReadString());LoadedFlights=saved?.Flights?.Length??0;
        WeaponMissiles.Restore(saved);
    }
}

// Travelling direct-damage component. Skill launch/stat collection and item-event dispatch remain separate consumers.
public static class WeaponMissiles
{
    private static readonly Dictionary<long,WeaponFlight> _flights=new();
    private static readonly Dictionary<(long Cast,uint Target),WeaponFlightLease> _leases=new();
    private static readonly List<long> _keys=new();
    private static long _nextId,_nextCast;
    internal static Func<int,int> DamageRoll=Utility.Random;
    internal static Func<double> HitRoll=Utility.RandomDouble;
    internal static Random ItemEffects=Random.Shared;
    public static WeaponMissileProfiles Profiles{get;private set;}
    public static void Configure()=>Profiles=new();
    public static void Initialize()=>Timer.StartTimer(TimeSpan.FromMilliseconds(40),TimeSpan.FromMilliseconds(40),()=>Tick(TimedEffects.Now));
    internal static long NewCast()=>++_nextCast;
    internal static WeaponFlight State(long id)=>_flights.GetValueOrDefault(id);
    internal static int CurrentPierce(PlayerMobile owner)=>NamedProjectiles.PierceChance(owner);
    internal static long Launch(PlayerMobile owner,CombatAbility ability,FanRay ray,WeaponMissileDamage damage,long cast,int pierces=0,int velocity=24,int frames=50,int nextDelay=4)
    {
        if(owner is not{Alive:true,Deleted:false} || owner.Map==null || owner.Map==Map.Internal || !ClassCombat.Enabled(owner)
            || SpellHelper.IsTown(owner.Location,owner) || cast<=0 || frames is <1 or >250 || velocity is <1 or >64 || ray.ActivationFrame<0 || nextDelay<0
            || damage?.Elements==null || damage.Elements.LifeDrain<0 || damage.Elements.ManaDrain<0
            || damage.Elements.BurnMin!=0 || damage.Elements.BurnMax!=0 || damage.Elements.BurnFrames!=0){return 0;}
        var v=FanGeometry.Velocity(ray,velocity);long id=++_nextId;
        _flights.Add(id,new(id,cast,owner.Serial.Value,owner.Map.MapID,ability,owner.X,owner.Y,owner.Z,v.X,v.Y,0,frames,
            ray.ActivationFrame,nextDelay,Math.Clamp(pierces,0,4),0,ray.Flags,damage,TimedEffects.Now));return id;
    }
    private static PlayerMobile Owner(WeaponFlight flight)=>World.FindMobile((Serial)flight.Owner) is PlayerMobile{Alive:true,Deleted:false} owner
        && ClassCombat.Enabled(owner) && owner.Map!=null
        && (owner.Map==Map.Internal || owner.Map.MapID==flight.Map && !SpellHelper.IsTown(owner.Location,owner))?owner:null;
    internal static void Cancel(PlayerMobile owner)
    {
        var ids=new List<long>();foreach(var (id,flight) in _flights){if(flight.Owner==owner.Serial.Value){ids.Add(id);}}
        foreach(long id in ids){_flights.Remove(id);}Prune();
    }
    internal static void Tick(DateTime at)
    {
        _keys.Clear();_keys.AddRange(_flights.Keys);_keys.Sort();
        foreach(long id in _keys)
        {
            if(!_flights.TryGetValue(id,out var flight)){continue;}
            var owner=Owner(flight);if(owner==null){_flights.Remove(id);continue;}
            if(owner.Map==Map.Internal)
            {
                if(at>=flight.NextUtc.AddMilliseconds((flight.Frames-flight.Frame)*40L)){_flights.Remove(id);}continue;
            }
            // Process overdue source frames before lifetime completion; lag must not skip impacts.
            while(_flights.TryGetValue(id,out flight) && flight.NextUtc<=at){Step(owner,flight);}
        }
        Prune();
    }
    private static void Step(PlayerMobile owner,WeaponFlight flight)
    {
        var next=flight with{X=flight.X+flight.Vx,Y=flight.Y+flight.Vy,Frame=flight.Frame+1,NextUtc=flight.NextUtc.AddMilliseconds(40)};
        if(next.Frame>=flight.Frames || next.X<0 || next.Y<0 || next.X>=owner.Map.Width || next.Y>=owner.Map.Height
            || !owner.Map.LineOfSight(new Point3D((int)Math.Round(flight.X),(int)Math.Round(flight.Y),flight.Z+12),
                new Point3D((int)Math.Round(next.X),(int)Math.Round(next.Y),flight.Z+12)))
        {_flights.Remove(flight.Id);return;}
        _flights[flight.Id]=next;
        if(next.Frame<flight.Activation){return;}
        Mobile selected=null;double nearest=double.MaxValue;
        var end=new Point3D((int)Math.Round(next.X),(int)Math.Round(next.Y),flight.Z);
        int query=(int)Math.Ceiling(Math.Max(Math.Abs(flight.Vx),Math.Abs(flight.Vy))+CombatHitboxes.BodyRadius+CombatHitboxes.TeethRadius+0.5);
        foreach(var candidate in owner.Map.GetMobilesInRange<Mobile>(end,query))
        {
            if(candidate.Serial.Value==flight.LastCollision || !ClassCombat.IsHostile(owner,candidate)
                || !CombatHitboxes.Intersect(flight.X,flight.Y,next.X,next.Y,flight.Z,candidate.Location,out double entry)){continue;}
            if(entry<nearest || entry==nearest && (selected==null || candidate.Serial.Value<selected.Serial.Value)){selected=candidate;nearest=entry;}
        }
        if(next.Frame%2==0){Effects.SendLocationEffect(end,owner.Map,0xF42,2);}
        if(selected==null){return;}
        if(_leases.TryGetValue((flight.Cast,selected.Serial.Value),out var lease) && lease.UntilUtc>flight.NextUtc){return;}
        next=next with{LastCollision=selected.Serial.Value};_flights[flight.Id]=next;
        if(flight.NextDelay>0){_leases[(flight.Cast,selected.Serial.Value)]=new(flight.Cast,selected.Serial.Value,flight.NextUtc.AddMilliseconds(flight.NextDelay*40L));}
        bool pierce=CurrentPierce(owner)>0 && flight.Pierces>0;
        if(pierce){next=next with{Pierces=flight.Pierces-1};_flights[flight.Id]=next;}
        if(ClassStats.CheckHit(owner,selected,0,HitRoll,projectile:true)!=true){_flights.Remove(flight.Id);return;}
        var damage=flight.Damage.Roll(selected,DamageRoll);
        if(selected is not BaseCreature creature || !SourceBlocking.TryBlock(creature,damage.Physical>0,out bool blocked))
        {_flights.Remove(flight.Id);return;}
        if(!blocked)
        {
            owner.DoHarmful(selected,true);
            var parts=SkillCombat.MitigatePrepared(owner,selected,damage with{Poison=0});
            if((flight.Flags&0x10000)==0){NamedCombat.Crushing(owner,selected,true,ItemEffects);}
            WeaponLeech.Apply(owner,creature,NamedElements.To256(parts.Physical),flight.Damage.Elements);
            int loss=SkillHealth.Damage(owner,selected,NamedElements.To256(parts.Total));CreatureHitRecovery.Apply(creature,owner,loss);
            if(selected.Alive && !selected.Deleted)
            {
                if(damage.Cold>0){GearEffects.ApplyChill(selected,Core.TickCount,damage.ColdMs,owner);}
                if(damage.Poison>0){GearEffects.ApplyPoison(owner,selected,damage.Poison,Core.TickCount,damage.PoisonMs,enhance:false);}
            }
            CombatDamage.Finish(selected,owner);
        }
        if(!pierce){_flights.Remove(flight.Id);}
    }
    private static void Prune()
    {
        if(_flights.Count==0){_leases.Clear();return;}
        var active=new HashSet<long>();foreach(var flight in _flights.Values){active.Add(flight.Cast);}
        var ended=new List<(long,uint)>();foreach(var (key,lease) in _leases){if(!active.Contains(lease.Cast)){ended.Add(key);}}
        foreach(var key in ended){_leases.Remove(key);}
    }
    internal static WeaponFlightSave Snapshot()=>new(new List<WeaponFlight>(_flights.Values).ToArray(),new List<WeaponFlightLease>(_leases.Values).ToArray(),_nextId,_nextCast);
    internal static void Restore(WeaponFlightSave saved)
    {
        _flights.Clear();_leases.Clear();_nextId=saved?.NextId??0;_nextCast=saved?.NextCast??0;
        foreach(var flight in saved?.Flights??[])
        {
            if(flight.Id<=0 || flight.Cast<=0 || flight.Frame<0 || flight.Frame>=flight.Frames || flight.Frames>250 || flight.Damage?.Elements==null
                || flight.Pierces is <0 or >4){throw new InvalidDataException("Invalid saved weapon missile");}
            if(flight.NextUtc.AddMilliseconds((flight.Frames-flight.Frame)*40L)<=TimedEffects.Now){continue;}
            if(!_flights.TryAdd(flight.Id,flight)){throw new InvalidDataException("Duplicate saved weapon missile");}
            _nextId=Math.Max(_nextId,flight.Id);_nextCast=Math.Max(_nextCast,flight.Cast);
        }
        foreach(var lease in saved?.Leases??[]){if(!_leases.TryAdd((lease.Cast,lease.Target),lease)){throw new InvalidDataException("Duplicate weapon missile lease");}}
        Prune();
    }
}
