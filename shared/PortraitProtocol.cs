using System;

namespace Sanctuary;

[Flags]
public enum StoryDiscoveries : ushort
{
    None = 0, UnexpectedSummons = 1, MetGeoffrey = 2, IoloRetired = 4, RoyalAudience = 8,
    DisplacedPixies = 16, UnquietRatman = 32, DespiseLetter = 64, StolenSapling = 128,
    CoveSiege = 256, CoveJaana = 512, CoveMessenger = 1024, CoveFerry = 2048, GnawFreed = 4096, CoveChief = 8192
}

[Flags]
public enum VendorShopTabs : byte
{
    None = 0, Buy = 1, Sell = 2
}

// These values expect four portraits per sheet, in row-major order.
public enum NpcPortrait : byte
{
    WatchGuardDuncan = 21,
    WatchGuardMara,
    HumanRanger,
    ElvenRanger,

    RoadBandit,
    BanditLeader,
    HoodedThief,
    RedheadedThief,

    VeteranBarbarian,
    BraidedBarbarian,
    CoastalWarrior,
    Swordfighter,

    ElderMage,
    ElvenMage,
    FieldCleric,
    TempleCleric,

    PixieLinnet,
    PixieAlder,
    PixieSorrel,
    RatmanSkrik
}

public static class DespiseClue
{
    public const int X = 5504, Y = 626, Z = 27;
    public const ushort CorpseGraphic = 0x1B1D;
}

public static class PortraitProtocol
{
    public const ushort SubCommand = 0xF1;
    public const byte Version = 1;
    public const byte AvatarFace = 0, Conversation = 1, Closed = 2, InteractionEnabled = 3, StoryState = 4, IdentifyShop = 5;
    public const byte BritainDiscoveries = 6, VendorShop = 7, WorldDiscoveries = 8;
    public const byte Leave = 255;
    public const int FaceCount = 8, NpcPortraitCount = PerrinPortrait + 1, MaxReplies = 4;
    public const byte WolfgangPortrait = 20;
    public const byte MerydwenPortrait = 41;
    public const byte AyilaPortrait = 42;
    public const byte GnawPortrait = 43, ThomasPortrait = 44, CoveFerrymanPortrait = 45, JaanaPortrait = 46, PerrinPortrait = 47;
    public const int ReplyPacketLength = 11;
    public const int InteractPacketLength = 10;
    public const int ShopTabPacketLength = 12;
    public const int NpcInteractionRange = 3;
    public const int MaxTextBytes = 512;
    private const int ExtendedCreation = 0xA0, ClassCount = 8;

    // Preserve the original class byte for face 1 and accept it from older clients.
    public static byte EncodeCreation(DiabloClass characterClass, byte face) => face == 0
        ? (byte)characterClass
        : (byte)(ExtendedCreation + (face - 1) * ClassCount + (int)characterClass - (int)DiabloClass.Amazon);

    public static bool TryDecodeCreation(int value, out DiabloClass characterClass, out byte face)
    {
        face = 0;
        if (DiabloClasses.TryFromWire(value, out characterClass))
            return true;
        int offset = value - ExtendedCreation;
        if (offset is < 0 or >= ((FaceCount - 1) * ClassCount))
            return false;
        face = (byte)(1 + offset / ClassCount);
        return DiabloClasses.TryFromWire((int)DiabloClass.Amazon + offset % ClassCount, out characterClass);
    }
}

public static class CoveClue
{
    public const int X = 2164, Y = 1379, Z = 4;
    public const ushort CorpseGraphic = 0x3D67;
}
