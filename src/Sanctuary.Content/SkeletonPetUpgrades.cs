// SPDX-License-Identifier: GPL-3.0-or-later
using Server;
using Server.Mobiles;
namespace Sanctuary;

// Approved Sanctuary unlocks use live hard points. Birth resources/ranks,
// equipment and affix identities stay captured by their existing services.
internal static class SkeletonPetUpgrades
{
    internal static int HardRank(Mobile actor)=>actor is SkeletonGuardian skeleton&&NecromancerSkeletons.Kind(skeleton)==CombatAbility.RaiseSkeleton
        &&skeleton.ControlMaster is PlayerMobile{Deleted:false} owner?ClassCreation.Profiles.GetRank(owner.Serial,"necromancer.raiseskeleton"):0;
    internal static int FlatDamage(Mobile actor)=>HardRank(actor)>=10?34:0;
    internal static bool Has(Mobile actor,EliteModifier modifier)=>modifier==EliteModifier.ExtraFast&&HardRank(actor)>=20;
    internal static int MovementPercent(Mobile actor)=>Has(actor,EliteModifier.ExtraFast)?EncounterProfiles.ModifierMovementPercent(EliteModifier.ExtraFast):0;
}
