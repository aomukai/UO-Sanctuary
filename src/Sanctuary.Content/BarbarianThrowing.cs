// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server.Items;
using Server.Mobiles;

namespace Sanctuary;

public readonly record struct ThrowRelease(bool Critical,int Pierces);
public static class BarbarianThrowing
{
    internal static Func<Random> RandomSource=()=>Random.Shared;
    internal static bool Supported(PlayerMobile owner)=>BarbarianMasteries.Throwing(owner,CombatTiming.Equipped(owner),6)>0
        && NamedAmmunition.Maximum(CombatTiming.Equipped(owner))>0;
    internal static ThrowRelease? Release(PlayerMobile owner,DateTime now,Random random=null,bool allowUntrained=false)
    {
        var weapon=CombatTiming.Equipped(owner);
        if((!Supported(owner) && !allowUntrained) || NamedAmmunition.Maximum(weapon)<=0 || !NamedAmmunition.CanAttack(weapon)){return null;}
        random??=RandomSource();bool critical=NamedCombat.ProjectileCritical(owner,random);
        if(!NamedAmmunition.Throw(weapon,now,critical,BarbarianMasteries.Throwing(owner,weapon,5),random)){return null;}
        int chance=NamedProjectiles.PierceChance(owner);
        return new(critical,NamedProjectiles.PierceBudget(chance,random));
    }
}
