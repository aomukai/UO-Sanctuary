namespace Sanctuary;

public enum BeltRequest : byte { Poll, Use, Load, Remove, Fill }
public enum RestorationKind : byte { Healing = 1, Mana = 2 }
public static class BeltProtocol
{
    public const ushort SubCommand = 0xF6;
    public const byte Version = 1;
    public const int Columns = 4, MaxRows = 4, Cells = 16, PollMs = 250;
    public const int RequestLength = 12, StateLength = 172;
}
