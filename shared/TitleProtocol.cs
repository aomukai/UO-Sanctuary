namespace Sanctuary;

public static class TitleProtocol
{
    public const ushort SubCommand = 0xF8;
    public const byte Version = 1;
    public const int HeaderLength = 10, MaxTextBytes = 512;
}
