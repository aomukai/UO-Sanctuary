// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Mobiles;

namespace Sanctuary;

public sealed record DropWeight(string Code, int Weight);
public sealed record DropPool(string Name, DropWeight[] Entries);
public sealed record DropVariant(string Type, string Name);
public sealed record DropBase(string Code, int QualityLevel, GearTier Tier, bool ClassSpecific, bool AlwaysMagic,
    bool Rare, DropVariant[] Variants, string ExcludedReason);
public sealed record DropChance(int Value, int Divisor, int Minimum);
public sealed record DropRatio(bool Uber, bool ClassSpecific, DropChance Unique, DropChance Set, DropChance Rare, DropChance Magic);
public sealed record DropExclusion(string Item, string Reason);
public sealed record DropCatalog(string SourceCommit, string Difficulty, int Players, int MaxDrops, string SetUniquePolicy,
    string WhiteQuality, DropRatio[] Ratios, DropPool[] Pools, DropBase[] Bases, DropExclusion[] ExcludedLeaves);
public enum DropQuality { Normal, Magic, Rare, Set, Unique }

public static class GearDrops
{
    private static readonly Dictionary<string, DropPool> _pools = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, DropBase> _bases = new(StringComparer.Ordinal);
    private static readonly Dictionary<(bool Uber, bool ClassSpecific), DropRatio> _ratios = new();
    public static IReadOnlyCollection<DropPool> Pools => _pools.Values;
    public static IReadOnlyCollection<DropBase> Bases => _bases.Values;
    public static void Load()
    {
        _pools.Clear(); _bases.Clear(); _ratios.Clear();
        foreach (var difficulty in Enum.GetValues<MonsterDifficulty>()) { Load(difficulty); }
    }
    private static void Load(MonsterDifficulty difficulty)
    {
        string suffix = difficulty == MonsterDifficulty.Normal ? "" : "-" + difficulty.ToString().ToLowerInvariant();
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        options.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
        var catalog = JsonSerializer.Deserialize<DropCatalog>(File.ReadAllText(Path.Join(Core.BaseDirectory,
            "Data", "Sanctuary", "Balance", "gear-drops" + suffix + ".json")), options);
        if (catalog is not { Players: 1, MaxDrops: 1, SetUniquePolicy: "Empty", WhiteQuality: "Normal" }
            || catalog.Difficulty != difficulty.ToString() || catalog.SourceCommit != "45112569deb9384738ccafe5c24ebbb71f41c7c9" || catalog.Ratios.Length != 4)
        { throw new InvalidDataException("Unsupported ordinary gear-drop policy."); }
        foreach (var row in catalog.Ratios)
        {
            foreach (var chance in new[] { row.Unique, row.Set, row.Rare, row.Magic })
            {
                if (chance is not { Value: > 0 and <= 10000, Divisor: > 0 and <= 1000, Minimum: > 0 and <= 1000000 })
                { throw new InvalidDataException("Invalid source item ratio."); }
            }
            if (_ratios.TryGetValue((row.Uber, row.ClassSpecific), out var existing) && existing != row)
            { throw new InvalidDataException("Inconsistent shared quality ratio."); }
            _ratios[(row.Uber, row.ClassSpecific)] = row;
        }
        foreach (var row in catalog.Bases)
        {
            if (string.IsNullOrWhiteSpace(row.Code) || row.QualityLevel is < 0 or > 99 || !Enum.IsDefined(row.Tier)
                || (row.Variants.Length == 0) != (row.ExcludedReason != null))
            { throw new InvalidDataException("Invalid drop base/exclusion."); }
            if (row.Variants.Length > 0)
            {
                var basis = EquipmentBalance.Base(row.Code);
                if (basis.QualityLevel != row.QualityLevel || basis.Tier != row.Tier.ToString()
                    || EquipmentBalance.Types[basis.Type].Rare != row.Rare || EquipmentBalance.Types[basis.Type].AlwaysMagic != row.AlwaysMagic)
                { throw new InvalidDataException("Drop base differs from saved gear catalogue."); }
                var seen = new HashSet<string>();
                foreach (var variant in row.Variants)
                {
                    var type = EquipmentCrafting.Resolve(variant.Type);
                    if (!seen.Add(variant.Type) || string.IsNullOrWhiteSpace(variant.Name)
                        || !EquipmentBalance.TryGet(type, out var definition) || !definition.NormalDrop || !definition.AllowsAffixes
                        || !EquipmentCrafting.Identity(type, row.Tier, row.Code, definition.BaseCode))
                    { throw new InvalidDataException("Invalid UO drop variant."); }
                }
            }
            if (_bases.TryGetValue(row.Code, out var existing) && JsonSerializer.Serialize(existing) != JsonSerializer.Serialize(row))
            { throw new InvalidDataException("Inconsistent shared drop base."); }
            _bases[row.Code] = row;
        }
        foreach (var row in catalog.Pools)
        {
            if (string.IsNullOrWhiteSpace(row.Name)) { throw new InvalidDataException("Unnamed drop pool."); }
            int total = 0; var seen = new HashSet<string>();
            foreach (var entry in row.Entries)
            {
                if (entry.Weight is < 1 or > 1000 || !_bases.ContainsKey(entry.Code) || !seen.Add(entry.Code))
                { throw new InvalidDataException("Invalid weighted drop base."); }
                total = checked(total + entry.Weight);
            }
            if (_pools.TryGetValue(row.Name, out var existing) && JsonSerializer.Serialize(existing) != JsonSerializer.Serialize(row))
            { throw new InvalidDataException("Inconsistent shared drop pool."); }
            _pools[row.Name] = row;
        }
        var exclusions = new HashSet<string>();
        foreach (var row in catalog.ExcludedLeaves)
        {
            if (string.IsNullOrWhiteSpace(row.Reason) || !exclusions.Add(row.Item))
            { throw new InvalidDataException("Invalid unsupported treasure leaf."); }
        }
        foreach (string leaf in CreatureGold.LeavesFor(difficulty))
        {
            if (leaf != "gld" && !_pools.ContainsKey(leaf) && !(_bases.TryGetValue(leaf, out var basis) && basis.Variants.Length > 0)
                && !exclusions.Contains(leaf)) { throw new InvalidDataException("Unaccounted treasure leaf: " + leaf); }
        }
    }
    public static int MagicFind(PlayerMobile player) => Math.Max(0,
        checked(ClassGear.Bonus(player, "mag%") + ClassGear.Material(player, "magic-find") + MapSurveys.MagicFind(player)));
    internal static DropBase Base(string code) => _bases.GetValueOrDefault(code);
    internal static DropBase SelectBase(string leaf, Random random)
    {
        if (leaf == null) { return null; }
        if (!_pools.TryGetValue(leaf, out var pool)) { return _bases.GetValueOrDefault(leaf); }
        int total = 0;
        foreach (var entry in pool.Entries) { total += entry.Weight; }
        if (total == 0) { return null; }
        int choice = random.Next(total);
        foreach (var entry in pool.Entries)
        { choice -= entry.Weight; if (choice < 0) { return _bases[entry.Code]; } }
        throw new InvalidDataException("Weighted drop selection escaped its pool.");
    }
    // D2's quality check order, fixed-point denominator and integer truncation.
    // The source applies diminishing returns above 10 MF; Magic stays linear.
    internal static int EffectiveFind(int find, DropQuality quality)
    {
        if (find < 0) { throw new ArgumentOutOfRangeException(nameof(find)); }
        if (find <= 10 || quality == DropQuality.Magic) { return find; }
        int factor = quality switch { DropQuality.Unique => 250, DropQuality.Set => 500, DropQuality.Rare => 600, _ => 0 };
        return factor == 0 ? 0 : (int)((long)factor * find / (find + (long)factor));
    }
    internal static int Denominator(DropBase basis, int level, int find, DropQuality quality, int factor = 0)
    {
        if (factor is < 0 or > 1024) { throw new ArgumentOutOfRangeException(nameof(factor)); }
        if (level is < 1 or > 99 || find < 0) { throw new ArgumentOutOfRangeException(); }
        var ratio = _ratios[(basis.Tier != GearTier.Normal, basis.ClassSpecific)];
        var chance = quality switch
        {
            DropQuality.Unique => ratio.Unique, DropQuality.Set => ratio.Set,
            DropQuality.Rare => ratio.Rare, DropQuality.Magic => ratio.Magic,
            _ => throw new ArgumentOutOfRangeException(nameof(quality))
        };
        long value = (chance.Value - (level - basis.QualityLevel) / chance.Divisor) * 128L;
        if (find != 0) { value = value * 100 / (100L + EffectiveFind(find, quality)); }
        value = Math.Max(chance.Minimum, value);
        return (int)(value - value * factor / 1024);
    }
    internal static DropQuality Quality(DropBase basis, int level, int find, Random random, int[] factors = null)
    {
        if (QualityPass(basis, level, find, DropQuality.Unique, random, factors?[0] ?? 0)) { return DropQuality.Unique; }
        if (QualityPass(basis, level, find, DropQuality.Set, random, factors?[1] ?? 0)) { return DropQuality.Set; }
        if (basis.Rare && QualityPass(basis, level, find, DropQuality.Rare, random, factors?[2] ?? 0)) { return DropQuality.Rare; }
        if (basis.AlwaysMagic || QualityPass(basis, level, find, DropQuality.Magic, random, factors?[3] ?? 0)) { return DropQuality.Magic; }
        return DropQuality.Normal;
    }
    private static bool QualityPass(DropBase basis, int level, int find, DropQuality quality, Random random, int factor)
    {
        int denominator = Denominator(basis, level, find, quality, factor);
        return denominator <= 0 || random.Next(denominator) < 128;
    }
    internal static Item Generate(DropBase basis, int level, int find, Random random, int[] factors = null)
    {
        if (basis == null || basis.Variants.Length == 0) { return null; }
        // Pick the UO appearance before quality so MF cannot select another base.
        var variant = basis.Variants[random.Next(basis.Variants.Length)];
        var quality = Quality(basis, level, find, random, factors);
        if (quality is DropQuality.Set or DropQuality.Unique) { return null; }
        var rarity = quality switch { DropQuality.Rare => GearRarity.Rare, DropQuality.Magic => GearRarity.Magic, _ => GearRarity.Normal };
        var item = ItemOrigins.Create(ItemOrigin.Drop, () => EquipmentCrafting.Resolve(variant.Type).CreateInstance<Item>());
        try
        {
            ClassGear.Profiles.FromMonster(item, level, rarity, basis.Code, basis.Tier, random);
            item.Name = $"{basis.Tier} {variant.Name}";
            return item;
        }
        catch { item.Delete(); throw; }
    }
    internal static void Award(RewardLife life, Random random, Type source = null)
    {
        // Gold and gear share one terminal treasure outcome, not independent rolls.
        string leaf = CreatureGold.Select(source ?? life.Creature.GetType(), random, life.Difficulty);
        if (leaf == "gld") { CreatureGold.Award(life, random); return; }
        var item = AlchemyRestoratives.Drop(leaf) ?? Generate(SelectBase(leaf, random), life.Level, life.MagicFind, random);
        if (item != null) { life.Corpse.DropItem(item); }
    }
}
