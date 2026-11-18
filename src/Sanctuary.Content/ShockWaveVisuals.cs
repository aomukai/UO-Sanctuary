// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using Server;

namespace Sanctuary;

internal static class ShockWaveVisuals
{
    private static readonly HashSet<(long Cast, int X, int Y)> Painted = new();

    internal static void BeginFrame() => Painted.Clear();

    internal static void Release(Mobile owner)
    {
        Effects.SendLocationEffect(owner.Location, owner.Map, SpellEffectGraphics.ShockWaveRelease, 8);
        owner.PlaySound(SpellEffectSounds.ShockWaveRelease);
    }

    internal static void Travel(ShockWaveMissile wave, Map map)
    {
        if (!Painted.Add((wave.Cast, (int)Math.Round(wave.X * 2), (int)Math.Round(wave.Y * 2)))) { return; }
        int phase = (14 - wave.Remaining) / 4;
        int graphic = SpellEffectGraphics.ShockWaveFlight
            + SpellEffectGraphics.ProjectileFacing(wave.Vx, wave.Vy) * SpellEffectGraphics.DruidStatusEffectFrames + phase;
        GroundSpellVisuals.Sample(map, wave.X, wave.Y, wave.Z, graphic, 2);
    }

    internal static void Contact(uint owner, Point3D point, Map map)
    {
        Effects.SendLocationEffect(point, map, SpellEffectGraphics.ShockWaveImpact, 7);
        SpellAreaSounds.Pulse(owner, SpellEffectSounds.ShockWaveImpact, point, map, TimedEffects.Now, 110);
    }
}
