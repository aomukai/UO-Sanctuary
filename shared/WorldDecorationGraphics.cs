// SPDX-License-Identifier: GPL-3.0-or-later
namespace Sanctuary;

public static class WorldDecorationGraphics
{
    public const int ThroneRoomX = 6890, ThroneRoomY = 3740, ThroneRoomWidth = 31, ThroneRoomHeight = 39;
    public const ushort LeoricThrone = 59994;
    public const byte LeoricThroneBlockHeight = 3;
    public const ushort ThroneRoomFloor = 0x04FC;
    public static bool InThroneRoom(int map, int x, int y) => map == 1
        && x >= ThroneRoomX && x < ThroneRoomX + ThroneRoomWidth
        && y >= ThroneRoomY && y < ThroneRoomY + ThroneRoomHeight;

    public const int MausoleumX = 6935, MausoleumY = 3685, MausoleumSize = 89;
    // Local posts on the raised landing, in Britannia map coordinates.
    public const int PrelateX = 42, PrelateY = 45;
    public const int AltarX = 45, AltarY = 45;
    public const ushort MausoleumAltar = 59993;
    public const ushort MausoleumAltarBlocker = 0x1183; // Low, impassable stone.
    public static bool InAltarFootprint(int localX, int localY) =>
        localY == MausoleumMid && System.Math.Abs(localX - MausoleumMid) <= 2;

    public const int MausoleumMid = MausoleumSize / 2;
    // The renderer uses the same footprints to sort each pier against passing actors.
    public static readonly (int X, int Y)[] MausoleumPiers = [(26, 60), (60, 26), (68, 68), (20, 20)];
    public static bool InMausoleum(int map, int x, int y) => map == 1
        && x >= MausoleumX && x < MausoleumX + MausoleumSize && y >= MausoleumY && y < MausoleumY + MausoleumSize;

    public const ushort DragonHoard = 65520;
    public const ushort DragonHoardGroundFirst = 65521;
    public const int DragonHoardGroundCount = 12;
    public const int DragonHoardGroundDrawOffset = 112;

    public const ushort DiabloCarpetFirst = 59760, DiabloBloodFirst = 59920, DiabloBloodEnd = 59960;
    public const ushort DiabloLavaBarrier = 59992;
    public const int DiabloRitualDrawOffset = DragonHoardGroundDrawOffset;
    public const int DiabloCarpetDrawOffset = 240;
    public const int DiabloCaveX = 6936, DiabloCaveY = 3892, DiabloCaveSize = 81;
    public const int DiabloTerrainRange = 24;

    public static bool IsDiabloBlood(ushort graphic) => graphic is >= DiabloBloodFirst and < DiabloBloodEnd;

    public static bool IsDiabloCarpet(ushort graphic) => graphic is >= DiabloCarpetFirst and < DiabloBloodFirst;

    public static bool IsDiabloFloorArt(ushort graphic) => graphic is >= DiabloCarpetFirst and < DiabloBloodEnd;

    public static bool IsDiabloCaveFloor(int map, int x, int y, ushort graphic) =>
        graphic is >= 0x053B and <= 0x053F && InDiabloCave(map, x, y);

    public static bool IsDiabloCaveTerrain(int map, int x, int y, ushort graphic) =>
        InDiabloCave(map, x, y) && (IsDiabloFloorArt(graphic) || graphic is
            DiabloLavaBarrier or >= 0x053B and <= 0x053F or 0x051E or
            0x025D or 0x0263 or 0x0265 or 0x0267 or 0x027D or 0x027E or
            0x08E1 or 0x08E4 or 0x08E6 or 0x08E8 or 0x0ED0 or 0x1A07 or 0x1A08 or 0x1B0B or 0x1B0E or 0x1B10);

    public static bool InDiabloCave(int map, int x, int y) => map == 1
        && x is >= DiabloCaveX and < DiabloCaveX + DiabloCaveSize
        && y is >= DiabloCaveY and < DiabloCaveY + DiabloCaveSize;

    public static bool IsDragonHoardGround(ushort graphic) =>
        graphic >= DragonHoardGroundFirst && graphic < DragonHoardGroundFirst + DragonHoardGroundCount;

    public const int DaggerThingX = 4013, DaggerThingY = 553, DaggerThingWidth = 21, DaggerThingDepth = 14;
    public const ushort DaggerGroundFirst = 59000, DaggerRoofFirst = 59048, DaggerLintel = 59080;
    public const ushort DaggerPalisade = 59081, DaggerStall = 59082, DaggerRack = 59083, DaggerPalisadeCross = 59084, DaggerMooring = 59085;
    public const ushort DaggerMeadow = 58500, DaggerHallRoof = 59100, DaggerGable = 59120;
    public static bool IsDaggerRoof(ushort graphic) => graphic is >= DaggerRoofFirst and < DaggerLintel
        or >= DaggerHallRoof and < DaggerHallRoof + 12 or >= DaggerGable and < DaggerGable + 24;
    public static bool SameDaggerBuilding(int map, int roofX, int roofY, int x, int y)
    {
        if (map != 1) return false;
        foreach (var (left, top, width, depth) in DaggerBuildings)
            if (roofX >= left && roofX < left + width && roofY >= top && roofY < top + depth)
                return x >= left && x < left + width && y >= top && y < top + depth;
        return false;
    }

    // Thing, market, workshop and byre. All open onto their southern yard.
    public static readonly (int X, int Y, int Width, int Depth)[] DaggerBuildings =
        [(DaggerThingX,DaggerThingY,DaggerThingWidth,DaggerThingDepth), (3985,557,10,10),
         (3998,555,11,12), (3990,577,8,8)];
}
