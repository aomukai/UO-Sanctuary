// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;

namespace Sanctuary;

// Poison Dagger supplies the initial poison packet to ordinary weapon damage.
// Reuse the shared item-poison lane rather than applying two competing states.
internal static class PoisonDaggerPackets
{
    internal static Func<double> HitRoll=Utility.RandomDouble;
    internal static Func<int,int> PoisonRoll=Utility.Random;

    internal static WeaponPoisonRoll Skill(SkillValues values,int mastery,Func<int,int> random)
    {
        if(values.Ability!=CombatAbility.PoisonDagger||values.Rank<1||mastery<0)
        {throw new ArgumentOutOfRangeException(nameof(values));}
        int Enhance(int value)=>checked((int)(value+(long)value*mastery/100));
        int minimum=Enhance(values.Damage256(false)),maximum=Enhance(values.Damage256(true));
        int span=Math.Max(0,maximum-minimum);
        return new(minimum+(span>0?Math.Clamp(random(span),0,span-1):0),values.DurationFrames);
    }
    internal static WeaponPoisonRoll Combine(MissileSourceStats raw,int mastery,WeaponPoisonRoll skill,int overrideFrames,Func<int,int> random)
        =>WeaponPoisonPackets.Melee(raw,mastery,128,random,skill.Rate256,skill.Frames,overrideFrames);

    internal static GearDamage Damage(PlayerMobile owner,Mobile target,SkillValues values,Func<double> elementRandom,out double elementRoll)
    {
        // The elemental skill roll precedes ordinary weapon allocation. Item
        // poison is combined after the existing ordinary weapon preparation.
        var skill=Skill(values,NamedElements.MasteryPercent(owner,"pois"),PoisonRoll);
        elementRoll=elementRandom();
        return GearEffects.Damage(owner,elementRoll,target,poisonFactory:()=>
        {
            var raw=BowLaunch.RawPoison(owner,CombatTiming.Equipped(owner));
            var venom=Venoms.State(owner);
            if(venom!=null){raw=WeaponPoisonPackets.AddVenom(raw,venom);}
            return Combine(raw,NamedElements.MasteryPercent(owner,"pois"),skill,venom?.Frames??0,PoisonRoll);
        });
    }
}
