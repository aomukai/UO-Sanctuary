namespace Sanctuary;

public enum SkeletonAppearance : byte
{
    None,
    Sword,
    SwordAndShield,
    ArmouredWarrior,
    MageHat,
    MageStaff,
    MageRobe,
    BlackRobe,
    Bow,
    Crossbow,
    Jester,
    Mace,
    Executioner,
    CorruptedHatchling,
    ShameGhost,
    FleetCaptain
}

public static class SkeletonAppearanceProtocol
{
    public const ushort SubCommand = 0xFE;
    public const byte Version = 1;
    public const int Length = 11;

    public static SkeletonAppearance For(bool mage, int hardRank) => (mage, hardRank) switch
    {
        (false, >= 20) => SkeletonAppearance.ArmouredWarrior,
        (false, >= 10) => SkeletonAppearance.SwordAndShield,
        (false, >= 5) => SkeletonAppearance.Sword,
        (true, >= 20) => SkeletonAppearance.MageRobe,
        (true, >= 10) => SkeletonAppearance.MageStaff,
        (true, >= 5) => SkeletonAppearance.MageHat,
        _ => SkeletonAppearance.None
    };
}
