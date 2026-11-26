namespace Sanctuary;

public static class DruidFormVisualProtocol
{
    public const ushort SubCommand = 0xFA;
    public const byte Version = 1;
    public const int Length = 11;

    public const byte Human = 0;
    public const byte Wolf = 1;
    public const byte Bear = 2;

    public const ushort WerewolfBody = 2017;
    public const ushort WerebearBody = 2018;
    public const int BodyCount = 2;

    public static bool IsCustomBody(ushort body) => body is WerewolfBody or WerebearBody;
}
