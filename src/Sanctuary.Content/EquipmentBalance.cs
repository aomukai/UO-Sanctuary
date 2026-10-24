// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Server;
using Server.Items;

namespace Sanctuary;

public enum GearWeight { None, Light, Medium, Heavy }
public enum GearRarity { Normal = 0, Magic = 1, Rare = 2, Set = 3, Unique = 4 }
public enum GearSlot { Cosmetic, Weapon, Shield, Head, Body, Hands, Feet, Belt, Ring, Amulet }
public sealed record GearDefinition(string Type, string Category, string AffixType, GearWeight Weight,
    string Tier, bool NormalDrop, int? MaxSockets, string Source, string ExcludedReason,
    GearSlot Slot, string BaseCode, bool AllowsAffixes);
public sealed record NormalBase(string Code, string Name, string Type, int QualityLevel, int MagicLevel,
    int RequiredLevel, int Strength, int Dexterity, int MinDamage, int MaxDamage, int TwoHandMin,
    int TwoHandMax, int MissileMin, int MissileMax, int MinDefense, int MaxDefense, int Speed, int Block,
    int StrengthBonus, int DexterityBonus, int? MaxSockets, string Tier, bool TwoHanded = false, bool OneOrTwoHanded = false);
public sealed record GearItemType(string Code, string[] Parents, bool AlwaysMagic, bool Rare, string CharacterClass);

public static class EquipmentBalance
{
    private static readonly Dictionary<string, GearDefinition> _gear = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, NormalBase> _bases = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, GearItemType> _types = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, GamblingCostRow> _sourceFamilies = new(StringComparer.Ordinal);
    public static IReadOnlyCollection<GearDefinition> All => _gear.Values;
    public static IReadOnlyCollection<NormalBase> NormalBases => _bases.Values;
    public static IReadOnlyDictionary<string, GearItemType> Types => _types;
    public static bool TryGet(Item item, out GearDefinition gear) => _gear.TryGetValue(item.GetType().Name, out gear);
    public static bool TryGet(Type type, out GearDefinition gear) => _gear.TryGetValue(type.Name, out gear);
    public static NormalBase Base(string code) => _bases[code];
    internal static void AddNamedAppearances()
    {
        _gear[nameof(NamedThrowingKnife)] = new(nameof(NamedThrowingKnife), "Throwing", "tkni", GearWeight.None,
            "Normal", false, null, "Named throwing knife using native dagger art", "Named factory only", GearSlot.Weapon, "9tk", true);
    }
    internal static void AddNamedBase(NamedBaseDefinition definition)
    {
        if (_bases.ContainsKey(definition.Code) || definition.SourceTable == "misc") { return; }
        var row = definition.SourceFields;
        int Number(string key) => row.TryGetProperty(key, out var value) && value.TryGetInt32(out int n) ? n : 0;
        bool Flag(string key) => Number(key) != 0;
        // Quest-only source bases remain catalogue records, outside the
        // ordinary affix pool and its quality-level 1–99 contract.
        if (Number("level") is < 1 or > 99) { return; }
        // Future classes/families need their real affix ancestry before they
        // can join the runtime base pool. Their source definitions stay intact.
        if (definition.RequiredClass == "war" || !_types.ContainsKey(definition.ItemType)) { return; }
        _bases.Add(definition.Code, new(definition.Code, definition.Name, definition.ItemType, Number("level"),
            Number("magic lvl"), Number("levelreq"), Number("reqstr"), Number("reqdex"), Number("mindam"), Number("maxdam"),
            Number("2handmindam"), Number("2handmaxdam"), Number("minmisdam"), Number("maxmisdam"), Number("minac"), Number("maxac"),
            Number("speed"), Number("block"), Number("StrBonus"), Number("DexBonus"), Number("gemsockets"), definition.Tier,
            Flag("2handed"), Flag("1or2handed")));
    }
    internal static bool LootTierIdentity(Type type, GearTier tier, string code, string normal)
        => tier is GearTier.Exceptional or GearTier.Elite && TryGet(type, out var gear) && gear.NormalDrop && gear.AllowsAffixes
            && gear.BaseCode == normal && _sourceFamilies.TryGetValue(normal, out var row)
            && (tier == GearTier.Exceptional ? row.Exceptional : row.Elite) == code && Base(code).Tier == tier.ToString();
    internal static void AddProjectBelt(string type, string code) => _gear.Add(type,
        new(type, "Clothing", "belt", GearWeight.None, "Normal", false, null,
            "src/Sanctuary.Content/PotionTabards.cs", null, GearSlot.Belt, code, true));
    public static bool IsType(string code, string ancestor)
        => IsType(_types, code, ancestor);
    internal static bool IsType(IReadOnlyDictionary<string,GearItemType> types,string code,string ancestor)
    {
        if (code == ancestor)
        {
            return true;
        }
        if (!types.TryGetValue(code, out var definition))
        {
            return false;
        }
        foreach (var parent in definition.Parents)
        {
            if (IsType(types,parent,ancestor))
            {
                return true;
            }
        }
        return false;
    }
    public static int WeightPenalty(GearWeight weight) => weight switch
    {
        GearWeight.Medium => 5, GearWeight.Heavy => 10, _ => 0
    };

    // UO has several body pieces. Count the heaviest body class once, plus the shield.
    public static int RunPenalty(Mobile mobile)
    {
        if (!mobile.Alive)
        {
            return 0;
        }
        int body = 0, shield = 0;
        foreach (var item in mobile.Items)
        {
            if (TryGet(item, out var gear))
            {
                if (gear.Slot == GearSlot.Shield)
                {
                    shield = Math.Max(shield, WeightPenalty(gear.Weight));
                }
                else if (gear.Slot == GearSlot.Body)
                {
                    body = Math.Max(body, WeightPenalty(EffectiveWeight(item)));
                }
            }
        }
        return body + shield;
    }
    public static GearWeight EffectiveWeight(Item item) => item is BaseArmor { MaterialType: ArmorMaterialType.Bone } && ClassGear.State(item)?.CraftedEquipment == true
        ? GearWeight.Heavy : ClassGear.Definition(item)?.Weight ?? GearWeight.None;
    // A 10% speed loss means dividing speed by .9, not adding 10% to time.
    public static int RunDelay(int baseDelayMs, int penalty, int bonus = 0)
        => checked((baseDelayMs * 100 + 99 + bonus - penalty) / (100 + bonus - penalty));

    public static void Load()
    {
        _gear.Clear();
        _bases.Clear();
        _types.Clear(); _sourceFamilies.Clear();
        foreach (var row in BalanceCatalog.Read<GamblingCostRow>("gambling-costs"))
        { if (row.Code == row.Normal) { _sourceFamilies.Add(row.Code, row); } }
        foreach (var type in BalanceCatalog.Read<GearItemType>("item-types"))
        {
            _types.Add(type.Code, type);
        }
        foreach (var type in _types.Values)
        {
            ValidateType(type.Code, new HashSet<string>());
        }
        foreach (var basis in BalanceCatalog.Read<NormalBase>("normal-bases"))
        {
            if (basis.Tier != "Normal" || basis.MaxSockets < 0 || !_types.ContainsKey(basis.Type)
                || basis.QualityLevel is < 1 or > 99 || basis.MagicLevel < 0
                || basis.MinDamage > basis.MaxDamage || basis.MinDefense > basis.MaxDefense)
            {
                throw new InvalidDataException("Invalid normal equipment base.");
            }
            _bases.Add(basis.Code, basis);
        }
        foreach (var basis in BalanceCatalog.Read<NormalBase>("equipment-tier-bases").Concat(BalanceCatalog.Read<NormalBase>("gambling-tier-bases")))
        {
            if (basis.Tier is not ("Exceptional" or "Elite") || !_types.ContainsKey(basis.Type)
                || basis.QualityLevel is < 1 or > 99 || basis.MinDamage > basis.MaxDamage || basis.MinDefense > basis.MaxDefense)
            { throw new InvalidDataException("Invalid equipment tier base."); }
            _bases.Add(basis.Code, basis);
        }
        foreach (var gear in BalanceCatalog.Read<GearDefinition>("uo-equipment"))
        {
            if (string.IsNullOrEmpty(gear.Type) || string.IsNullOrEmpty(gear.Category) || gear.Tier != "Normal"
                || gear.MaxSockets < 0 || !Enum.IsDefined(gear.Weight)
                || !Enum.IsDefined(gear.Slot) || gear.AllowsAffixes != (gear.Slot != GearSlot.Cosmetic)
                || gear.Slot == GearSlot.Cosmetic && (gear.AffixType != null || gear.BaseCode != null)
                || gear.Slot != GearSlot.Cosmetic && (!_bases.TryGetValue(gear.BaseCode ?? "", out var reference) || reference.Type != gear.AffixType)
                || gear.AffixType != null && !_types.ContainsKey(gear.AffixType))
            {
                throw new InvalidDataException("Invalid UO equipment classification.");
            }
            _gear.Add(gear.Type, gear);
        }
    }
    private static void ValidateType(string code, HashSet<string> path)
    {
        if (!path.Add(code) || !_types.TryGetValue(code, out var type))
        {
            throw new InvalidDataException("Unknown or cyclic equipment type.");
        }
        foreach (var parent in type.Parents)
        {
            ValidateType(parent, path);
        }
        path.Remove(code);
    }
}
