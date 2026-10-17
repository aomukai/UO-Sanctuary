using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Logging;

namespace Sanctuary.Integration;

public static class FoundationChecks
{
    private static readonly ILogger _logger = LogFactory.GetLogger(typeof(FoundationChecks));
    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
    private sealed record ClassRow(string Name, int Strength, int Dexterity, int Vitality, int Energy,
        int Life, int Stamina, int Mana, int LifeLevel4, int StaminaLevel4, int ManaLevel4,
        int LifeVitality4, int StaminaVitality4, int ManaEnergy4, int AttackBonus, int StatPerLevel);
    private sealed record ExperienceRow(int Level, long Threshold, int Ratio1024);
    public static void Check()
    {
        foreach (var row in BalanceCatalog.Read<ClassRow>("classes"))
        {
            var characterClass = Enum.Parse<DiabloClass>(row.Name);
            var expected = new ClassStatProfile(row.Strength, row.Dexterity, row.Vitality, row.Energy,
                row.Life, row.Stamina, row.Mana, row.LifeLevel4, row.StaminaLevel4, row.ManaLevel4,
                row.LifeVitality4, row.StaminaVitality4, row.ManaEnergy4, row.AttackBonus);
            Require(ClassStats.For(characterClass) == expected && row.StatPerLevel == 5,
                $"{row.Name} diverged from CharStats.txt");
            var player = new PlayerMobile { Body = 0x190, Player = true };
            try
            {
                ClassCreation.Profiles.Assign(player, characterClass);
                int vitality = 0, energy = 0;
                for (int level = 1; level <= 99; level++)
                {
                    if (level > 1)
                    {
                        ClassCreation.Profiles.AdvanceLevel(player, level);
                        for (int point = 0; point < 5; point++)
                        {
                            var stat = (CharacterStat)((level + point) % 4);
                            Require(ClassCreation.Profiles.TrySpendStat(player, stat), "Valid level reward was not spendable");
                            vitality += stat == CharacterStat.Vitality ? 1 : 0;
                            energy += stat == CharacterStat.Energy ? 1 : 0;
                        }
                    }
                    Require(player.HitsMax == row.Life + ((level - 1) * row.LifeLevel4 + vitality * row.LifeVitality4) / 4
                        && player.ManaMax == row.Mana + ((level - 1) * row.ManaLevel4 + energy * row.ManaEnergy4) / 4
                        && player.StamMax == row.Stamina + ((level - 1) * row.StaminaLevel4 + vitality * row.StaminaVitality4) / 4,
                        $"{row.Name} resource scaling diverged at level {level}");
                    Require(ClassCreation.Profiles.AvailableStatPoints(player.Serial) == 0,
                        "Stat budget diverged from five points per level");
                }
            }
            finally
            {
                player.Delete();
                ClassCreation.Profiles.PruneDeleted();
            }
        }
        foreach (var row in BalanceCatalog.Read<ExperienceRow>("experience"))
        {
            Require(ExperienceLevels.Threshold(row.Level) == row.Threshold
                && MonsterExperience.Ratio1024(row.Level) == row.Ratio1024,
                $"XP threshold or reduction diverged at level {row.Level}");
        }
        Require(MonsterExperience.Award(10000, 16, 10) == 8800
            && MonsterExperience.Award(10000, 4, 10) == 8100
            && MonsterExperience.Award(10000, 20, 10) == 200
            && MonsterExperience.Award(10000, 1, 20) == 500
            && MonsterExperience.Award(10000, 50, 25) == 5000
            && MonsterExperience.Award(10000, 70, 70) == 9531
            && MonsterExperience.Award(10000, 98, 98) == 58
            && MonsterExperience.Award(int.MaxValue, 99, 99) == 0, "Solo XP adjustment is incorrect");
        Require(MonsterBalance.All.Count == 396, "Expected exactly one players-1 curve per archetype");
        foreach (var row in MonsterBalance.All)
        {
            var samples = new HashSet<int>();
            for (int seed = 0; seed < 32; seed++)
            {
                var rolled = row.Roll(new Random(seed));
                Require(rolled.Life >= row.Life.Min && rolled.Life <= row.Life.Max
                    && rolled.AttackRating >= row.AttackRating.Min && rolled.AttackRating <= row.AttackRating.Max
                    && rolled.Defense >= row.Defense.Min && rolled.Defense <= row.Defense.Max,
                    "Monster roll escaped its level band");
                samples.Add(rolled.Life);
                if (row.Level < 99)
                {
                    Require(rolled.Life < MonsterBalance.For(row.Archetype, row.Level + 1).Life.Min,
                        "A level roll overtook the next level");
                }
            }
            Require(row.Life.Min == row.Life.Max || samples.Count > 1, "Nonzero monster variance did not vary");
            Require(row.SourceBand == (row.Level <= 35 ? "Normal" : row.Level <= 65 ? "Nightmare" : "Hell"),
                "D2 source band diverged from the documented players-1 progression");
        }
        GearCoverage();
        foreach (var basis in EquipmentBalance.NormalBases)
        {
            if (!EquipmentBalance.Types[basis.Type].Rare)
            {
                continue;
            }
            foreach (var rarity in new[] { GearRarity.Magic, GearRarity.Rare })
            {
                foreach (int itemLevel in new[] { 1, 40, 70, 99 })
                {
                    for (int seed = 0; seed < 4; seed++)
                    {
                        var roll = AffixBalance.Roll(basis, itemLevel, rarity, new Random(seed));
                        Require(roll.Affixes.Length >= 1 && roll.Affixes.Length <= (rarity == GearRarity.Magic ? 2 : 6),
                            "Affix count escaped rarity bounds");
                        Require(roll.Affixes.Count(a => a.Definition.Kind == AffixKind.Prefix) <= (rarity == GearRarity.Magic ? 1 : 3)
                            && roll.Affixes.Count(a => a.Definition.Kind == AffixKind.Suffix) <= (rarity == GearRarity.Magic ? 1 : 3)
                            && roll.Affixes.Select(a => a.Definition.Group).Distinct().Count() == roll.Affixes.Length,
                            "Duplicate group or too many prefixes/suffixes");
                        foreach (var affix in roll.Affixes)
                        {
                            Require(AffixBalance.Eligible(affix.Definition, basis.Type, roll.AffixLevel, rarity),
                                "An illegal affix rolled for this base/rarity/level");
                            foreach (var modifier in affix.Modifiers)
                            {
                                Require(modifier.Code != "sock" && (modifier.Mode != ModifierMode.Numeric
                                    ? modifier.Value == null : modifier.Value >= modifier.Min && modifier.Value <= modifier.Max),
                                    "Structured modifier or numeric roll was corrupted");
                            }
                        }
                    }
                }
            }
        }
        Require(AffixBalance.Level(1, 1) == 1 && AffixBalance.Level(80, 60) == 61
            && AffixBalance.Level(50, 60) == 30 && AffixBalance.Level(1, 3, 1) == 4,
            "Affix level formula rounded incorrectly");
        var magicOnly = AffixBalance.All.First(a => !a.Rare && a.Modifiers.Any(m => m.Code == "dmg%")
            && AffixBalance.Eligible(a, "swor", 99, GearRarity.Magic));
        Require(!AffixBalance.Eligible(magicOnly, "swor", 99, GearRarity.Rare), "Magic-only tier was allowed on rare gear");
        _logger.Information("foundation-check: passed; classes=7, levels=99, monsterProfiles={Profiles}, equipmentTypes={Gear}, affixes={Affixes}",
            MonsterBalance.All.Count, EquipmentBalance.All.Count, AffixBalance.All.Count);
    }

    private static void GearCoverage()
    {
        Type[] roots = [typeof(BaseWeapon), typeof(BaseArmor), typeof(BaseClothing), typeof(BaseJewel),
            typeof(BaseQuiver), typeof(BaseTalisman), typeof(Spellbook), typeof(BaseEquipableLight)];
        var types = typeof(Dagger).Assembly.GetTypes();
        var missing = types.Where(t => !t.IsAbstract && roots.Any(r => r.IsAssignableFrom(t))
            && !EquipmentBalance.TryGet(t, out _)).Select(t => t.FullName).ToArray();
        Require(missing.Length == 0, "Uncatalogued equipment: " + string.Join(", ", missing));
        foreach (var definition in EquipmentBalance.All)
        {
            Require(definition.MaxSockets == null && definition.Tier == "Normal", "Socket or future base tier slipped into the baseline");
            Require(types.Any(t => !t.IsAbstract && t.Name == definition.Type && typeof(Item).IsAssignableFrom(t)),
                $"Catalog entry is not an actual concrete item: {definition.Type}");
        }
        if (Environment.GetEnvironmentVariable("SANCTUARY_EQUIPMENT_AUDIT") is { Length: > 0 } auditPath)
        {
            var rows = new List<object>();
            foreach (var definition in EquipmentBalance.All.Where(d => d.NormalDrop))
            {
                var type = types.Single(t => t.Name == definition.Type);
                var constructor = type.GetConstructors().FirstOrDefault(c => c.GetParameters().All(p => p.HasDefaultValue));
                Require(constructor != null, $"Normal base cannot be constructed: {definition.Type}");
                var item = (Item)constructor.Invoke(constructor.GetParameters().Select(p => p.DefaultValue).ToArray());
                try
                {
                    rows.Add(new { definition.Type, Layer = item.Layer.ToString(), Material = (item as BaseArmor)?.MaterialType.ToString() });
                }
                finally
                {
                    item.Delete();
                }
            }
            File.WriteAllText(auditPath, JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }));
        }
        var player = new PlayerMobile { Body = 0x190, Player = true };
        var medium = new ChainChest();
        var heavy = new PlateChest();
        var arms = new PlateArms();
        var lightShield = new WoodenShield();
        var heavyShield = new HeaterShield();
        try
        {
            ClassCreation.Profiles.Assign(player, DiabloClass.Barbarian);
            player.Str = 200;
            int run = player.ComputeMovementSpeed(Direction.East | Direction.Running, false);
            int walk = player.ComputeMovementSpeed(Direction.East, false);
            Require(player.EquipItem(lightShield) && EquipmentBalance.RunPenalty(player) == 0,
                "Light shield slowed the player");
            Require(player.EquipItem(medium) && EquipmentBalance.RunPenalty(player) == 5,
                "Medium armour has the wrong penalty");
            Require(player.ComputeMovementSpeed(Direction.East | Direction.Running, false) == EquipmentBalance.RunDelay(run, 5)
                && player.ComputeMovementSpeed(Direction.East, false) == walk, "Equipment penalty did not apply only to running");
            medium.Delete();
            lightShield.Delete();
            Require(player.EquipItem(heavy) && player.EquipItem(arms) && player.EquipItem(heavyShield)
                && EquipmentBalance.RunPenalty(player) == 20, "UO armour pieces multiplied a full body penalty");
            Require(ClassStats.Snapshot(player)[(int)StatField.RunPenalty] == 20
                && player.ComputeMovementSpeed(Direction.East | Direction.Running, false) == EquipmentBalance.RunDelay(run, 20),
                "Client snapshot and server movement penalty disagree");
        }
        finally
        {
            player.Delete();
            medium.Delete();
            heavy.Delete();
            arms.Delete();
            lightShield.Delete();
            heavyShield.Delete();
            ClassCreation.Profiles.PruneDeleted();
        }
        Require(EquipmentBalance.WeightPenalty(GearWeight.Light) == 0
            && EquipmentBalance.WeightPenalty(GearWeight.Medium) == 5
            && EquipmentBalance.WeightPenalty(GearWeight.Heavy) == 10
            && EquipmentBalance.RunDelay(200, 20) == 250, "Equipment running penalties are incorrect");
    }
}
