// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using Server;

namespace Sanctuary;

public sealed record TreasureRoom(Point2D Center, int Shape);
public sealed record TreasurePlan(Point2D[] Floor, Point2D[] Walls, TreasureRoom[] Rooms, int[][] Links);
public static class TreasureLayout
{
    // Rooms are authored masks; only their positions and connections are shuffled.
    private static readonly string[][] _rooms =
    [
        ["      ###########      ", "  ###################  ", "#######################", "#######################",
         "#######################", "#######################", "#######################", "#######################",
         "#######################", "#######################", "#######################", "#######################",
         "#######################", "#######################", "#######################", "  ###################  ", "      ###########      "],
        ["     #############     ", "     #############     ", "     #############     ", "     #############     ",
         "#######################", "#######################", "#######################", "#######################",
         "#######################", "#######################", "#######################", "#######################",
         "#######################", "     #############     ", "     #############     ", "     #############     ", "     #############     "],
        [" ##################### ", "#######################", "#######################", "#######################",
         "#####  #########  #####", "#####  #########  #####", "#######################", "#######################",
         "#######################", "#######################", "#######################", "#####  #########  #####",
         "#####  #########  #####", "#######################", "#######################", "#######################", " ##################### "]
    ];
    public static TreasurePlan Generate(int seed)
    {
        var random = new Random(seed); var rules = TreasureHunts.Settings;
        int count = random.Next(rules.MinimumRooms, rules.MaximumRooms + 1);
        var cells = new List<Point2D> { new(1, 1) }; var edges = new List<int[]>();
        while (cells.Count < count)
        {
            var candidates = new List<(int From, Point2D To)>();
            for (int i = 0; i < cells.Count; i++)
            {
                foreach (var next in Neighbors(cells[i]))
                { if (next.X is >= 0 and < 4 && next.Y is >= 0 and < 4 && !cells.Contains(next)) { candidates.Add((i, next)); } }
            }
            var chosen = candidates[random.Next(candidates.Count)];
            edges.Add([chosen.From, cells.Count]); cells.Add(chosen.To);
        }
        // A few loops offer alternative approaches without moving encounter anchors closer.
        for (int i = 0; i < cells.Count; i++)
        {
            for (int j = i + 1; j < cells.Count; j++)
            {
                if (Math.Abs(cells[i].X - cells[j].X) + Math.Abs(cells[i].Y - cells[j].Y) == 1
                    && !edges.Exists(e => e[0] == i && e[1] == j || e[0] == j && e[1] == i) && random.Next(4) == 0)
                { edges.Add([i, j]); }
            }
        }
        var rooms = new TreasureRoom[count]; var floor = new HashSet<Point2D>();
        for (int i = 0; i < cells.Count; i++)
        {
            var center = new Point2D(32 + cells[i].X * rules.CellSize, 32 + cells[i].Y * rules.CellSize);
            int shape = random.Next(_rooms.Length); rooms[i] = new(center, shape); var mask = _rooms[shape];
            for (int y = 0; y < mask.Length; y++)
            { for (int x = 0; x < mask[y].Length; x++) { if (mask[y][x] == '#') { floor.Add(new(center.X + x - 11, center.Y + y - 8)); } } }
        }
        foreach (var edge in edges)
        {
            var a = rooms[edge[0]].Center; var b = rooms[edge[1]].Center;
            if (a.X == b.X)
            { for (int y = Math.Min(a.Y, b.Y); y <= Math.Max(a.Y, b.Y); y++) { for (int d = -2; d <= 2; d++) { floor.Add(new(a.X + d, y)); } } }
            else
            { for (int x = Math.Min(a.X, b.X); x <= Math.Max(a.X, b.X); x++) { for (int d = -2; d <= 2; d++) { floor.Add(new(x, a.Y + d)); } } }
        }
        var walls = new HashSet<Point2D>();
        foreach (var point in floor)
        {
            for (int x = -1; x <= 1; x++)
            { for (int y = -1; y <= 1; y++) { var next = new Point2D(point.X + x, point.Y + y); if (!floor.Contains(next)) { walls.Add(next); } } }
        }
        var floors = new List<Point2D>(floor); var border = new List<Point2D>(walls);
        floors.Sort(Compare); border.Sort(Compare);
        return new(floors.ToArray(), border.ToArray(), rooms, edges.ToArray());
    }
    private static int Compare(Point2D a, Point2D b) => a.Y != b.Y ? a.Y.CompareTo(b.Y) : a.X.CompareTo(b.X);
    internal static IEnumerable<Point2D> Neighbors(Point2D p)
    { yield return new(p.X - 1, p.Y); yield return new(p.X + 1, p.Y); yield return new(p.X, p.Y - 1); yield return new(p.X, p.Y + 1); }
}
