// SPDX-License-Identifier: GPL-3.0-or-later
using Server;
using Server.Mobiles;

namespace Sanctuary;

public static class ClassSkills
{
    public static void Configure()
    {
        ClassCombatHooks.SkillGainAllowed = CanGain;
    }

    public static bool CanGain(PlayerMobile player, SkillName skill) =>
        !ClassCombat.Enabled(player) || TradeSkills.Includes((int)skill);
}
