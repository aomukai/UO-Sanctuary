// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using Server;
using static Sanctuary.GroundSpellVisuals;

namespace Sanctuary;

internal static class DruidVineVisuals
{
    private static readonly Dictionary<(Map Map, int X, int Y, int Z), CreeperMat> GroundSamples = new();

    internal static void BeginFrame() => GroundSamples.Clear();

    internal static void Launch(DruidCreeperBody body, DateTime at)
        => SpellAreaSounds.Pulse((body.ControlMaster ?? body).Serial.Value,
            SpellEffectSounds.CreeperTrail, body.Location, body.Map, at, 320);

    internal static void Head(Map map, CreeperHead head, double dx, double dy)
    {
        int phase = ((20 - head.Remaining) / 2 + (int)(head.Id % 4)) & 3;
        int art = SpellEffectGraphics.CreeperHead + SpellEffectGraphics.ProjectileFacing(dx, dy) * SpellEffectGraphics.DruidVineFrames + phase;
        Sample(map, head.X, head.Y, head.Z, art, 1);
    }

    internal static void CollectGround(Map map, CreeperFields fields)
    {
        if (fields.Mats.Length == 0) { return; }
        int quietEnd = DruidCreeperVines.InactiveTrailFrames;
        foreach (var mat in fields.Mats)
        {
            if (mat.Poison == null || (mat.Remaining & 3) != 0 && mat.Remaining != quietEnd) { continue; }
            var cell = (map, mat.X, mat.Y, mat.Z);
            if (!GroundSamples.TryGetValue(cell, out var visible) || mat.Remaining > visible.Remaining)
            { GroundSamples[cell] = mat; }
        }
    }

    internal static void DrawGround()
    {
        if (GroundSamples.Count == 0) { return; }
        int quietEnd = DruidCreeperVines.InactiveTrailFrames;
        foreach (var (cell, mat) in GroundSamples)
        {
            bool fading = mat.Remaining <= quietEnd;
            int phase = fading ? Math.Min(3, (quietEnd - mat.Remaining + 1) / 4)
                : ((mat.Total - mat.Remaining) / 4 + cell.X + cell.Y) & 3;
            int art = (fading ? SpellEffectGraphics.CreeperMatFade : SpellEffectGraphics.CreeperMat) + phase;
            int lifetime = fading ? Math.Min(mat.Remaining, 5) : Math.Min(mat.Remaining - quietEnd, 5);
            int duration = (lifetime * 40 + 49) / 50;
            Sample(cell.Map, cell.X / 2.0, cell.Y / 2.0, cell.Z, art, duration);
        }
    }

    internal static void Poison(Mobile owner, Mobile victim, DateTime at)
    {
        Effects.SendLocationEffect(victim.Location, victim.Map, SpellEffectGraphics.CreeperPoison, 6);
        SpellAreaSounds.Pulse(owner.Serial.Value, SpellEffectSounds.CreeperPoison, victim.Location, victim.Map, at, 210);
    }

    internal static void Consume(Mobile owner, Point3D point, Map map, DateTime at)
    {
        Effects.SendLocationEffect(point, map, SpellEffectGraphics.VineCorpse, 10);
        SpellAreaSounds.Pulse(owner.Serial.Value, SpellEffectSounds.VineCorpse, point, map, at, 195);
    }

    internal static void Recover(Mobile owner, bool mana)
    {
        if (owner.Map == null || owner.Map == Map.Internal) { return; }
        owner.FixedEffect(mana ? SpellEffectGraphics.SolarRecovery : SpellEffectGraphics.CarrionRecovery, 10, 10);
        owner.PlaySound(mana ? SpellEffectSounds.SolarRecovery : SpellEffectSounds.CarrionRecovery);
    }
}
