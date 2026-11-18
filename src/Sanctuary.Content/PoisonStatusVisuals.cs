// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;

namespace Sanctuary;

internal static class PoisonStatusVisuals
{
    private static bool Visible(Mobile target) =>
        !World.Loading && !target.Deleted && target.Map is { } map && map != Map.Internal;

    internal static void Refresh(Mobile target, TimedActor actor, DateTime now)
    {
        if (actor.PoisonMs <= 0)
        {
            if (actor.PoisonVisualRefresh == default) { return; }
            Clear(target, actor);
            if (target.Alive && Visible(target))
            {
                target.FixedEffect(SpellEffectGraphics.PoisonClear, 10, 7);
                SpellAreaSounds.Pulse(actor.PoisonSource.Value, SpellEffectSounds.PoisonClear,
                    target.Location, target.Map, now, 330);
            }
            return;
        }

        if (actor.PoisonVisualRefresh > now || !Visible(target)) { return; }
        int duration = (Math.Min(actor.PoisonMs, 650) + 49) / 50;
        target.FixedEffect(SpellEffectGraphics.PoisonAfflicted, 10, duration);
        actor.PoisonVisualRefresh = now.AddMilliseconds(Math.Min(actor.PoisonMs, 400));
    }

    internal static void Clear(Mobile target, TimedActor actor)
    {
        if (actor.PoisonVisualRefresh == default) { return; }
        actor.PoisonVisualRefresh = default;
        if (Visible(target)) { target.FixedEffect(SpellEffectGraphics.PoisonAfflicted, 10, 0); }
    }
}
