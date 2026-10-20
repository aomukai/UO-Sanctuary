// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Server;
using Server.Items;

namespace Sanctuary;

public enum MouldGrade { Ordinary, Champion, Unique, SuperUnique }
public sealed record MouldTier(int Tier, string Name, int MinimumLevel, int MaximumLevel);
public sealed record MouldStat(string Id, string Label, string Code, string[] Aliases, int Baseline, int Weight,
    string SourceAffix, string AffixName, AffixKind AffixKind, string[] WeightAffixes);
public sealed record MouldSettings(int SchemaVersion, string SourceCommit, int DropDenominator, int OrdinaryTickets,
    int Quantity, bool MagicFindAffects, string Selection, MouldTier[] Tiers, MouldStat[] Stats);

public static class MouldRewards
{
    private static readonly Dictionary<string, MouldStat> _stats = new(StringComparer.Ordinal);
    private static readonly HashSet<string> _percentageCodes = new(StringComparer.Ordinal)
    { "ac%", "dmg%", "att%", "res-all", "res-fire", "res-cold", "res-ltng", "res-pois",
        "swing1", "cast1", "move1", "block", "balance1", "gold%", "mag%" };
    private static bool LegalAliases(MouldStat stat)
    {
        string[] expected = stat.Code switch
        {
            "swing1" => ["swing1", "swing2", "swing3"], "cast1" => ["cast1", "cast2", "cast3"],
            "move1" => ["move1", "move2", "move3"], "balance1" => ["balance1", "balance2", "balance3"],
            _ => [stat.Code]
        };
        if (stat.Aliases == null || stat.Aliases.Length != expected.Length) { return false; }
        for (int i = 0; i < expected.Length; i++) { if (stat.Aliases[i] != expected[i]) { return false; } }
        return true;
    }
    public static MouldSettings Settings { get; private set; }
    public static int TotalWeight { get; private set; }
    public static void Load()
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        options.Converters.Add(new JsonStringEnumConverter());
        Settings = JsonSerializer.Deserialize<MouldSettings>(File.ReadAllText(Path.Join(Core.BaseDirectory,
            "Data", "Sanctuary", "Balance", "moulds.json")), options);
        if (Settings is not { SchemaVersion: 1, SourceCommit: "45112569deb9384738ccafe5c24ebbb71f41c7c9",
                DropDenominator: 10000, Quantity: 1, MagicFindAffects: false, Selection: "SumMappedEquipmentAffixFrequencies" }
            || Settings.OrdinaryTickets is < 0 or > 10000 || Settings.Tiers is not { Length: 8 } || Settings.Stats is not { Length: 15 })
        { throw new InvalidDataException("Invalid ordinary mould rules."); }
        _stats.Clear(); TotalWeight = 0;
        for (int i = 0; i < 8; i++)
        {
            var tier = Settings.Tiers[i];
            if (tier.Tier != i + 1 || tier.MinimumLevel != 20 + i * 10 || tier.MaximumLevel != 29 + i * 10
                || string.IsNullOrWhiteSpace(tier.Name)) { throw new InvalidDataException("Invalid mould level band."); }
        }
        var codes = new HashSet<string>();
        foreach (var stat in Settings.Stats)
        {
            if (string.IsNullOrWhiteSpace(stat.Id) || string.IsNullOrWhiteSpace(stat.Label)
                || !_percentageCodes.Contains(stat.Code) || !codes.Add(stat.Code) || stat.Baseline <= 0 || stat.Weight <= 0
                || !LegalAliases(stat)
                || stat.WeightAffixes is not { Length: > 0 }) { throw new InvalidDataException("Invalid mould percentage family."); }
            var source = AffixBalance.Find(stat.SourceAffix);
            if (source.Name != stat.AffixName || source.Kind != stat.AffixKind || !HasMinimum(source, stat))
            { throw new InvalidDataException("Mould baseline/name differs from source."); }
            int weight = 0; var seen = new HashSet<string>(); bool minimumIncluded = false;
            foreach (string id in stat.WeightAffixes)
            {
                var affix = AffixBalance.Find(id);
                if (!seen.Add(id) || !Mapped(affix) || !HasProperty(affix, stat))
                { throw new InvalidDataException("Invalid source mould frequency."); }
                foreach (var modifier in affix.Modifiers)
                {
                    if (Array.IndexOf(stat.Aliases, modifier.Code) >= 0 && modifier.Mode == ModifierMode.Numeric
                        && string.IsNullOrEmpty(modifier.Parameter) && modifier.Min > 0 && modifier.Min < stat.Baseline)
                    { throw new InvalidDataException("Mould baseline is not the category minimum."); }
                }
                weight = checked(weight + affix.Frequency); minimumIncluded |= id == stat.SourceAffix;
            }
            if (!minimumIncluded || weight != stat.Weight || !ClassGear.Implemented(source with
                { Modifiers = [new(stat.Code, "", stat.Baseline, stat.Baseline, ModifierMode.Numeric)] }))
            { throw new InvalidDataException("Unimplemented or misweighted mould stat."); }
            _stats.Add(stat.Id, stat); TotalWeight = checked(TotalWeight + stat.Weight);
        }
    }
    private static bool HasMinimum(AffixDefinition affix, MouldStat stat)
    {
        foreach (var modifier in affix.Modifiers)
        { if (Array.IndexOf(stat.Aliases, modifier.Code) >= 0 && modifier.Mode == ModifierMode.Numeric
            && string.IsNullOrEmpty(modifier.Parameter) && modifier.Min == stat.Baseline) { return true; } }
        return false;
    }
    private static bool HasProperty(AffixDefinition affix, MouldStat stat)
    {
        foreach (var modifier in affix.Modifiers)
        { if (Array.IndexOf(stat.Aliases, modifier.Code) >= 0 && modifier.Mode == ModifierMode.Numeric
            && string.IsNullOrEmpty(modifier.Parameter) && modifier.Min > 0) { return true; } }
        return false;
    }
    private static bool Mapped(AffixDefinition affix)
    {
        if (affix.Level is < 1 or > 99) { return false; }
        foreach (var basis in GearDrops.Bases)
        {
            if (basis.Variants.Length == 0) { continue; }
            string type = EquipmentBalance.Base(basis.Code).Type;
            bool excluded = false;
            foreach (string skip in affix.Exclude) { if (EquipmentBalance.IsType(type, skip)) { excluded = true; break; } }
            if (excluded) { continue; }
            foreach (string include in affix.Include) { if (EquipmentBalance.IsType(type, include)) { return true; } }
        }
        return false;
    }
    public static MouldStat Stat(string id) => id != null && _stats.TryGetValue(id, out var stat) ? stat
        : throw new ArgumentException("Unknown mould stat.", nameof(id));
    public static bool LegalSavedStat(string id, string code) => _stats.TryGetValue(id ?? "", out var stat) && stat.Code == code;
    public static int TierForLevel(int level) => level is >= 20 and <= 99 ? (level - 10) / 10 : 0;
    public static MouldTier Tier(int tier) => tier is >= 1 and <= 8 ? Settings.Tiers[tier - 1]
        : throw new ArgumentOutOfRangeException(nameof(tier));
    internal static MouldStat Select(int ticket)
    {
        if (ticket < 0 || ticket >= TotalWeight) { throw new ArgumentOutOfRangeException(nameof(ticket)); }
        foreach (var stat in Settings.Stats) { ticket -= stat.Weight; if (ticket < 0) { return stat; } }
        throw new InvalidDataException("Mould selection escaped its catalogue.");
    }
    internal static RuneMould Roll(int level, MonsterDifficulty difficulty, Random random)
    {
        int tier = TierForLevel(level);
        if (tier == 0 || random.Next(Settings.DropDenominator) >= Settings.OrdinaryTickets) { return null; }
        var stat = Select(random.Next(TotalWeight));
        return ItemOrigins.Create(ItemOrigin.Drop, () => new RuneMould(tier, stat.Id, level, difficulty));
    }
    internal static void Award(RewardLife life, Random random)
    {
        // Moulds are a separate reward after the shared gold/gear draw and life claim.
        var item = Roll(life.Level, life.Difficulty, random);
        if (item != null) { life.Corpse.DropItem(item); }
    }
}
