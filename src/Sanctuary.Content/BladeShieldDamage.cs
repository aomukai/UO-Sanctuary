// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;

namespace Sanctuary;

// Live melee fill, distinct from Blade Sentinel's first-missile snapshot. Skill
// physical is already rolled once for the whole area and is copied per target.
internal static class BladeShieldDamage
{
    internal static int Scale(int value)=>checked((int)((long)Math.Max(0,value)*96/128));
    internal static int Roll(int min,int max,Func<int,int> random)
    {int span=Math.Max(0,max-min);return min+(span>0?Math.Clamp(random(span),0,span-1):0);}
    internal static int Physical(int skill,int rolledWeapon,bool critical)
        =>checked(Scale(checked(skill+rolledWeapon))*(critical?2:1));
    internal static GearDamage Capture(PlayerMobile owner,Mobile target,int skill,Func<int,int> random,Random criticalRandom)
    {
        if(ClassGear.Bonus(owner,"dmg-ac")>0){throw new NotSupportedException("Positive target-defense adjustment has no native consumer");}
        var weapon=CombatTiming.Equipped(owner);
        int targetBonus=GemCombat.Undead(target)?ClassGear.Bonus(owner,"dmg-undead%")+PaladinSanctuary.DamagePercent(owner,target)
            :NamedCombat.Demon(target)?ClassGear.Bonus(owner,"dmg-demon%"):0;
        if(GemCombat.Undead(target) && weapon!=null && ClassGear.Base(weapon) is{} basis && ClassItemTypes.Is(basis.Type,"blun")){targetBonus+=50;}
        var range=ClassStats.SkillPhysicalRange256(owner,0,targetBonus);
        int weaponRoll=Roll(range.Min,range.Max,random);
        int supplement=BowLaunch.PhysicalSupplement(owner,weapon,96)?.Roll(random)??0;
        bool critical=NamedCombat.Deadly(owner,criticalRandom)>1;
        int physical=checked(Physical(skill,weaponRoll,critical)+supplement*(critical?2:1));
        var elements=BowLaunch.WeaponElements(owner,weapon);
        int Element(int min,int max)=>max<8?0:Scale(Roll(min,max,random));
        int fire=Element(elements.FireMin,elements.FireMax),lightning=Element(elements.LightningMin,elements.LightningMax),
            cold=Element(elements.ColdMin,elements.ColdMax),magic=Element(elements.MagicMin,elements.MagicMax);
        var venom=Venoms.State(owner);var raw=BowLaunch.RawPoison(owner,weapon);
        if(venom!=null){raw=WeaponPoisonPackets.AddVenom(raw,venom);}
        var poison=WeaponPoisonPackets.Melee(raw,NamedElements.MasteryPercent(owner,"pois"),96,random,overrideFrames:venom?.Frames??0);
        return new(NamedElements.Enhance(owner,physical/256.0,"physical"),fire/256.0,cold/256.0,lightning/256.0,
            poison.Total,poison.Frames*40,cold>0?Scale(elements.ColdFrames)*40:0,magic/256.0){SourcePoison=poison};
    }
}
