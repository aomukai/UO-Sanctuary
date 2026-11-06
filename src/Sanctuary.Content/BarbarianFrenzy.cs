// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;
namespace Sanctuary;

// Explicit approved startup/payment/charge timing; current start67 is unverified.
public static class BarbarianFrenzy
{
    internal static Func<double> HitRoll=Utility.RandomDouble;
    internal static Func<double> ElementRoll=Utility.RandomDouble;
    internal static Func<int,int> PhysicalRoll=Utility.Random;
    internal static Func<int,int> PoisonRoll=Utility.Random;
    internal static Random Effects=Random.Shared;
    internal static ActionTiming Timing(PlayerMobile owner)=>BarbarianPairedMelee.Timing(owner,CombatAbility.Frenzy);
    public static bool Begin(PlayerMobile owner,Serial selected,Point3D point,long now)=>BarbarianPairedMelee.Begin(owner,CombatAbility.Frenzy,selected,point,now);
}
