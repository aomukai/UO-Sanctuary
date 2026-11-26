// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using static Sanctuary.GroundSpellVisuals;

namespace Sanctuary;

internal static class DruidWindVisuals
{
    internal static void Release(Mobile owner, int sound)
    {
        Effects.SendLocationEffect(owner.Location, owner.Map, SpellEffectGraphics.DruidWindRelease, 9);
        owner.PlaySound(sound);
    }

    internal static void Travel(Map map, double x, double y, int z, int art, int frame, int duration = 1)
        => Sample(map, x, y, z, art + (frame / 2) % SpellEffectGraphics.DruidWindFrames, duration);

    internal static void Disperse(Map map, double x, double y, int z, int art)
        => Sample(map, x, y, z, art, 6);

    internal static void TornadoPulse(Map map, TornadoMissile missile)
    {
        var point = Sample(map, missile.X, missile.Y, missile.Z, SpellEffectGraphics.TornadoPulse, 6);
        SpellAreaSounds.Pulse(missile.Owner, SpellEffectSounds.TornadoPulse, point, map, missile.NextUtc, 350);
    }

    internal static void Contact(uint owner, Map map, Point3D point, DateTime at, bool cold = false)
    {
        int graphic = cold ? SpellEffectGraphics.IceBoltContact : SpellEffectGraphics.DruidWindContact;
        int sound = cold ? SpellEffectSounds.IceBoltContact : SpellEffectSounds.DruidWindContact;
        Effects.SendLocationEffect(point, map, graphic, 6);
        SpellAreaSounds.Pulse(owner, sound, point, map, at, 130);
    }
}
