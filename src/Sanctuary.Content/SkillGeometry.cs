// SPDX-License-Identifier: GPL-3.0-or-later
namespace Sanctuary;

public static class SkillGeometry
{
    // Provisional port mapping: the two-subtile player footprint occupies one UO tile.
    // Keep every distance/velocity on one scale; validate it in the client before class sign-off.
    public const double SubtilesPerTile = 2;
    public static double Tiles(double subtiles) => subtiles / SubtilesPerTile;
    // Missile factory scales velocity to 75%; axis Q12 then converts FP16 to subtiles.
    public static double Velocity(int sourceVelocity) => Tiles(((sourceVelocity << 8) * 75 / 100) / 4096.0);
    public static double CorpseExplosionRadius(double halfSubtiles) => Tiles(halfSubtiles / 2.0);
}
