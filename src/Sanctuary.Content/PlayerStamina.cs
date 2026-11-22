// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;

namespace Sanctuary;

internal static class PlayerStamina
{
    internal static bool Regenerate(Mobile actor)
    {
        if (actor is not PlayerMobile player || !ClassCombat.Enabled(player))
            return false;

        int maximum = BattleOrders.StaminaMaximum256(player);
        int current = BattleOrders.Stamina256(player);
        if (current >= maximum)
            return true;

        long now = Core.TickCount;
        bool moving = ClassMovement.IsMoving(player, now);
        bool walking = moving && !ClassMovement.IsRunning(player, now);
        long bonus = (long)ClassGear.Bonus(player, "regen-stam") + PaladinVigor.RecoveryPercent(player);
        if (!walking && bonus < 1000 && (moving || player.Spell != null || HitRecovery.Active(player, now)
            || ClassCombat.Existing(player) is { } state && now < state.NextAction))
            return true;

        int baseGain = maximum >> (walking ? 9 : 8);
        long gain = baseGain + bonus * baseGain / 100;
        if (gain > 0)
            BattleOrders.SetStamina256(player, current + (int)Math.Min(gain, maximum - current));
        return true;
    }
}
