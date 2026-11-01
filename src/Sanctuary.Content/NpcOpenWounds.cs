// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using Server;
using Server.Logging;
using Server.Mobiles;
namespace Sanctuary;

public sealed record SourceOpenWoundsClock(int Rate256,int ElapsedMs,uint CreditOwner);

// Item function15 and approved Blood hard10 share one event/state consumer.
internal static class NpcOpenWounds
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(NpcOpenWounds));
    private static readonly HashSet<Type> Unmapped=new();
    internal static bool Potential(SkeletonGuardian actor)=>actor is NecromancerMinion{Kind:CombatAbility.BloodGolem};
    internal static int Inherent(SkeletonGuardian actor)=>Potential(actor)&&actor.ControlMaster is PlayerMobile{Deleted:false} owner
        &&ClassCreation.Profiles.GetRank(owner.Serial,"necromancer.bloodgolem")>=10?100:0;
    internal static int Chance(SkeletonGuardian actor)=>checked(Inherent(actor)+Math.Max(0,NpcEquipmentCombat.Bonus(actor,"openwounds")));
    private static bool Eligible(BaseCreature target)
    {
        if(target is not{Alive:true,Deleted:false}||target is BaseVendor){return false;}
        if(target is EncounterCreature){return target.CanBeDamaged();}
        var rule=SourceAuraTargets.Rule(CreatureProfiles.Definition(target).Reference);
        if(rule!=null){return !rule.Npc&&rule.Attackable;}
        if(Unmapped.Add(target.GetType())){Log.Warning("NPC Open Wounds state receiver is unsupported: {Type}",target.GetType().Name);}
        return false;
    }
    internal static bool Apply(SkeletonGuardian actor,BaseCreature target,DateTime at,bool ranged=false)
    {
        int chance=Chance(actor);if(chance<=0){return false;}
        // Function15 draws even at100%, before state-factory eligibility.
        if(NpcEquipmentRandom.Limited(actor,100)>=chance||!Eligible(target)){return false;}
        if(actor.ControlMaster is not PlayerMobile{Deleted:false} owner){return false;}
        int rate=checked((int)Math.Round(NamedCombat.OpenWoundsDamage(ClassStats.Level(actor),target,ranged)*256/200));
        return TimedEffects.PreparedOpenWounds(actor,target,rate,owner.Serial.Value,at);
    }
    // One signed HP-regeneration commitment per source frame. Poison's existing
    // state/owner wins death credit while active; item replenish offsets the sum.
    internal static void Advance(Mobile target,TimedActor actor,int poisonMs,int woundMs,int bonus256,Mobile bonusSource)
    {
        var clock=actor.OpenWounds??throw new InvalidOperationException("Missing source wound clock");
        int phase=actor.PoisonClock?.ElapsedMs??actor.NpcPoisonElapsedMs??clock.ElapsedMs;
        int poisonFrames=(phase+poisonMs)/40,woundFrames=(phase+woundMs)/40,frames=Math.Max(poisonFrames,woundFrames);
        int item=MinionSafeAreas.RecoveryRate(target,NpcEquipmentRegeneration.ItemRate(target));bool blocked=actor.HealBlockedUntil>(TimedEffects.DamageTime??TimedEffects.Now);
        var woundSource=World.FindMobile((Serial)clock.CreditOwner)??World.FindMobile(actor.BleedSource);
        for(int i=0;i<frames&&target is{Alive:true,Deleted:false};i++)
        {
            int poison=i<poisonFrames?PoisonStates.AccrueFrame(actor):0,wound=i<woundFrames?clock.Rate256:0;
            int delta=NpcEquipmentRegeneration.Delta(0,item,checked(poison+wound+(i==frames-1?bonus256:0)),true,blocked);
            NpcEquipmentRegeneration.Apply(target,delta,i<poisonFrames?World.FindMobile(actor.PoisonSource):woundSource);
        }
        if(frames==0&&bonus256>0){PoisonStates.Drain(bonusSource,target,bonus256);}
        if(actor.PoisonMs==0){actor.PoisonClock=null;actor.NpcPoisonElapsedMs=null;}
        else if(actor.PoisonClock is{} poison){actor.PoisonClock=poison with{ElapsedMs=(phase+poisonMs)%40};}
        else if(poisonMs>0||actor.NpcPoisonElapsedMs!=null){actor.NpcPoisonElapsedMs=(phase+poisonMs)%40;}
        if(actor.BleedMs==0){actor.OpenWounds=null;actor.BleedRate=actor.BleedFraction=0;}
        else{actor.OpenWounds=clock with{ElapsedMs=(phase+woundMs)%40};}
    }
}
