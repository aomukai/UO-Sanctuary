using System;

namespace Sanctuary;

// Immutable legacy Q12 tangent table; modern D2R/client path comparison remains open.
public static class MissileVectors
{
    private static readonly Point2D[] _tangent =
    [
        new(0, 4096), new(32, 4095), new(64, 4095), new(96, 4094), new(128, 4093), new(161, 4092), new(193, 4091), new(225, 4089),
        new(257, 4087), new(289, 4085), new(321, 4083), new(353, 4080), new(385, 4077), new(417, 4074), new(448, 4071), new(480, 4067),
        new(511, 4063), new(543, 4059), new(574, 4055), new(606, 4050), new(637, 4046), new(668, 4041), new(699, 4035), new(729, 4030),
        new(760, 4024), new(791, 4018), new(821, 4012), new(851, 4006), new(881, 3999), new(911, 3993), new(941, 3986), new(971, 3979),
        new(1000, 3971), new(1030, 3964), new(1059, 3956), new(1088, 3948), new(1117, 3940), new(1145, 3932), new(1174, 3924), new(1202, 3915),
        new(1230, 3906), new(1258, 3897), new(1286, 3888), new(1313, 3879), new(1340, 3870), new(1368, 3860), new(1394, 3851), new(1421, 3841),
        new(1448, 3831), new(1474, 3821), new(1500, 3811), new(1526, 3800), new(1552, 3790), new(1577, 3780), new(1602, 3769), new(1627, 3758),
        new(1652, 3747), new(1677, 3736), new(1701, 3725), new(1725, 3714), new(1749, 3703), new(1773, 3692), new(1796, 3680), new(1820, 3669),
        new(1843, 3657), new(1866, 3646), new(1888, 3634), new(1911, 3622), new(1933, 3610), new(1955, 3599), new(1977, 3587), new(1998, 3575),
        new(2020, 3563), new(2041, 3551), new(2062, 3539), new(2082, 3526), new(2103, 3514), new(2123, 3502), new(2143, 3490), new(2163, 3478),
        new(2183, 3465), new(2202, 3453), new(2221, 3441), new(2240, 3428), new(2259, 3416), new(2278, 3403), new(2296, 3391), new(2314, 3379),
        new(2332, 3366), new(2350, 3354), new(2368, 3341), new(2385, 3329), new(2402, 3317), new(2419, 3304), new(2436, 3292), new(2453, 3279),
        new(2469, 3267), new(2486, 3255), new(2502, 3242), new(2518, 3230), new(2533, 3218), new(2549, 3205), new(2564, 3193), new(2580, 3181),
        new(2595, 3169), new(2609, 3156), new(2624, 3144), new(2639, 3132), new(2653, 3120), new(2667, 3108), new(2681, 3096), new(2695, 3084),
        new(2709, 3072), new(2722, 3060), new(2736, 3048), new(2749, 3036), new(2762, 3024), new(2775, 3012), new(2788, 3000), new(2800, 2988),
        new(2813, 2977), new(2825, 2965), new(2837, 2953), new(2849, 2942), new(2861, 2930), new(2873, 2919), new(2884, 2907), new(2896, 2896),
    ];
    public static Point2D Direction(int dx, int dy)
    {
        long x = Math.Abs((long)dx), y = Math.Abs((long)dy);
        int index = Math.Max(x, y) == 0 ? 0 : (int)(127 * Math.Min(x, y) / Math.Max(x, y));
        var row = _tangent[index];
        return new((dx < 0 ? -1 : 1) * (x > y ? row.Y : row.X), (dy < 0 ? -1 : 1) * (x > y ? row.X : row.Y));
    }
    public static (double X, double Y) Velocity(int dx, int dy, int sourceVelocity)
    {
        var direction = Direction(dx, dy); int velocity = (sourceVelocity << 8) * 75 / 100;
        return (SkillGeometry.Tiles(((long)direction.X * velocity >> 8) / 65536.0),
            SkillGeometry.Tiles(((long)direction.Y * velocity >> 8) / 65536.0));
    }
}
