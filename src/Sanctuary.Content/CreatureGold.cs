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
    private static readonly Dictionary<string, GoldPolicy> _policies = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, GoldTreasureClass> _classes = new(StringComparer.Ordinal);
    public static IReadOnlyCollection<GoldPolicy> Policies => _policies.Values;
    public static void Load()
    {
        var catalog = JsonSerializer.Deserialize<GoldCatalog>(File.ReadAllText(Path.Join(Core.BaseDirectory,
            "Data", "Sanctuary", "Balance", "creature-gold.json")), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (catalog is not { Difficulty: "Normal", Players: 1, MaxDrops: 6 })
        {
            throw new InvalidDataException("Unsupported gold source rules.");
        }
        _policies.Clear();
        _classes.Clear();
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
            _classes.Add(row.Name, row);
        }
        foreach (var row in catalog.Policies)
        {
            if (row.Eligible && string.IsNullOrWhiteSpace(row.Reference)
                || !row.Eligible && (row.Reference != null || row.TreasureClass != null))
            {
                throw new InvalidDataException("Invalid creature gold policy.");
            }
            if (!string.IsNullOrEmpty(row.TreasureClass)) { ValidateTree(row.TreasureClass, new HashSet<string>()); }
            foreach (string type in row.Types) { _policies.Add(type, row); }
        }
        if (_policies.Count != CreatureProfiles.All.Count)
        {
            throw new InvalidDataException("Incomplete creature gold coverage.");
        }
        foreach (var definition in CreatureProfiles.All)
        {
            if (!_policies.ContainsKey(definition.Type)) { throw new InvalidDataException($"Missing gold policy: {definition.Type}"); }
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
    public static GoldPolicy Policy(Type type) => _policies.GetValueOrDefault(type.FullName);
    public static int GoldFind(PlayerMobile player) => Math.Max(0,
        checked(ClassGear.Bonus(player, "gold%") + ClassGear.Material(player, "gold-find")));
    public static int Roll(Type type, int level, int goldFind, Random random) =>
        Policy(type) is { Eligible: true } policy ? RollTreasureClass(policy.TreasureClass, level, goldFind, random) : 0;
    internal static int RollTreasureClass(string name, int level, int goldFind, Random random)
    {
        if (level is < 1 or > 99 || goldFind < 0) { throw new ArgumentOutOfRangeException(); }
        // All reachable Normal rows have exactly one pick; nested rows keep their
        // original NoDrop and non-gold weights instead of redistributing them.
        while (!string.IsNullOrEmpty(name))
        {
            var row = _classes[name];
            int total = row.NoDrop;
            foreach (var entry in row.Entries) { total = checked(total + entry.Weight); }
            int choice = random.Next(total) - row.NoDrop;
            if (choice < 0) { return 0; }
            GoldEntry selected = null;
            foreach (var entry in row.Entries)
            {
                choice -= entry.Weight;
                if (choice < 0) { selected = entry; break; }
            }
            if (selected.Gold)
            {
                int basis = level + random.Next(5 * level);
                return checked((int)((long)basis * (100L + goldFind) / 100));
            }
            name = selected.Nested;
        }
        return 0;
    }
    internal static void Award(RewardLife life, Random random)
    {
        int amount = Roll(life.Creature.GetType(), life.Level, life.GoldFind, random);
        while (amount > 0)
        {
            int stack = Math.Min(amount, 60000);
            life.Corpse.DropItem(new Gold(stack));
            amount -= stack;
        }
    }
}
