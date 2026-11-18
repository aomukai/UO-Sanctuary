// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;

namespace Sanctuary;

internal static class PaladinAuraVisuals
{
    internal static bool InWorld(Mobile actor) => !World.Loading && actor is { Deleted: false, Map: { } map }
        && map != Map.Internal;

    internal static bool Attach(Mobile actor, int graphic, DateTime until, DateTime now)
    {
        if (!InWorld(actor)) { return false; }
        int duration = (int)Math.Ceiling(Math.Clamp((until - now).TotalMilliseconds / 50, 0, byte.MaxValue));
        if (duration <= 0) { return false; }
        actor.FixedEffect(graphic, 10, duration);
        return true;
    }

    internal static void Clear(Mobile actor, int graphic)
    {
        if (InWorld(actor)) { actor.FixedEffect(graphic, 10, 0); }
    }
}
