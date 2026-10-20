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
            foreach (var item in items) { if (!TreasureHunts.FishingReward(item)) { item.Delete(); } }
        }
        if (creature is BaseVendor || creature.Controlled || creature.Summoned || creature.IsAnimatedDead
            || creature.NoKillAwards || creature is GraveyardUndead { Raised: true }
            || ClassLoot.Killer(creature) is not { Deleted: false } player || !ClassCombat.Enabled(player))
        {
            if (!creature.Controlled) { TreasureHunts.DiscardFishing(corpse); }
            return false;
        }
        var life = new RewardLife(creature, corpse, player, CreatureProfiles.Level(creature), CreatureGold.GoldFind(player), GearDrops.MagicFind(player), MonsterVariants.Difficulty(creature));
        if (creature is not EncounterCreature && CreatureGold.Policy(creature.GetType(), life.Difficulty) is not { Eligible: true })
        { TreasureHunts.DiscardFishing(corpse); Fortification.Award(life, random); return false; }
        if (creature is TreasureMonster treasure && (treasure.TreasureTier < 4 || treasure.Grade is not (EncounterGrade.Unique or EncounterGrade.Champion or EncounterGrade.Minion))) { GearDrops.Award(life, random, treasure.SourceType); }
        else if (creature is EncounterCreature encounter) { EncounterTreasure.Award(life, encounter, random); }
        else { GearDrops.Award(life, random); }
        MouldRewards.Award(life, random);
        Fortification.Award(life, random);
        TreasureHunts.Award(life, random);
        return CreatureExperience.Award(life);
    }
}
