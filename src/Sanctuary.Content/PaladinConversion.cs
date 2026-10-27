// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Server;
using Server.Mobiles;

namespace Sanctuary;

public sealed record ConversionSourceRule(string Source,string Base,bool Evil,bool SwitchAi,bool Walk,bool Boss,bool Run=false,bool Attack1=false,bool Attackable=false,bool Npc=false,bool Interact=false,int Velocity=0,int RunVelocity=0,int Size=0,string Ai=null);
public sealed record ConversionLife(int OriginalLevel,int OriginalMaximum,int Level,int Maximum256);
public sealed record ConversionAllegiance(uint Target,uint Source,DateTime UntilUtc,CombatAbility Skill=CombatAbility.Conversion,ConversionLife Life=null);
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
    internal static ConversionSourceRule SourceRule(BaseCreature target)=>target==null?null:_rules.GetValueOrDefault(CreatureProfiles.Definition(target).Reference??"");
    // Keep the temporary stats until the removal transaction runs. Expiring a
    // maximum in a getter first would let native resource clamps discard life.
    internal static ConversionLife Life(Mobile target)=>target==null?null:_states.GetValueOrDefault(target.Serial.Value)?.Life;
    internal static int? Maximum256(Mobile target)
    {
        if(Life(target) is not{} life){return null;}
        long basis=life.Maximum256;
        return (int)Math.Clamp(basis+ClassResources.Percentage(basis,BattleOrders.LifePercent(target)),1,65000*256);
    }
    internal static bool MindBlastEligible(PlayerMobile owner,BaseCreature target)
    {
        // AIUTIL rejects uniques/superuniques, not champions. Attached Imps
        // have no native attachment-state adapter and remain an explicit gap.
        return owner is{Alive:true,Deleted:false} && WildEnemy(target) && ClassCombat.IsHostile(owner,target)
            && target is not BaseHire && !target.BardProvoked
            && target is not EncounterCreature{Grade:EncounterGrade.Unique or EncounterGrade.SuperUnique}
            && SourceRule(target) is{Evil:true,SwitchAi:true,Walk:true,Boss:false,Attackable:true,Base:not "imp1"};
    }
    internal static void ClearAuras(Mobile target)
    {
        // Source stat-list flag8 is the AURA state bit, not every curse/buff.
        // In particular Enchant, Cloak, Venom and ordinary curses survive.
        PaladinAuras.Clear(target);BattleCommand.Clear(target);BarbarianShout.Clear(target);BattleOrders.Clear(target);
    }
    internal static ConversionLife Scale(int level,int maximum,int ownerLevel,out int current256,int currentWhole)
    {
        int max=(int)Math.Max(1,(long)maximum*ownerLevel/level*256);
        current256=(int)Math.Clamp((long)currentWhole*ownerLevel/level*256,1,max);
        return new(level,maximum,ownerLevel,max);
    }
    internal static int RestoredLife256(ConversionLife life,int maximum256,int current256)
    {
        int maximum=maximum256>>8,current=current256>>8;
        int reconstructed=maximum==0?1:(int)Math.Max(1,(long)life.OriginalMaximum*current/maximum);
        // Literal complete Mind Blast removal callback: the restored current
        // life is capped by the converted maximum, not just the old maximum.
        return Math.Min(maximum,reconstructed)*256;
    }
    internal static bool ApplyMindBlast(PlayerMobile owner,BaseCreature target,int frames)
    {
        if(frames<=0 || !MindBlastEligible(owner,target)){return false;}
        ClearAuras(target);
        int level=ClassStats.Level(target),ownerLevel=ClassStats.Level(owner),current=SkillHealth.Balance256(target);
        ConversionLife life=null;
        if(level>ownerLevel && level>0){life=Scale(level,SkillHealth.Maximum256(target)>>8,ownerLevel,out current,current>>8);}
        _states[target.Serial.Value]=new(target.Serial.Value,owner.Serial.Value,TimedEffects.Now.AddMilliseconds(frames*40L),CombatAbility.MindBlast,life);
        if(life!=null){SkillHealth.SetBalance256(target,current);target.Delta(MobileDelta.Hits);}
        ResetTargeting(target);ClassCombat.Stop(owner);return true;
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
        Prune();
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
        Prune();
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
            var state=_states[serial];var target=World.FindMobile((Serial)serial) as BaseCreature;
            int restored=state.Life!=null && target is{Alive:true,Deleted:false}?RestoredLife256(state.Life,SkillHealth.Maximum256(target),SkillHealth.Balance256(target)):-1;
            _states.Remove(serial);
            if(target is{Alive:true,Deleted:false})
            {
                if(restored>=0){SkillHealth.SetBalance256(target,restored);target.Delta(MobileDelta.Hits);}
                if(state.Skill==CombatAbility.MindBlast){ClearAuras(target);}
                ResetTargeting(target);
            }
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
                || row.Skill is not(CombatAbility.Conversion or CombatAbility.MindBlast)
                || row.Life is{} life && (row.Skill!=CombatAbility.MindBlast || life.Level<1 || life.OriginalLevel<=life.Level
                    || life.OriginalMaximum is <1 or >65000 || life.Maximum256 is <1 or >65000*256
                    || life.Maximum256!=Math.Max(1,(long)life.OriginalMaximum*life.Level/life.OriginalLevel*256))
                || !next.TryAdd(row.Target,row)){throw new InvalidDataException("Invalid Conversion allegiance");}
        }
        _states.Clear();foreach(var row in next){_states.Add(row.Key,row.Value);}
    }
}
