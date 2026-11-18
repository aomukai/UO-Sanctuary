// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using Server;

namespace Sanctuary;

internal static class HurricaneVisuals
{
    private readonly record struct Lease(DateTime Started, DateTime CoreAfter, DateTime WispsAfter);
    private static readonly Dictionary<uint, Lease> Visible = new();

    internal static void Release(Mobile owner)
    {
        owner.FixedEffect(SpellEffectGraphics.HurricaneRelease, 10, 9);
        owner.PlaySound(SpellEffectSounds.HurricaneRelease);
    }

    internal static void Observe(Mobile owner, HurricaneState state, DateTime at, double radius)
    {
        if (owner is not { Alive: true, Deleted: false } || owner.Map == null || owner.Map == Map.Internal) { return; }

        var lease = Visible.GetValueOrDefault(state.Owner);
        if (lease.Started != state.StartedUtc) { lease = new(state.StartedUtc, default, default); }
        double remaining = (state.UntilUtc - at).TotalMilliseconds;
        if (lease.CoreAfter <= at)
        {
            owner.FixedEffect(SpellEffectGraphics.HurricaneCore, 10, (int)Math.Ceiling(Math.Min(remaining, 650) / 50));
            lease = lease with { CoreAfter = at.AddMilliseconds(380) };
        }
        if (lease.WispsAfter <= at)
        {
            DrawWisps(owner, (at - state.StartedUtc).TotalMilliseconds, radius, remaining < 80 ? 1 : 2);
            SpellAreaSounds.Pulse(state.Owner, SpellEffectSounds.HurricaneWind, owner.Location, owner.Map, at, 1500);
            lease = lease with { WispsAfter = at.AddMilliseconds(72) };
        }
        Visible[state.Owner] = lease;
    }

    private static void DrawWisps(Mobile owner, double elapsed, double radius, int duration)
    {
        const int strands = 9;
        double turn = elapsed / 1850 * Math.Tau;
        int phase = (int)(elapsed / 100);
        for (int index = 0; index < strands; index++)
        {
            double angle = turn + index * Math.Tau / strands;
            double distance = radius * (index % 2 == 0 ? 0.88 : 0.66);
            double cos = Math.Cos(angle), sin = Math.Sin(angle);
            int facing = SpellEffectGraphics.ProjectileFacing(-sin, cos);
            int art = SpellEffectGraphics.HurricaneWisp + facing * SpellEffectGraphics.DruidWindFrames
                + (phase + index) % SpellEffectGraphics.DruidWindFrames;
            double x = owner.X + cos * distance, y = owner.Y + sin * distance;
            if (x < 0 || y < 0 || x >= owner.Map.Width - 1 || y >= owner.Map.Height - 1) { continue; }
            GroundSpellVisuals.Sample(owner.Map, x, y, owner.Z, art, duration);
        }
    }

    internal static void Retire(uint serial, Mobile owner, bool fade)
    {
        if (!Visible.Remove(serial) || owner is not { Deleted: false }
            || owner.Map == null || owner.Map == Map.Internal) { return; }

        owner.FixedEffect(SpellEffectGraphics.HurricaneCore, 10, 0);
        if (!fade || !owner.Alive) { return; }

        owner.FixedEffect(SpellEffectGraphics.HurricaneFade, 10, 9);
        owner.PlaySound(SpellEffectSounds.HurricaneFade);
    }
}
