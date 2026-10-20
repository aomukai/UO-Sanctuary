namespace Sanctuary;

public enum BeltRequest : byte { Poll, Use, Load, Remove, Fill }
public enum RestorationKind : byte { Healing = 1, Mana = 2, Rejuvenation = 3, InstantHealing = 4, InstantMana = 5, CombatBuff = 6, TeleportScroll = 7, IdentifyScroll = 8, TownPortalScroll = 9 }
public static class BeltProtocol
{
    public const ushort SubCommand = 0xF6;
    public const byte Version = 3;
    public const int Columns = 4, MaxRows = 4, Cells = 16, PollMs = 250;
    public const int RequestLength = 12, UseRequestLength = 18, StateLength = 172;
    public const int IdentifyGraphic = 0x1F2D, PortalGraphic = 0x1F4C, IdentifyTomeGraphic = 0xFF1, PortalTomeGraphic = 0xFF0;
    public const int TeleportGraphic = 0x1F42, TeleportHue = 0x482;
}
