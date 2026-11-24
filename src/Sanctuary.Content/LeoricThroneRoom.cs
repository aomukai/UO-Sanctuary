// SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
using Server;
using Server.Regions;

namespace Sanctuary;

internal sealed class LeoricThroneRegion(string name, Map map, Rectangle3D bounds) : DungeonRegion(name, map, 200, bounds)
{
    public override void AlterLightLevel(Mobile mobile, ref int global, ref int personal) => global = 11;
}

internal static class LeoricThroneRoom
{
    internal const int Width = WorldDecorationGraphics.ThroneRoomWidth, Height = WorldDecorationGraphics.ThroneRoomHeight;
    internal const int VestibuleY = 31;
    internal const int Throne = WorldDecorationGraphics.LeoricThrone;
    private const int NorthStandard = 59995, NorthTornStandard = 59996;
    private const int WestStandard = 59997, WestTornStandard = 59998;
    internal static readonly Point3D Origin = new(WorldDecorationGraphics.ThroneRoomX, WorldDecorationGraphics.ThroneRoomY, 0);

    public static void Initialize() =>
        TileData.ItemTable[Throne] = new ItemData("the broken lion throne", TileFlag.Impassable, 255, 0, 0, 0, 0,
            WorldDecorationGraphics.LeoricThroneBlockHeight);

    internal static void Build(Map map, CampaignArenas.Arena hall)
    {
        var existing = new HashSet<(Point3D, int)>();
        var obsoleteTiles = new List<CampaignArenaTile>();
        foreach (var tile in map.GetItemsInBounds<CampaignArenaTile>(hall.Bounds))
        {
            var placement = (tile.X - hall.Origin.X, tile.Y - hall.Origin.Y, tile.ItemID);
            bool halfBanner = tile.Z == 12 && placement is (6 or 13 or 19, 1, 0x0426) or (1, 9, 0x042B) or (1, 25, 0x042F);
            bool oldThrone = (placement, tile.Z) is ((15, 3, Throne), 1) or ((15, 3, 0x00DF), 0);
            if (halfBanner || oldThrone)
                obsoleteTiles.Add(tile);
            else
                existing.Add((tile.Location, tile.ItemID));
        }
        foreach (var tile in obsoleteTiles) tile.Delete();

        void Place(int x, int y, int id, int z = 0, bool lit = false)
        {
            var p = new Point3D(hall.Origin.X + x, hall.Origin.Y + y, z);
            if (!existing.Add((p, id))) return;
            var tile = new CampaignArenaTile(id, 0);
            if (lit) tile.Light = LightType.Circle225;
            if (id == Throne) tile.Name = "the broken lion throne";
            if (id is >= NorthStandard and <= WestTornStandard) tile.Name = "a tattered royal standard";
            tile.MoveToWorld(p, map);
        }

        for (int y = 0; y < Height; y++)
        for (int x = 0; x < Width; x++)
        {
            Place(x, y, hall.Floor);

            bool eastWest = y == 0 || y == Height - 1;
            if (!eastWest && x != 0 && x != Width - 1) continue;
            // Standing rear walls rise above the fragments along the near sides.
            if (y == 0 && x == 0) Place(x, y, 0x00C7);
            else if (y == 0 || x == 0)
            {
                bool breach = y == 0 ? x is >= 23 and <= 27 : y is >= 13 and <= 18;
                int axis = eastWest ? x : y;
                int wall = breach ? (eastWest ? 0x00DE : 0x00DD)
                    : eastWest ? 0x00C8 : 0x00C9;
                Place(x, y, wall);
                if (!breach && axis % 8 != 0)
                    Place(x, y, axis % 6 == 3 ? (eastWest ? 0x00CA : 0x00CB) : wall, 20);
            }
            else Place(x, y, eastWest ? 0x00DE : 0x00DD);
        }

        // The outer galleries have alternating intact piers and broken stumps.
        foreach (int x in new[] { 7, 23 })
        foreach (int y in new[] { 8, 17, 26 })
        {
            bool ruined = (x, y) is (7, 17) or (23, 26);
            Place(x, y, ruined ? 0x00DF : 0x011F);
            if (ruined) Place(x + 1, y, 0x027B);
        }

        // A broken screen separates the arrival vestibule from the fighting floor.
        foreach (int x in new[] { 5, 6, 7, 8, 9, 10, 20, 21, 22, 23, 24, 25 })
            Place(x, VestibuleY, x is 10 or 20 ? 0x011F : 0x00DE);

        Place(15, 3, Throne);
        Place(11, 4, 0x0E31, lit: true);
        Place(19, 4, 0x0E31, lit: true);
        Place(10, 28, 0x0E31, lit: true);
        Place(20, 28, 0x0E31, lit: true);
        Place(5, 1, NorthTornStandard, 4);
        Place(10, 1, NorthStandard, 4);
        Place(20, 1, NorthTornStandard, 4);
        Place(1, 9, WestStandard, 4);
        Place(1, 25, WestTornStandard, 4);
        Place(5, 15, 0x0C10);
        Place(5, 16, 0x0C2E);
        Place(25, 25, 0x0C11);
        Place(26, 24, 0x0C2F);
        Place(25, 2, 0x0278);
        Place(26, 3, 0x0279);
    }
}
