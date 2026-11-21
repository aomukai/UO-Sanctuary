namespace Sanctuary;

public static class DespiseLighting
{
    public const ushort MushroomGraphic = 0x0D16;
    public const ushort Green = 0x0042, Red = 0x0028, Blue = 0x0063, Yellow = 0x0036;

    public static bool IsGlowingMushroom(int x, int y, ushort graphic, ushort hue) =>
        graphic == MushroomGraphic && x is >= 5384 and <= 5511 && y is >= 520 and <= 632
        && hue is Green or Red or Blue or Yellow;
}
