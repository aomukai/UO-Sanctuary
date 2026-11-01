// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;
namespace Sanctuary;

internal static class MageOffensiveSkills
{
    // One ordered percentage draw. Unavailable slots remain basic attacks,
    // without rerolls or reallocating another skill's five-percent interval.
    internal static CombatAbility Select(int element,int hard,Func<CombatAbility,bool> ready,Func<int> percentage)
    {
        if(element is <0 or >3||hard is <0 or >20){throw new ArgumentOutOfRangeException();}
        CombatAbility first=hard<10?CombatAbility.None:element switch
        {1=>CombatAbility.GlacialSpike,2=>CombatAbility.Meteor,3=>CombatAbility.ChainLightning,_=>CombatAbility.None};
        CombatAbility second=element==3&&hard>=20?CombatAbility.StaticField:CombatAbility.None;
        bool a=first!=CombatAbility.None&&ready(first),b=second!=CombatAbility.None&&ready(second);
        if(!a&&!b){return CombatAbility.None;}
        int roll=percentage();if(roll is <0 or >=100){throw new NotSupportedException("Mage proc percentage must be0..99");}
        return roll<5&&a?first:roll is >=5 and <10&&b?second:CombatAbility.None;
    }
    internal static bool Release(NecromancerMinion actor,PlayerMobile owner,Mobile target,DateTime at)
    {
        if(!IronGolemWorld.Eligible(actor,owner,target)){return false;}
        // Cooldown-unavailable slots remain basic attacks without rerolling
        // or reallocating another secondary's probability interval.
        var choice=Select(actor.MageElement,MagePetBuffs.HardRank(actor),ability=>ability is CombatAbility.StaticField or CombatAbility.ChainLightning or CombatAbility.GlacialSpike || ability==CombatAbility.Meteor&&FireGround.Ready(actor,ability,at),
            ()=>NecromancerMageMissiles.ActorLimited(actor,100));
        if(choice==CombatAbility.Meteor){return FireGround.CastNpcMeteor(actor,target,at)>0;}
        if(choice==CombatAbility.GlacialSpike){return NecromancerMageMissiles.LaunchGlacial(actor,target,at)>0;}
        if(choice==CombatAbility.ChainLightning)
        {return ChainLightning.CastNpc(actor,target.Location,MagePetBuffs.Values(CombatAbility.ChainLightning,10,actor.Level),at)>0;}
        if(choice==CombatAbility.StaticField)
        {return StaticFields.Cast(actor,owner,MagePetBuffs.Values(CombatAbility.StaticField,5,actor.Level));}
        return NecromancerMageMissiles.Launch(actor,target,at)>0;
    }
}
