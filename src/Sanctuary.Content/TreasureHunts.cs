// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Regions;

namespace Sanctuary;

public sealed record TreasureCohort(string Type, MonsterDifficulty Difficulty);
public sealed record TreasureTier(int Tier, string Name, int MinimumLevel, int MaximumLevel, double Cartography,
    string KeyName, double KeySkill, string KeyMaterial, int Gold, int GearRolls, int ItemLevel, int GemQuality,
    int Resources, int RecipePercent, int MonsterMinimum, int MonsterMaximum, MonsterDifficulty Difficulty, TreasureCohort[] Creatures);
public sealed record TreasureSettings(int SchemaVersion, int DropDenominator, int DropTickets, int LifetimeMinutes,
    int SlotSize, int SlotX, int SlotY, int SlotsPerRow, int SlotsPerFacet, int MinimumRooms, int MaximumRooms,
    int CellSize, int PackSize, TreasureTier[] Tiers);
public static class TreasureHunts
{
    private static readonly Dictionary<string, DungeonProfile> _profiles = new(StringComparer.Ordinal);
    internal static DungeonProfile Profile(string id) => id == null ? null : _profiles.GetValueOrDefault(id);
    public static TreasureSettings Settings { get; private set; }
    public static TreasureExpeditions Profiles { get; private set; }
    public static TreasureTier Tier(int tier) => tier is >= 1 and <= 5 ? Settings.Tiers[tier - 1] : throw new ArgumentOutOfRangeException(nameof(tier));
    public static int TierFor(int level) => level is < 1 or > 99 ? 0 : Math.Min(5, (level - 1) / 20 + 1);
    public static void Load()
    {
        var rows = BalanceCatalog.Read<TreasureSettings>("treasure-hunts");
        if (rows.Length != 1) { throw new InvalidDataException("One treasure settings row required."); }
        Settings = rows[0];
        if (Settings is not { SchemaVersion: 1, Tiers.Length: 5, SlotSize: 224, CellSize: 48, SlotsPerRow: 3, SlotsPerFacet: 9 }
            || Settings.SlotX < 5376 || Settings.SlotX + Settings.SlotsPerRow * Settings.SlotSize > 6143
            || Settings.SlotY < 512 || Settings.SlotY + 3 * Settings.SlotSize > 1279
            || Settings.MinimumRooms is < 8 or > 12 || Settings.MaximumRooms < Settings.MinimumRooms || Settings.MaximumRooms > 12
            || Settings.PackSize is < 2 or > 5 || Settings.LifetimeMinutes is < 30 or > 240
            || Settings.DropDenominator != 10000 || Settings.DropTickets is < 0 or > 10000)
        { throw new InvalidDataException("Invalid treasure hunt geometry or probability."); }
        _profiles.Clear();
        for (int i = 1; i <= 5; i++)
        {
            var r = Tier(i);
            if (r.Tier != i || r.MinimumLevel != (i == 1 ? 1 : (i - 1) * 20 + 1) || r.MaximumLevel != (i == 5 ? 99 : i * 20)
                || r.Cartography is < 0 or > 120 || r.KeySkill is < 0 or > 120 || r.Gold is < 0 or > 60000
                || r.GearRolls is < 1 or > 6 || r.ItemLevel is < 1 or > 99 || r.GemQuality != i - 1
                || r.Resources is < 1 or > 1000 || r.RecipePercent is < 0 or > 100 || r.Creatures is not { Length: >= 1 and <= 6 }
                || !typeof(BaseIngot).IsAssignableFrom(EquipmentCrafting.Resolve(r.KeyMaterial)))
            { throw new InvalidDataException("Invalid treasure tier."); }
            if (r.MonsterMinimum < 1 || r.MonsterMaximum > 99 || r.MonsterMaximum < r.MonsterMinimum || !Enum.IsDefined(r.Difficulty))
            { throw new InvalidDataException("Invalid treasure monster levels."); }
            foreach (var template in EncounterProfiles.All)
            {
                if (template.MinimumLevel != 60) { continue; }
                var profile = template with { Id = $"treasure-{i}-{template.Archetype}", MinimumLevel = r.MonsterMinimum, MaximumLevel = r.MonsterMaximum };
                _profiles.Add(profile.Id, profile);
            }
            foreach (var c in r.Creatures)
            {
                var type = typeof(BaseCreature).Assembly.GetType("Server.Mobiles." + c.Type); var policy = type == null ? null : CreatureGold.Policy(type, c.Difficulty);
                if (type == null || c.Difficulty != r.Difficulty || !typeof(BaseCreature).IsAssignableFrom(type) || type.IsAbstract || policy is not { Eligible: true }
                    || type.GetConstructor(Type.EmptyTypes) == null)
                { throw new InvalidDataException("Invalid treasure encounter cohort."); }
            }
        }
    }
    public static void Configure()
    {
        Profiles = new();
        ClassMapHooks.FishingCargo = FishingReward;
        ClassMapHooks.TreasureItem = item => item is TreasureChart;
        ClassMapHooks.TreasureFished = (owner, level, map) => ClassProfessions.Affected(owner)
            ? ItemOrigins.Create(ItemOrigin.Drop, () => new TreasureChart(LegacyTier(level), map)) : null;
        ClassMapHooks.TreasureUsed = (from, map) =>
        {
            if (!ClassProfessions.Affected(from)) { return false; }
            if (from is not PlayerMobile owner || !ClassProfessions.Ready(owner) || !RuneCrafting.Accessible(owner, map)) { return true; }
            if (map.Completed) { owner.SendMessage("This treasure map is already completed."); return true; }
            if (map.ChestMap != Map.Felucca && map.ChestMap != Map.Trammel) { return true; }
            var chart = ItemOrigins.Create(map.Origin, () => new TreasureChart(LegacyTier(map.Level), map.ChestMap, map.ChestLocation, Random.Shared.Next()));
            if (map.Decoder != null) { chart.Decoder = map.Decoder; chart.LootType = map.LootType; }
            var delivery = MarketDelivery.Create(owner, [(map, 1)], [chart]);
            if (delivery == null) { chart.Delete(); return true; }
            delivery.Commit(); chart.OnDoubleClick(owner); return true;
        };
        EventSink.WorldSave += Profiles.Prune;
        EventSink.Connected += owner => Profiles.CheckLogin(owner);
    }
    public static void Initialize() => Timer.StartTimer(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1), Profiles.Tick);
    internal static string DefenseName(int index) => index switch { 0 => "Physical", 1 => "Magic", 2 => "Fire", 3 => "Lightning", 4 => "Cold", 5 => "Poison", _ => "None" };
    internal static (int First, int Second, int High) Defenses(int tier, int seed)
    {
        if (tier < 4) { return (-1, -1, -1); }
        var random = new Random(seed ^ 0x47A281); int first = random.Next(6);
        if (tier == 5) { int second = random.Next(5); if (second >= first) { second++; } return (first, second, -1); }
        var elements = new List<int>(); foreach (int element in new[] { 2, 3, 4 }) { if (element != first) { elements.Add(element); } }
        return (first, -1, elements[random.Next(elements.Count)]);
    }
    internal static string DefenseText(int first, int second, int high) => first < 0 ? "No dungeon-wide immunities"
        : second >= 0 ? $"Immune: {DefenseName(first)} and {DefenseName(second)}"
        : $"Immune: {DefenseName(first)}; high {DefenseName(high)} resistance";
    internal static bool FishingReward(Item item) => item is TreasureChart or MessageInABottle or SpecialFishingNet;
    internal static void DiscardFishing(Corpse corpse)
    {
        using var items = Server.Collections.PooledRefList<Item>.Create();
        foreach (var item in corpse.Items) { if (FishingReward(item)) { items.Add(item); } }
        foreach (var item in items) { item.Delete(); }
    }
    internal static int LegacyTier(int level) => level switch { <= 1 => 1, <= 3 => 2, <= 5 => 3, 6 => 4, _ => 5 };
    internal static void Award(RewardLife life, Random random)
    {
        int tier = TierFor(life.Level);
        if (tier == 0 || life.Corpse.Map != Map.Felucca && life.Corpse.Map != Map.Trammel
            || random.Next(Settings.DropDenominator) >= Settings.DropTickets) { return; }
        life.Corpse.DropItem(ItemOrigins.Create(ItemOrigin.Drop, () => new TreasureChart(tier, life.Corpse.Map)));
    }
    internal static ExpeditionSession Session(Item item) => item is TreasureChart chart ? Profiles.Find(chart.Serial)
        : item is ExpeditionChest chest ? Profiles.Find(chest.ChartSerial) : item is ExpeditionPortal portal ? Profiles.Find(portal.ChartSerial) : null;
    internal static bool CanLand(Mobile owner, Map map, Point3D point)
    {
        var source = owner.Map == null ? null : Region.Find(owner.Location, owner.Map) as ExpeditionRegion;
        var destination = Region.Find(point, map) as ExpeditionRegion;
        return (source == null || source == destination) && (destination == null || destination.Accepts(owner, point));
    }
    internal static bool Unlock(PlayerMobile owner, TreasureSkeletonKey key, ExpeditionChest chest)
    {
        var hunt = Session(chest);
        if (hunt == null || hunt.Owner != owner.Serial || !hunt.Active || hunt.Expires <= DateTime.UtcNow
            || !ClassProfessions.Ready(owner) || !RuneCrafting.Accessible(owner, key) || key.Tier != hunt.Tier
            || owner.Map != chest.Map || !owner.InRange(chest, 2) || !owner.InLOS(chest) || chest.Unlocked)
        { owner.SendMessage("Use a matching key beside your active treasure chest. No key was consumed."); return false; }
        foreach (var serial in hunt.Guardians)
        { if (World.FindMobile(serial) is { Deleted: false, Alive: true }) { owner.SendMessage("Defeat the chest's guarding pack first. No key was consumed."); return false; } }
        chest.Unlocked = true; key.Consume(1); chest.InvalidateProperties();
        owner.SendMessage("The skeleton key unlocks the treasure chest and is consumed."); chest.OnDoubleClick(owner); return true;
    }
    internal static void Fill(ExpeditionChest chest, TreasureTier row, int find, Random random)
    {
        chest.DropItem(ItemOrigins.Create(ItemOrigin.Drop, () => new Gold(row.Gold)));
        for (int i = 0; i < row.GearRolls; i++)
        {
            string prefix = random.Next(2) == 0 ? "weap" : "armo"; DropPool pool = null; int highest = -1;
            foreach (var candidate in GearDrops.Pools)
            {
                if (candidate.Name.StartsWith(prefix, StringComparison.Ordinal) && int.TryParse(candidate.Name.AsSpan(prefix.Length), out int level)
                    && level <= row.ItemLevel && level > highest) { pool = candidate; highest = level; }
            }
            var basis = GearDrops.SelectBase(pool?.Name, random);
            var item = GearDrops.Generate(basis, row.ItemLevel, find, random);
            if (item != null) { chest.DropItem(item); }
        }
        var families = SocketGems.Settings.Families;
        chest.DropItem(ItemOrigins.Create(ItemOrigin.Drop, () => new SocketGem(families[random.Next(families.Length)].Id, (GemQuality)row.GemQuality)));
        var resource = new[] { typeof(IronIngot), typeof(Board), typeof(Leather), typeof(Cloth), typeof(Bone) }[random.Next(5)];
        chest.DropItem(ItemOrigins.Create(ItemOrigin.Drop, () => { var item = resource.CreateInstance<Item>(); item.Amount = row.Resources; return item; }));
        if (random.Next(100) < row.RecipePercent) { chest.DropItem(ItemOrigins.Create(ItemOrigin.Drop, () => RareRecipes.Roll(random))); }
    }
}
