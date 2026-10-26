// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
namespace Sanctuary;

// UNITS_UpdateAttackAnimRateAndVelocity, non-dual-wield attack branch.
// Total attackrate includes its source base100. Do not pre-clamp combined
// slows to85 before adding haste; only the final15..175rate is clamped.
internal static class SourceAttackRate
{
    internal static int Speed(int baseSpeed,int fasterAttackRate,int attackRate,int meleeOnlyBase=0)
    {
        if(baseSpeed is <0 or >32767 || meleeOnlyBase is <0 or >32767 || fasterAttackRate is <=-120 or >10000)
        {throw new NotSupportedException("Unsupported source nonweapon attack-rate context");}
        int effective=fasterAttackRate==0?0:checked(120*fasterAttackRate)/(fasterAttackRate+120);
        int rate=(int)Math.Clamp((long)effective+attackRate,15,175);
        return Math.Clamp(checked(rate*(meleeOnlyBase==0?baseSpeed:meleeOnlyBase))/100,0,32767);
    }
    internal static int WolfRate(Mobile actor)=>checked(100+PaladinOffense.AttackRate(actor)-GearEffects.ChillPercent(actor)
        -TimedEffects.Slow(actor,TimedEffects.Now)-NecromancerEffects.Slow(actor)-PaladinHolyFreeze.AttackSlow(actor)-ImpaleSlow.Attack(actor));
    // Summon equipment creation/IAS transport is not enabled by this adapter.
    // Owner IAS, weapon speed and player sequence penalties are not inherited.
    internal static int WolfSpeed(Mobile actor)=>Speed(NecromancerSummons.Source("spiritwolf-animation","A1Speed"),0,WolfRate(actor));
}
