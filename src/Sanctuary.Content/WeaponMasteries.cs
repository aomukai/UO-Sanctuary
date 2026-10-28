// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server.Items;
using Server.Mobiles;

namespace Sanctuary;

public static class WeaponMasteries
{
    // The source selector takes the strongest applicable item-type layer.
    // Current class-specific suppliers are mutually exclusive; foreign oskill
    // mastery grants remain part of the deferred item-skill work.
    public static int Bonus(PlayerMobile owner,BaseWeapon weapon,int stat)=>Math.Max(
        Math.Max(BarbarianMasteries.Bonus(owner,weapon,stat),AssassinCombat.Mastery(owner,weapon,stat)),WarlockLevitation.Bonus(owner,weapon,stat));
}
