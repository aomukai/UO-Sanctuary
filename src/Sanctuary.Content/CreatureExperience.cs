// SPDX-License-Identifier: GPL-3.0-or-later
using Server;
using Server.Items;
using Server.Mobiles;

namespace Sanctuary;

public static class CreatureExperience
{
    internal static bool Award(BaseCreature creature, Container container) => RewardLife.Process(creature, container);
    internal static bool Award(RewardLife life)
    {
        var player = life.Player;
        if (!player.Alive || PaladinConversion.WasConverted(life.Creature) || life.Corpse.Map != player.Map || !player.InRange(life.Corpse, LootProtocol.SightRange))
        {
            return false;
        }
        int basis = CreatureProfiles.Experience(life.Creature);
        if (basis <= 0) { return false; }
        int amount = MonsterExperience.Award(basis, life.Level, ClassStats.Level(player));
        amount = (int)System.Math.Clamp((long)amount * System.Math.Max(0, 100 + ClassGear.Bonus(player, "addxp")) / 100, 0, int.MaxValue);
        return ClassCreation.Profiles.AwardExperience(player, amount);
    }
}
