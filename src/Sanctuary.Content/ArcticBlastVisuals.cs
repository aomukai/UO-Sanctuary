// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;

namespace Sanctuary;

internal static class ArcticBlastVisuals
{
    internal static void Release(Mobile owner, Point3D aim)
    {
        int dx = aim.X - owner.X, dy = aim.Y - owner.Y;
        if (dx == 0 && dy == 0) { dx = dy = 1; }
        int art = SpellEffectGraphics.ArcticBlastRelease + SpellEffectGraphics.ProjectileFacing(dx, dy) * SpellEffectGraphics.DruidColdFrames;
        Effects.SendLocationEffect(owner.Location, owner.Map, art, 8);
        owner.PlaySound(SpellEffectSounds.ArcticBlastRelease);
    }

    internal static void Emission(Mobile owner, DateTime at)
        => SpellAreaSounds.Pulse(owner.Serial.Value, SpellEffectSounds.ArcticBlastStream, owner.Location, owner.Map, at, 570);

    internal static void Travel(Map map, ArcticMissile missile)
    {
        int age = missile.Damage.Frames - missile.Remaining;
        int phase = (age / 2 + (int)(missile.Id % SpellEffectGraphics.DruidColdFrames)) % SpellEffectGraphics.DruidColdFrames;
        int art = SpellEffectGraphics.ArcticBlastFlight
            + SpellEffectGraphics.ProjectileFacing(missile.Vx, missile.Vy) * SpellEffectGraphics.DruidColdFrames + phase;
        GroundSpellVisuals.Sample(map, missile.X, missile.Y, missile.Z, art, 2);
    }

    internal static void Fade(Map map, ArcticMissile missile)
        => GroundSpellVisuals.Sample(map, missile.X, missile.Y, missile.Z, SpellEffectGraphics.ArcticBlastFade, 6);

    internal static void Contact(uint owner, Point3D point, Map map, DateTime at)
    {
        Effects.SendLocationEffect(point, map, SpellEffectGraphics.ArcticBlastContact, 6);
        SpellAreaSounds.Pulse(owner, SpellEffectSounds.ArcticBlastContact, point, map, at, 140);
    }
}
