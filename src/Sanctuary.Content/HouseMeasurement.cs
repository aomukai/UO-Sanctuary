// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Serialization;
using Server;

namespace Sanctuary;

public sealed record HouseFloor(int X, int Y, int Width, int Height, int Z, bool Inherited = false)
{
    [JsonIgnore] public Rectangle2D Bounds => new(X, Y, Width, Height);
    [JsonIgnore] public int Area => checked(Width * Height);
    public bool Contains(Point3D p, int height) => Bounds.Contains(p) && p.Z + Math.Max(height, 1) > Z && p.Z < Z + 20;
}

public sealed record HouseDefinition(HouseFloor[] Floors, HouseFloor Garden, string Name, Point3D Exit)
{
    [JsonIgnore] public int LivingTiles => Floors.Sum(f => f.Area);
    [JsonIgnore] public int GardenTiles
    {
        get
        {
            if (Garden == null) { return 0; }
            int total = 0;
            for (int x = Garden.X; x < Garden.X + Garden.Width; x++)
            {
                for (int y = Garden.Y; y < Garden.Y + Garden.Height; y++)
                {
                    if (!Floors.Any(f => f.Bounds.Contains(new Point2D(x, y)))) { total++; }
                }
            }
            return total;
        }
    }
    [JsonIgnore] public int SuggestedPrice => checked(LivingTiles * Housing.IndoorRate + GardenTiles * Housing.GardenRate);
    public bool Inside(Point3D p, int height = 16) => Floors.Any(f => f.Contains(p, height))
        || Garden != null && Garden.Bounds.Contains(p) && p.Z + height > Garden.Z - 16 && p.Z < Garden.Z + 20;
    public bool Indoors(Point3D p) => Floors.Any(f => f.Contains(p, 16));
    public void Validate(Map map, bool checkExit = true)
    {
        if (map == null || map == Map.Internal || Floors is not { Length: >= 1 and <= 5 }
            || string.IsNullOrWhiteSpace(Name) || Name.Length > 60)
        { throw new InvalidDataException("Choose a name, map and one to five floors."); }
        foreach (var floor in Floors.Concat(Garden == null ? [] : new[] { Garden }))
        {
            if (floor == null || floor.Width is < 1 or > 64 || floor.Height is < 1 or > 64 || floor.Z is < -128 or > 127
                || floor.X < 0 || floor.Y < 0 || floor.X + floor.Width > map.Width || floor.Y + floor.Height > map.Height)
            { throw new InvalidDataException("Each area must fit on the map and be at most 64 by 64 tiles."); }
        }
        if (Floors[0].Inherited) { throw new InvalidDataException("Floor 1 needs its own measurement."); }
        for (int i = 1; i < Floors.Length; i++)
        {
            var floor = Floors[i]; var first = Floors[0];
            if (floor.Z < Floors[i - 1].Z + 16 || floor.Inherited &&
                (floor.X != first.X || floor.Y != first.Y || floor.Width != first.Width || floor.Height != first.Height || floor.Z != first.Z + 20 * i))
            { throw new InvalidDataException("Invalid upper-floor measurement or inheritance."); }
        }
        if (Exit.X < 0 || Exit.Y < 0 || Exit.X >= map.Width || Exit.Y >= map.Height || Exit.Z is < -128 or > 127 || Inside(Exit)
            || checkExit && !map.CanFit(Exit, 16, false, false, true))
        { throw new InvalidDataException("Mark a clear exit outside the property."); }
    }
}

public sealed class HouseMeasurement
{
    public Map Map { get; }
    public Point3D?[][] Corners { get; } = Enumerable.Range(0, 6).Select(_ => new Point3D?[4]).ToArray();
    public int FloorCount { get; set; } = 1;
    public bool HasGarden { get; set; }
    public Point3D? Exit { get; set; }
    public HouseMeasurement(Map map) => Map = map;
    public static HouseFloor Rectangle(Point3D?[] corners)
    {
        if (corners.Any(p => p == null)) { throw new InvalidDataException("Complete all four corners, or clear an upper-floor override."); }
        var points = corners.Select(p => p.Value).ToArray();
        int left = points.Min(p => p.X), right = points.Max(p => p.X), top = points.Min(p => p.Y), bottom = points.Max(p => p.Y);
        if (left == right || top == bottom || points.Select(p => (p.X, p.Y)).Distinct().Count() != 4
            || points.Any(p => (p.X != left && p.X != right) || (p.Y != top && p.Y != bottom))
            || points.Max(p => p.Z) - points.Min(p => p.Z) > 2)
        { throw new InvalidDataException("Mark the four distinct corners of one rectangular floor at the same height."); }
        return new(left, top, right - left + 1, bottom - top + 1, points.Min(p => p.Z));
    }
    public HouseDefinition Build(string name)
    {
        if (FloorCount is < 1 or > 5 || Exit == null) { throw new InvalidDataException("Set one to five floors and mark an outside exit."); }
        var first = Rectangle(Corners[0]);
        var floors = new HouseFloor[FloorCount]; floors[0] = first;
        for (int i = 1; i < floors.Length; i++)
        {
            floors[i] = Corners[i].All(p => p == null) ? first with { Z = first.Z + i * 20, Inherited = true } : Rectangle(Corners[i]);
            if (floors[i].Z < floors[i - 1].Z + 16) { throw new InvalidDataException("Upper floors must be above the preceding floor."); }
        }
        var result = new HouseDefinition(floors, HasGarden ? Rectangle(Corners[5]) : null, name, Exit.Value);
        result.Validate(Map);
        return result;
    }
}
