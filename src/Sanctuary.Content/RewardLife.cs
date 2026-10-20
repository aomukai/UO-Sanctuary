// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Items;
using Server.Mobiles;

namespace Sanctuary;

// Consumers share this death-time entitlement; XP success is never an eligibility test.
public sealed record RewardLife(BaseCreature Creature, Corpse Corpse, PlayerMobile Player, int Level, int GoldFind, int MagicFind, MonsterDifficulty Difficulty = MonsterDifficulty.Normal)
{
    public static bool Process(BaseCreature creature, Container container) => Process(creature, container, Random.Shared);
    internal static bool Process(BaseCreature creature, Container container, Random random)
    {
        if (creature is not { Deleted: false } || container is not Corpse corpse || corpse.Deleted
            || !corpse.OwnerWasBaseCreature || corpse.Owner != creature || corpse.SanctuaryNoRewards)
        {
            return false;
        }
        // Claim before cleanup or consumers. This saved flag also follows revivals.
        corpse.SanctuaryNoRewards = true;
        if (ClassLootHooks.SuppressDrops(creature))
        {
            using var items = Server.Collections.PooledRefList<Item>.Create();
            foreach (var item in corpse.Items) { items.Add(item); }
            foreach (var item in items) { item.Delete(); }
        }
        if (creature is BaseVendor || creature.Controlled || creature.Summoned || creature.IsAnimatedDead
            || creature.NoKillAwards || creature is GraveyardUndead { Raised: true }
            || CreatureGold.Policy(creature.GetType(), MonsterVariants.Difficulty(creature)) is not { Eligible: true }
            || ClassLoot.Killer(creature) is not { Deleted: false } player || !ClassCombat.Enabled(player))
        {
            return false;
        }
        var life = new RewardLife(creature, corpse, player, CreatureProfiles.Level(creature), CreatureGold.GoldFind(player), GearDrops.MagicFind(player), MonsterVariants.Difficulty(creature));
        GearDrops.Award(life, random);
        MouldRewards.Award(life, random);
        return CreatureExperience.Award(life);
    }
}
