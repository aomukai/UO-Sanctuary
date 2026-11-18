// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using Server;
using static Sanctuary.PaladinAuraVisuals;

namespace Sanctuary;

internal static class PaladinConversionVisuals
{
    private static readonly Dictionary<uint, (DateTime Until, DateTime Next)> _visible = new();

    internal static void Begin(Mobile target, ConversionAllegiance lease, DateTime now)
    {
        Moment(target, SpellEffectGraphics.ConversionLock, SpellEffectSounds.ConversionLock, now);
        Refresh(target, lease, now);
    }

    internal static void Refresh(Mobile target, ConversionAllegiance lease, DateTime now)
    {
        if (!InWorld(target)) { _visible.Remove(lease.Target); return; }
        if (_visible.TryGetValue(lease.Target, out var live) && live.Until == lease.UntilUtc && live.Next > now) { return; }
        var until = now.AddMilliseconds(700);
        if (lease.UntilUtc < until) { until = lease.UntilUtc; }
        if (Attach(target, SpellEffectGraphics.ConversionMark, until, now))
        { _visible[lease.Target] = (lease.UntilUtc, now.AddMilliseconds(450)); }
    }

    internal static void End(uint serial, Mobile target, bool expired)
    {
        if (!_visible.Remove(serial)) { return; }
        Clear(target, SpellEffectGraphics.ConversionMark);
        if (expired && target is { Alive: true })
        { Moment(target, SpellEffectGraphics.ConversionFade, SpellEffectSounds.ConversionFade, TimedEffects.Now); }
    }

    private static void Moment(Mobile target, int graphic, int cue, DateTime now)
    {
        if (!InWorld(target)) { return; }
        target.FixedEffect(graphic, 10, 8);
        SpellAreaSounds.Pulse(target.Serial.Value, cue, target.Location, target.Map, now, 240);
    }
}
