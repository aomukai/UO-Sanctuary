// SPDX-License-Identifier: GPL-3.0-or-later
namespace Sanctuary;
public static class WarlockCombat
{
    // Passive only. No active cast or hotkey route is registered here.
    public static bool Implemented(CombatAbility ability) => ability is CombatAbility.LevitationMastery;
}
