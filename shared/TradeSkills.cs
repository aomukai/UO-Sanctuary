using System;

namespace Sanctuary;

public static class TradeSkills
{
    public static ReadOnlySpan<byte> Gathering => [18, 44, 45];
    public static ReadOnlySpan<byte> Crafting => [0, 7, 8, 11, 12, 13, 23, 34, 37, 56];
    public const int Count = 13;
    public static bool IsCraft(int index) => index is 0 or 7 or 8 or 11 or 12 or 13 or 23 or 34 or 37 or 56;
    public static bool IsGathering(int index) => index is 18 or 44 or 45;

    public static bool Includes(int index) => index is 0 or 7 or 8 or 11 or 12 or 13 or 18 or 23 or 34 or 37 or 44 or 45 or 56;
}
