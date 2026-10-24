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
        } : 0;
    public static int MagicResistance(Mobile target) => target is PlayerMobile
        ? Math.Clamp(Bonus(target, "res-mag"), -100, 75)
        : target is NecromancerMinion summon ? summon.SummonMagicResistance : target is BaseCreature creature ? CreatureProfiles.For(creature).Resistances.Magic ?? 0 : 0;
    public static bool Attack(Mobile owner) => owner is PlayerMobile player
        && (SorceressCombat.Passive(player, CombatAbility.FireMastery) > 0 || SorceressCombat.Passive(player, CombatAbility.LightningMastery) > 0
            || SorceressCombat.Passive(player, CombatAbility.ColdMastery) > 0) || Bonus(owner, "extra-fire") != 0 || Bonus(owner, "extra-cold") != 0
        || Bonus(owner, "extra-ltng") != 0 || Bonus(owner, "extra-pois") != 0 || Bonus(owner, "extra-mag") != 0 || Bonus(owner, "pierce-mag") != 0
        || Bonus(owner, "pierce-fire") != 0 || Bonus(owner, "pierce-cold") != 0
        || Bonus(owner, "pierce-ltng") != 0 || Bonus(owner, "pierce-pois") != 0;
    public static bool Defense(Mobile target) => Bonus(target, "abs-fire") != 0 || Bonus(target, "abs-fire%") != 0
        || Bonus(target, "abs-cold") != 0 || Bonus(target, "abs-cold%") != 0
        || Bonus(target, "abs-ltng") != 0 || Bonus(target, "abs-ltng%") != 0;
    public static int Resistance(Mobile source, Mobile target, string element, int resistance, int? pierceOverride = null)
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
        resistance = NecromancerEffects.Resistance(target, element, resistance,PaladinConviction.Managed(target)?immunityBase:null,PaladinConviction.Managed(target));
        resistance+=PaladinConviction.ResistanceDelta(target,element,immunityBase);
        if (element == "cold" && source is PlayerMobile caster && (target is not BaseCreature || resistance < 100))
        { resistance -= SorceressCombat.Passive(caster, CombatAbility.ColdMastery); }
        // Ordinary item pierce cannot break a monster's immunity.
        if (target is not BaseCreature || resistance < 100)
        { int pierce=pierceOverride??Bonus(source,"pierce-"+element);resistance-=source is BladeSentinelBody?pierce:Math.Max(0,pierce); }
        if(element=="physical"){resistance=PaladinSanctuary.PhysicalResistance(source,target,resistance);}
        return Math.Clamp(resistance, -100, playerResistance?NamedCombat.ResistanceCap((PlayerMobile)target,type):200);
    }
    public static double Enhance(Mobile source, double damage, string element)
    {
        if (damage <= 0) { return 0; }
        return damage * Math.Max(0,100+MasteryPercent(source,element)) / 100.0;
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
        long value=Math.Max(0,(long)damage256-reduction256)*Math.Clamp(100-resistance,0,200)/100;
        long percent=value*Math.Clamp(absorbPercent,0,absorbCap)/100;
        long flat=Math.Min(value-percent,Math.Max(0,absorbFlat256));
        return new((int)Math.Clamp(value-percent-flat,0,int.MaxValue),(int)Math.Clamp(percent+flat,0,int.MaxValue));
    }
    internal static ElementReduction Resolve256(Mobile source,Mobile target,int damage256,string element,int? resistance=null,bool enhance=true,int damageRate=0,int? pierceOverride=null)
    {
        if(damage256<=0){return default;}
        if(enhance){damage256=(int)Math.Clamp((long)damage256*Math.Max(0,100+MasteryPercent(source,element))/100,0,int.MaxValue);}
        int resist = resistance ?? element switch
        {
            "fire" => target.FireResistance, "cold" => target.ColdResistance, "ltng" => target.EnergyResistance,
            "pois" => target.PoisonResistance, "mag" => MagicResistance(target), _ => target.PhysicalResistance
        };
        resist = Resistance(source, target, element, resist, pierceOverride);
        int reduction = element == "pois" ? 0 : element == "physical"
            ? GemCombat.PhysicalReduction(target) : GemCombat.Reduction(target);
        int scaledReduction=(int)Math.Clamp((long)reduction*256*(damageRate>0?damageRate:1024)/1024,0,int.MaxValue);
        if (element is "fire" or "cold" or "ltng")
        {
            int absorb = target is NecromancerMinion minion && element == "fire" ? minion.FireAbsorb : Bonus(target, "abs-" + element + "%");
            int flat=(int)Math.Clamp((long)Bonus(target,"abs-"+element)*256,0,int.MaxValue);
            return Reduce256(damage256,resist,scaledReduction,absorb,flat,target is NecromancerMinion?100:40);
        }
        return Reduce256(damage256,resist,scaledReduction,0,0);
    }
}
