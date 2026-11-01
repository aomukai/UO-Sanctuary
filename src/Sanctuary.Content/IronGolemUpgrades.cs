// SPDX-License-Identifier: GPL-3.0-or-later
using Server.Mobiles;
namespace Sanctuary;

// Approved direct affix, not a boss modifier inherited by encounter minions.
// Live hard-point eligibility is separate from captured summon/item recipes.
internal static class IronGolemUpgrades
{
    internal static int HardRank(BaseCreature actor)=>actor is SkeletonGuardian
        &&(actor is IronGolemBody||actor is NecromancerMinion{Kind:CombatAbility.IronGolem})
        &&actor.ControlMaster is PlayerMobile{Deleted:false} owner
        ?ClassCreation.Profiles.GetRank(owner.Serial,"necromancer.irongolem"):0;
    private static (int DamagePercent,int AttackPercent) Strength(BaseCreature actor)=>HardRank(actor)>=10
        ?EncounterProfiles.ExtraStrong(((SkeletonGuardian)actor).SummonDifficulty,false):default;
    internal static int DamagePercent(BaseCreature actor)=>Strength(actor).DamagePercent;
    internal static int AttackPercent(BaseCreature actor)=>Strength(actor).AttackPercent;
}
