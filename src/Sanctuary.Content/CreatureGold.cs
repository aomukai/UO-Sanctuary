// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Mobiles;

namespace Sanctuary;

public sealed record GoldEntry(string Item, int Weight, string Nested, bool Gold);
public sealed record GoldTreasureClass(string Name, int Picks, int NoDrop, GoldEntry[] Entries);
public sealed record GoldPolicy(string Id, string[] Types, bool Eligible, string Reference, string TreasureClass);
public sealed record GoldCatalog(string SourceCommit, string Difficulty, int Players, int MaxDrops,
    GoldPolicy[] Policies, GoldTreasureClass[] Classes);

public static class CreatureGold
{
    private static readonly Dictionary<(string, MonsterDifficulty), GoldPolicy> _policies = new();
    private static readonly Dictionary<string, GoldTreasureClass> _classes = new(StringComparer.Ordinal);
    private static readonly List<GoldPolicy> _normalPolicies = new();
    public static IReadOnlyCollection<GoldPolicy> Policies => _normalPolicies;
    internal static IEnumerable<string> Leaves
    {
        get
        {
            foreach (var row in _classes.Values)
            { foreach (var entry in row.Entries) { if (entry.Nested == null) { yield return entry.Item; } } }
        }
    }
    internal static IEnumerable<string> LeavesFor(MonsterDifficulty difficulty)
    {
        var visited = new HashSet<string>();
        var pending = new Stack<string>();
        foreach (var ((_, key), policy) in _policies)
        { if (key == difficulty && !string.IsNullOrEmpty(policy.TreasureClass)) { pending.Push(policy.TreasureClass); } }
        while (pending.Count > 0)
        {
            string name = pending.Pop();
            if (!visited.Add(name)) { continue; }
            foreach (var entry in _classes[name].Entries)
            { if (entry.Nested != null) { pending.Push(entry.Nested); } else { yield return entry.Item; } }
        }
    }
    public static void Load()
    {
        _normalPolicies.Clear();
        _policies.Clear();
        _classes.Clear();
        foreach (var difficulty in Enum.GetValues<MonsterDifficulty>()) { Load(difficulty); }
    }
    private static void Load(MonsterDifficulty difficulty)
    {
        string suffix = difficulty == MonsterDifficulty.Normal ? "" : "-" + difficulty.ToString().ToLowerInvariant();
        var catalog = JsonSerializer.Deserialize<GoldCatalog>(File.ReadAllText(Path.Join(Core.BaseDirectory,
            "Data", "Sanctuary", "Balance", "creature-gold" + suffix + ".json")), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (catalog is not { Players: 1, MaxDrops: 6 } || catalog.Difficulty != difficulty.ToString())
        {
            throw new InvalidDataException("Unsupported gold source rules.");
        }
        foreach (var row in catalog.Classes)
        {
            if (row.Picks != 1 || row.NoDrop < 0 || row.Entries is not { Length: > 0 })
            {
                throw new InvalidDataException("Unsupported gold treasure class.");
            }
            foreach (var entry in row.Entries)
            {
                if (entry.Weight <= 0 || entry.Gold != (entry.Item == "gld") || entry.Gold && entry.Nested != null)
                {
                    throw new InvalidDataException("Invalid gold treasure entry.");
                }
            }
            if (_classes.TryGetValue(row.Name, out var existing))
            {
                if (JsonSerializer.Serialize(existing) != JsonSerializer.Serialize(row)) { throw new InvalidDataException("Inconsistent shared treasure class."); }
            }
            else { _classes.Add(row.Name, row); }
        }
        foreach (var row in catalog.Policies)
        {
            if (row.Eligible && string.IsNullOrWhiteSpace(row.Reference)
                || !row.Eligible && (row.Reference != null || row.TreasureClass != null))
            {
                throw new InvalidDataException("Invalid creature gold policy.");
            }
            if (!string.IsNullOrEmpty(row.TreasureClass)) { ValidateTree(row.TreasureClass, new HashSet<string>()); }
            foreach (string type in row.Types)
            {
                _policies.Add((type, difficulty), row);
                if (difficulty == MonsterDifficulty.Normal) { _normalPolicies.Add(row); }
            }
        }
        if (_policies.Count != CreatureProfiles.All.Count * ((int)difficulty + 1))
        {
            throw new InvalidDataException("Incomplete creature gold coverage.");
        }
        foreach (var definition in CreatureProfiles.All)
        {
            if (!_policies.ContainsKey((definition.Type, difficulty))) { throw new InvalidDataException($"Missing gold policy: {definition.Type}"); }
        }
    }
    private static void ValidateTree(string name, HashSet<string> path)
    {
        if (!path.Add(name) || !_classes.TryGetValue(name, out var row))
        {
            throw new InvalidDataException("Missing or cyclic gold treasure class.");
        }
        foreach (var entry in row.Entries)
        {
            if (entry.Nested != null) { ValidateTree(entry.Nested, path); }
        }
        path.Remove(name);
    }
    public static GoldPolicy Policy(Type type, MonsterDifficulty difficulty = MonsterDifficulty.Normal) => _policies.GetValueOrDefault((type.FullName, difficulty));
    public static int GoldFind(PlayerMobile player) => Math.Max(0,
        checked(ClassGear.Bonus(player, "gold%") + ClassGear.Material(player, "gold-find")));
    public static int Roll(Type type, int level, int goldFind, Random random, MonsterDifficulty difficulty = MonsterDifficulty.Normal) =>
        Policy(type, difficulty) is { Eligible: true } policy ? RollTreasureClass(policy.TreasureClass, level, goldFind, random) : 0;
    internal static int RollTreasureClass(string name, int level, int goldFind, Random random)
    {
        if (level is < 1 or > 99 || goldFind < 0) { throw new ArgumentOutOfRangeException(); }
        return SelectTreasureClass(name, random) == "gld" ? Amount(level, goldFind, random) : 0;
    }
    internal static string Select(Type type, Random random, MonsterDifficulty difficulty = MonsterDifficulty.Normal, bool ignoreNoDrop = false) => Policy(type, difficulty) is { Eligible: true } policy
        ? SelectTreasureClass(policy.TreasureClass, random, ignoreNoDrop) : null;
    internal static int Roll(GoldTreasureClass row, Random random, bool ignoreNoDrop)
    {
        // Find Item's successful source drop passes a7=1 through every
        // nested TC. This bypasses NoDrop without changing item weights.
        int noDrop = ignoreNoDrop ? 0 : row.NoDrop;
        int total = noDrop;
        foreach (var entry in row.Entries) { total = checked(total + entry.Weight); }
        return random.Next(total) - noDrop;
    }
    internal static string SelectTreasureClass(string name, Random random, bool ignoreNoDrop = false)
    {
        // All reachable ordinary rows have exactly one pick; nested rows keep their
        // original NoDrop and non-gold weights instead of redistributing them.
        while (!string.IsNullOrEmpty(name))
        {
            var row = _classes[name];
            int choice = Roll(row, random, ignoreNoDrop);
            if (choice < 0) { return null; }
            GoldEntry selected = null;
            foreach (var entry in row.Entries)
            {
                choice -= entry.Weight;
                if (choice < 0) { selected = entry; break; }
            }
            if (selected.Nested == null) { return selected.Item; }
            name = selected.Nested;
        }
        return null;
    }
    private static int Amount(int level, int goldFind, Random random)
    {
        int basis = level + random.Next(5 * level);
        return checked((int)((long)basis * (100L + goldFind) / 100));
    }
    internal static void Award(RewardLife life, Random random)
    {
        int amount = Amount(life.Level, life.GoldFind, random);
        while (amount > 0)
        {
            int stack = Math.Min(amount, 60000);
            life.Corpse.DropItem(new Gold(stack));
            amount -= stack;
        }
    }
}
