// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using Server;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary;

public sealed record RecognitionHall(string Id, string Town, string Map, int X, int Y, int Z, string Source);

public static partial class TownRecognition
{
    private static readonly ILogger _logger = LogFactory.GetLogger(typeof(TownRecognition));
    public static RecognitionHall[] Halls { get; private set; } = [];
    public static void Load()
    {
        Halls = BalanceCatalog.Read<RecognitionHall>("recognition-halls");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var hall in Halls)
        {
            if (string.IsNullOrWhiteSpace(hall.Id) || !ids.Add(hall.Id) || string.IsNullOrWhiteSpace(hall.Town)
                || hall.Map is not ("Felucca" or "Trammel") || hall.X < 0 || hall.Y < 0 || string.IsNullOrWhiteSpace(hall.Source))
            { throw new InvalidDataException("Invalid recognition hall."); }
        }
        if (Halls.Length == 0) { throw new InvalidDataException("Recognition needs a town hall."); }
    }
    [CallPriority(210)]
    public static void Initialize()
    {
        if (!ClassProfessions.Enabled) { return; }
        int created = 0;
        var used = new HashSet<(Map, Point3D)>();
        foreach (var hall in Halls)
        {
            Map.TryParse(hall.Map, null, out var map);
            var saved = Profiles.Office(hall.Id);
            var registrar = saved == null ? null : World.FindMobile(saved.Registrar) as CivicRegistrar;
            var wardrobe = saved == null ? null : World.FindItem(saved.Wardrobe) as PublicRecognitionWardrobe;
            if (registrar is not { Deleted: false })
            {
                registrar = new CivicRegistrar(hall.Id, hall.Town);
                registrar.MoveToWorld(OfficeFloor(map, new(hall.X, hall.Y, hall.Z), used), map);
                created++;
            }
            used.Add((map, registrar.Location));
            if (wardrobe is not { Deleted: false })
            {
                wardrobe = new PublicRecognitionWardrobe(hall.Id, hall.Town);
                wardrobe.MoveToWorld(OfficeFloor(map, new(registrar.X + 2, registrar.Y, registrar.Z), used), map);
                created++;
            }
            used.Add((map, wardrobe.Location));
            Profiles.Office(hall.Id, new(registrar.Serial, wardrobe.Serial));
        }
        _logger.Information("Recognition: {Halls} civic offices; {Created} service objects created", Halls.Length, created);
    }
    private static Point3D OfficeFloor(Map map, Point3D anchor, HashSet<(Map, Point3D)> used)
    {
        for (int radius = 0; radius <= 8; radius++)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dy = -radius; dy <= radius; dy++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != radius) { continue; }
                    int x = anchor.X + dx, y = anchor.Y + dy;
                    foreach (int z in new[] { anchor.Z, map.GetAverageZ(x, y) })
                    {
                        var point = new Point3D(x, y, z);
                        if (!used.Contains((map, point)) && map.CanFit(point, 16, false, true, true)) { return point; }
                    }
                }
            }
        }
        throw new InvalidOperationException($"No civic office floor near {anchor} on {map}.");
    }
    public static RecognitionHall Hall(string id) => Array.Find(Halls, hall => hall.Id == id);
    public static CivicRegistrar Registrar(string id) => Profiles.Office(id) is { } office
        ? World.FindMobile(office.Registrar) as CivicRegistrar : null;
    public static PublicRecognitionWardrobe PublicWardrobe(string id) => Profiles.Office(id) is { } office
        ? World.FindItem(office.Wardrobe) as PublicRecognitionWardrobe : null;
    public static bool AtOffice(PlayerMobile player, string id)
    {
        if (Hall(id) is not { } hall || player.Map?.Name != hall.Map) { return false; }
        var registrar = Registrar(id);
        var wardrobe = PublicWardrobe(id);
        return registrar is { Deleted: false, Alive: true, CantWalk: true } && registrar.Site == id && registrar.Map == player.Map
                && Math.Abs(registrar.X - hall.X) <= 8 && Math.Abs(registrar.Y - hall.Y) <= 8
                && player.InRange(registrar, 3) && Math.Abs(player.Z - registrar.Z) < 16 && player.InLOS(registrar)
            || wardrobe is { Deleted: false, Parent: null, Movable: false } && wardrobe.Site == id && wardrobe.Map == player.Map
                && Math.Abs(wardrobe.X - hall.X) <= 18 && Math.Abs(wardrobe.Y - hall.Y) <= 18
                && player.InRange(wardrobe, 2) && Math.Abs(player.Z - wardrobe.Z) < 16 && player.InLOS(wardrobe);
    }
    public static string NearbyOffice(PlayerMobile player)
    {
        foreach (var hall in Halls) { if (AtOffice(player, hall.Id)) { return hall.Id; } }
        return null;
    }
    private static void Invite(PlayerMobile player)
    {
        RecognitionHall nearest = null;
        long distance = long.MaxValue;
        var map = player.Map == Map.Internal ? player.LogoutMap : player.Map;
        var location = player.Map == Map.Internal ? player.LogoutLocation : player.Location;
        foreach (var hall in Halls)
        {
            if (hall.Map != map?.Name) { continue; }
            long dx = location.X - hall.X, dy = location.Y - hall.Y;
            long candidate = dx * dx + dy * dy;
            if (candidate < distance) { nearest = hall; distance = candidate; }
        }
        nearest ??= Halls[0];
        player.SendMessage($"You have earned an invitation to recognition. Visit any civic town-hall office. Nearest: {nearest.Town}, {nearest.Map} ({nearest.X}, {nearest.Y}). Use [Recognition for details.");
    }
}
