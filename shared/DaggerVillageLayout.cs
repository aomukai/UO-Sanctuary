// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;

namespace Sanctuary;

// Both engines load these buildings with the map, independently of the range
// at which mobiles and interactive items are streamed.
public static class DaggerVillageLayout
{
    public readonly record struct Cell(int X, int Y, int Z);
    public readonly record struct Tile(int Graphic, Cell At);
    // The carved porch sits west of the midpoint of the long roof.
    public const int DoorX = 4021, DoorY = 566, DoorHalfWidth = 2;
    private const int Planks = 0x04C6;
    public static readonly ILookup<(int X, int Y), Tile> Blocks = Build().ToLookup(t => (t.At.X >> 3, t.At.Y >> 3));

    private static HashSet<Tile> Build()
    {
        HashSet<Tile> tiles = [];
        void Piece(int graphic, Cell at) => tiles.Add(new Tile(graphic, at));
        for (int i = 0; i < WorldDecorationGraphics.DaggerBuildings.Length; i++)
        {
            var (x, y, width, depth) = WorldDecorationGraphics.DaggerBuildings[i];
            TimberHall(x, y, width, depth, i == 0);
        }
        Waterfront();
        Enclosures();
        Piece(0xA300, new(3986, 574, 0));
        Piece(WorldDecorationGraphics.DaggerStall, new(3987, 567, 0));
        Piece(WorldDecorationGraphics.DaggerRack, new(3993, 567, 0));
        Piece(WorldDecorationGraphics.DaggerRack, new(4006, 565, 0));
        Piece(0x0E31, new(4023, 559, 0));
        foreach (int x in new[] { 4017, 4020, 4026, 4029 })
        {
            Piece(0x0B2D, new(x, 558, 0));
            Piece(0x0B2D, new(x, 561, 0));
        }
        Piece(0x0B32, new(4023, 555, 0));
        Piece(0x0B32, new(4025, 555, 0));
        BearRug(4018, 563, 0x1E36); // Brown bearskin
        BearRug(4028, 563, 0x1E49); // Polar bearskin
        return tiles;

        static int Variant(int x, int y) => (x & 3) + 4 * (y & 3);

        // South-facing UO bearskins use three rows of art, starting in the southeast.
        void BearRug(int x, int y, int graphic)
        {
            for (int dy = 1; dy >= -1; dy--)
                for (int column = 0; column < 3; column++)
                    Piece(graphic++, new(x + (dy == 0 ? column - 1 : 1 - column), y + dy, 1));
        }

        void Waterfront()
        {
            for (int x = 3966; x <= 3982; x++)
                for (int y = 567; y <= 577; y++)
                {
                    if (x >= 3970 && y is < 570 or > 574) continue;
                    Piece(59032 + Variant(x, y), new(x, y, 0));
                    if ((x == 3966 || x == 3969 || x == 3974 || x == 3980) && (y == (x < 3970 ? 567 : 570) || y == (x < 3970 ? 577 : 574)))
                        Piece(0x0093, new(x, y, -14));
                }
            foreach (int x in new[] {3973,3977,3981})
                Piece(WorldDecorationGraphics.DaggerMooring,new(x,574,0));
            Piece(WorldDecorationGraphics.DaggerMooring,new(3974,570,0));
            Piece(0x0E77, new(3967, 568, 0));
            Piece(0x0E7F, new(3967, 576, 0));
            Piece(0x14F8, new(3968, 576, 0));
        }

        void Enclosures()
        {
            for (int x = 3983; x <= 4036; x++)
            {
                Piece(59081, new(x, 551, 0));
                if (x is < 4011 or > 4014 && x >= 3986) Piece(59081, new(x, 587, 0));
            }
            for (int y = 552; y < 587; y++)
            {
                if (y is < 570 or > 574) Piece(59084, new(4036, y, 0));
                if (y < 568) Piece(59084, new(3983, y, 0));
                if (y > 575) Piece(59084, new(3986, y, 0));
            }
            for (int x = 4001; x <= 4008; x++)
            {
                if (x != 4004) Piece(0x085D, new(x, 578, 0));
                Piece(0x085D, new(x, 584, 0));
            }
            for (int y = 579; y < 584; y++)
            {
                Piece(0x085C, new(4001, y, 0));
                Piece(0x085C, new(4008, y, 0));
            }
            Piece(0x0F36, new(3992, 579, 0));
            Piece(0x0F36, new(3993, 579, 0));
        }

        void TimberHall(int left, int top, int width, int depth, bool thing)
        {
            int door = thing ? DoorX : left + width / 2, south = top + depth - 1, east = left + width - 1;
            for (int x = left; x <= east; x++)
                for (int y = top; y <= south; y++)
                {
                    int slope = Math.Min(y - top, south - y);
                    var at = new Cell(x, y, 0);
                    Piece(Planks, at);
                    bool end = x == left || x == east;
                    bool side = y == top || y == south;
                    bool entrance = y == south && Math.Abs(x - door) <= (thing ? DoorHalfWidth : 0);
                    if (side && !entrance)
                        Piece(end ? 0x0093 : x % 4 == 0 ? 0x0098 : 0x0092, at);
                    else if (end)
                        Piece(y % 4 == 0 ? 0x0099 : 0x0091, at);
                    int eave = thing ? 25 : 20;
                    if (thing && side && !entrance)
                        Piece(0x0092, new(x, y, 5));
                    if (!thing)
                    {
                        bool far = y - top < depth / 2;
                        int roof = WorldDecorationGraphics.DaggerRoofFirst + Variant(x, y) + (far ? 16 : 0);
                        Piece(roof, new(x, y, eave + 4 * slope));
                        if (end)
                            Piece(WorldDecorationGraphics.DaggerGable + (x == east ? 12 : 0) + (far ? 6 : 0) + slope, new(x, y, eave));
                    }
                }
            if (thing)
            {
                (int X, int Y)[] anchors = [(-20, 0), (-17, 0), (-14, 0), (-11, 0), (-8, 0), (-5, 0), (-2, 0), (0, 0), (0, -3), (0, -6), (0, -9), (0, -12)];
                for (int i = 0; i < anchors.Length; i++)
                    Piece(WorldDecorationGraphics.DaggerHallRoof + i, new(east + anchors[i].X, south + anchors[i].Y, 25));
                Piece(59083, new(door - 3, south + 1, 0));
            }
        }

    }
}
