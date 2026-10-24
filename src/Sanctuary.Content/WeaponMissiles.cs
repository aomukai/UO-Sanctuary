// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Server.Mobiles;
using Server.Items;
using Server.Spells;

namespace Sanctuary;

public sealed record WeaponElementConversion(string Element,int Percent);

public sealed record WeaponMissileDamage(int Min256,int Max256,int Percent,int DemonPercent,int UndeadPercent,
    bool Critical,MissileSourceStats Elements,int FireMastery=0,int LightningMastery=0,int MagicMastery=0,int ColdMastery=0,int PoisonMastery=0)
{
    // Optional saved field: old player/NPC flights retain zero conversion.
    // Magic Arrow captures its DmgCalc at launch; retain this saved field.
    public int PhysicalToMagicPercent{get;init;}
    public int SkillAttackPercent{get;init;}
    // Null retains the original saved Magic Arrow field and old packet meaning.
    public WeaponElementConversion Conversion{get;init;}
    public bool FreezeColdLength{get;init;}
    public int? LightningJavelinPercent{get;init;}
    internal bool ValidConversion=>(LightningJavelinPercent==null || Conversion==null && PhysicalToMagicPercent==0 && !FreezeColdLength) && (!FreezeColdLength || Conversion==null && PhysicalToMagicPercent==0) && (Conversion==null || PhysicalToMagicPercent==0 && Conversion.Element is "fire" or "cold" or "mag");
    // Existing Sanctuary material/potion physical additions are separate from
    // the source weapon endpoints, so victim-specific ED cannot multiply them.
    public WeaponPhysicalSupplement PhysicalSupplement{get;init;}
    internal static (int Physical256,int Magic256) ConvertPhysical(int physical256,int percent)
    {
        if(percent<=0){return(physical256,0);}
        int converted=Math.Min(SourcePercentage.Calculate(physical256,Math.Min(percent,100),100),physical256);
        return(physical256-converted,converted);
    }
    internal static int RollValue(int minimum,int maximum,int mastery,Func<int,int> random)
    {
        if(maximum<=0 || minimum<=0){return 0;}
        if(minimum>maximum){(minimum,maximum)=(maximum,minimum);}
        minimum+=(int)((long)minimum*mastery/100);maximum+=(int)((long)maximum*mastery/100);
        int span=Math.Max(0,maximum-minimum);int roll=random(span);
        return minimum+(span>0?Math.Clamp(roll,0,span-1):0);
    }
    internal GearDamage Roll(Mobile target,Func<int,int> random)=>Roll(target!=null,NamedCombat.Demon(target),GemCombat.Undead(target),random);
    internal GearDamage Roll(Mobile target,Func<int,int> random,out int poisonRate256)=>Roll(target!=null,NamedCombat.Demon(target),GemCombat.Undead(target),random,out poisonRate256);
    internal GearDamage Roll(bool hasTarget,bool demon,bool undead,Func<int,int> random)
        =>Roll(hasTarget,demon,undead,random,out _);
    private GearDamage Roll(bool hasTarget,bool demon,bool undead,Func<int,int> random,out int poisonRate256)
    {
        int physical=RollValue(Min256,Max256,0,random);
        int percent=Math.Max(-90,Percent+(demon?DemonPercent:0)+(undead?UndeadPercent:0));
        if(hasTarget){physical+=(int)((long)physical*percent/100);}
        if(PhysicalSupplement is{} supplement){physical+=supplement.Roll(random);}
        if(Critical){physical*=2;}
        var e=Elements;
        int fire=RollValue(e.FireMin,e.FireMax,FireMastery,random),magic=RollValue(e.MagicMin,e.MagicMax,MagicMastery,random);
        int lightning=RollValue(e.LightningMin,e.LightningMax,LightningMastery,random),cold=RollValue(e.ColdMin,e.ColdMax,ColdMastery,random);
        int poison=RollValue(e.PoisonMin,e.PoisonMax,PoisonMastery,random);
        poisonRate256=poison;
        if(LightningJavelinPercent is >0)
        {
            int convertedLightning=ConvertPhysical(physical,LightningJavelinPercent.Value).Magic256;
            // srvdmg12 deliberately rerolls the lightning endpoint, then resets
            // all damage, lengths and drain channels before adding lightning.
            int second=RollValue(e.LightningMin,e.LightningMax,LightningMastery,random);poisonRate256=0;
            return new(0,0,0,(second+convertedLightning)/256.0,0,0,0);
        }
        // srvdmg1 runs after FillDamageParams: conversion sees the percentage-
        // enhanced, critical physical roll; existing elemental mastery is not
        // reapplied to this converted amount.
        var converted=ConvertPhysical(physical,Conversion?.Percent??PhysicalToMagicPercent);
        physical=converted.Physical256;
        switch(Conversion?.Element??"mag")
        {
            case "fire":fire+=converted.Magic256;break;
            case "cold":cold+=converted.Magic256;break;
            case "mag":magic+=converted.Magic256;break;
            default:throw new NotSupportedException("Unsupported saved weapon conversion element");
        }
        return new(physical/256.0,fire/256.0,cold/256.0,lightning/256.0,
            (long)poison*e.PoisonImpactFrames/256.0,e.PoisonImpactFrames*40,e.ColdFrames*40,magic/256.0);
    }
}
public sealed record WeaponFlight(long Id,long Cast,uint Owner,int Map,CombatAbility Ability,double X,double Y,int Z,
    double Vx,double Vy,int Frame,int Frames,int Activation,int NextDelay,int Pierces,uint LastCollision,uint Flags,
    WeaponMissileDamage Damage,DateTime NextUtc)
{
    // Optional version0 properties preserve existing player-flight saves.
    public int SkillRank{get;init;}
    public GuidedFlight Guided{get;init;}
    public bool HostileSpell{get;init;}
    public string Missile{get;init;}
    public SlowMissilePayload? Slow{get;init;}
    public bool PathChanged{get;init;}
    public SourceStepTrace SourceSteps{get;init;}
}
public sealed record WeaponFlightLease(long Cast,uint Target,DateTime UntilUtc)
{
    // Strafe reuses its cast across individually emitted arrows. Keep its
    // cast-local gate through empty-flight gaps; modern cross-cast/supplier
    // same-skill scope remains a separately recorded acceptance gap.
    public bool RetainWithoutFlight{get;init;}
}
public sealed record WeaponFlightSave(WeaponFlight[] Flights,WeaponFlightLease[] Leases,long NextId,long NextCast)
{
    public BowExplosion[] Explosions{get;init;}=[];
    public PoisonCloud[] PoisonClouds{get;init;}=[];
    public PlagueCloud[] PlagueClouds{get;init;}=[];
}

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
    internal static long Launch(PlayerMobile owner,CombatAbility ability,FanRay ray,WeaponMissileDamage damage,long cast,int pierces=0,int velocity=24,int frames=50,int nextDelay=4,string missile=null,int skillRank=0,GuidedFlight guided=null)
    {
        if(owner is not{Alive:true,Deleted:false} || owner.Map==null || owner.Map==Map.Internal || !ClassCombat.Enabled(owner)
            || SpellHelper.IsTown(owner.Location,owner) || cast<=0 || frames is <1 or >250 || velocity is <1 or >64 || ray.ActivationFrame<0 || nextDelay<0
            || ability is CombatAbility.PlagueJavelin or CombatAbility.LightningBolt or CombatAbility.PoisonJavelin or CombatAbility.ExplodingArrow or CombatAbility.FreezingArrow or CombatAbility.ImmolationArrow && skillRank<1
            || ability==CombatAbility.GuidedArrow && (!GuidedArrows.Valid(guided) || missile!="guidedarrow" || pierces!=0 || nextDelay!=0 || skillRank<1 || frames!=Math.Max(1,guided.Remaining) || ray.ActivationFrame!=0 || guided.Mode is not(1 or 2) || ray.Flags!=(guided.Mode==1?0x20u:0x420u))
            || ability!=CombatAbility.GuidedArrow && guided!=null
            || ability==CombatAbility.LightningBolt && (missile!="lightningjavelin" || skillRank is <1 or >255 || frames!=25 || nextDelay!=0 || ray.ActivationFrame!=0 || damage?.LightningJavelinPercent!=100)
            || ability!=CombatAbility.LightningBolt && damage?.LightningJavelinPercent!=null
            || damage?.Elements==null || !damage.ValidConversion || damage.SkillAttackPercent<0 || ability!=CombatAbility.Strafe && damage.SkillAttackPercent!=0 || damage.Elements.LifeDrain<0 || damage.Elements.ManaDrain<0
            || damage.Elements.BurnMin!=0 || damage.Elements.BurnMax!=0 || damage.Elements.BurnFrames!=0){return 0;}
        var v=FanGeometry.Velocity(ray,velocity);long id=++_nextId;
        _flights.Add(id,new(id,cast,owner.Serial.Value,owner.Map.MapID,ability,owner.X,owner.Y,owner.Z,v.X,v.Y,0,frames,
            ray.ActivationFrame,nextDelay,Math.Clamp(pierces,0,4),0,ray.Flags,damage,TimedEffects.Now){Missile=missile,SkillRank=skillRank,Guided=guided,SourceSteps=ability==CombatAbility.LightningBolt?new([new(owner.X*2,owner.Y*2)]):null});return id;
    }
    internal static long LaunchHostileFirebolt(BaseCreature owner,Point3D aim,int minimum256,int maximum256)
    {
        if(owner is not{Alive:true,Deleted:false} || owner.Map==null || owner.Map==Map.Internal
            || SpellHelper.IsTown(owner.Location,owner) || minimum256<=0 || maximum256<minimum256){return 0;}
        const string missile="firebolt";int frames=NecromancerSummons.Source(missile,"Range");
        var slow=AmazonSlowMissiles.Capture(owner,NecromancerSummons.Source(missile,"CanSlow")!=0);
        int velocity=SlowMissilePayload.VelocityFp8(NecromancerSummons.Source(missile,"Vel"),NecromancerSummons.Source(missile,"VelLev"),1,0,0,true,slow.VelocityPercent);
        int dx=aim.X-owner.X,dy=aim.Y-owner.Y;if(dx==0 && dy==0){dx=dy=1;}
        var vector=MissileVectors.VelocityFp8(dx,dy,velocity);long id=++_nextId;
        var elements=slow.Scale(new(FireMin:minimum256,FireMax:maximum256));
        _flights.Add(id,new(id,NewCast(),owner.Serial.Value,owner.Map.MapID,CombatAbility.None,owner.X,owner.Y,owner.Z,
            vector.X,vector.Y,0,frames,0,0,0,0,0,new(0,0,0,0,0,false,elements),TimedEffects.Now)
            {HostileSpell=true,Missile=missile,Slow=slow});return id;
    }
    private static Mobile Owner(WeaponFlight flight)
    {
        var owner=World.FindMobile((Serial)flight.Owner);
        if(owner is not{Alive:true,Deleted:false} || owner.Map==null){return null;}
        if(flight.HostileSpell)
        {return owner is BaseCreature && owner.Map!=Map.Internal && owner.Map.MapID==flight.Map && !SpellHelper.IsTown(owner.Location,owner)?owner:null;}
        return owner is PlayerMobile player && ClassCombat.Enabled(player)
            && (owner.Map==Map.Internal || owner.Map.MapID==flight.Map && !SpellHelper.IsTown(owner.Location,owner))?owner:null;
    }
    private static bool IncomingTarget(BaseCreature source,Mobile target)=>target is{Alive:true,Deleted:false,Blessed:false,IsDeadBondedPet:false}
        && target!=source && target.Map==source.Map && !ClassCombat.Friendly(source,target)
        && source.IsEnemy(target) && source.CanSee(target) && source.CanBeHarmful(target,false);
    internal static void Cancel(Mobile owner)
    {
        var ids=new List<long>();foreach(var (id,flight) in _flights){if(flight.Owner==owner.Serial.Value){ids.Add(id);}}
        foreach(long id in ids){_flights.Remove(id);}BowExplosions.Cancel(owner);PoisonJavelins.ClearClouds(owner);PlagueJavelins.Clear(owner);Prune();
    }
    internal static void CancelPoison(Mobile owner)
    {foreach(long id in new List<long>(_flights.Keys)){if(_flights[id].Owner==owner.Serial.Value && _flights[id].Ability==CombatAbility.PoisonJavelin){_flights.Remove(id);}}PoisonJavelins.ClearClouds(owner);Prune();}
    internal static void CancelJavelins(Mobile owner)
    {foreach(long id in new List<long>(_flights.Keys)){if(_flights[id].Owner==owner.Serial.Value && AmazonJavelins.Handles(_flights[id].Ability)){_flights.Remove(id);}}PoisonJavelins.ClearClouds(owner);PlagueJavelins.Clear(owner);Prune();}
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
            while(_flights.TryGetValue(id,out flight) && flight.NextUtc<=at){BowExplosions.Tick(flight.NextUtc);PoisonJavelins.Tick(flight.NextUtc);PlagueJavelins.Tick(flight.NextUtc);Step(owner,flight);}
        }
        BowExplosions.Tick(at);PoisonJavelins.Tick(at);PlagueJavelins.Tick(at);Prune();
    }
    private static void Step(Mobile owner,WeaponFlight flight)
    {
        if(flight.Ability==CombatAbility.PoisonJavelin && flight.PathChanged){PoisonJavelins.Trail((PlayerMobile)owner,flight);}
        if(flight.Guided!=null){flight=GuidedArrows.Steer((PlayerMobile)owner,flight);}
        var next=flight with{X=flight.X+flight.Vx,Y=flight.Y+flight.Vy,Frame=flight.Frame+1,NextUtc=flight.NextUtc.AddMilliseconds(40)};
        if(flight.Ability==CombatAbility.PoisonJavelin)
        {next=next with{PathChanged=(int)Math.Floor(flight.X*2+.5)!=(int)Math.Floor(next.X*2+.5)||(int)Math.Floor(flight.Y*2+.5)!=(int)Math.Floor(next.Y*2+.5)};}
        if(flight.Guided==null && next.Frame>=flight.Frames || next.X<0 || next.Y<0 || next.X>=owner.Map.Width || next.Y>=owner.Map.Height
            || !owner.Map.LineOfSight(new Point3D((int)Math.Round(flight.X),(int)Math.Round(flight.Y),flight.Z+12),
                new Point3D((int)Math.Round(next.X),(int)Math.Round(next.Y),flight.Z+12)))
        {if(!flight.HostileSpell){BowExplosions.Create((PlayerMobile)owner,next,flight.NextUtc);PlagueJavelins.Burst((PlayerMobile)owner,next,null,flight.NextUtc);} _flights.Remove(flight.Id);return;}
        if(flight.Guided is{} guidance)
        {
            next=next with{Guided=guidance with{Remaining=guidance.Remaining-1}};
            if(next.Guided.Remaining<=0)
            {
                var extended=GuidedArrows.Extend((PlayerMobile)owner,next);
                if(extended==null){_flights.Remove(flight.Id);}else{_flights[flight.Id]=extended;}return;
            }
        }
        if(flight.Ability==CombatAbility.LightningBolt){next=next with{SourceSteps=SourceMissileSteps.Advance(flight)};}
        _flights[flight.Id]=next;
        if(next.Frame<flight.Activation){return;}
        Mobile selected=null;double nearest=double.MaxValue;
        var end=new Point3D((int)Math.Round(next.X),(int)Math.Round(next.Y),flight.Z);
        int query=(int)Math.Ceiling(Math.Max(Math.Abs(flight.Vx),Math.Abs(flight.Vy))+CombatHitboxes.BodyRadius+CombatHitboxes.TeethRadius+0.5);
        if(flight.Ability==CombatAbility.LightningBolt)
        {
            foreach(var cell in next.SourceSteps.Cells)
            {
                var point=new Point3D((int)Math.Round(cell.X/2.0),(int)Math.Round(cell.Y/2.0),flight.Z);
                foreach(var candidate in owner.Map.GetMobilesInRange<Mobile>(point,3))
                {
                    int size=candidate is BaseCreature c?CreatureProfiles.For(c).Traits.Size??2:2;
                    if(Math.Abs(candidate.Z-flight.Z)<16 && ClassCombat.IsHostile((PlayerMobile)owner,candidate)
                        && CombatHitboxes.SourceTouches(2,size,cell.X-candidate.X*2,cell.Y-candidate.Y*2)){selected=candidate;break;}
                }
                if(selected!=null){break;}
            }
        }
        else
        {
        foreach(var candidate in owner.Map.GetMobilesInRange<Mobile>(end,query))
        {
            if(candidate.Serial.Value==flight.LastCollision && flight.Ability is not(CombatAbility.PoisonJavelin or CombatAbility.PlagueJavelin) || !(flight.HostileSpell?IncomingTarget((BaseCreature)owner,candidate):ClassCombat.IsHostile((PlayerMobile)owner,candidate))
                || !CombatHitboxes.Intersect(flight.X,flight.Y,next.X,next.Y,flight.Z,candidate.Location,out double entry)){continue;}
            if(entry<nearest || entry==nearest && (selected==null || candidate.Serial.Value<selected.Serial.Value)){selected=candidate;nearest=entry;}
        }
        }
        if(flight.HostileSpell)
        {
            // Barrier connectors are item footprints, not additional mobiles.
            foreach(var tile in owner.Map.GetItemsInRange<BoneBarrierTile>(end,query))
            {
                if(tile.Segment is not{Alive:true,Deleted:false} segment || !IncomingTarget((BaseCreature)owner,segment)
                    || !CombatHitboxes.Intersect(flight.X,flight.Y,next.X,next.Y,flight.Z,tile.Location,out double entry)){continue;}
                if(entry<nearest || entry==nearest && (selected==null || segment.Serial.Value<selected.Serial.Value))
                {selected=segment;nearest=entry;}
            }
        }
        if(next.Frame%2==0){Effects.SendLocationEffect(end,owner.Map,flight.HostileSpell?0x36D4:0xF42,2);}
        if(selected==null){return;}
        if(flight.HostileSpell)
        {
            HostileImpact((BaseCreature)owner,selected,flight);_flights.Remove(flight.Id);return;
        }
        var player=(PlayerMobile)owner;
        if(AmazonJavelins.Handles(flight.Ability) && selected.Serial.Value==flight.LastCollision){return;}
        if(_leases.TryGetValue((flight.Cast,selected.Serial.Value),out var lease) && lease.UntilUtc>flight.NextUtc){return;}
        next=next with{LastCollision=selected.Serial.Value};_flights[flight.Id]=next;
        if(flight.NextDelay>0){_leases[(flight.Cast,selected.Serial.Value)]=new(flight.Cast,selected.Serial.Value,flight.NextUtc.AddMilliseconds(flight.NextDelay*40L)){RetainWithoutFlight=flight.Ability==CombatAbility.Strafe};}
        if(next.Guided!=null && !GuidedArrows.Accepts(next,selected)){return;}
        bool pierce=CurrentPierce(player)>0 && flight.Pierces>0;
        if(pierce){next=next with{Pierces=flight.Pierces-1};_flights[flight.Id]=next;}
        bool hit=flight.Ability==CombatAbility.LightningBolt || next.Guided!=null || ClassStats.CheckHit(owner,selected,flight.Damage.SkillAttackPercent,HitRoll,projectile:true)==true;
        if(flight.Ability==CombatAbility.ImmolationArrow)
        {
            // hit09 clears missile cold length before the direct hit.
            // The separate attacker item-freeze event remains active.
            next=next with{Damage=next.Damage with{Elements=next.Damage.Elements with{ColdFrames=0}}};
            flight=flight with{Damage=next.Damage};_flights[flight.Id]=next;
        }
        BowExplosions.Create(player,next,flight.NextUtc);PlagueJavelins.Burst(player,next,selected,flight.NextUtc); // AlwaysExplode survives a parent attack miss.
        if(!hit){_flights.Remove(flight.Id);return;}
        var damage=flight.Damage.Roll(selected,DamageRoll,out int poisonRate256);
        if(selected is not BaseCreature creature || !SourceBlocking.TryBlock(creature,damage.Physical>0,out bool blocked))
        {_flights.Remove(flight.Id);return;}
        if(!blocked)
        {
            owner.DoHarmful(selected,true);
            var parts=SkillCombat.MitigatePrepared(owner,selected,damage with{Poison=0});
            if((flight.Flags&0x10000)==0){NamedCombat.Crushing(player,selected,true,ItemEffects);}
            NecromancerEffects.LifeTap(owner,selected,NamedElements.To256(parts.Physical));
            WeaponLeech.Apply(player,creature,NamedElements.To256(parts.Physical),flight.Damage.Elements);
            if(damage.Cold>0)
            {
                if(flight.Damage.FreezeColdLength){FreezeStates.Apply(owner,selected,flight.Damage.Elements.ColdFrames,Core.TickCount);}
                else{ColdStates.Apply(owner,selected,flight.Damage.Elements.ColdFrames,Core.TickCount);}
            }
            int loss=SkillHealth.Damage(owner,selected,NamedElements.To256(parts.Total));CreatureHitRecovery.Apply(creature,owner,loss);
            if(selected.Alive && !selected.Deleted)
            {
                if(flight.Ability is CombatAbility.PoisonJavelin or CombatAbility.PlagueJavelin){PoisonJavelins.Poison(player,creature,poisonRate256,flight.Damage.Elements.PoisonImpactFrames,flight.NextUtc);}
                else if(damage.Poison>0){GearEffects.ApplyPoison(owner,selected,damage.Poison,Core.TickCount,damage.PoisonMs,enhance:false);}
            }
            if(AmazonBow.Handles(flight.Ability) || AmazonJavelins.Handles(flight.Ability))
            {
                // Preserve the separate attacker/defender event paths. Leech
                // and critical were already delivered from the saved packet.
                if((flight.Flags&0x10000)==0)
                {
                    NecromancerEffects.WeaponHit(player,selected,Math.Min(loss,(int)parts.Physical),true,lifeTap:false);
                    NamedCombat.AfterStrike(player,selected,true,ItemEffects);
                }
                GemCombat.WeaponHit(player,selected,loss,true);
            }
            CombatDamage.Finish(selected,owner);
        }
        if(!pierce){_flights.Remove(flight.Id);}
    }
    private static void HostileImpact(BaseCreature source,Mobile target,WeaponFlight flight)
    {
        // Return-fire is an earlier missile event, independent of avoidance.
        SorceressEffects.MissileHit(source,target,flight.Missile);
        target.FixedParticles(0x3709,10,10,5052,EffectLayer.LeftFoot);target.PlaySound(0x208);
        // Preserve the earlier native damage-draw-before-avoidance order.
        var damage=flight.Damage.Roll(target,DamageRoll);
        if(AmazonAvoidance.Missile(target)){return;}
        source.DoHarmful(target,true);
        var parts=SkillCombat.MitigatePrepared(source,target,damage);
        int loss=SkillHealth.Damage(source,target,NamedElements.To256(parts.Total));
        if(target is PlayerMobile player){HitRecovery.Apply(player,loss,Core.TickCount);}
        else if(target is BaseCreature creature){CreatureHitRecovery.Apply(creature,source,loss);}
        CombatDamage.Finish(target,source);
    }
    private static void Prune()
    {
        var active=new HashSet<long>();foreach(var flight in _flights.Values){active.Add(flight.Cast);}
        var ended=new List<(long,uint)>();foreach(var (key,lease) in _leases){if(!active.Contains(lease.Cast) && (!lease.RetainWithoutFlight || lease.UntilUtc<=TimedEffects.Now)){ended.Add(key);}}
        foreach(var key in ended){_leases.Remove(key);}
    }
    internal static WeaponFlightSave Snapshot()=>new(new List<WeaponFlight>(_flights.Values).ToArray(),new List<WeaponFlightLease>(_leases.Values).ToArray(),_nextId,_nextCast){Explosions=BowExplosions.Snapshot(),PoisonClouds=PoisonJavelins.Snapshot(),PlagueClouds=PlagueJavelins.Snapshot()};
    internal static void Restore(WeaponFlightSave saved)
    {
        _flights.Clear();_leases.Clear();_nextId=saved?.NextId??0;_nextCast=saved?.NextCast??0;
        foreach(var flight in saved?.Flights??[])
        {
            if(flight.Id<=0 || flight.Cast<=0 || flight.Ability is CombatAbility.PlagueJavelin or CombatAbility.PoisonJavelin or CombatAbility.ExplodingArrow or CombatAbility.FreezingArrow or CombatAbility.ImmolationArrow && flight.SkillRank<1 || flight.Frame<0 || flight.Frame>=flight.Frames || flight.Frames>250 || flight.Damage?.Elements==null || !flight.Damage.ValidConversion || flight.Damage.SkillAttackPercent<0 || flight.Ability!=CombatAbility.Strafe && flight.Damage.SkillAttackPercent!=0
                || flight.Ability==CombatAbility.LightningBolt && (flight.Missile!="lightningjavelin" || flight.SkillRank is <1 or >255 || flight.Activation!=0 || flight.NextDelay!=0 || flight.Damage.LightningJavelinPercent!=100 || flight.SourceSteps?.Cells is not{Length:>=1 and <=10})
                || flight.SourceSteps?.Cells is{} sourceCells && Array.Exists(sourceCells,c=>c.X is <0 or >65535 || c.Y is <0 or >65535)
                || flight.Ability!=CombatAbility.LightningBolt && (flight.SourceSteps!=null || flight.Damage.LightningJavelinPercent!=null)
                || flight.PathChanged && flight.Ability!=CombatAbility.PoisonJavelin
                || flight.Ability==CombatAbility.GuidedArrow && (!GuidedArrows.Valid(flight.Guided) || flight.Missile!="guidedarrow" || flight.Pierces!=0 || flight.NextDelay!=0 || flight.SkillRank<1
                    || flight.Damage.Conversion!=null || flight.Damage.PhysicalToMagicPercent!=0 || flight.Damage.FreezeColdLength || flight.HostileSpell || flight.Activation!=0
                    || flight.Frames!=flight.Frame+Math.Max(1,flight.Guided.Remaining) || flight.Flags!=(flight.Guided.Mode==1?0x20u:0x420u))
                || flight.Ability!=CombatAbility.GuidedArrow && flight.Guided!=null
                || flight.Pierces is <0 or >4 || flight.HostileSpell && (flight.Missile!="firebolt" || flight.Pierces!=0
                    || flight.Slow is not{} slow || slow.VelocityPercent is <0 or >100 || slow.DamagePercent is <0 or >100
                    || flight.Damage.PhysicalSupplement!=null || flight.Damage.FreezeColdLength || flight.Damage.Conversion!=null || flight.Damage.PhysicalToMagicPercent!=0 || flight.Damage.Min256!=0 || flight.Damage.Max256!=0 || flight.Damage.Elements.ColdMin!=0 || flight.Damage.Elements.ColdMax!=0
                    || flight.Damage.Elements.LightningMin!=0 || flight.Damage.Elements.LightningMax!=0 || flight.Damage.Elements.MagicMin!=0 || flight.Damage.Elements.MagicMax!=0
                    || flight.Damage.Elements.PoisonMin!=0 || flight.Damage.Elements.PoisonMax!=0 || flight.Damage.Elements.BurnMin!=0 || flight.Damage.Elements.BurnMax!=0))
            {throw new InvalidDataException("Invalid saved weapon missile");}
            if(flight.NextUtc.AddMilliseconds((flight.Frames-flight.Frame)*40L)<=TimedEffects.Now){continue;}
            if(!_flights.TryAdd(flight.Id,flight)){throw new InvalidDataException("Duplicate saved weapon missile");}
            _nextId=Math.Max(_nextId,flight.Id);_nextCast=Math.Max(_nextCast,flight.Cast);
        }
        foreach(var lease in saved?.Leases??[]){if(!_leases.TryAdd((lease.Cast,lease.Target),lease)){throw new InvalidDataException("Duplicate weapon missile lease");}}
        BowExplosions.Restore(saved?.Explosions??[]);PoisonJavelins.Restore(saved?.PoisonClouds??[]);PlagueJavelins.Restore(saved?.PlagueClouds??[]);Prune();
    }
}
