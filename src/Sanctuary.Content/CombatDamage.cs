// SPDX-License-Identifier: GPL-3.0-or-later
using Server;

namespace Sanctuary;

internal static class CombatDamage
{
    public static void Finish(Mobile target, Mobile attacker)
    {
        // Native damage records a killer below zero; class combat also completes exact-zero kills.
        if (target.Alive && target.Hits == 0)
        {
            target.LastKiller = attacker;
            target.Kill();
        }
    }
}
