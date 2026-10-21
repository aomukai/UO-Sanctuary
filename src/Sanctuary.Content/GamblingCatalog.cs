// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Server;
using Server.Items;
using Server.Mobiles;

namespace Sanctuary;

public sealed record GamblingCostRow(string Code, int Level, int Cost, int GambleCost, string Normal,
    string Exceptional, string Elite, int Stack);
public sealed record TavernSite(string Id, string Map, int X, int Y, int Z, string Source);
public sealed record GambleVariant(string Type, string Name, int Graphic);
public sealed record GambleBase(string Code, GambleVariant[] Variants);
public sealed record GambleOffer(string Code, GambleVariant Variant, int ItemLevel, int Price);
public enum GambleQuality { Magic, Rare, Set, Unique }

public static partial class Gambling
{
    private static readonly Dictionary<string, GamblingCostRow> _costs = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, GambleBase> _bases = new(StringComparer.Ordinal);
    public static IReadOnlyCollection<GambleBase> Bases => _bases.Values;
    public static TavernSite[] Sites { get; private set; } = [];
    public static void Load()
    {
        _costs.Clear(); _bases.Clear();
        foreach (var row in BalanceCatalog.Read<GamblingCostRow>("gambling-costs"))
        {
            if (string.IsNullOrWhiteSpace(row.Code) || row.Level is < 0 or > 99 || row.Cost < 0
                || row.GambleCost < 0 || row.Stack < 1 || !_costs.TryAdd(row.Code, row))
            { throw new InvalidDataException("Invalid gambling source cost."); }
        }
        foreach (var group in EquipmentBalance.All.Where(g => g.AllowsAffixes && g.BaseCode != null
            && (g.NormalDrop || g.Slot == GearSlot.Belt && g.ExcludedReason == null)).GroupBy(g => g.BaseCode))
        {
            var variants = new List<GambleVariant>();
            if (!_costs.TryGetValue(group.Key, out var cost)) { throw new InvalidDataException("Missing gamble base cost."); }
            foreach (var gear in group)
            {
                string name = GearDrops.Bases.FirstOrDefault(b => b.Code == group.Key)?.Variants
                    .FirstOrDefault(v => v.Type == gear.Type || v.Type.EndsWith("." + gear.Type, StringComparison.Ordinal))?.Name ?? gear.Type;
                variants.Add(new(gear.Type, name, 0));
            }
            _bases.Add(group.Key, new(group.Key, variants.ToArray()));
        }
        if (!_bases.ContainsKey("rin") || !_bases.ContainsKey("amu")) { throw new InvalidDataException("Missing gamble jewellery."); }
        foreach (var basis in _bases.Values)
        {
            var row = _costs[basis.Code];
            foreach (string code in new[] { basis.Code, row.Exceptional, row.Elite })
            {
                if (code is null or "" or "0") { continue; }
                if (!_costs.ContainsKey(code)) { throw new InvalidDataException("Missing gamble tier cost."); }
                EquipmentBalance.Base(code);
            }
        }
        Sites = BalanceCatalog.Read<TavernSite>("tavern-gamblers");
        var ids = new HashSet<string>();
        foreach (var site in Sites)
        {
            if (string.IsNullOrWhiteSpace(site.Id) || !ids.Add(site.Id) || string.IsNullOrWhiteSpace(site.Map)
                || site.X < 0 || site.Y < 0)
            { throw new InvalidDataException("Invalid tavern site: " + site.Id); }
        }
    }
    public static int ItemLevel(int level, Random random) => Math.Clamp(level + random.Next(10) - 5, 5, 99);
    public static int Price(string code, int level)
    {
        if (level is < 1 or > 99 || !_bases.ContainsKey(code)) { throw new ArgumentOutOfRangeException(); }
        var row = _costs[code];
        if (code is "rin" or "amu") { return row.GambleCost; }
        var exceptional = row.Exceptional != null && _costs.TryGetValue(row.Exceptional, out var v) ? v : null;
        var elite = row.Elite != null && _costs.TryGetValue(row.Elite, out var e) ? e : null;
        int vw = exceptional == null ? 0 : Math.Max(0, 50 * (level - exceptional.Level) + 1);
        int ew = elite == null ? 0 : Math.Max(0, 25 * (level - elite.Level) + 1);
        long weighted = (long)vw * (exceptional?.Cost ?? 0) + (long)ew * (elite?.Cost ?? 0)
            + (long)row.Stack * row.Cost * (10000 - vw - ew);
        int clamped = Math.Max(5, level);
        return checked((int)((((2 * clamped + 1) / 3 + 20) * (weighted / 10000
            + 250L * (clamped + Math.Max(row.Level - 45, 0) - row.Level / 2) / 3)) / 15));
    }
    public static GambleQuality Quality(Random random)
    {
        int n = random.Next(100000);
        return n < 50 ? GambleQuality.Unique : n < 150 ? GambleQuality.Set : n < 10150 ? GambleQuality.Rare : GambleQuality.Magic;
    }
    // The documented expansion gamble probabilities; checks stay independent of Magic Find.
    public static string Upgrade(string code, int itemLevel, Random random)
    {
        var row = _costs[code];
        if (row.Exceptional is { } v && _costs.TryGetValue(v, out var exceptional)
            && random.Next(10000) < Math.Clamp(90 * (itemLevel - exceptional.Level) + 100, 0, 10000)) { return v; }
        if (row.Elite is { } e && _costs.TryGetValue(e, out var elite)
            && random.Next(10000) < Math.Clamp(33 * (itemLevel - elite.Level) + 100, 0, 10000)) { return e; }
        return code;
    }
    public static GambleOffer[] Stock(int level, Random random)
    {
        var output = new GambleOffer[14];
        for (int i = 0; i < output.Length; i++)
        {
            int ilvl = ItemLevel(level, random);
            var pool = _bases.Values.Where(b => EquipmentBalance.Base(b.Code).QualityLevel <= ilvl).ToArray();
            var basis = i == 0 ? _bases["rin"] : i == 1 ? _bases["amu"] : pool[random.Next(pool.Length)];
            var variant = basis.Variants[random.Next(basis.Variants.Length)];
            output[i] = new(basis.Code, variant, ilvl, Price(basis.Code, level));
        }
        return output;
    }
    internal static Item Generate(GambleOffer offer, Random random)
    {
        string code = Upgrade(offer.Code, offer.ItemLevel, random);
        var quality = Quality(random);
        if (quality is GambleQuality.Set or GambleQuality.Unique)
        { return NamedGear.Generate(code, quality == GambleQuality.Unique ? GearRarity.Unique : GearRarity.Set, offer.ItemLevel, random, true, offer.Variant.Type); }
        var basis = EquipmentBalance.Base(code);
        var tier = Enum.Parse<GearTier>(basis.Tier);
        var item = ItemOrigins.Create(ItemOrigin.Drop, () => EquipmentCrafting.Resolve(offer.Variant.Type).CreateInstance<Item>());
        try
        {
            item.LootType = LootType.Regular;
            var state = ClassGear.Profiles.FromMonster(item, offer.ItemLevel,
                quality == GambleQuality.Rare ? GearRarity.Rare : GearRarity.Magic, code, tier, random);
            ClassGear.Profiles.Store(item, state with { Identified = true });
            item.Name = $"{tier} {offer.Variant.Name}";
            return item;
        }
        catch { item.Delete(); throw; }
    }
}
