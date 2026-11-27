// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    string SetUniquePolicy, EncounterTreasureRoot[] Roots, Dictionary<string, string> CampaignBossRoots,
    Dictionary<string, string> HythlothMinibossRoots, EncounterTreasureClass[] Classes);
internal sealed record EncounterDrop(string Leaf, int GoldMultiplier, int[] Factors);

public static class EncounterTreasure
{
    private static readonly Dictionary<string, EncounterTreasureClass> _classes = new(StringComparer.Ordinal);
    private static readonly Dictionary<(string, MonsterDifficulty, EncounterGrade), string> _roots = new();
    private static readonly Dictionary<string, EncounterTreasureEntry[]> _equipmentEntries = new(StringComparer.Ordinal);
    private static Dictionary<string, string> _campaignBossRoots;
    private static Dictionary<string, string> _hythlothMinibossRoots;
    public static void Load()
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
        options.Converters.Add(new JsonStringEnumConverter());
        var catalog = JsonSerializer.Deserialize<EncounterTreasureCatalog>(File.ReadAllText(Path.Join(Core.BaseDirectory,
            "Data", "Sanctuary", "Balance", "encounter-treasure.json")), options);
        if (catalog is not { SourceCommit: "45112569deb9384738ccafe5c24ebbb71f41c7c9", MaxDrops: 6, SetUniquePolicy: "NamedWhenAvailable", Roots.Length: 36 })
        { throw new InvalidDataException("Unsupported encounter treasure source."); }
        _classes.Clear(); _roots.Clear(); _equipmentEntries.Clear();
        foreach (var row in catalog.Classes)
        {
            if (row.Picks is < -6 or 0 or > 7 || row.NoDrop < 0 || row.Entries is not { Length: > 0 }
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
        if (catalog.CampaignBossRoots is not { Count: 3 } bossRoots || CampaignArenas.All.Any(arena =>
                !bossRoots.TryGetValue(arena.Profile.Id, out var name) || !_classes.TryGetValue(name, out var row) || row.Picks != 7))
            throw new InvalidDataException("Missing or invalid campaign boss treasure root.");
        _campaignBossRoots = bossRoots;
        if (catalog.HythlothMinibossRoots is not { Count: 4 } minibossRoots || Enum.GetValues<HythlothRole>()
                .Where(role => role >= HythlothRole.Herald)
                .Any(role => !minibossRoots.TryGetValue($"hythloth-{role.ToString().ToLowerInvariant()}", out var name)
                    || !_classes.TryGetValue(name, out var row) || row.Picks != -4 || row.Entries.Length != 2))
            throw new InvalidDataException("Missing or invalid Hythloth miniboss treasure root.");
        _hythlothMinibossRoots = minibossRoots;
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
        if (creature is CampaignBoss)
            return _campaignBossRoots[creature.Dungeon.Id];
        if (creature is HythlothEncounter { Leader: true })
            return _hythlothMinibossRoots[creature.Dungeon.Id];
        if (creature is HavenEncounter haven)
        {
            return haven.Leader ? "Act 1 Unique B" : CreatureProfiles.Level(haven) switch
            {
                >= 10 => "Act 1 H2H C",
                >= 5 => "Act 1 H2H B",
                _ => "Act 1 H2H A"
            };
        }
        if (creature is DestardEncounter destard)
        {
            if (destard.Role is DestardRole.CorruptedHatchling or DestardRole.DaemonSpawn)
                return "Act 2 H2H B";

            string kind = destard.Dungeon.Archetype == MonsterArchetype.Caster ? "Cast" : "H2H";
            return $"Act 1 (H) {kind} A";
        }
        if (creature is WrongEncounter)
        {
            string kind = creature.Leader ? "Unique" : creature.Grade == EncounterGrade.Champion ? "Champ"
                : creature.Dungeon.Archetype == MonsterArchetype.Caster ? "Cast"
                : creature.Dungeon.Archetype == MonsterArchetype.Ranged ? "Miss" : "H2H";
            return $"Act 5 {kind} C";
        }
        if (creature is FrontierEncounter or CovetousEncounter or CoveEncounter or HedgeEncounter or YewEncounter
            or YewVampire or OrcFortEncounter or BrigandEncounter or HythlothEncounter or ShameEncounter)
        {
            string tier = CreatureProfiles.Level(creature) >= 25 ? "B" : "A";
            string kind = creature.Leader ? "Unique" : creature.Grade == EncounterGrade.Champion ? "Champ"
                : creature.Dungeon.Archetype == MonsterArchetype.Caster ? "Cast" : "H2H";
            return $"Act 2 {kind} {tier}";
        }
        var grade = creature.Grade switch { EncounterGrade.Minion => EncounterGrade.Ordinary,
            EncounterGrade.SuperUnique => EncounterGrade.Unique, var value => value };
        string name;
        if (creature is DeceitEncounter or MausoleumDemon)
        {
            var kind = grade == EncounterGrade.Unique ? "Unique" : grade == EncounterGrade.Champion ? "Champ"
                : creature.Dungeon.Archetype == MonsterArchetype.Caster ? "Cast" : "H2H";
            name = $"Act 1 (N) {kind} A";
        }
        else
            name = _roots[(creature.Dungeon.Family, creature.Difficulty, grade)];
        var root = _classes[name];
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

    private static EncounterTreasureEntry[] EquipmentEntries(EncounterTreasureClass row)
    {
        if (_equipmentEntries.TryGetValue(row.Name, out var entries)) { return entries; }
        entries = row.Entries.Where(entry => _classes.TryGetValue(entry.Item, out var nested)
            ? EquipmentEntries(nested).Length > 0
            : GearDrops.HasEquipment(entry.Item)).ToArray();
        _equipmentEntries[row.Name] = entries;
        return entries;
    }

    internal static void AwardRequiredGear(RewardLife life, EncounterCreature creature, Random random) =>
        life.Corpse.DropItem(RollEquipment(Root(creature), life.Level, life.MagicFind, random));

    internal static Item RollEquipment(string name, int level, int magicFind, Random random)
    {
        int[] factors = new int[4];
        while (_classes.TryGetValue(name, out var row))
        {
            for (int i = 0; i < factors.Length; i++) { factors[i] = Math.Max(factors[i], row.Factors[i]); }
            var entries = EquipmentEntries(row);
            if (entries.Length == 0) { throw new InvalidDataException($"No supported gear in encounter treasure class '{name}'."); }

            int ticket = random.Next(entries.Sum(entry => entry.Weight));
            foreach (var entry in entries)
            {
                ticket -= entry.Weight;
                if (ticket >= 0) { continue; }
                name = entry.Item;
                break;
            }
        }
        var basis = GearDrops.SelectBase(name, random, equipmentOnly: true);
        // A Set or Unique quality roll can fail when the selected base lacks a named item.
        return GearDrops.Generate(basis, level, magicFind, random, factors)
            ?? GearDrops.GenerateMagic(basis, level, random);
    }
    internal static IReadOnlyList<EncounterDrop> Roll(EncounterCreature creature, Random random)
    {
        var drops = new List<EncounterDrop>(6);
        var root = _classes[Root(creature)];
        if (creature.Grade == EncounterGrade.SuperUnique && !HasDedicatedLoot(creature))
        {
            // Project named bosses reserve two gear outcomes plus four potion outcomes within D2's six-drop bound.
            if (root.Picks != -3 || root.Entries.Length != 2) { throw new InvalidDataException("Named encounter root is not a unique tree."); }
            root = root with { Picks = -4, Entries = [root.Entries[0] with { Weight = 2 }, root.Entries[1] with { Weight = 2 }] };
        }
        Walk(root, new int[4], 0, 256, creature.EncounterPlayers, random, drops);
        if (creature is HavenEncounter { Leader: true })
        {
            // Keep the named encounter's two gear rolls and six-drop budget.
            drops[^1] = new("gld", 512, new int[4]);
        }
        return drops;
    }
    private static void Walk(EncounterTreasureClass row, int[] inherited, int depth, int multiplier,
        int players, Random random, List<EncounterDrop> drops)
    {
        if (depth > 64 || drops.Count >= 6) { return; }
        int[] factors = new int[4];
        for (int i = 0; i < 4; i++) { factors[i] = Math.Max(inherited[i], row.Factors[i]); }
        int weight = 0; foreach (var entry in row.Entries) { weight = checked(weight + entry.Weight); }
        int noDrop = row.Picks > 0 ? DungeonPlayerScaling.NoDrop(row.NoDrop, weight, players) : 0;
        for (int pick = 0; pick < Math.Abs(row.Picks) && drops.Count < 6; pick++)
        {
            int ticket = row.Picks < 0 ? pick : random.Next(checked(noDrop + weight)) - noDrop;
            if (ticket < 0 || ticket >= weight) { continue; }
            foreach (var entry in row.Entries)
            {
                ticket -= entry.Weight;
                if (ticket >= 0) { continue; }
                int goldMultiplier = entry.GoldMultiplier == 256 ? multiplier : entry.GoldMultiplier;
                if (_classes.TryGetValue(entry.Item, out var nested))
                    Walk(nested, factors, depth + 1, goldMultiplier, players, random, drops);
                else { drops.Add(new(entry.Item, goldMultiplier, factors)); }
                break;
            }
        }
    }
    internal static bool HasDedicatedLoot(EncounterCreature creature) =>
        creature is CampaignBoss or HythlothEncounter { Leader: true } or ShameEncounter { Role: ShameRole.FleetCaptain };

    internal static void Award(RewardLife life, EncounterCreature creature, Random random)
    {
        if (creature is ShameEncounter { Role: ShameRole.FleetCaptain })
            return; // This captain's reward is placed in the expedition chest.
        bool dedicated = HasDedicatedLoot(creature);
        foreach (var drop in Roll(creature, random))
        {
            if (drop.Leaf == "gld")
            {
                int amount = checked((int)((long)(life.Level + random.Next(5 * life.Level)) * drop.GoldMultiplier / 256 * (100L + life.GoldFind) / 100));
                while (amount > 0) { int stack = Math.Min(60000, amount); life.Corpse.DropItem(new Gold(stack)); amount -= stack; }
                continue;
            }
            var item = AlchemyRestoratives.Drop(drop.Leaf) ?? GearDrops.Generate(
                GearDrops.SelectBase(drop.Leaf, random, equipmentOnly: dedicated), life.Level, life.MagicFind, random, drop.Factors,
                fallbackNamed: dedicated, bowCarrier: life.BowCarrier);
            if (item != null) { life.Corpse.DropItem(item); }
        }
        if (creature is CoveEncounter { Orc: true })
            life.Corpse.DropItem(new OrcEar());
        if (creature is DestardEncounter { DropsHorn: true, Controlled: false, Summoned: false })
            life.Corpse.DropItem(new DaemonHorn());
        if (creature is HavenEncounter { Dungeon.Id: HavenEncounter.EarthElemental } && random.Next(10) == 0)
            life.Corpse.DropItem(new IronOre(1));
    }
}
