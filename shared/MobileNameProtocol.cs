namespace Sanctuary;

public enum MobileNameStyle : byte
{
    Native = 0,
    Friendly = 1,
    Neutral = 3,
    Hostile = 6
}

public static class MobileNameProtocol
{
    public const ushort SubCommand = 0x0100;
    public const byte Version = 1;
    public const int Length = 11;
    public const byte ClickThroughSummon = 0x80;
}
