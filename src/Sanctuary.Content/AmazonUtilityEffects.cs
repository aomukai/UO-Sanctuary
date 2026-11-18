// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using Server;

namespace Sanctuary;

internal static class AmazonUtilityEffects
{
    internal static readonly Loop InnerSight = new(SpellEffectGraphics.InnerSightMark);
    internal static readonly Loop SlowMissiles = new(SpellEffectGraphics.SlowMissilesMark);
    internal static readonly Loop Decoy = new(SpellEffectGraphics.DecoyMirror);
    internal static readonly Loop Valkyrie = new(SpellEffectGraphics.ValkyrieAura);

    internal static void Burst(Mobile actor, ushort graphic, int sound) =>
        Present(actor.Map, actor.Location, graphic, sound, SpellEffectGraphics.AmazonUtilityBurst);

    internal static void Present(Map map, Point3D point, ushort graphic, int sound, int duration)
    {
        if (map == null || map == Map.Internal) { return; }
        Effects.SendLocationEffect(point, map, graphic, duration);
        Effects.PlaySound(point, map, sound);
    }

    internal sealed class Loop(ushort graphic)
    {
        private readonly Dictionary<uint, DateTime> _refresh = new();

        internal void Refresh(Mobile actor, DateTime now, DateTime until)
        {
            if (until <= now || actor.Map == null || actor.Map == Map.Internal) { return; }
            uint id = actor.Serial.Value;
            if (_refresh.GetValueOrDefault(id) > now) { return; }

            // Keep the phase on renewals and leave only a short visual lease
            // in clients that move out of packet range.
            double remaining = (until - now).TotalMilliseconds;
            actor.FixedEffect(graphic, 10, (int)Math.Ceiling(Math.Min(remaining, 750) / 50));
            _refresh[id] = now.AddMilliseconds(Math.Min(remaining, 400));
        }

        internal void Clear(uint id, Mobile actor)
        {
            if (_refresh.Remove(id) && actor is { Deleted: false, Map: { } map } && map != Map.Internal)
            {
                actor.FixedEffect(graphic, 10, 0);
            }
        }
    }
}
