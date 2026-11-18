// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using static Sanctuary.GroundSpellVisuals;

namespace Sanctuary;

internal static class VolcanoVisuals
{
    internal static void Opening(Point3D point, Map map)
    {
        Effects.SendLocationEffect(point, map, SpellEffectGraphics.VolcanoOpening, 8);
        Effects.PlaySound(point, map, SpellEffectSounds.VolcanoOpening);
    }

    internal static void Erupt(uint owner, Point3D point, Map map, DateTime at)
        => SpellAreaSounds.Pulse(owner, SpellEffectSounds.VolcanoErupt, point, map, at, 530);

    internal static void Vent(Map map, VolcanoActor vent)
    {
        int age = 150 - vent.Remaining;
        if (age < 10 || (age & 1) != 0) { return; }

        int art = SpellEffectGraphics.VolcanoVent + (age / 3) % SpellEffectGraphics.DruidEruptionFrames;
        Sample(map, vent.X, vent.Y, vent.Z, art, vent.Remaining <= 2 ? 1 : 2);
    }

    internal static void Debris(Map map, VolcanoActor rock, VolcanoActor vent)
    {
        double lift = Math.Min(rock.Remaining, 6) * 5;
        if (vent != null)
        {
            // Recover travel time from the accepted position and velocity.
            double vx = rock.Vx, vy = rock.Vy;
            double age = ((rock.X - vent.X) * vx + (rock.Y - vent.Y) * vy) / (vx * vx + vy * vy);
            lift = Math.Sin(Math.PI * age / (age + rock.Remaining)) * 37;
        }
        int art = SpellEffectGraphics.VolcanoDebris + (rock.Remaining + (int)(rock.Id % 4)) % SpellEffectGraphics.DruidEruptionFrames;
        Sample(map, rock.X, rock.Y, rock.Z, art, 1, (int)Math.Round(lift));
    }

    internal static void Landing(Map map, VolcanoActor lava, DateTime at)
    {
        var point = Sample(map, lava.X, lava.Y, lava.Z, SpellEffectGraphics.VolcanoLanding, 6);
        SpellAreaSounds.Pulse(lava.Owner, SpellEffectSounds.VolcanoLanding, point, map, at, 190);
    }

    internal static void Cooling(Point3D point, Map map)
        => Effects.SendLocationEffect(point, map, SpellEffectGraphics.VolcanoCooling, 11);
}
