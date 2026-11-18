// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;

namespace Sanctuary;

internal static class WoundStatusVisuals
{
    private static bool Visible(Mobile target) =>
        !World.Loading && !target.Deleted && target.Map is { } map && map != Map.Internal;

    internal static void Refresh(Mobile target, TimedActor actor, DateTime now)
    {
        if (actor.OpenWounds == null || actor.BleedMs <= 0)
        {
            if (actor.WoundVisualRefresh == default) { return; }
            Clear(target, actor);
            if (target.Alive && Visible(target)) { target.FixedEffect(SpellEffectGraphics.WoundFade, 10, 9); }
            return;
        }
        if (actor.WoundVisualRefresh > now || !Visible(target)) { return; }
        int duration = (Math.Min(actor.BleedMs, 700) + 49) / 50;
        target.FixedEffect(SpellEffectGraphics.WoundActive, 10, duration);
        actor.WoundVisualRefresh = now.AddMilliseconds(Math.Min(actor.BleedMs, 450));
    }

    internal static void Clear(Mobile target, TimedActor actor)
    {
        if (actor.WoundVisualRefresh == default) { return; }
        actor.WoundVisualRefresh = default;
        if (Visible(target)) { target.FixedEffect(SpellEffectGraphics.WoundActive, 10, 0); }
    }
}
