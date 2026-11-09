// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;

namespace Sanctuary;

internal readonly record struct ElementReduction(int Damage256,int Absorbed256);

public static class NamedElements
{
    private static int Bonus(Mobile owner, string code) => owner is PlayerMobile player && ClassCombat.Enabled(player)
        ? ClassGear.Bonus(player, code) : owner is BladeSentinelBody blade ? code switch
        {
            "pierce-fire"=>blade.Passive("passive_fire_pierce"),"pierce-cold"=>blade.Passive("passive_cold_pierce"),
            "pierce-ltng"=>blade.Passive("passive_ltng_pierce"),"pierce-pois"=>blade.Passive("passive_pois_pierce"),
            "extra-fire"=>blade.Passive("passive_fire_mastery"),"extra-cold"=>blade.Passive("passive_cold_mastery"),
            "extra-ltng"=>blade.Passive("passive_ltng_mastery"),"extra-pois"=>blade.Passive("passive_pois_mastery"),_=>0
        } : owner is AssassinSentryBody sentry ? code switch
        {"pierce-ltng"=>sentry.Passive("passive_ltng_pierce"),"extra-ltng"=>sentry.Passive("passive_ltng_mastery"),_=>0}
        :owner is HydraBody hydra?code switch{"pierce-fire"=>hydra.Passive("passive_fire_pierce"),"extra-fire"=>hydra.Passive("passive_fire_mastery"),_=>0}
        :owner is BaseCreature creature&&(code is "pierce-fire" or "pierce-cold" or "pierce-ltng" or "pierce-pois" or "pierce-mag"
            or "abs-fire" or "abs-fire%" or "abs-cold" or "abs-cold%" or "abs-ltng" or "abs-ltng%")
            ?((code.StartsWith("abs-",StringComparison.Ordinal)?ReviveEquipmentDefenses.Bonus(creature,code):null)??NpcEquipmentCombat.Bonus(creature,code)):0;
    internal static int PoisonPierce(Mobile source)=>Math.Max(0,Bonus(source,"pierce-pois"));
    public static int MagicResistance(Mobile target) => target is PlayerMobile
        ? Math.Clamp(Bonus(target, "res-mag"), -100, 75)
        : target is BaseCreature creature ? checked((creature is NecromancerMinion summon ? summon.SummonMagicResistance : CreatureProfiles.For(creature).Resistances.Magic ?? 0)
            +NpcEquipmentCombat.DefensiveBonus(creature,"res-mag")) : 0;
    public static bool Attack(Mobile owner) => owner is PlayerMobile player
        && (SorceressCombat.Passive(player, CombatAbility.FireMastery) > 0 || SorceressCombat.Passive(player, CombatAbility.LightningMastery) > 0
            || SorceressCombat.Passive(player, CombatAbility.ColdMastery) > 0) || Bonus(owner, "extra-fire") != 0 || Bonus(owner, "extra-cold") != 0
        || Bonus(owner, "extra-ltng") != 0 || Bonus(owner, "extra-pois") != 0 || Bonus(owner, "extra-mag") != 0 || Bonus(owner, "pierce-mag") != 0
        || Bonus(owner, "pierce-fire") != 0 || Bonus(owner, "pierce-cold") != 0
        || Bonus(owner, "pierce-ltng") != 0 || Bonus(owner, "pierce-pois") != 0;
    public static bool Defense(Mobile target) => target is FireGolemBody{Kind:CombatAbility.FireGolem} fire && fire.FireAbsorb>0 || Bonus(target, "abs-fire") != 0 || Bonus(target, "abs-fire%") != 0
        || Bonus(target, "abs-cold") != 0 || Bonus(target, "abs-cold%") != 0
        || Bonus(target, "abs-ltng") != 0 || Bonus(target, "abs-ltng%") != 0;
    public static int Resistance(Mobile source, Mobile target, string element, int resistance, int? pierceOverride = null,int skillResistanceDelta=0)
    {
        ResistanceType type=element switch {"fire"=>ResistanceType.Fire,"cold"=>ResistanceType.Cold,"ltng"=>ResistanceType.Energy,"pois"=>ResistanceType.Poison,_=>ResistanceType.Physical};
        bool playerResistance=element!="mag" && target is PlayerMobile player && ClassCombat.Enabled(player);
        if(playerResistance)
        {
            int raw=ClassGear.RawResistance((PlayerMobile)target,type);
            // Expand our displayed value back to its raw pool; preserve distinct native/explicit overrides.
            if(resistance==Math.Clamp(raw,-100,NamedCombat.ResistanceCap((PlayerMobile)target,type))){resistance=raw;}
        }
        int immunityBase=PaladinConviction.UnderlyingResistance(target,element,resistance);
        resistance=immunityBase;
        if(!playerResistance && element!="mag"){resistance+=PaladinResistance.Bonus(target,type);}
        // Retain the pool before skill reductions. Legacy elite Conviction
        // may already be a native resistance mod; exclude its known offset.
        int beforeBreaking=resistance-(PaladinConviction.Managed(target)?0:EliteAuras.ConvictionOffset(target,element));
        resistance = NecromancerEffects.Resistance(target, element, resistance,PaladinConviction.Managed(target)?immunityBase:null,PaladinConviction.Managed(target));
        resistance+=PaladinConviction.ResistanceDelta(target,element,immunityBase);
        // A per-hit skill reduction sees the existing aggregate skill/aura
        // pool and can break immunity before ordinary item pierce is tested.
        if(skillResistanceDelta!=0){resistance+=PaladinConviction.ImmunityDelta(target,resistance,skillResistanceDelta);}
        if (element == "cold" && source is PlayerMobile caster && (target is not BaseCreature || resistance < 100))
        {
            int mastery=SorceressCombat.Passive(caster,CombatAbility.ColdMastery);
            // D2R2.6/current reference: skill Cold Mastery is one fifth after
            // a monster immunity is broken. Item pierce below stays separate.
            if(target is BaseCreature and not BaseHire&&beforeBreaking>=100){mastery/=5;}
            resistance-=mastery;
        }
        // Ordinary item pierce cannot break a monster's immunity.
        if (target is not BaseCreature || resistance < 100)
        { int pierce=pierceOverride??Bonus(source,"pierce-"+element);resistance-=source is BladeSentinelBody or AssassinSentryBody or HydraBody?pierce:Math.Max(0,pierce); }
        if(element=="physical"){resistance=PaladinSanctuary.PhysicalResistance(source,target,resistance);}
        return Math.Clamp(resistance, -100, playerResistance?NamedCombat.ResistanceCap((PlayerMobile)target,type):200);
    }
    public static double Enhance(Mobile source, double damage, string element)
    {
        if (damage <= 0) { return 0; }
        return EnhanceAtMastery(damage,MasteryPercent(source,element));
    }
    internal static double EnhanceAtMastery(double damage,int mastery)
    {
        if (damage <= 0) { return 0; }
        return damage * Math.Max(0,100+mastery) / 100.0;
    }
    internal static int MasteryPercent(Mobile source,string element)
    {
        int mastery = source is PlayerMobile caster ? element switch
        { "fire" => SorceressCombat.Passive(caster, CombatAbility.FireMastery), "ltng" => SorceressCombat.Passive(caster, CombatAbility.LightningMastery), _ => 0 } : 0;
        return mastery+Bonus(source,"extra-"+element);
    }
    public static double Damage(Mobile source, Mobile target, double damage, string element, int? resistance = null, bool enhance = true, int damageRate = 0, int? pierceOverride = null)
    {
        var result=Resolve256(source,target,To256(damage),element,resistance,enhance,damageRate,pierceOverride);
        if(result.Absorbed256>0){SkillHealth.Heal(target,result.Absorbed256);}
        return result.Damage256/256.0;
    }
    internal static int To256(double value)=>(int)Math.Clamp(Math.Floor(value*256),0,int.MaxValue);
    internal static ElementReduction Reduce256(int damage256,int resistance,int reduction256,int absorbPercent,int absorbFlat256,int absorbCap=40)
    {
        // Preserve Sanctuary's zero clamp; legacy negative MDR overflow remains an explicit modern acceptance question.
        long value=Math.Max(0,(long)damage256-reduction256);
        // Zero resistance bypasses the source percentage helper,retaining all
        // low bits even above its large-value early-division threshold.
        if(resistance!=0){value=SourcePercentage.Damage(value,Math.Clamp(100-resistance,0,200));}
        long percent=SourcePercentage.Damage(value,Math.Clamp(absorbPercent,0,absorbCap));
        long flat=Math.Min(value-percent,Math.Max(0,absorbFlat256));
        return new((int)Math.Clamp(value-percent-flat,0,int.MaxValue),(int)Math.Clamp(percent+flat,0,int.MaxValue));
    }
    internal static ElementReduction Resolve256(Mobile source,Mobile target,int damage256,string element,int? resistance=null,bool enhance=true,int damageRate=0,int? pierceOverride=null,int skillResistanceDelta=0)
    {
        if(damage256<=0){return default;}
        if(enhance){damage256=(int)Math.Clamp((long)damage256*Math.Max(0,100+MasteryPercent(source,element))/100,0,int.MaxValue);}
        int resist = resistance ?? element switch
        {
            "fire" => target.FireResistance, "cold" => target.ColdResistance, "ltng" => target.EnergyResistance,
            "pois" => target.PoisonResistance, "mag" => MagicResistance(target), _ => target.PhysicalResistance
        };
        resist = Resistance(source, target, element, resist, pierceOverride,skillResistanceDelta);
        int reduction = element == "pois" ? 0 : element == "physical"
            ? GemCombat.PhysicalReduction(target) : GemCombat.Reduction(target);
        int scaledReduction=GemCombat.Reduction256(reduction,damageRate);
        if (element is "fire" or "cold" or "ltng")
        {
            int innate=target is NecromancerMinion minion && element=="fire"?minion.FireAbsorb:0;
            int absorb=Bonus(target,"abs-"+element+"%");
            int flat=(int)Math.Clamp((long)Bonus(target,"abs-"+element)*256,0,int.MaxValue);
            // New learned Fire Golems use the same accrued percentage pool
            // and40percent cap as the complete source reducer. Flat absorption
            // follows that percentage; no owner item stats are inherited.
            if(element=="fire"&&target is FireGolemBody)
            {return Reduce256(damage256,resist,scaledReduction,checked(innate+absorb),flat);}
            // Keep the existing innate Fire Golem adapter isolated pending its
            // source audit. Ordinary item absorption is capped40,including on
            // non-fire Necromancer minions. Do not invent a mixed innate/item
            // composition for a prepared Fire Golem holding equipment.
            if(innate>0&&(absorb!=0||flat!=0))
            {throw new NotSupportedException("Fire Golem innate/item absorption composition is not verified");}
            return Reduce256(damage256,resist,scaledReduction,innate>0?innate:absorb,flat,innate>0?100:40);
        }
        return Reduce256(damage256,resist,scaledReduction,0,0);
    }
}
