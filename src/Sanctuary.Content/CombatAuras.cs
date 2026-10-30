// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;
namespace Sanctuary;

// Aggregate like source stats before consumers scale their base. Material
// elemental reference calculations explicitly exclude this physical pool.
internal static class CombatAuras
{
    internal static int AttackPercent(Mobile m)=>PaladinAuras.AttackPercent(m)+DruidWolverineAuras.AttackPercent(m);
    internal static int DamagePercent(Mobile m)=>PaladinAuras.DamagePercent(m)+DruidWolverineAuras.DamagePercent(m);
    internal static bool Managed(Mobile m)=>PaladinAuras.CombatStats(m)||DruidWolverineAuras.Managed(m);
    internal static double NativeWeaponMultiplier(Mobile m,bool weapon)=>weapon&&m is BaseCreature and not SkeletonGuardian
        ?Math.Max(10,100+DamagePercent(m)+NecromancerEffects.DamageStatPercent(m)+ReviveCombat.DamagePercent(m))/100.0:1;
}
