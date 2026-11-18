// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;

namespace Sanctuary;

internal static class GrimWardVisuals
{
    private static int Variant(GrimWardActor ward) => (ward.StartClass - 249) / 3 * SpellEffectGraphics.BarbarianCryFrames;

    internal static void Rise(Item marker, GrimWardActor ward)
    {
        Effects.SendLocationEffect(marker, SpellEffectGraphics.GrimWardRise + Variant(ward), (ward.StartFrames * 40 + 49) / 50);
        Effects.PlaySound(marker.Location, marker.Map, SpellEffectSounds.GrimWardRise);
    }

    internal static void Body(Item marker, GrimWardActor ward, int pulse)
    {
        if (ward.LastPulse >= 0 && pulse / 24 == ward.LastPulse / 24) { return; }
        int duration = Math.Min(34, ((1000 - pulse) * 40 + 49) / 50);
        Effects.SendTargetEffect(marker, SpellEffectGraphics.GrimWardBody + Variant(ward), 10, duration);
    }

    internal static void Pulse(Item marker, int subtileRadius, DateTime at)
    {
        int radius = (int)Math.Clamp(Math.Round(SkillGeometry.Tiles(subtileRadius) * SpellEffectGraphics.HolyAuraRadiusUnitsPerTile),
            1, ushort.MaxValue);
        Effects.SendLocationEffect(marker, SpellEffectGraphics.GrimWardPulse, 8, hue: radius);
        SpellAreaSounds.Pulse(marker.Serial.Value, SpellEffectSounds.GrimWardPulse, marker.Location, marker.Map, at, 1900);
    }

    internal static void Mark(Item marker, Mobile target, DateTime at)
        => SpellAreaSounds.Pulse(marker.Serial.Value, SpellEffectSounds.GrimWardMark, target.Location, target.Map, at, 360);

    internal static void Fall(Item marker, GrimWardActor ward)
    {
        int variant = Variant(ward);
        Effects.SendTargetEffect(marker, SpellEffectGraphics.GrimWardBody + variant, 10, 0);
        Effects.SendLocationEffect(marker, SpellEffectGraphics.GrimWardFall + variant, 8);
        Effects.PlaySound(marker.Location, marker.Map, SpellEffectSounds.GrimWardFall);
    }
}
