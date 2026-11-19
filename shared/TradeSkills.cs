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

[Flags]
public enum TradeSkillSheetFlags : byte
{
    None = 0,
    Managed = 1,
    WardrobeUnlocked = 2
}

public static class TradeSkillSheetProtocol
{
    public const ushort SubCommand = 0xFC;
    public const byte Version = 1;
    public const byte NoActiveCraft = byte.MaxValue;
    public const int HeaderLength = 8;
    public const int PacketLength = HeaderLength + TradeSkills.Count * 2;
    public const int PollMs = 650;
}
