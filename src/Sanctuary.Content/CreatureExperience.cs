// SPDX-License-Identifier: GPL-3.0-or-later
using Server;
using Server.Items;
using Server.Mobiles;

namespace Sanctuary;

public static class CreatureExperience
{
    internal static bool Award(BaseCreature creature, Container container)
    {
        if (creature is not { Deleted: false } || container is not Corpse corpse || corpse.Deleted || !corpse.OwnerWasBaseCreature
            || corpse.Owner != creature || corpse.SanctuaryNoRewards)
        {
            return false;
        }
        // A life is spent even when nobody qualifies for its XP. The saved corpse
        // flag also carries that fact through the existing revival chain.
        corpse.SanctuaryNoRewards = true;
        if (creature is BaseVendor || creature.Controlled || creature.Summoned || creature.IsAnimatedDead
            || creature.NoKillAwards || creature is GraveyardUndead { Raised: true }
            || ClassLoot.Killer(creature) is not { Deleted: false, Alive: true } player || !ClassCombat.Enabled(player)
            || corpse.Map != player.Map || !player.InRange(corpse, LootProtocol.SightRange))
        {
            return false;
        }
        int basis = CreatureProfiles.Experience(creature);
        if (basis <= 0)
        {
            return false;
        }
        int amount = MonsterExperience.Award(basis, CreatureProfiles.Level(creature), ClassStats.Level(player));
        return ClassCreation.Profiles.AwardExperience(player, amount);
    }
}
