// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using Server;

namespace Sanctuary;

internal static class NecromancerPoisonVisuals
{
    private static readonly HashSet<(Map Map, int X, int Y, int Z, bool Nova)> Painted = new();

    internal static void BeginFrame() => Painted.Clear();

    internal static void Travel(Mobile owner, Point3D point, NecromancerSpellFlight flight, DateTime now)
    {
        bool nova = flight.Ability == CombatAbility.PoisonNova;
        var map = owner.Map;
        // The dense Nova ring shares one sprite per native tile. The eight
        // corpse clouds are sparse enough to keep separate half-tile positions.
        int x = nova ? point.X : (int)Math.Round(flight.X * 2);
        int y = nova ? point.Y : (int)Math.Round(flight.Y * 2);
        if (!Painted.Add((map, x, y, point.Z, nova))) { return; }

        int phase = flight.Frame / (nova ? 2 : 4) % SpellEffectGraphics.PoisonFrames;
        int graphic = nova
            ? SpellEffectGraphics.PoisonNovaWisp + SpellEffectGraphics.ProjectileFacing(flight.Vx, flight.Vy)
                * SpellEffectGraphics.PoisonFrames + phase
            : SpellEffectGraphics.PoisonExplosionMist + phase;
        double dx = flight.X - point.X, dy = flight.Y - point.Y;
        int screenX = (int)Math.Round((dx - dy) * 22);
        int screenY = (int)Math.Round((dx + dy) * 22);
        Effects.SendLocationEffect(point, map, graphic, 2,
            speed: screenX + SpellEffectGraphics.GroundEffectPositionBias, hue: screenY + SpellEffectGraphics.GroundEffectPositionBias);

        if (!nova && flight.Frame >= 12)
        {
            SpellAreaSounds.Pulse(owner.Serial.Value, SpellEffectSounds.JavelinCloud, point, map, now, 930);
        }
    }

    internal static void Release(Map map, Point3D origin, bool nova)
    {
        Effects.SendLocationEffect(origin, map, nova ? SpellEffectGraphics.PoisonNovaCast : SpellEffectGraphics.PoisonExplosionBloom, 7);
        Effects.PlaySound(origin, map, nova ? SpellEffectSounds.PoisonNovaRelease : SpellEffectSounds.PoisonExplosionRelease);
    }

    internal static void PoisonContact(Mobile owner, Mobile victim)
    {
        victim.FixedEffect(SpellEffectGraphics.JavelinVenomContact, 10, SpellEffectGraphics.JavelinEffectFrames);
        SpellAreaSounds.Pulse(owner.Serial.Value, SpellEffectSounds.JavelinVenomContact,
            victim.Location, victim.Map, TimedEffects.Now, 130);
    }
}
