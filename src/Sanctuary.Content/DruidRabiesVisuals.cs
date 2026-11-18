// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using Server;

namespace Sanctuary;

internal static class DruidRabiesVisuals
{
    private static readonly Dictionary<uint, DateTime> RenewAfter = new();

    private static bool Visible(Mobile target) =>
        !World.Loading && target is { Alive: true, Deleted: false, Map: { } map } && map != Map.Internal;

    internal static void Refresh(RabiesMarker marker, DateTime now)
    {
        var target = World.FindMobile((Serial)marker.Host);
        if (!Visible(target) || target.Map.MapID != marker.Map)
        {
            Clear(marker, fade: false);
            return;
        }
        if (RenewAfter.GetValueOrDefault(marker.Host) > now) { return; }
        int remaining = (int)Math.Clamp(Math.Ceiling((marker.UntilUtc - now).TotalMilliseconds), 1, 700);
        target.FixedEffect(SpellEffectGraphics.RabiesCarrier, 10, (remaining + 49) / 50);
        RenewAfter[marker.Host] = now.AddMilliseconds(Math.Min(remaining, 450));
    }

    internal static void Spread(Mobile target, RabiesMarker marker, DateTime now)
    {
        if (!Visible(target)) { return; }
        target.FixedEffect(SpellEffectGraphics.RabiesSpread, 10, 7);
        SpellAreaSounds.Pulse(marker.Caster, SpellEffectSounds.RabiesSpread, target.Location, target.Map, now, 160);
    }

    internal static void Clear(RabiesMarker marker, bool fade)
    {
        if (!RenewAfter.Remove(marker.Host)) { return; }
        var target = World.FindMobile((Serial)marker.Host);
        if (!Visible(target)) { return; }
        target.FixedEffect(SpellEffectGraphics.RabiesCarrier, 10, 0);
        if (!fade || target.Map.MapID != marker.Map) { return; }
        target.FixedEffect(SpellEffectGraphics.RabiesFade, 10, 9);
        SpellAreaSounds.Pulse(marker.Caster, SpellEffectSounds.RabiesFade, target.Location, target.Map, TimedEffects.Now, 240);
    }
}
