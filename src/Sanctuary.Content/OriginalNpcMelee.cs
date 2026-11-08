// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Linq;
using Server;
using Server.Mobiles;
namespace Sanctuary;

// Original equipped summons share full fill/contact/poison/piercing and item
// callbacks. Species keep their own graphs,passives,weapon bindings and Crit.
internal static class OriginalNpcMelee
{
    private static NpcCombatEvents Registry(SkeletonGuardian body)=>body switch
    {
        AmazonValkyrieBody v=>ValkyrieEquipmentContact.Registry(v),
        AssassinShadowBody shadow=>ShadowEquipmentDefense.Registry(shadow),
        _=>throw new NotSupportedException("Original melee needs its qualified species adapter")
    };
    internal static void RequireAllocation(SkeletonGuardian body,BaseCreature target)
    {
        _=Registry(body);OriginalNpcEquipment.WeaponFrame(body).Enter();
        if(target is not{Alive:true,Deleted:false}){throw new NotSupportedException("Original equipped NPC allocation needs a living NPC recipient");}
        if(OriginalNpcEquipment.Graph(body).Stats.Any(v=>v.Stat==180&&v.Value!=0))
        {throw new NotSupportedException("Original monster-type damage needs a concrete current target-type adapter");}
        WeaponStartEffects.RequireArmorDelta(OriginalNpcEquipment.Graph(body).Read(120));
        if(checked(CreatureProfiles.Defense(target)+NpcEquipmentCombat.Defense(target))<0)
        {throw new NotSupportedException("Original equipped NPC target armor is outside the native base-stat adapter");}
    }
    internal static IronGolemContact.Prepared Prepare(SkeletonGuardian body,PlayerMobile owner,BaseCreature target,Func<int,bool> range,DateTime at)
    {
        ArgumentNullException.ThrowIfNull(range);
        if(at.Kind!=DateTimeKind.Utc||owner is not{Alive:true,Deleted:false}||body?.ControlMaster!=owner||body.Map==null||body.Map==Map.Internal||body.Map!=target?.Map||owner.Map!=body.Map)
        {throw new NotSupportedException("Original equipped NPC contact needs its living world owner and explicit UTC allocation phase");}
        RequireAllocation(body,target);var rules=IronGolemContact.Require(body,owner,target);
        if(body is AssassinShadowBody&&AssassinCharges.Any(body))
        {return ShadowFinishers.Prepare((AssassinShadowBody)body,owner,target,range,at,rules);}
        if(!SourceBlocking.TryRate(target,out _,out _)){throw new NotSupportedException("Original equipped NPC recipient block rule is unavailable");}
        return IronGolemContact.Prepare(body,target,rules,()=>NpcMeleeResult.Resolve(body,target,range,()=>ClassCombat.IsHostile(owner,target),n=>NpcEquipmentRandom.Limited(body,n)));
    }
    internal static NpcWeaponImpact Impact(SkeletonGuardian body,BaseCreature target,int flags,NpcFillPacket initial=null,int sourceDamage=128,
        Func<NpcFillPacket,NpcFillPacket> afterFill=null)
    {
        RequireAllocation(body,target);
        int Armor()=>Math.Max(0,checked(CreatureProfiles.Defense(target)+NpcEquipmentCombat.Defense(target))-TimedEffects.DefenseLoss(target));
        void SetArmor(int value)
        {
            int delta=checked(value-Armor());WeaponStartEffects.ApplyArmorDelta(target,delta,TimedEffects.Now);
            if(target is not{Alive:true,Deleted:false}){throw new NotSupportedException("Recipient retired during original equipped NPC armor setup;do not retry allocation");}
        }
        var facts=new GeneratedNpcFillTarget(true,true,NamedCombat.Demon(target),GemCombat.Undead(target),0,Armor,SetArmor,
            (_,_)=>throw new NotSupportedException("Original target monster-type service is not admitted"));
        var graph=OriginalNpcEquipment.Graph(body);
        var fill=Allocate(body,facts,(initial??new()) with{ResultFlags=flags,AdditionalPercent=checked((initial?.AdditionalPercent??0)+CombatAuras.DamagePercent(body)+NecromancerEffects.DamageStatPercent(body))},sourceDamage);
        if(afterFill!=null){fill=afterFill(fill);}
        var poison=new NpcPoisonSnapshot(body.Serial.Value,body.ControlMaster.Serial.Value,new(PoisonMin:graph.Read(57),PoisonMax:graph.Read(58),PoisonFrames:graph.Read(59),PoisonCount:graph.Read(326)),graph.Read(332),Math.Max(0,graph.Read(336)));
        var pierce=new NpcElementPierce(graph.Read(333),graph.Read(335),graph.Read(334),graph.Read(358));
        return NpcEquipmentImpact.FromFill(body,target,fill,poison,pierce.For,PaladinAuras.NativeWeaponMultiplier(body,true));
    }
    internal static NpcFillPacket Allocate(SkeletonGuardian body,GeneratedNpcFillTarget target,NpcFillPacket initial=null,int sourceDamage=128)
    {
        ArgumentNullException.ThrowIfNull(target);ArgumentNullException.ThrowIfNull(target.Armor);ArgumentNullException.ThrowIfNull(target.SetArmor);ArgumentNullException.ThrowIfNull(target.TypeMatches);
        if(sourceDamage is<0 or>255){throw new ArgumentOutOfRangeException(nameof(sourceDamage));}
        var graph=OriginalNpcEquipment.Graph(body);var weapon=OriginalNpcEquipment.WeaponFrame(body);
        var passives=body is AmazonValkyrieBody v?ValkyrieEquipmentDamage.Passives(v):Array.Empty<ValkyrieInheritedPassive>();
        NpcEquipmentRandom.Require(body);
        int critical=passives.Where(s=>s.Stat==337).Sum(s=>s.Value);
        var external=NpcEquipmentElements.WithBonuses(body,new(),0,0,0,0).Stats;
        // Enchant's enma/exma stat-list writers store whole-point mastered
        // endpoints. Reuse live buff composition without changing the graph
        // or adding a second elemental roll;the full filler applies own mastery.
        int Extra(NpcFillStat stat)=>stat switch
        {
            NpcFillStat.FIREMINDAM=>external.FireMin/256,NpcFillStat.FIREMAXDAM=>external.FireMax/256,
            NpcFillStat.LIGHTMINDAM=>external.LightningMin/256,NpcFillStat.LIGHTMAXDAM=>external.LightningMax/256,
            NpcFillStat.COLDMINDAM=>external.ColdMin/256,NpcFillStat.COLDMAXDAM=>external.ColdMax/256,
            NpcFillStat.PASSIVE_CRITICAL_STRIKE=>critical,_=>0
        };
        int Read(NpcFillStat stat)=>checked(graph.Read((int)stat)+Extra(stat));
        // Source NPCs are not hirelings. The player/hireling-only melee event
        // must never be substituted with a generic item-proc callback here.
        return weapon.Fill(target,initial??new(),0,1,true,0,NecromancerSummons.Source(body.SourceMonsterId,"Crit"),
            bound=>NpcEquipmentRandom.Limited(body,bound),()=>NpcEquipmentRandom.Raw(body),
            _=>throw new NotSupportedException("Unexpected player/hireling allocation event for original equipped NPC"),sourceDamage:sourceDamage,read:Read);
    }
}
