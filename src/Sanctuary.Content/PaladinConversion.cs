// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Server;
using Server.Mobiles;

namespace Sanctuary;

public sealed record ConversionSourceRule(string Source,string Base,bool Evil,bool SwitchAi,bool Walk,bool Boss);
public sealed record ConversionAllegiance(uint Target,uint Source,DateTime UntilUtc);
public sealed class ConversionProfiles:GenericPersistence
{
    public ConversionProfiles():base("SanctuaryConversion",137){}
    public override void Serialize(IGenericWriter writer)
    {writer.Write(0);writer.Write(JsonSerializer.Serialize(PaladinConversion.Snapshot()));}
    public override void Deserialize(IGenericReader reader)
    {
        if(reader.ReadInt()!=0){throw new InvalidDataException("Unknown Conversion allegiance version");}
        PaladinConversion.Restore(JsonSerializer.Deserialize<ConversionAllegiance[]>(reader.ReadString()));
    }
    public override void PostDeserialize()=>PaladinConversion.Prune();
}

// Species-preserving allegiance. Unresolved higher-level life restoration is an
// individual target gate; the ordinary attack and supported conversion paths remain available.
public static class PaladinConversion
{
    internal sealed record Prepared(Serial Target,bool Hit,GearDamage Damage,double ElementRoll,long EffectAt);
    internal static Func<double> HitRoll=Utility.RandomDouble;
    internal static Func<double> ElementRoll=Utility.RandomDouble;
    internal static Func<int,int> PhysicalRoll=Utility.Random;
    internal static Func<int,int> ChanceRoll=Utility.Random;
    private static readonly Dictionary<string,ConversionSourceRule> _rules=new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<uint,ConversionAllegiance> _states=new();
    private static readonly List<uint> _expired=new();
    public static ConversionProfiles Profiles{get;private set;}
    public static void Load()
    {
        _rules.Clear();
        foreach(var row in BalanceCatalog.Read<ConversionSourceRule>("conversion-source-rules")){_rules.Add(row.Source,row);}
    }
    public static void Configure()
    {
        Profiles=new();
        ClassCombatHooks.CreatureEnemy=Enemy;
        ClassCombatHooks.CreatureFriend=Friend;
        ClassCombatHooks.CreatureDamageMaster=DamageMaster;
    }
    public static void Initialize()=>Timer.StartTimer(TimeSpan.FromMilliseconds(40),TimeSpan.FromMilliseconds(40),Prune);
    private static ConversionAllegiance Lease(Mobile actor)=>actor!=null && _states.TryGetValue(actor.Serial.Value,out var state)
        && state.UntilUtc>TimedEffects.Now?state:null;
    public static bool Active(Mobile actor)=>actor is BaseCreature{Alive:true,Deleted:false} && Lease(actor)!=null;
    internal static bool WasConverted(Mobile actor)=>actor!=null && Lease(actor)!=null;
    private static bool Cooperative(Mobile actor)=>actor is PlayerMobile or SkeletonGuardian
        || actor is BaseCreature creature && creature.GetMaster() is PlayerMobile;
    internal static bool Friendly(Mobile actor,Mobile target)=>actor!=null && target!=null
        && (Active(actor) && (Cooperative(target) || Active(target)) || Active(target) && Cooperative(actor));
    private static bool WildEnemy(Mobile actor)=>actor is BaseCreature{Alive:true,Deleted:false,Blessed:false,Controlled:false,Summoned:false,
        IsDeadBondedPet:false,InitialInnocent:false} creature && creature is not BaseVendor && creature.GetMaster()==null
        && creature.FightMode is not(FightMode.None or FightMode.Aggressor or FightMode.Evil);
    public static bool? Enemy(BaseCreature actor,Mobile target)
    {
        bool ours=Active(actor),theirs=Active(target);
        if(!ours && !theirs){return null;}
        if(actor==target || Friendly(actor,target)){return false;}
        return ours?WildEnemy(target):theirs && WildEnemy(actor);
    }
    public static bool? Friend(BaseCreature actor,Mobile target)=>Active(actor) || Active(target)?Friendly(actor,target):null;
    public static Mobile DamageMaster(BaseCreature actor,Mobile target)=>Active(actor) && Lease(actor) is{} state
        && World.FindMobile((Serial)state.Source) is PlayerMobile{Deleted:false} owner?owner:null;
    internal static bool Eligible(PlayerMobile owner,BaseCreature target)
    {
        if(owner is not{Alive:true,Deleted:false} || !ClassCombat.Enabled(owner) || !ClassCombat.IsHostile(owner,target)
            || target is BaseHire || NamedCombat.Elite(target) || target.BardProvoked){return false;}
        string key=CreatureProfiles.Definition(target).Reference;
        if(key==null || !_rules.TryGetValue(key,out var rule) || !rule.Evil || !rule.SwitchAi || !rule.Walk || rule.Boss){return false;}
        // Mounted imp attachment and higher-level life restoration have no verified
        // native consumer. Do not silently approximate either source branch.
        return rule.Base!="imp1" && ClassStats.Level(owner)>=ClassStats.Level(target);
    }
    private static bool Supported(PlayerMobile owner,BaseCreature target)
    {
        string key=target==null?null:CreatureProfiles.Definition(target).Reference;
        if(key==null || !_rules.TryGetValue(key,out var rule) || rule.Base=="imp1" || target.BardProvoked){return false;}
        bool convertible=rule.Evil && rule.SwitchAi && rule.Walk && !rule.Boss && !NamedCombat.Elite(target) && target is not BaseHire;
        return !convertible || ClassStats.Level(owner)>=ClassStats.Level(target);
    }
    public static bool ValidTarget(PlayerMobile owner,Serial selected)=>PaladinVengeance.ValidTarget(owner,selected)
        && World.FindMobile(selected) is BaseCreature target && Supported(owner,target);
    public static bool Begin(PlayerMobile owner,Serial selected,Point3D point,long now)
    {
        var session=ClassCombat.For(owner);
        if(now-session.NextAction>=0 && PaladinVengeance.ValidTarget(owner,selected)
            && World.FindMobile(selected) is BaseCreature unsupported && !Supported(owner,unsupported))
        {
            owner.SendMessage("Conversion is not supported for this creature or level case yet.");
            session.NextAction=now+1000;return false;
        }
        if(!SkillCombat.Begin(owner,CombatAbility.Conversion,selected,point,now,(_,_,id,_)=>ValidTarget(owner,id))){return false;}
        var target=World.FindMobile(selected);var values=SkillMechanics.Values(owner,CombatAbility.Conversion);
        bool hit=ClassStats.CheckHit(owner,target,0,HitRoll)==true;double roll=ElementRoll();
        var damage=hit?GearEffects.Damage(owner,roll,target,(int)values.Eval("calc1"),PhysicalRoll):default;
        if(!NecromancerEffects.Pay(owner,values.Mana256)){session.PendingAbility=CombatAbility.None;return false;}
        session.Conversion=new(selected,hit,damage,roll,session.EffectAt);return true;
    }
    public static bool Release(PlayerMobile owner,Serial selected,long now)
    {
        var session=ClassCombat.For(owner);var prepared=session.Conversion;
        if(prepared==null || now-prepared.EffectAt<0){return false;}
        session.Conversion=null;
        if(prepared.Target!=selected || !PaladinCombat.Available(owner,CombatAbility.Conversion) || !ValidTarget(owner,selected)){return false;}
        var target=(BaseCreature)World.FindMobile(selected);var values=SkillMechanics.Values(owner,CombatAbility.Conversion);
        // Do79 tests prepared combat-record presence, not the successful-hit bit.
        // Successful conversion bypasses durability/damage commitment entirely.
        if(Eligible(owner,target) && ChanceRoll(100)<values.Eval("calc1") && ApplyAllegiance(owner,target,(int)values.Eval("auralencalc"))){return true;}
        owner.DoHarmful(target,true);owner.PlaySound(0x238); // Ordinary weapon sound placeholder.
        using var attempt=PaladinThorns.Attempt(owner,target,false);
        if(prepared.Hit){GearEffects.Strike(owner,target,prepared.ElementRoll,preparedDamage:prepared.Damage);}
        return true;
    }
    internal static bool ApplyAllegiance(PlayerMobile owner,BaseCreature target,int frames)
    {
        if(frames<=0 || !Eligible(owner,target)){return false;}
        _states[target.Serial.Value]=new(target.Serial.Value,owner.Serial.Value,TimedEffects.Now.AddMilliseconds(frames*40L));
        ResetTargeting(target);
        // The converting attack relinquishes its old enemy; committed projectiles
        // still run through the ordinary friendly-damage gates at delivery time.
        ClassCombat.Stop(owner);
        return true;
    }
    private static void ResetTargeting(BaseCreature creature)
    {
        creature.Combatant=null;creature.FocusMob=null;
        creature.AIObject?.ResetApproachState();creature.AIObject?.ClearMoveIntent();
        if(creature is GraveyardUndead undead){undead.InterruptForRecovery();}
        if(creature.AIObject is{} ai){ai.Action=ActionType.Wander;}
        // Preserve the species' timer/cooldown. Allegiance expiry has no invented grace period.
    }
    public static void Prune()
    {
        _expired.Clear();
        foreach(var state in _states.Values)
        {if(state.UntilUtc<=TimedEffects.Now || World.FindMobile((Serial)state.Target) is not BaseCreature{Alive:true,Deleted:false}){_expired.Add(state.Target);}}
        foreach(uint serial in _expired)
        {
            _states.Remove(serial);
            if(World.FindMobile((Serial)serial) is BaseCreature{Alive:true,Deleted:false} target){ResetTargeting(target);}
        }
    }
    public static ConversionAllegiance[] Snapshot()
    {Prune();return new List<ConversionAllegiance>(_states.Values).ToArray();}
    public static void Restore(ConversionAllegiance[] rows)
    {
        if(rows==null){throw new InvalidDataException("Missing Conversion allegiance");}
        var next=new Dictionary<uint,ConversionAllegiance>();
        foreach(var row in rows)
        {
            if(!((Serial)row.Target).IsMobile || !((Serial)row.Source).IsMobile || row.UntilUtc.Kind!=DateTimeKind.Utc
                || !next.TryAdd(row.Target,row)){throw new InvalidDataException("Invalid Conversion allegiance");}
        }
        _states.Clear();foreach(var row in next){_states.Add(row.Key,row.Value);}
    }
}
