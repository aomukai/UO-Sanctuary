// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Mobiles;

namespace Sanctuary;

internal static class WarlockRitualVisuals
{
    private readonly record struct Lease(Map Map, DateTime? Until, DateTime Renew, DateTime VisibleUntil);
    private static readonly Dictionary<(Serial Actor, ushort Graphic), Lease> Active = new();
    private static DateTime _pruneAt;

    private static bool Visible(Mobile actor) => PaladinAuraVisuals.InWorld(actor)
        && actor is { Alive: true, Hidden: false };

    internal static void Attached(Mobile actor, ushort graphic, int cue)
    {
        if (!Visible(actor)) { return; }
        actor.FixedEffect(graphic, 10, 7);
        actor.PlaySound(cue);
    }

    internal static void Ground(Map map, Point3D point, ushort graphic, int cue, int duration = 9)
    {
        if (World.Loading || map == null || map == Map.Internal) { return; }
        Effects.SendLocationEffect(point, map, graphic, duration);
        Effects.PlaySound(point, map, cue);
    }

    internal static void Trail(Map map, Point3D origin, Point3D target, ushort graphic)
    {
        if (World.Loading || map == null || map == Map.Internal) { return; }
        int dx = target.X - origin.X, dy = target.Y - origin.Y, dz = target.Z - origin.Z;
        if (dx == 0 && dy == 0 && dz == 0) { return; }
        for (int sample = 1; sample <= 4; sample++)
        {
            double fraction = sample / 5.0;
            int art = SorceressVisuals.DirectionalFrame(graphic, dx, dy, sample - 1);
            GroundSpellVisuals.Sample(map, origin.X + dx * fraction, origin.Y + dy * fraction,
                origin.Z + (int)Math.Round(dz * fraction), art, 5, 13);
        }
    }

    internal static bool Renew(Mobile actor, ushort graphic, DateTime now, DateTime? until = null)
    {
        if (!Visible(actor) || until <= now) { return false; }
        var key = (actor.Serial, graphic);
        if (Active.TryGetValue(key, out var old) && old.Map == actor.Map && old.Until == until && old.Renew > now)
        { return false; }
        DateTime end = now.AddMilliseconds(1250);
        if (until is { } expiry && expiry < end) { end = expiry; }
        if (!PaladinAuraVisuals.Attach(actor, graphic, end, now)) { return false; }
        Active[key] = new(actor.Map, until, now.AddMilliseconds(570), end);
        return true;
    }

    internal static bool Clear(Mobile actor, ushort graphic)
    {
        if (actor == null || !Active.Remove((actor.Serial, graphic), out var old)) { return false; }
        if (actor.Map == old.Map) { PaladinAuraVisuals.Clear(actor, graphic); }
        return true;
    }

    internal static void Prune(DateTime now)
    {
        if (_pruneAt > now) { return; }
        _pruneAt = now.AddSeconds(1);
        foreach (var key in Active.Where(pair => pair.Value.VisibleUntil <= now).Select(pair => pair.Key).ToArray())
        { Active.Remove(key); }
    }

    internal static void Warp(BaseCreature body, Point3D origin)
    {
        if (!Visible(body)) { return; }
        if (origin != body.Location)
        { Ground(body.Map, origin, SpellEffectGraphics.DeathMarkDeparture, -1); }
        Ground(body.Map, body.Location, SpellEffectGraphics.DeathMarkArrival, SpellEffectSounds.DeathMarkWarp);
    }

    internal static void Boil(Map map, Point3D center, int radius)
    {
        if (World.Loading || map == null || map == Map.Internal) { return; }
        Effects.SendLocationEffect(center, map, SpellEffectGraphics.BloodBoilRing, 9,
            hue: radius * SpellEffectGraphics.HolyAuraRadiusUnitsPerTile / 2);
        Ground(map, center, SpellEffectGraphics.BloodBoilSource, SpellEffectSounds.BloodBoilBurst, 6);
    }

    internal static void Engorge(BaseCreature body, DateTime now)
    {
        if (EngorgeEffects.At(body, now) is { } state
            && WarlockDemonRecipients.Active(body.ControlMaster as PlayerMobile, body))
        { Renew(body, SpellEffectGraphics.EngorgeWard, now, state.UntilUtc); }
    }
}
