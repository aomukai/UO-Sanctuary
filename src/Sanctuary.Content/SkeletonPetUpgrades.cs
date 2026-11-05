// SPDX-License-Identifier: GPL-3.0-or-later
using Server;
using Server.Mobiles;
namespace Sanctuary;

// New learned warriors freeze upgrade hard points at birth. Older actors with
// no birth metadata keep the explicit legacy rule until resummoned.
internal static class SkeletonPetUpgrades
{
    internal static int HardRank(Mobile actor)=>actor is SkeletonGuardian skeleton&&NecromancerSkeletons.Kind(skeleton)==CombatAbility.RaiseSkeleton
        ?SummonUpgradeSnapshots.HardRank(skeleton,CombatAbility.RaiseSkeleton):0;
    internal static int FlatDamage(Mobile actor)=>HardRank(actor)>=10?34:0;
    internal static bool Has(Mobile actor,EliteModifier modifier)=>modifier==EliteModifier.ExtraFast&&HardRank(actor)>=20;
    internal static int MovementPercent(Mobile actor)=>Has(actor,EliteModifier.ExtraFast)?EncounterProfiles.ModifierMovementPercent(EliteModifier.ExtraFast):0;
}
