// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Items;
using Server.Mobiles;

namespace Sanctuary;

public readonly record struct ClassResourceParts(long Base256,long Attribute256,long PerLevel256,int SkillPercent,int PassivePercent)
{
    // Source op1/op11 each multiply the pre-operation subtotal. Equipment
    // attribute/op2 contributions join afterward, with FP256 retained until UI/native projection.
    public long Maximum256=>Math.Clamp(Base256+ClassResources.Percentage(Base256,SkillPercent)
        +ClassResources.Percentage(Base256,PassivePercent)+Attribute256+PerLevel256,256,65000L*256);
    public int NativeMaximum=>(int)(Maximum256/256);
}
public static class ClassResources
{
    internal static long Percentage(long value,int percent)=>(long)(value*(percent/100.0));
    internal static (long Exact256,int Projected) PerLevel(PlayerMobile owner,string code)
    {
        long exact=0;int projected=0;
        if(NamedGear.Catalog==null){return(0,0);}
        int level=ClassStats.Level(owner);
        foreach(var (property,roll) in NamedGear.ActiveProperties(owner))
        {
            if(property.Code!=code+"/lvl"){continue;}
            exact+=(long)roll.Value*level*32;
            projected+=NamedPropertyRuntime.Bonus(property,roll,code,level);
        }
        return(exact,projected);
    }
    internal static int VitalityBonus(PlayerMobile owner)
    {
        var scaled=PerLevel(owner,"vit");
        return ClassGear.Bonus(owner,"vit")-scaled.Projected+(int)(scaled.Exact256/256);
    }
    public static ClassResourceParts Parts(PlayerMobile owner,int resource,int extraSkillPercent=0)
    {
        if(resource is <0 or >2 || !ClassCreation.Profiles.TryGet(owner.Serial,out var profile)){throw new ArgumentException("Class resource requires a class player and resource0..2");}
        var basis=ClassStats.For(profile.Class);var allocated=ClassCreation.Profiles.Stats(owner.Serial);
        int coefficient=resource==0?basis.LifeVitality4:resource==1?basis.StaminaVitality4:basis.ManaEnergy4;
        int growth=resource==0?basis.LifeLevel4:resource==1?basis.StaminaLevel4:basis.ManaLevel4;
        int initial=resource==0?basis.Life:resource==1?basis.Stamina:basis.Mana;
        int attribute=resource==2?allocated.Energy:allocated.Vitality;
        int bonusAttribute=resource==2?owner.Int-basis.Energy-allocated.Energy:VitalityBonus(owner);
        string code=resource==0?"hp":resource==1?"stam":"mana";
        var scaled=PerLevel(owner,code);
        int flat=AosAttributes.GetValue(owner,resource==0?AosAttribute.BonusHits:resource==1?AosAttribute.BonusStam:AosAttribute.BonusMana)-scaled.Projected;
        long base256=(long)(initial+flat)*256+((long)(profile.Level-1)*growth+(long)attribute*coefficient)*64;
        int skill=extraSkillPercent+BattleOrders.Percent(owner)+(resource==0?ClassGear.Bonus(owner,"hp%"):resource==2?ClassGear.Bonus(owner,"mana%"):PaladinVigor.StaminaPercent(owner));
        int passive=resource==1?BarbarianCombat.Passive(owner,CombatAbility.IncreasedStamina):0;
        return new(base256,(long)bonusAttribute*coefficient*64,scaled.Exact256,skill,passive);
    }
}
