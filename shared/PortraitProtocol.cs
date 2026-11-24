using System;

namespace Sanctuary;

[Flags]
public enum StoryDiscoveries : ulong
{
    None = 0, UnexpectedSummons = 1, MetGeoffrey = 2, IoloRetired = 4, RoyalAudience = 8,
    DisplacedPixies = 16, UnquietRatman = 32, DespiseLetter = 64, StolenSapling = 128,
    CoveSiege = 256, CoveJaana = 512, CoveMessenger = 1024, CoveFerry = 2048, GnawFreed = 4096, CoveChief = 8192,
    // New entries travel outside the original 16-bit journal snapshot.
    HedgeTobin = 1u << 16, HedgeRoderic = 1u << 17, HedgeOrvath = 1u << 18,
    IoloDungeonReports = 1u << 19, RoyalInvestigation = 1u << 20, JaanaBeltReceived = 1u << 21,
    CampaignSummons = 1u << 22, FireIsland = 1u << 23, SkeletonKing = 1u << 24,
    BlackthornSummons = 1u << 25, DaggerIsle = 1u << 26, Lazarus = 1u << 27,
    EmptyTowns = 1u << 28, DestardLead = 1u << 29, Diablo = 1u << 30, RoyalProclamation = 1u << 31,
    DestardQueen = 1UL << 32, DestardConsort = 1UL << 33, DestardNursery = 1UL << 34,
    DestardBetrayal = 1UL << 35, DestardDaemons = 1UL << 36, DestardEnslaved = 1UL << 37,
    DestardAftermath = 1UL << 38
}

[Flags]
public enum VendorShopTabs : byte
{
    None = 0, Buy = 1, Sell = 2
}

public enum NpcPortrait : ushort
{
    Elin = 3,
    Geoffrey = 4,
    LordBritish = 10,
    Iolo = 11,

    // The original expansion expects four portraits per sheet, in row-major order.
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
    RatmanSkrik,

    // Standalone paintings named portrait-npc-<lowercase enum name>.png.
    Garran = 49,
    Bess,
    Leofric,
    Aldric,
    Tamsin,
    Edda,
    Hugh,
    Elara,
    Gareth,
    Harlan,
    HedgeGirl,
    HedgeGrandfather,
    HedgeEldestDaughter,
    HedgeEldestSon,
    HedgeYoungerSon,
    HedgeGrandmother,
    HedgeMother,
    HedgeFather,
    HedgeDaemon,
    BritainHorseMerchant,
    YewMountTrader,
    YewMayor,
    YewVampire,
    AbbeyCleric,
    Nightwind,
    JensWon,
    GeoffreyDishevelled,
    GeoffreyExhausted,
    GeoffreyRelieved,
    BritainHarbourMaster,
    Blackthorn,
    Dupre,
    Shamino,
    DestardQueen,
    DestardConsort,
    Lazarus,
    DestardGuardWest,
    DestardGuardNorth,
    DestardServant
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
    public const byte BritainDiscoveries = 6, VendorShop = 7, WorldDiscoveries = 8, ExtendedConversation = 9;
    public const byte Leave = 255;
    public const int FaceCount = 8, MaxReplies = 4;
    public const byte WolfgangPortrait = 20;
    public const byte MerydwenPortrait = 41;
    public const byte AyilaPortrait = 42;
    public const byte GnawPortrait = 43, ThomasPortrait = 44, CoveFerrymanPortrait = 45, JaanaPortrait = 46, PerrinPortrait = 47;
    public const byte VesperMayorPortrait = 48;
    public const ushort FirstStandaloneNpcPortrait = (ushort)NpcPortrait.Garran;
    public const ushort FirstTrainerPortrait = 256;
    public const int TrainerPortraitGrid = 6, TrainerPortraitsPerSheet = TrainerPortraitGrid * TrainerPortraitGrid;
    public const int TrainerPortraitSheets = 13;
    public const int ReplyPacketLength = 11;
    public const int InteractPacketLength = 10;
    public const int ShopTabPacketLength = 12;
    public const int NpcInteractionRange = 3;
    public const int MaxTextBytes = 512;
    private const int ExtendedCreation = 0xA0, ClassCount = 8;

    public static bool IsTrainerPortrait(ushort portrait) =>
        portrait is >= FirstTrainerPortrait and < FirstTrainerPortrait + TrainerPortraitSheets * TrainerPortraitsPerSheet;

    public static bool IsNpcPortrait(ushort portrait) =>
        portrait <= VesperMayorPortrait || IsTrainerPortrait(portrait)
        || portrait is >= FirstStandaloneNpcPortrait and < FirstTrainerPortrait && Enum.IsDefined((NpcPortrait)portrait);

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

public static class HedgeMazeLayout
{
    public const ushort SparkleGraphic = 0x375A;
    public static readonly (int X, int Y, int Z) Entry = (1166, 2237, 60);
    public static readonly (int X, int Y, int Z) Exit = (1163, 2204, 60);
    public static readonly (int X, int Y, int Z) RitualRoom = (1165, 2210, 60);
    public static readonly (int X, int Y, int Z) Lever = (1138, 2218, 60);
    public static readonly (int X, int Y, int Z)[] Ghosts =
    [
        (1163, 2241, 40), (1164, 2234, 60), (1137, 2241, 40), (1169, 2255, 4),
        (1136, 2228, 40), (1130, 2217, 40), (1131, 2220, 40), (1132, 2219, 60)
    ];
    public static readonly (int X, int Y, int Z)[] Fowl = [(1136, 2240, 40), (1135, 2241, 40), (1136, 2242, 40)];
}
