// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Server;
using Server.Items;

namespace Sanctuary;

public sealed record EncounterTreasureEntry(string Item, int Weight, int GoldMultiplier);
public sealed record EncounterTreasureClass(string Name, int Group, int Level, int Picks, int NoDrop,
    int[] Factors, EncounterTreasureEntry[] Entries);
public sealed record EncounterTreasureRoot(string Family, MonsterDifficulty Difficulty, EncounterGrade Grade, string Name);
public sealed record EncounterTreasureCatalog(string SourceCommit, Dictionary<string, string> SourceSha256, int MaxDrops,
    string SetUniquePolicy, EncounterTreasureRoot[] Roots, EncounterTreasureClass[] Classes);
internal sealed record EncounterDrop(string Leaf, int GoldMultiplier, int[] Factors);

public static class EncounterTreasure
{
    private static readonly Dictionary<string, EncounterTreasureClass> _classes = new(StringComparer.Ordinal);
    private static readonly Dictionary<(string, MonsterDifficulty, EncounterGrade), string> _roots = new();
    public static void Load()
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
        options.Converters.Add(new JsonStringEnumConverter());
        var catalog = JsonSerializer.Deserialize<EncounterTreasureCatalog>(File.ReadAllText(Path.Join(Core.BaseDirectory,
            "Data", "Sanctuary", "Balance", "encounter-treasure.json")), options);
        if (catalog is not { SourceCommit: "45112569deb9384738ccafe5c24ebbb71f41c7c9", MaxDrops: 6, SetUniquePolicy: "Empty", Roots.Length: 36 })
        { throw new InvalidDataException("Unsupported encounter treasure source."); }
        _classes.Clear(); _roots.Clear();
        foreach (var row in catalog.Classes)
        {
            if (row.Picks is < -6 or 0 or > 6 || row.NoDrop < 0 || row.Entries is not { Length: > 0 }
                || row.Factors is not { Length: 4 } || row.Level is < 0 or > 99 || row.Group < 0)
            { throw new InvalidDataException("Invalid encounter treasure class."); }
            foreach (int factor in row.Factors) { if (factor is < 0 or > 1024) { throw new InvalidDataException("Invalid quality factor."); } }
            int total = row.NoDrop;
            foreach (var entry in row.Entries)
            {
                if (string.IsNullOrWhiteSpace(entry.Item) || entry.Weight <= 0 || entry.GoldMultiplier is < 1 or > 65536)
                { throw new InvalidDataException("Invalid encounter treasure weight."); }
                total = checked(total + entry.Weight);
            }
            _classes.Add(row.Name, row);
        }
        foreach (var root in catalog.Roots)
        {
            if (root.Grade is not (EncounterGrade.Ordinary or EncounterGrade.Champion or EncounterGrade.Unique)
                || !Enum.IsDefined(root.Difficulty) || !_classes.ContainsKey(root.Name)) { throw new InvalidDataException("Invalid encounter treasure root."); }
            _roots.Add((root.Family, root.Difficulty, root.Grade), root.Name);
        }
        foreach (var row in _classes.Values) { Validate(row.Name, new HashSet<string>(), 0); }
    }
    private static void Validate(string name, HashSet<string> path, int depth)
    {
        if (!_classes.TryGetValue(name, out var row)) { return; }
        if (depth > 64 || !path.Add(name)) { throw new InvalidDataException("Cyclic encounter treasure class."); }
        foreach (var entry in row.Entries) { Validate(entry.Item, path, depth + 1); }
        path.Remove(name);
    }
    internal static string Root(EncounterCreature creature)
    {
        var grade = creature.Grade switch { EncounterGrade.Minion => EncounterGrade.Ordinary,
            EncounterGrade.SuperUnique => EncounterGrade.Unique, var value => value };
        var root = _classes[_roots[(creature.Dungeon.Family, creature.Difficulty, grade)]];
        if (creature.Difficulty != MonsterDifficulty.Normal && root.Group != 0)
        {
            foreach (var candidate in _classes.Values)
            {
                if (candidate.Group == root.Group && candidate.Level <= CreatureProfiles.Level(creature) && candidate.Level > root.Level)
                { root = candidate; }
            }
        }
        return root.Name;
    }
    internal static IReadOnlyList<EncounterDrop> Roll(EncounterCreature creature, Random random)
    {
        var drops = new List<EncounterDrop>(6);
        var root = _classes[Root(creature)];
        if (creature.Grade == EncounterGrade.SuperUnique)
        {
            // Project named bosses reserve two gear outcomes plus four potion outcomes within D2's six-drop bound.
            if (root.Picks != -3 || root.Entries.Length != 2) { throw new InvalidDataException("Named encounter root is not a unique tree."); }
            root = root with { Picks = -4, Entries = [root.Entries[0] with { Weight = 2 }, root.Entries[1] with { Weight = 2 }] };
        }
        Walk(root, new int[4], 0, 256, random, drops);
        return drops;
    }
    private static void Walk(EncounterTreasureClass row, int[] inherited, int depth, int multiplier, Random random, List<EncounterDrop> drops)
    {
        if (depth > 64 || drops.Count >= 6) { return; }
        int[] factors = new int[4];
        for (int i = 0; i < 4; i++) { factors[i] = Math.Max(inherited[i], row.Factors[i]); }
        int weight = 0; foreach (var entry in row.Entries) { weight = checked(weight + entry.Weight); }
        for (int pick = 0; pick < Math.Abs(row.Picks) && drops.Count < 6; pick++)
        {
            int ticket = row.Picks < 0 ? pick : random.Next(checked(row.NoDrop + weight)) - row.NoDrop;
            if (ticket < 0 || ticket >= weight) { continue; }
            foreach (var entry in row.Entries)
            {
                ticket -= entry.Weight;
                if (ticket >= 0) { continue; }
                int goldMultiplier = entry.GoldMultiplier == 256 ? multiplier : entry.GoldMultiplier;
                if (_classes.TryGetValue(entry.Item, out var nested)) { Walk(nested, factors, depth + 1, goldMultiplier, random, drops); }
                else { drops.Add(new(entry.Item, goldMultiplier, factors)); }
                break;
            }
        }
    }
    internal static void Award(RewardLife life, EncounterCreature creature, Random random)
    {
        foreach (var drop in Roll(creature, random))
        {
            if (drop.Leaf == "gld")
            {
                int amount = checked((int)((long)(life.Level + random.Next(5 * life.Level)) * drop.GoldMultiplier / 256 * (100L + life.GoldFind) / 100));
                while (amount > 0) { int stack = Math.Min(60000, amount); life.Corpse.DropItem(new Gold(stack)); amount -= stack; }
                continue;
            }
            var item = AlchemyRestoratives.Drop(drop.Leaf) ?? GearDrops.Generate(GearDrops.SelectBase(drop.Leaf, random),
                life.Level, life.MagicFind, random, drop.Factors);
            if (item != null) { life.Corpse.DropItem(item); }
        }
    }
}
