// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;

namespace Sanctuary;

public enum AffixKind { Prefix, Suffix }
public enum ModifierMode { Numeric, Proc, Charges, DamageRange, Parameter }
public sealed record AffixModifier(string Code, string Parameter, int Min, int Max, ModifierMode Mode);
public sealed record AffixDefinition(string Id, string Name, AffixKind Kind, bool Rare, int Level,
    int MaxLevel, int RequiredLevel, int Frequency, int Group, string ClassSpecific,
    string CharacterClass, int ClassRequiredLevel, string[] Include, string[] Exclude, AffixModifier[] Modifiers);
public sealed record RolledModifier(string Code, string Parameter, int Min, int Max, ModifierMode Mode, int? Value);
public sealed record RolledAffix(AffixDefinition Definition, RolledModifier[] Modifiers);
public sealed record GearRoll(GearRarity Rarity, int ItemLevel, int AffixLevel, int RequiredLevel, RolledAffix[] Affixes);

public static class AffixBalance
{
    private static AffixDefinition[] _affixes = [];
    private static readonly Dictionary<string, AffixDefinition> _byId = new(StringComparer.Ordinal);
    public static AffixDefinition Find(string id) => _byId.TryGetValue(id, out var affix) ? affix
        : throw new InvalidDataException($"Unknown affix ID: {id}");
    private static readonly int[] _rareCounts = [3, 4, 4, 5, 5, 5, 6, 6];
    public static IReadOnlyList<AffixDefinition> All => _affixes;
    public static void Load()
    {
        _affixes = BalanceCatalog.Read<AffixDefinition>("affixes");
        _byId.Clear();
        var ids = new HashSet<string>();
        foreach (var affix in _affixes)
        {
            _byId.Add(affix.Id, affix);
            if (!ids.Add(affix.Id) || string.IsNullOrWhiteSpace(affix.Name) || !Enum.IsDefined(affix.Kind)
                || affix.Level < 1 || affix.MaxLevel != 0 && affix.MaxLevel < affix.Level
                || affix.Frequency < 1 || affix.Group < 1 || affix.Modifiers.Length == 0 || affix.Include.Length == 0)
            {
                throw new InvalidDataException($"Invalid affix: {affix.Id}");
            }
            foreach (var type in affix.Include)
            {
                if (!EquipmentBalance.Types.ContainsKey(type))
                {
                    throw new InvalidDataException($"Unknown affix item type: {type}");
                }
            }
            foreach (var type in affix.Exclude)
            {
                if (!EquipmentBalance.Types.ContainsKey(type))
                {
                    throw new InvalidDataException($"Unknown affix exclusion: {type}");
                }
            }
            foreach (var modifier in affix.Modifiers)
            {
                if (string.IsNullOrEmpty(modifier.Code) || !Enum.IsDefined(modifier.Mode)
                    || modifier.Mode == ModifierMode.Numeric && modifier.Min > modifier.Max)
                {
                    throw new InvalidDataException($"Invalid affix range: {affix.Id}");
                }
            }
        }
    }
    public static int Level(int itemLevel, int qualityLevel, int magicLevel = 0)
    {
        if (itemLevel is < 1 or > 99 || qualityLevel is < 1 or > 99 || magicLevel < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(itemLevel));
        }
        int level = Math.Max(itemLevel, qualityLevel);
        return Math.Clamp(magicLevel > 0 ? level + magicLevel
            : level < 99 - qualityLevel / 2 ? level - qualityLevel / 2 : 2 * level - 99, 1, 99);
    }
    public static bool Eligible(AffixDefinition affix, string itemType, int affixLevel, GearRarity rarity)
    {
        if (rarity is not (GearRarity.Magic or GearRarity.Rare) || !EquipmentBalance.Types.TryGetValue(itemType, out var type)
            || rarity == GearRarity.Rare && (!type.Rare || !affix.Rare)
            || affix.Level > affixLevel || affix.MaxLevel != 0 && affix.MaxLevel < affixLevel
            || !string.IsNullOrEmpty(affix.ClassSpecific) && !string.IsNullOrEmpty(type.CharacterClass)
                && affix.ClassSpecific != type.CharacterClass)
        {
            return false;
        }
        foreach (var modifier in affix.Modifiers)
        {
            if (modifier.Code == "sock")
            {
                return false; // Our socket ceilings are intentionally unconfigured.
            }
        }
        foreach (var exclude in affix.Exclude)
        {
            if (EquipmentBalance.IsType(itemType, exclude))
            {
                return false;
            }
        }
        foreach (var include in affix.Include)
        {
            if (EquipmentBalance.IsType(itemType, include))
            {
                return true;
            }
        }
        return false;
    }

    // Pure roll results, independent of UO's native magical properties or quality enums.
    // The caller must persist these IDs and values before implementing their effects.
    public static GearRoll Roll(NormalBase basis, int itemLevel, GearRarity rarity, Random random,
        Func<AffixDefinition, bool> filter = null)
        => Roll(basis.Type, itemLevel, basis.QualityLevel, basis.MagicLevel, basis.RequiredLevel, rarity, random, filter);
    public static GearRoll Roll(string itemType, int itemLevel, int qualityLevel, int magicLevel,
        int requiredLevel, GearRarity rarity, Random random, Func<AffixDefinition, bool> filter = null)
    {
        if (!Enum.IsDefined(rarity) || !EquipmentBalance.Types.TryGetValue(itemType, out var type))
        {
            throw new ArgumentOutOfRangeException(nameof(rarity));
        }
        int level = Level(itemLevel, qualityLevel, magicLevel);
        if (rarity == GearRarity.Normal)
        {
            return new(rarity, itemLevel, level, requiredLevel, []);
        }
        if (rarity == GearRarity.Rare && !type.Rare)
        {
            throw new InvalidOperationException("This equipment type cannot roll the requested rarity.");
        }
        var chosen = new List<RolledAffix>(6);
        var groups = new HashSet<int>();
        int prefixes = 0, suffixes = 0;
        bool exhaustedPrefixes = false, exhaustedSuffixes = false;
        int magicChoice = rarity == GearRarity.Magic ? random.Next(4) : 0;
        int count = rarity == GearRarity.Magic ? magicChoice == 0 ? 2 : 1 : _rareCounts[random.Next(8)];
        while (chosen.Count < count && !(exhaustedPrefixes && exhaustedSuffixes))
        {
            AffixKind kind;
            if (rarity == GearRarity.Magic)
            {
                kind = magicChoice == 0 ? chosen.Count == 0 ? AffixKind.Prefix : AffixKind.Suffix
                    : magicChoice == 1 ? AffixKind.Prefix : AffixKind.Suffix;
                if (kind == AffixKind.Prefix && exhaustedPrefixes || kind == AffixKind.Suffix && exhaustedSuffixes)
                {
                    break;
                }
            }
            else
            {
                kind = exhaustedPrefixes ? AffixKind.Suffix : exhaustedSuffixes ? AffixKind.Prefix
                    : random.Next(2) == 0 ? AffixKind.Prefix : AffixKind.Suffix;
            }
            var options = new List<AffixDefinition>();
            int total = 0;
            foreach (var affix in _affixes)
            {
                if (affix.Kind == kind && !groups.Contains(affix.Group) && Eligible(affix, itemType, level, rarity)
                    && (filter == null || filter(affix)))
                {
                    options.Add(affix);
                    total += Weight(affix, magicLevel);
                }
            }
            if (options.Count == 0)
            {
                if (kind == AffixKind.Prefix)
                {
                    exhaustedPrefixes = true;
                }
                else
                {
                    exhaustedSuffixes = true;
                }
                // Magic items still try the suffix when a combined roll has no legal prefix.
                if (rarity == GearRarity.Magic && magicChoice == 0 && kind == AffixKind.Prefix)
                {
                    magicChoice = 2;
                    count = 1;
                }
                continue;
            }
            int ticket = random.Next(total);
            AffixDefinition selected = options[^1];
            foreach (var affix in options)
            {
                ticket -= Weight(affix, magicLevel);
                if (ticket < 0)
                {
                    selected = affix;
                    break;
                }
            }
            groups.Add(selected.Group);
            var modifiers = new RolledModifier[selected.Modifiers.Length];
            for (int i = 0; i < modifiers.Length; i++)
            {
                var modifier = selected.Modifiers[i];
                modifiers[i] = new(modifier.Code, modifier.Parameter, modifier.Min, modifier.Max, modifier.Mode,
                    modifier.Mode == ModifierMode.Numeric ? random.Next(modifier.Min, modifier.Max + 1) : null);
            }
            chosen.Add(new(selected, modifiers));
            requiredLevel = Math.Max(requiredLevel, selected.RequiredLevel);
            if (kind == AffixKind.Prefix)
            {
                exhaustedPrefixes = ++prefixes >= (rarity == GearRarity.Magic ? 1 : 3);
            }
            else
            {
                exhaustedSuffixes = ++suffixes >= (rarity == GearRarity.Magic ? 1 : 3);
            }
        }
        if (chosen.Count == 0)
        {
            throw new InvalidOperationException("No legal affix exists for this type, rarity and level.");
        }
        return new(rarity, itemLevel, level, requiredLevel, chosen.ToArray());
    }
    private static int Weight(AffixDefinition affix, int magicLevel) => affix.Frequency * (magicLevel > 0 ? affix.Level : 1);
}
