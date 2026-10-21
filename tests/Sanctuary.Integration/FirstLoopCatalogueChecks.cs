// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;

namespace Sanctuary.Integration;

// Authoring evidence from the actual eligibility/roll APIs; never a live-world command.
public static class FirstLoopCatalogueChecks
{
    private static readonly ILogger _logger = LogFactory.GetLogger(typeof(FirstLoopCatalogueChecks));
    private static void Require(bool condition, string message)
    {
        if (!condition) { throw new InvalidOperationException(message); }
    }
    private static string Hash(string path) => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));

    public static void Configure() => ConsoleInputHandler.RegisterCommand("first-loop-catalogue-check",
        "Export the bounded reference cohort using current gear APIs", _ => Core.LoopContext.Post(() =>
        {
            try { Check(); }
            catch (Exception error) { _logger.Error(error, "first-loop-catalogue-check: failed"); }
        }));

    private static void Check()
    {
        string root = Environment.GetEnvironmentVariable("SANCTUARY_FIRST_LOOP_ROOT")
            ?? throw new InvalidOperationException("An explicit authoring root is required");
        string destination = Environment.GetEnvironmentVariable("SANCTUARY_FIRST_LOOP_AUDIT")
            ?? throw new InvalidOperationException("An explicit audit output is required");
        string manifestPath = Path.Join(root, "tools/reference/first-loop-cohort.json");
        using var manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
        var declaredEffects = manifest.RootElement.GetProperty("effects").EnumerateArray()
            .Select(e => e.GetProperty("code").GetString()).ToHashSet();
        var spawnableEffects = AffixBalance.All.Where(ClassGear.Implemented)
            .SelectMany(a => a.Modifiers).Select(m => m.Code).ToHashSet();
        var candidates = AffixBalance.All.SelectMany(a => a.Modifiers).Select(m => m.Code).Concat(declaredEffects).Distinct();
        var actualEffects = candidates.Where(code => ClassGear.Implemented(AffixBalance.All[0] with
            { Modifiers = [new AffixModifier(code, "", 0, 1, ModifierMode.Numeric)] })).ToHashSet();
        Require(declaredEffects.SetEquals(actualEffects), "Consumer inventory differs from live-generation guard");
        var factories = new Dictionary<string, Func<Item>>
        {
            ["Longsword"] = () => new Longsword(), ["WoodenShield"] = () => new WoodenShield(),
            ["LeatherCap"] = () => new LeatherCap(), ["RingmailChest"] = () => new RingmailChest(),
            ["GoldRing"] = () => new GoldRing(), ["Necklace"] = () => new Necklace(),
            ["SkullCap"] = () => new SkullCap(), ["BoneChest"] = () => new BoneChest()
        };
        var items = new List<object>();
        var codes = new HashSet<string>();
        var baseFactories = new Dictionary<string, Func<Item>>();
        foreach (var entry in manifest.RootElement.GetProperty("items").EnumerateArray())
        {
            string name = entry.GetProperty("type").GetString();
            Require(factories.TryGetValue(name, out var factory), $"No bounded constructor for {name}");
            var item = factory();
            try
            {
                var definition = ClassGear.Definition(item);
                Require(definition != null && definition.NormalDrop && definition.AllowsAffixes
                    && definition.BaseCode == entry.GetProperty("baseCode").GetString()
                    && definition.Tier == entry.GetProperty("baseTier").GetString()
                    && definition.Slot.ToString() == entry.GetProperty("slot").GetString(), $"Mapping drift: {name}");
                codes.Add(definition.BaseCode);
                baseFactories.TryAdd(definition.BaseCode, factory);
                var white = ClassGear.Profiles.Convert(item, 35, GearRarity.Normal, new Random(42));
                Require(white.Roll.Affixes.Length == 0, $"White {name} acquired affixes");
                items.Add(new
                {
                    Type = name, definition.BaseCode, Slot = definition.Slot.ToString(), EffectiveSlot = ClassGear.Slot(item).ToString(),
                    CurrentWeight = definition.Weight.ToString(), TargetWeight = entry.GetProperty("targetWeight").GetString(),
                    NativeResource = item switch { BaseArmor a => a.Resource.ToString(), BaseWeapon w => w.Resource.ToString(),
                        BaseClothing c => c.Resource.ToString(), BaseJewel j => j.Resource.ToString(), _ => "None" },
                    NativeArmorMaterial = (item as BaseArmor)?.MaterialType.ToString(),
                    CurrentSalvageMaterial = EquipmentEconomy.Material(item).ToString(),
                    CurrentMaxSockets = definition.MaxSockets,
                    TargetSocketCap = entry.GetProperty("targetSocketCap").GetInt32()
                });
            }
            finally { item.Delete(); }
        }
        Require(items.Count == factories.Count, "Bounded cohort has missing or duplicate entries");
        var pools = new List<object>();
        foreach (string code in codes.OrderBy(c => c, StringComparer.Ordinal))
        {
            var basis = EquipmentBalance.Base(code);
            foreach (var level in manifest.RootElement.GetProperty("affixQueryItemLevels").EnumerateArray())
            {
                int itemLevel = level.GetInt32();
                int affixLevel = AffixBalance.Level(itemLevel, basis.QualityLevel, basis.MagicLevel);
                foreach (var rarity in new[] { GearRarity.Magic, GearRarity.Rare })
                {
                    var eligible = AffixBalance.All.Where(a => ClassGear.Implemented(a)
                        && AffixBalance.Eligible(a, basis.Type, affixLevel, rarity)).OrderBy(a => a.Id, StringComparer.Ordinal).ToArray();
                    Require(eligible.Length > 0 && eligible.All(a => a.Modifiers.All(m =>
                        m.Mode == ModifierMode.Numeric && declaredEffects.Contains(m.Code) && m.Code != "sock")),
                        $"Unsupported pool for {code}/{itemLevel}/{rarity}");
                    // Exercise the real roller across seeds; validate ranges, groups,
                    // rarity and requirements through the persisted-state validator.
                    var host = baseFactories[code]();
                    bool observedNonMinimum = false;
                    try
                    {
                        foreach (int seed in Enumerable.Range(0, 64))
                        {
                            var roll = AffixBalance.Roll(basis, itemLevel, rarity, new Random(seed), ClassGear.Implemented);
                            Require(roll.Affixes.Length > 0 && roll.Affixes.All(a => eligible.Any(e => e.Id == a.Definition.Id)),
                                $"Roll escaped eligible pool for {code}");
                            GearProfiles.Validate(host, new GearState(code, basis.MinDefense, false, roll));
                            observedNonMinimum |= roll.Affixes.SelectMany(a => a.Modifiers).Any(m => m.Value > m.Min);
                        }
                    }
                    finally { host.Delete(); }
                    Require(observedNonMinimum, $"Gear rolls became minimum-only for {code}");
                    pools.Add(new { BaseCode = code, ItemLevel = itemLevel, AffixLevel = affixLevel,
                        Rarity = rarity.ToString(), AffixIds = eligible.Select(a => a.Id).ToArray() });
                }
            }
        }
        ClassGear.Profiles.Prune();
        string assembly = typeof(ClassGear).Assembly.Location;
        string artifact = Path.GetRelativePath(root, new FileInfo(assembly).ResolveLinkTarget(true)?.FullName ?? assembly).Replace('\\', '/');
        Require(!artifact.StartsWith("../", StringComparison.Ordinal) && !Path.IsPathRooted(artifact), "Audited content assembly must belong to the authoring workspace");
        var evidence = new
        {
            SchemaVersion = 1, ManifestSha256 = Hash(manifestPath),
            ContentAssemblySha256 = Hash(typeof(ClassGear).Assembly.Location),
            ContentAssemblyPath = artifact,
            InputHashes = new[] { "src/Sanctuary.Content/ClassGear.cs", "src/Sanctuary.Content/AffixBalance.cs",
                "src/Sanctuary.Content/GearProfiles.cs", "src/Sanctuary.Content/EquipmentEconomy.cs",
                "src/Sanctuary.Content/HitRecovery.cs", "src/Sanctuary.Content/GearEffects.cs", "src/Sanctuary.Content/EquipmentCrafting.cs",
                "data/Sanctuary/Balance/affixes.json", "data/Sanctuary/Balance/item-types.json",
                "data/Sanctuary/Balance/normal-bases.json", "data/Sanctuary/Balance/uo-equipment.json" }
                .Concat(manifest.RootElement.GetProperty("effects").EnumerateArray()
                    .Select(e => e.GetProperty("consumer").GetString().Split('#')[0]))
                .Distinct().OrderBy(p => p, StringComparer.Ordinal).ToDictionary(p => p, p => Hash(Path.Join(root, p))),
            ImplementedCodes = actualEffects.OrderBy(c => c, StringComparer.Ordinal).ToArray(),
            SpawnableCodes = spawnableEffects.OrderBy(c => c, StringComparer.Ordinal).ToArray(), Items = items, Pools = pools
        };
        File.WriteAllText(destination, JsonSerializer.Serialize(evidence, new JsonSerializerOptions { WriteIndented = true }) + "\n");
        _logger.Information("first-loop-catalogue-check: passed; items={Items}, pools={Pools}, effectCodes={Effects}, rolls={Rolls}",
            items.Count, pools.Count, actualEffects.Count, pools.Count * 64);
    }
}
