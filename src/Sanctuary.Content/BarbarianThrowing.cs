// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server.Items;
using Server.Mobiles;

namespace Sanctuary;

public readonly record struct ThrowRelease(bool Critical,int Pierces);
public static class BarbarianThrowing
{
    internal static Func<Random> RandomSource=()=>Random.Shared;
    internal static bool Throwable(BaseWeapon weapon) => !GargoyleBows.Matches(weapon)
        && ClassGear.Base(weapon) is { } basis && ClassItemTypes.Is(basis.Type, "thro");

    internal static bool Supported(PlayerMobile owner)
    {
        var weapon = CombatTiming.Equipped(owner);
        return Throwable(weapon) && BarbarianMasteries.Throwing(owner, weapon, 6) > 0;
    }

    internal static ThrowRelease? Release(PlayerMobile owner, Random random = null, bool allowUntrained = false)
    {
        if (!(allowUntrained ? Throwable(CombatTiming.Equipped(owner)) : Supported(owner)))
            return null;
        random??=RandomSource();bool critical=NamedCombat.ProjectileCritical(owner,random);
        int chance=NamedProjectiles.PierceChance(owner);
        return new(critical,NamedProjectiles.PierceBudget(chance,random));
    }
}
