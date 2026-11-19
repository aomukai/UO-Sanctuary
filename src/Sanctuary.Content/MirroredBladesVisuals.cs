// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;

namespace Sanctuary;

internal static class MirroredBladesVisuals
{
    internal static void Sweep(Mobile owner, Point3D aim, int ordinal)
    {
        double dx = aim.X - owner.X, dy = aim.Y - owner.Y;
        double distance = Math.Max(1, Math.Sqrt(dx * dx + dy * dy));
        double reach = Math.Min(distance, 1.55) * .7;
        ushort art = ordinal == 0 ? SpellEffectGraphics.MirroredBladesPrimary : SpellEffectGraphics.MirroredBladesCopy;
        int graphic = SorceressVisuals.DirectionalFrame(art, dx, dy, 0);
        Sample(owner.Map, owner.X + dx * reach / distance, owner.Y + dy * reach / distance,
            owner.Z, graphic, ordinal, dx, dy, 7, 18);
    }

    internal static void Travel(Map map, WeaponFlight flight)
    {
        var source = flight.Mirrored;
        ushort art = (MirroredWeaponKind)source.Kind switch
        {
            MirroredWeaponKind.Arrow => SpellEffectGraphics.MirroredBladesArrow,
            MirroredWeaponKind.Bolt => SpellEffectGraphics.MirroredBladesBolt,
            _ => SpellEffectGraphics.MirroredBladesThrow
        };
        int graphic = SorceressVisuals.DirectionalFrame(art, flight.Vx, flight.Vy, flight.Frame + source.Ordinal);
        Sample(map, flight.X, flight.Y, flight.Z, graphic, source.Ordinal, flight.Vx, flight.Vy, 1, 18);
    }

    internal static void Fade(Map map, WeaponFlight flight, Point3D? impact = null)
    {
        var point = Sample(map, impact?.X ?? flight.X, impact?.Y ?? flight.Y, impact?.Z ?? flight.Z,
            SpellEffectGraphics.MirroredBladesFade, flight.Mirrored.Ordinal, flight.Vx, flight.Vy, 6, 18);
        Effects.PlaySound(point, map, SpellEffectSounds.MirroredBladesFade);
    }

    internal static void Hit(Map map, Point3D point, bool playSound = true)
        => WarlockRitualVisuals.Ground(map, point, SpellEffectGraphics.MirroredBladesHit,
            playSound ? SpellEffectSounds.MirroredBladesHit : -1, 6);

    private static Point3D Sample(Map map, double x, double y, int z, int graphic, int ordinal,
        double vx, double vy, int duration, int lift)
    {
        // Render simultaneous copies on either side of the original path. The
        // packet carries pixel offsets from the unchanged combat sample tile.
        double tangentX = vx - vy, tangentY = vx + vy;
        double length = Math.Max(.01, Math.Sqrt(tangentX * tangentX + tangentY * tangentY));
        int side = (ordinal + 1) / 2 * (ordinal % 2 == 0 ? 10 : -10);
        int back = (ordinal + 1) / 2 * 5;
        int horizontal = (int)Math.Round((-tangentY * side - tangentX * back) / length);
        int vertical = (int)Math.Round((tangentX * side - tangentY * back) / length);
        return GroundSpellVisuals.Sample(map, x, y, z, graphic, duration, lift - vertical, horizontal);
    }
}
