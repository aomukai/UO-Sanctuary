// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;
namespace Sanctuary;

// The shared paired-contact engine preserves this skill's approved contract.
public static class BarbarianDoubleSwing
{
    internal static Func<double> HitRoll=Utility.RandomDouble;
    internal static Func<double> ElementRoll=Utility.RandomDouble;
    internal static Func<int,int> PhysicalRoll=Utility.Random;
    internal static Func<int,int> PoisonRoll=Utility.Random;
    internal static Random Effects=Random.Shared;
    internal static WeaponPair? Pair(PlayerMobile owner)=>BarbarianPairedMelee.Pair(owner);
    internal static bool InRange(PlayerMobile owner,Mobile target)=>BarbarianPairedMelee.InRange(owner,target);
    internal static ActionTiming Timing(PlayerMobile owner)=>BarbarianPairedMelee.Timing(owner,CombatAbility.DoubleSwing);
    public static bool Begin(PlayerMobile owner,Serial selected,Point3D point,long now)=>BarbarianPairedMelee.Begin(owner,CombatAbility.DoubleSwing,selected,point,now);
    internal static void Cancel(PlayerMobile owner)
    {if(ClassCombat.Existing(owner)?.PendingAbility==CombatAbility.DoubleSwing){BarbarianPairedMelee.Cancel(owner);}}
    internal static bool Resolve(PlayerMobile owner,long now)=>ClassCombat.For(owner).DoubleSwing!=null&&BarbarianPairedMelee.Resolve(owner,now);
}
