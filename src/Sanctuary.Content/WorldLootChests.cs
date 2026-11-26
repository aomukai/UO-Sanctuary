// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using Server;
using Server.Engines.Spawners;
using Server.Items;
using Server.Mobiles;
using Server.Multis;
using Server.Regions;

namespace Sanctuary;

public sealed class WorldLootChests : GenericPersistence
{
    private sealed record Cycle(DateTime ResetAfter, Serial[] Loot);
    private readonly Dictionary<Serial, Cycle> _opened = new();
    private static WorldLootChests _profiles;

    private static readonly HashSet<Type> _worldTypes =
    [
        typeof(WoodenTreasureChest), typeof(MetalTreasureChest), typeof(MetalGoldenTreasureChest),
        typeof(TreasureChestLevel1), typeof(TreasureChestLevel2), typeof(TreasureChestLevel3), typeof(TreasureChestLevel4)
    ];

    private static readonly HashSet<Type> _fixtureTypes =
    [
        typeof(WoodenChest), typeof(MetalChest), typeof(MetalGoldenChest), typeof(PlainWoodenChest),
        typeof(OrnateWoodenChest), typeof(GildedWoodenChest), typeof(FinishedWoodenChest), typeof(RarewoodChest),
        typeof(FillableWoodenChest), typeof(FillableMetalChest), typeof(FillableMetalGoldenChest)
    ];

    private WorldLootChests() : base("SanctuaryWorldLootChests", 410) { }

    public static void Configure()
    {
        _profiles = new();
        ClassLootHooks.WorldChestLocked = chest => IsWorldChest(chest) ? !_profiles._opened.ContainsKey(chest.Serial) : null;
        ClassLootHooks.OpenWorldChest = _profiles.Open;
    }

    public static void Initialize()
    {
        var interval = TimeSpan.FromSeconds(10);
        Timer.StartTimer(interval, interval, () => _profiles.ResetDue(DateTime.UtcNow));
    }

    internal static bool IsWorldChest(LockableContainer chest)
    {
        if (chest is not { Deleted: false, Movable: false, Parent: null, PlayerConstructed: false,
                IsLockedDown: false, IsSecure: false, QuestItem: false, Map: { } map }
            || map == Map.Internal)
        {
            return false;
        }

        var type = chest.GetType();
        bool worldType = _worldTypes.Contains(type);
        if ((!worldType && (chest.KeyValue != 0 || !_fixtureTypes.Contains(type)))
            || map == Map.Trammel && chest.Location == Celeste.Chest || BaseHouse.FindHouseAt(chest) != null)
        {
            return false;
        }

        var region = Region.Find(chest.Location, map);
        return !region.IsPartOf<ExpeditionRegion>() && (worldType || region.IsPartOf<DungeonRegion>());
    }

    private bool? Open(Mobile from, LockableContainer chest)
    {
        if (!IsWorldChest(chest))
        {
            return null;
        }

        if (from is not PlayerMobile { Alive: true, Deleted: false } player || player.Map != chest.Map
            || !player.InRange(chest.Location, 2) || !player.CanSee(chest) || !player.InLOS(chest)
            || !chest.IsAccessibleTo(player))
        {
            return false;
        }

        if (_opened.ContainsKey(chest.Serial))
        {
            return true;
        }

        int level = NearbyLevel(chest, player);
        if (level == 0)
        {
            player.SendMessage("It's empty.");
            return false;
        }

        WorldChestKey key = null;
        if (!AssassinLockpicking.CanPick(player, chest))
        {
            int tier = Tier(level);
            if (player.Backpack is { } pack)
            {
                foreach (var candidate in pack.FindItemsByType<WorldChestKey>(true))
                {
                    if (candidate.Tier == tier && RuneCrafting.Accessible(player, candidate))
                    {
                        key = candidate;
                        break;
                    }
                }
            }
            if (key == null)
            {
                player.SendMessage("It's locked.");
                return false;
            }
        }

        var random = Random.Shared;
        var gear = RollGear(level, GearDrops.MagicFind(player), random);
        var gold = ItemOrigins.Create(ItemOrigin.Drop, () => new Gold(CreatureGold.Amount(level, 0, random)));
        chest.AddItem(gear);
        chest.AddItem(gold);
        var loot = new Serial[chest.Items.Count];
        for (int i = 0; i < loot.Length; i++)
        {
            loot[i] = chest.Items[i].Serial;
        }
        _opened[chest.Serial] = new Cycle(DateTime.UtcNow.AddMinutes(15), loot);
        chest.Locked = false;
        key?.Consume(1);
        return true;
    }

    internal static int Tier(int level) => Math.Clamp(level / 20 + 1, 1, 5);

    private static Item RollGear(int level, int magicFind, Random random)
    {
        string prefix = random.Next(2) == 0 ? "weap" : "armo";
        int bestLevel = 3;
        string pool = prefix + "3";
        foreach (var candidate in GearDrops.Pools)
        {
            if (candidate.Name.StartsWith(prefix, StringComparison.Ordinal)
                && int.TryParse(candidate.Name.AsSpan(prefix.Length), out int poolLevel)
                && poolLevel <= level && poolLevel > bestLevel)
            {
                bestLevel = poolLevel;
                pool = candidate.Name;
            }
        }

        var basis = GearDrops.SelectBase(pool, random, equipmentOnly: true);
        return GearDrops.Generate(basis, level, magicFind, random, fallbackNamed: true);
    }

    private static bool SameFloor(Point3D first, Point3D second) => Math.Abs(first.Z - second.Z) < 16;

    private static int NearbyLevel(LockableContainer chest, PlayerMobile player)
    {
        var map = chest.Map;
        var origin = chest.Location;
        int bestDistance = int.MaxValue, bestLevel = 0;

        void Consider(Point3D location, int level)
        {
            if (level < 1 || !SameFloor(location, origin) || !player.InRange(location, LootProtocol.SightRange))
            {
                return;
            }

            int distance = Math.Max(Math.Abs(location.X - origin.X), Math.Abs(location.Y - origin.Y));
            if ((distance < bestDistance || distance == bestDistance && level > bestLevel)
                && EncounterProfiles.ClearSight(map, origin, location))
            {
                bestDistance = distance;
                bestLevel = Math.Min(level, 99);
            }
        }

        foreach (var creature in map.GetMobilesInRange<BaseCreature>(origin, LootProtocol.SightRange))
        {
            if (creature.Alive && IsEncounterMonster(creature) && player.CanSee(creature))
            {
                Consider(creature.Location, CreatureProfiles.Level(creature));
            }
        }

        foreach (var corpse in map.GetItemsInRange<Corpse>(origin, LootProtocol.SightRange))
        {
            if (corpse.Parent == null && player.CanSee(corpse))
            {
                Consider(corpse.Location, CorpseLevel(corpse));
            }
        }

        if (bestLevel == 0)
        {
            foreach (var spawner in map.GetItemsInRange<BaseSpawner>(origin, LootProtocol.SightRange))
            {
                if (spawner.Running && spawner.Parent == null)
                {
                    Consider(spawner.Location, WorldChestSpawnLevels.Level(spawner));
                }
            }
        }

        return bestLevel;
    }

    internal static bool IsEncounterMonster(BaseCreature creature) =>
        creature is not BaseVendor and not IClassCompanion
        && !creature.Controlled && !creature.Summoned && !creature.IsAnimatedDead && !creature.NoKillAwards
        && !creature.Blessed && !creature.IsInvulnerable && !creature.InitialInnocent && creature.GetMaster() == null
        && creature.FightMode is not (FightMode.None or FightMode.Aggressor or FightMode.Evil);

    private static int CorpseLevel(Corpse corpse)
    {
        var snapshot = CorpseSkillRules.Profiles.Find(corpse);
        bool eligible = snapshot?.WorldLootEligible
            ?? (snapshot is not { Owned: true } && (corpse.Owner is BaseCreature creature
                ? IsEncounterMonster(creature) : snapshot?.Treasure != null));
        if (!eligible)
        {
            return 0;
        }

        if (CorpseSkillRules.TryMonsterLevel(corpse, out int level))
        {
            return level;
        }

        return corpse.Owner is BaseCreature owner ? CreatureProfiles.Level(owner) : 0;
    }

    private void ResetDue(DateTime now)
    {
        foreach (var (serial, cycle) in _opened)
        {
            if (now < cycle.ResetAfter)
            {
                continue;
            }

            if (World.FindItem(serial) is not LockableContainer chest || !IsWorldChest(chest))
            {
                _opened.Remove(serial);
                continue;
            }

            if (HasNearbyPlayer(chest))
            {
                continue;
            }

            chest.Locked = true;
            _opened.Remove(serial);
            foreach (var itemSerial in cycle.Loot)
            {
                if (World.FindItem(itemSerial) is { } item && item.Parent == chest)
                {
                    item.Delete();
                }
            }
        }
    }

    private static bool HasNearbyPlayer(LockableContainer chest)
    {
        foreach (var player in chest.Map.GetMobilesInRange<PlayerMobile>(chest.Location, LootProtocol.SightRange))
        {
            if (player.NetState?.Running == true && SameFloor(player.Location, chest.Location))
            {
                return true;
            }
        }
        return false;
    }

    public override void Serialize(IGenericWriter writer)
    {
        writer.Write(0);
        writer.Write(_opened.Count);
        foreach (var (serial, cycle) in _opened)
        {
            writer.Write(serial);
            writer.Write(cycle.ResetAfter.Ticks);
            writer.Write(cycle.Loot.Length);
            foreach (var item in cycle.Loot)
            {
                writer.Write(item);
            }
        }
    }

    public override void Deserialize(IGenericReader reader)
    {
        _ = reader.ReadInt();
        int count = reader.ReadInt();
        for (int i = 0; i < count; i++)
        {
            var serial = reader.ReadSerial();
            var resetAfter = new DateTime(reader.ReadLong(), DateTimeKind.Utc);
            var loot = new Serial[reader.ReadInt()];
            for (int j = 0; j < loot.Length; j++)
            {
                loot[j] = reader.ReadSerial();
            }
            _opened.Add(serial, new Cycle(resetAfter, loot));
        }
    }
}
