// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;

namespace Sanctuary;

internal static class ColdStatusVisuals
{
    // Short leases let newly arrived clients see a status and retire effects
    // left behind by a map change. Ordinary expiry is bounded by the real status.
    private const int RefreshMilliseconds = 500;
    private const int LeaseMilliseconds = 650;

    internal static void Refresh(Mobile target, TimedActor actor, DateTime now)
    {
        (ushort graphic, DateTime until) = actor.Control is { Freeze: { Removed: false } } control
            && control.FreezeUntil > now
            ? (SpellEffectGraphics.Frozen, control.FreezeUntil)
            : actor.Cold != null && actor.ChillUntil > now
                ? (SpellEffectGraphics.Chilled, actor.ChillUntil)
                : ((ushort)0, default);

        if (actor.ColdVisualGraphic != graphic)
        {
            bool thawed = actor.ColdVisualGraphic == SpellEffectGraphics.Frozen && target.Alive;
            Clear(target, actor);
            if (thawed && target.Map != null && target.Map != Map.Internal)
            {
                target.FixedEffect(SpellEffectGraphics.Thaw, 10, SpellEffectGraphics.ColdFrames);
            }
        }
        if (graphic == 0 || now < actor.ColdVisualRefresh || target.Map == null || target.Map == Map.Internal)
        {
            return;
        }

        double remaining = (until - now).TotalMilliseconds;
        int duration = (int)Math.Ceiling(Math.Min(remaining, LeaseMilliseconds) / 50);
        target.FixedEffect(graphic, 10, duration);
        actor.ColdVisualGraphic = graphic;
        actor.ColdVisualRefresh = now.AddMilliseconds(Math.Min(remaining, RefreshMilliseconds));
    }

    internal static void Clear(Mobile target, TimedActor actor)
    {
        if (actor.ColdVisualGraphic != 0 && !target.Deleted && target.Map != null && target.Map != Map.Internal)
        {
            target.FixedEffect(actor.ColdVisualGraphic, 10, 0);
        }
        actor.ColdVisualGraphic = 0;
        actor.ColdVisualRefresh = default;
    }
}
