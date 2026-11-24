// SPDX-License-Identifier: GPL-3.0-or-later
namespace Sanctuary;

public static class RavenAppearance
{
    public const ushort FirstBody = 2000;
    public const int MaxCount = 5;
    public const byte DiveAction = 4;
    public const int DiveFrames = 10;

    public static bool IsFlock(ushort body) => body >= FirstBody && body < FirstBody + MaxCount;
}
