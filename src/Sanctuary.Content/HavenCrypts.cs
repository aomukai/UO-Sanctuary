// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using ModernUO.Serialization;
using Server;
using Server.Commands;
using Server.Items;
using Server.Json;
using Server.Mobiles;

namespace Sanctuary;

public static class HavenCrypts
{
    internal static readonly Point3D GraveyardLanding = new(3599, 2451, 16);
    private static readonly Point2D _digCenter = new(3599, 2452);
    internal const string RopeName = "a rope back to the graveyard";
    private const int Footprint = 56;
    private const int LegacyFootprint = 32;
    private const int DigRange = 8;
    private static Crypt[] _crypts = [];

    internal sealed record Crypt(string File, Point3D Origin, Point3D Entry, Point3D ArrivalStart,
        Point3D ArrivalEnd, Point3D[] Spawns, Point3D[] Chests, PreviousArrival PreviousArrival = null)
    {
        internal Rectangle2D Bounds => new(Origin.X, Origin.Y, Footprint, Footprint);
        internal Rectangle2D Arrival => new(ArrivalStart.X, ArrivalStart.Y,
            ArrivalEnd.X - ArrivalStart.X, ArrivalEnd.Y - ArrivalStart.Y);
    }

    internal sealed record PreviousArrival(string File, Point3D Entry, Point3D Start, Point3D End)
    {
        internal Rectangle2D Bounds => new(Start.X, Start.Y, End.X - Start.X, End.Y - Start.Y);
    }

    public static void Configure() => EventSink.ServerStarted += Install;

    private static void Install()
    {
        string directory = Path.Join(Core.BaseDirectory, "Data", "Sanctuary", "HavenCrypts");
        var options = new JsonSerializerOptions(JsonConfig.DefaultOptions) { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        _crypts = JsonConfig.Deserialize<Crypt[]>(Path.Join(directory, "crypts.json"), options);
        string legacyDirectory = Path.Join(directory, "legacy");
        var legacy = JsonConfig.Deserialize<Crypt[]>(Path.Join(legacyDirectory, "crypts.json"), options);
        if (_crypts is not { Length: 4 } || legacy is not { Length: 4 })
            throw new InvalidDataException("Expected four fixed Haven crypts.");
        var map = Map.Trammel;
        for (int i = 0; i < _crypts.Length; i++)
        {
            var crypt = _crypts[i];
            if (crypt is not { Spawns.Length: 9, Chests.Length: 3 } || !crypt.Arrival.Contains(crypt.Entry))
                throw new InvalidDataException($"Invalid Haven crypt placements in {crypt.File}.");

            foreach (var decoration in DecorationList.ReadAll(Path.Join(directory, crypt.File)))
                decoration.Generate([map]);

            UpgradeArrival(map, directory, crypt);
            NewHavenPopulation.PlaceRope(map, crypt.Entry, GraveyardLanding, RopeName);
            UpgradeLegacy(map, legacyDirectory, legacy[i], crypt);
            foreach (var point in crypt.Spawns)
            {
                if (SpawnerAt(map, point) is {} existing)
                {
                    if (existing.Running)
                        existing.Running = false;
                    continue;
                }
                var spawner = new HavenGhost { Running = false };
                spawner.MoveToWorld(point, map);
                spawner.Respawn();
            }
        }
    }

    private static void UpgradeArrival(Map map, string directory, Crypt crypt)
    {
        if (crypt.PreviousArrival is not {} previous)
            return;

        var retired = FindRetiredDecorations(map, Path.Join(directory, previous.File),
            previous.Entry == crypt.Entry ? null : previous.Entry);
        if (retired.Count == 0)
            return;

        var arrival = crypt.Arrival;
        var previousBounds = previous.Bounds;
        bool Removed(Point3D point) => previousBounds.Contains(point) && !arrival.Contains(point)
            && Math.Abs(point.Z - crypt.Entry.Z) < 16;

        foreach (var mobile in World.Mobiles.Values.ToArray())
        {
            if (mobile.Map == map && Removed(mobile.Location))
                mobile.MoveToWorld(crypt.Entry, map);
            else if (mobile is PlayerMobile { Map: var currentMap } player && currentMap == Map.Internal
                     && player.LogoutMap == map && Removed(player.LogoutLocation))
                player.LogoutLocation = crypt.Entry;
        }

        var looseItems = new List<Item>();
        foreach (var item in map.GetItemsInBounds(previousBounds))
            if ((item.Movable || item is Corpse) && Removed(item.Location))
                looseItems.Add(item);
        foreach (var item in looseItems)
            item.MoveToWorld(item is Corpse { Owner: PlayerMobile } ? GraveyardLanding : crypt.Entry, map);

        foreach (var item in retired)
            item.Delete();
    }

    private static void UpgradeLegacy(Map map, string directory, Crypt old, Crypt crypt)
    {
        // The new footprints sit beside the original ones. Transfer saved
        // encounters and chests before removing the original generated pieces.
        for (int i = 0; i < old.Spawns.Length; i++)
        {
            if (SpawnerAt(map, old.Spawns[i]) is not {} spawner)
                continue;

            var point = crypt.Spawns[i];
            if (SpawnerAt(map, point) != null)
            {
                spawner.Delete();
                continue;
            }
            spawner.Running = false;
            spawner.MoveToWorld(point, map);
            spawner.BringToHome();
            foreach (var monster in spawner.Spawned.Keys.OfType<HavenEncounter>())
            {
                monster.StopCombatActions();
                monster.Home = point;
                monster.HomeMap = map;
                monster.Combatant = null;
                monster.FocusMob = null;
            }
        }
        for (int i = 0; i < old.Chests.Length; i++)
        {
            var chest = ChestAt(map, old.Chests[i]);
            chest?.MoveToWorld(crypt.Chests[i], map);
        }

        var bounds = new Rectangle2D(old.Origin.X, old.Origin.Y, LegacyFootprint, LegacyFootprint);
        bool Inside(Point3D point) => bounds.Contains(point) && Math.Abs(point.Z - old.Entry.Z) < 16;
        foreach (var player in World.Mobiles.Values.OfType<PlayerMobile>())
        {
            var landing = player.Alive ? crypt.Entry : GraveyardLanding;
            if (player.Map == map && Inside(player.Location))
            {
                BaseCreature.TeleportPets(player, landing, map);
                player.MoveToWorld(landing, map);
            }
            else if (player.Map == Map.Internal && player.LogoutMap == map && Inside(player.LogoutLocation))
                player.LogoutLocation = landing;
        }

        var corpses = new List<Corpse>();
        foreach (var corpse in map.GetItemsInBounds<Corpse>(bounds))
            if (corpse.Owner is PlayerMobile && Inside(corpse.Location))
                corpses.Add(corpse);
        foreach (var corpse in corpses)
            corpse.MoveToWorld(GraveyardLanding, map);

        foreach (var item in FindRetiredDecorations(map, Path.Join(directory, old.File), old.Entry))
            item.Delete();
    }

    private static List<Item> FindRetiredDecorations(Map map, string file, Point3D? ropeLocation)
    {
        var retired = new List<Item>();
        if (ropeLocation is {} entry)
            foreach (var rope in map.GetItemsAt<InteractionTeleporter>(entry))
                if (rope.Name == RopeName && rope.PointDest == GraveyardLanding && rope.Z == entry.Z + 12)
                    retired.Add(rope);

        foreach (var decoration in DecorationList.ReadAll(file))
            decoration.Generate([map], (template, point) =>
            {
                foreach (var item in map.GetItemsAt<Static>(point))
                    if (item.GetType() == typeof(Static) && !item.Movable && item.ItemID == template.ItemID
                        && item.Z == point.Z && item.Hue == template.Hue)
                        retired.Add(item);
                return false;
            });
        return retired;
    }

    private static HavenGhost SpawnerAt(Map map, Point3D point)
    {
        foreach (var spawner in map.GetItemsAt<HavenGhost>(point))
            if (spawner.Location == point)
                return spawner;
        return null;
    }

    internal static bool IsArrivalRoom(Map map, Point3D point)
    {
        if (map != Map.Trammel || point.X < 7024 || Math.Abs(point.Z) >= 16)
            return false;
        foreach (var crypt in _crypts)
            if (crypt.Arrival.Contains(point))
                return true;
        return false;
    }

    internal static bool IsCryptArea(Map map, Point3D point)
    {
        if (map != Map.Trammel || point.X < 7024 || Math.Abs(point.Z) >= 16)
            return false;
        foreach (var crypt in _crypts)
            if (crypt.Bounds.Contains(point))
                return true;
        return false;
    }

    internal static void ReturnOnDeath(PlayerMobile player, Container corpse)
    {
        if (corpse is not Corpse { Deleted: false, Parent: null } body || body.Owner != player
            || !IsCryptArea(body.Map, body.Location))
            return;

        body.MoveToWorld(GraveyardLanding, Map.Trammel);
        // Let native death finish before moving the ghost, including any young-player transport.
        Timer.StartTimer(TimeSpan.Zero, () =>
        {
            if (!player.Deleted && !player.Alive && player.Corpse == body)
                player.MoveToWorld(GraveyardLanding, Map.Trammel);
        });
    }

    internal static bool TryDig(PlayerMobile player)
    {
        var map = Map.Trammel;
        if (!player.Alive || player.Map != map || !player.InRange(_digCenter, DigRange)
            || Math.Abs(player.Z - GraveyardLanding.Z) > 6)
        {
            player.SendMessage("Use the shovel in the New Haven graveyard.");
            return false;
        }
        if (player.Frozen || player.Paralyzed || player.Spell != null)
        {
            player.SendMessage("Thou canst not dig right now.");
            return false;
        }

        var choices = (Crypt[])_crypts.Clone();
        Random.Shared.Shuffle(choices);
        foreach (var crypt in choices)
        {
            if (!TryLanding(map, crypt, out var landing))
                continue;
            if (!HasVisitors(map, crypt))
                Refresh(map, crypt, Random.Shared);

            ClassCombat.Stop(player);
            player.RevealingAction();
            BaseCreature.TeleportPets(player, landing, map);
            player.MoveToWorld(landing, map);
            AlliedOverlap.DisperseFollowers(player);
            return player.Map == map && player.Location == landing;
        }

        player.SendMessage("The way into the crypts is blocked.");
        return false;
    }

    private static bool TryLanding(Map map, Crypt crypt, out Point3D landing)
    {
        var entry = crypt.Entry;
        if (map.CanFit(entry, 16))
        {
            landing = entry;
            return true;
        }
        for (int x = -2; x <= 2; ++x)
        for (int y = -2; y <= 2; ++y)
        {
            var point = new Point3D(entry.X + x, entry.Y + y, entry.Z);
            if (crypt.Arrival.Contains(point) && map.CanFit(point, 16))
            {
                landing = point;
                return true;
            }
        }
        landing = default;
        return false;
    }

    private static bool HasVisitors(Map map, Crypt crypt)
    {
        foreach (var mobile in map.GetMobilesInBounds(crypt.Bounds))
            if (mobile.Player && Math.Abs(mobile.Z - crypt.Entry.Z) < 16)
                return true;
        return false;
    }

    private static void Refresh(Map map, Crypt crypt, Random random)
    {
        foreach (var point in crypt.Spawns)
            SpawnerAt(map, point)?.Respawn();
        foreach (var point in crypt.Chests)
        {
            ChestAt(map, point)?.Delete();
            if (random.Next(2) == 0)
                new CryptGoldChest(random.Next(50, 101)).MoveToWorld(point, map);
        }
    }

    private static CryptGoldChest ChestAt(Map map, Point3D point)
    {
        foreach (var chest in map.GetItemsAt<CryptGoldChest>(point))
            if (chest.Location == point)
                return chest;
        return null;
    }
}

[SerializationGenerator(0)]
public partial class HavenCryptShovel : Item
{
    public override string DefaultName => "a graveyard shovel";
    public override double DefaultWeight => 5.0;

    [Constructible]
    public HavenCryptShovel() : base(0xF39) { }

    public override void OnDoubleClick(Mobile from)
    {
        if (from is not PlayerMobile player)
            return;
        if (player.Backpack is not { } pack || !IsChildOf(pack))
        {
            player.SendMessage("The shovel must be in thy backpack.");
            return;
        }
        if (HavenCrypts.TryDig(player))
            Delete();
    }
}
