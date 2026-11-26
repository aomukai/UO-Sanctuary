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
    FleetCaptain,
    BloodGolem,
    IronGolem,
    FireGolem,
    PlainSkeleton,
    Valkyrie
}

public static class SummonBodyAppearance
{
    public const ushort FirstBody = 2008;
    public const int Count = 9;
    public const ushort ValkyrieBody = FirstBody + 8;

    public static ushort For(ushort graphic, SkeletonAppearance appearance) => (graphic, appearance) switch
    {
        (50 or 57 or 147, SkeletonAppearance.SwordAndShield) => FirstBody,
        (50 or 57 or 147, SkeletonAppearance.ArmouredWarrior) => FirstBody + 1,
        (56 or 148, SkeletonAppearance.MageHat) => FirstBody + 2,
        (56, SkeletonAppearance.MageStaff) => FirstBody + 3,
        (56 or 148, SkeletonAppearance.MageRobe) => FirstBody + 4,
        (14, SkeletonAppearance.BloodGolem) => FirstBody + 5,
        (14, SkeletonAppearance.IronGolem) => FirstBody + 6,
        (14, SkeletonAppearance.FireGolem) => FirstBody + 7,
        (0x191, SkeletonAppearance.Valkyrie) => ValkyrieBody,
        _ => 0
    };

    public static bool IsBody(ushort graphic) => graphic >= FirstBody && graphic < FirstBody + Count;
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
