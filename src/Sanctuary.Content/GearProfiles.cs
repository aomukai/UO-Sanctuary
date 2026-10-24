// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using Server;
using Server.Items;
using Server.Mobiles;

namespace Sanctuary;

public enum GearTier : byte { Normal, Exceptional, Elite }
public enum CraftQuality : byte { Average, HighQuality, Superior }

public sealed record MaterialRoll(string Code, int Value);

public sealed record GearState(string BaseCode, int Defense, bool StarterTeeth, GearRoll Roll)
{
    public GearTier Tier { get; init; }
    public CraftQuality Quality { get; init; }
    public int NativeSockets { get; init; }
    public int TinkerSockets { get; init; }
    public bool CraftedEquipment { get; init; }
    public int DurabilityPercent { get; init; }
    public MaterialRoll[] MaterialRolls { get; init; } = [];
    public bool Identified { get; init; } = true;
    public int? IdentifiedSaleOffer { get; init; }
    public int? VendorPurchasePrice { get; init; }
    public InstalledRune[] Runes { get; init; } = [];
    public InstalledGem[] Gems { get; init; } = [];
    public string NamedItemId { get; init; }
    public NamedPropertyRoll[] NamedProperties { get; init; } = [];
    public int NamedSockets { get; init; }
    public int SocketCount => NativeSockets + TinkerSockets + NamedSockets;
}

public sealed class GearProfiles : GenericPersistence
{
    private readonly Dictionary<Serial, GearState> _items = new();
    private readonly HashSet<Serial> _starterMigrations = new();
    public bool BeginStarterMigration(Serial player) => _starterMigrations.Add(player);
    public GearProfiles() : base("SanctuaryEquipment", 110) { }
    public GearState Find(Serial serial) => _items.GetValueOrDefault(serial);
    internal GearState Preview(Item item) => Find(item.Serial) ?? DefaultState(item);
    public GearState Ensure(Item item)
    {
        if (!ClassGear.Converted(item))
        {
            throw new ArgumentException("A catalogued item is required.");
        }
        if (Find(item.Serial) is { } saved)
        {
            return saved;
        }
        var state = DefaultState(item);
        Store(item, state);
        return state;
    }
    private static GearState DefaultState(Item item)
    {
        var basis = ClassGear.Base(item);
        int defense = basis == null ? 0 : basis.MinDefense + (int)(item.Serial.Value % (uint)(basis.MaxDefense - basis.MinDefense + 1));
        return new(ClassGear.Definition(item).BaseCode, defense, false,
            new(GearRarity.Normal, 1, basis == null ? 1 : AffixBalance.Level(1, basis.QualityLevel, basis.MagicLevel),
                basis?.RequiredLevel ?? 0, []));
    }
    public void Store(Item item, GearState state)
    {
        Validate(item, state);
        _items[item.Serial] = state;
        NamedMaintenance.Track(item);
        NamedGear.UpdateName(item, state);
        item.InvalidateProperties();
        if (state.NamedItemId != null && item.Parent is PlayerMobile wearer) { ClassGear.Changed(wearer); }
    }
    public void Copy(Item source, Item destination)
    {
        if (!ClassGear.Converted(source))
        {
            _items.Remove(destination.Serial);
            return;
        }
        var state = Ensure(source);
        GearDetails.CopyNative(source, destination);
        var affixes = new RolledAffix[state.Roll.Affixes.Length];
        for (int i = 0; i < affixes.Length; i++)
        {
            var affix = state.Roll.Affixes[i];
            affixes[i] = new(affix.Definition, (RolledModifier[])affix.Modifiers.Clone());
        }
        Store(destination, state with { Roll = state.Roll with { Affixes = affixes }, MaterialRolls = (MaterialRoll[])state.MaterialRolls.Clone(), Runes = (InstalledRune[])state.Runes.Clone(), Gems = GemSockets.Copy(state.Gems), NamedProperties = (NamedPropertyRoll[])state.NamedProperties.Clone() });
        NamedAmmunition.Copy(source, destination); NamedMaintenance.Copy(source, destination);
    }
    public GearState Convert(Item item, int itemLevel = 1, GearRarity rarity = GearRarity.Normal, Random random = null)
    {
        if (!ClassGear.Converted(item) || itemLevel is < 1 or > 99
            || rarity is not (GearRarity.Normal or GearRarity.Magic or GearRarity.Rare) || Find(item.Serial)?.NamedItemId != null)
        {
            throw new ArgumentException("A catalogued item, item level 1–99, and supported rarity are required.");
        }
        var definition = ClassGear.Definition(item);
        var basis = ClassGear.Base(item);
        if (ClassGear.Slot(item) == GearSlot.Cosmetic && rarity != GearRarity.Normal)
        {
            throw new InvalidOperationException("UO-only equipment slots are cosmetic white items and cannot have affixes.");
        }
        random ??= new Random();
        var roll = basis == null ? new GearRoll(GearRarity.Normal, itemLevel, 1, 0, [])
            : AffixBalance.Roll(basis, itemLevel, rarity, random, ClassGear.Implemented);
        int defense = basis == null ? 0 : random.Next(basis.MinDefense, basis.MaxDefense + 1);
        var before = Find(item.Serial) ?? new GearState(definition.BaseCode, defense, false, roll);
        roll = roll with { RequiredLevel = Math.Max(EquipmentCrafting.EquipFloor(before.Tier), roll.RequiredLevel) };
        var state = before with { Defense = defense, Roll = roll };
        Validate(item, state);
        _items[item.Serial] = state;
        NamedMaintenance.Track(item);
        item.InvalidateProperties();
        if (item.Parent is PlayerMobile player)
        {
            ClassGear.Changed(player);
        }
        return state;
    }
    public GearState FromMonster(Item item, int monsterLevel, GearRarity rarity, Random random = null)
    {
        bool fresh = Find(item.Serial) == null;
        var state = Convert(item, monsterLevel, rarity, random);
        if (fresh && item.Origin == ItemOrigin.Drop && rarity is GearRarity.Magic or GearRarity.Rare)
        {
            state = state with { Identified = false };
            Store(item, state);
        }
        return state;
    }
    public GearState FromMonster(Item item, int monsterLevel, GearRarity rarity, string baseCode, GearTier tier, Random random = null)
    {
        if (!ClassGear.Converted(item) || Find(item.Serial) != null || item.Origin != ItemOrigin.Drop
            || !Enum.IsDefined(tier) || monsterLevel is < 1 or > 99
            || !EquipmentCrafting.Identity(item.GetType(), tier, baseCode, ClassGear.Definition(item).BaseCode))
        { throw new ArgumentException("A fresh mapped Drop item and valid base tier/level are required."); }
        var basis = EquipmentBalance.Base(baseCode); random ??= Random.Shared;
        var roll = AffixBalance.Roll(basis, monsterLevel, rarity, random, ClassGear.Implemented);
        roll = roll with { RequiredLevel = Math.Max(EquipmentCrafting.EquipFloor(tier), roll.RequiredLevel) };
        var state = new GearState(baseCode, random.Next(basis.MinDefense, basis.MaxDefense + 1), false, roll)
        { Tier = tier, Identified = rarity == GearRarity.Normal };
        Store(item, state); return state;
    }
    internal GearState PrepareImbue(Item item, GearRarity rarity, Random random)
    {
        var basis = ClassGear.Base(item);
        var before = Find(item.Serial);
        if (basis == null || ClassGear.Slot(item) == GearSlot.Cosmetic || before is { StarterTeeth: true } or { TinkerSockets: > 0 }
            || (before?.Roll.Rarity ?? GearRarity.Normal) != GearRarity.Normal
            || rarity is not (GearRarity.Magic or GearRarity.Rare))
        {
            return null;
        }
        var roll = AffixBalance.Roll(basis, before?.Roll.ItemLevel ?? 1, rarity, random, ClassGear.Implemented);
        if (roll.Affixes.Length == 0)
        {
            return null;
        }
        roll = roll with { RequiredLevel = Math.Max(before?.Roll.RequiredLevel ?? EquipmentCrafting.EquipFloor(GearTier.Normal), roll.RequiredLevel) };
        var state = (before ?? DefaultState(item)) with { Roll = roll, Identified = true };
        Validate(item, state);
        return state;
    }
    internal void ApplyImbue(Item item, GearState state)
    {
        Store(item, state);
    }
    public void MarkStarter(Dagger dagger)
    {
        var state = Find(dagger.Serial) ?? Convert(dagger);
        if (state.Roll.Rarity != GearRarity.Normal)
        {
            throw new InvalidOperationException("Only the normal starter base may receive the starter skill grant.");
        }
        Store(dagger, state with { StarterTeeth = true });
    }
    public void Prune()
    {
        _starterMigrations.RemoveWhere(serial => World.FindMobile(serial) is not { Deleted: false });
        var deleted = new List<Serial>();
        foreach (var serial in _items.Keys)
        {
            if (World.FindItem(serial) is not { Deleted: false })
            {
                deleted.Add(serial);
            }
        }
        foreach (var serial in deleted)
        {
            _items.Remove(serial);
        }
    }
    public static void Validate(Item item, GearState state)
    {
        var definition = NamedGear.Definition(item, state) ?? ClassGear.Definition(item)
            ?? throw new InvalidDataException("Unknown saved gear type.");
        if (state == null || state.Roll == null || !Enum.IsDefined(state.Tier) || !Enum.IsDefined(state.Quality)
            || state.NativeSockets < 0 || state.TinkerSockets < 0 || state.NativeSockets > 2
            || state.NamedSockets < 0 || state.NamedSockets > 5 || state.TinkerSockets > 5 || state.SocketCount > 5
            || state.NativeSockets != 0 && state.NativeSockets != (int)state.Quality
            || state.SocketCount > 0 && (state.StarterTeeth || item.LootType == LootType.Newbied
                || !GearDetails.Socketable(item) && !(ClassGear.CosmeticClothing(item) && definition.Slot == GearSlot.Head))
            || state.IdentifiedSaleOffer is { } offer && (!ValidOffer(offer) || state.StarterTeeth
                || item.LootType == LootType.Newbied || item.Origin == ItemOrigin.Vendor))
        {
            throw new InvalidDataException("Invalid equipment tier, quality, socket provenance or sale offer.");
        }
        if (state.Runes == null || state.Gems == null || RuneSockets.Filled(state) > state.SocketCount || RuneSockets.Filled(state) > 0
            && (!GearDetails.Socketable(item) || item.Origin == ItemOrigin.Vendor))
        { throw new InvalidDataException("Invalid installed rune count or host."); }
        foreach (var rune in state.Runes) { RuneSockets.Validate(rune); }
        foreach (var gem in state.Gems) { GemSockets.Validate(gem); }
        if (state.VendorPurchasePrice is < 0 or > 60000 || state.VendorPurchasePrice != null && item.Origin != ItemOrigin.Vendor)
        { throw new InvalidDataException("Invalid vendor purchase price."); }
        if (!(state.NamedItemId != null ? NamedGear.Identity(item, state)
                : EquipmentCrafting.Identity(item.GetType(), state.Tier, state.BaseCode, definition.BaseCode))
            || !EquipmentCrafting.ValidMaterialRolls(item, state) || state.Roll.ItemLevel is < 1 or > 99 || !Enum.IsDefined(state.Roll.Rarity)
            || state.StarterTeeth && (item is not Dagger || state.Roll.Rarity != GearRarity.Normal))
        {
            throw new InvalidDataException("Invalid converted equipment identity or level.");
        }
        if (ClassGear.Slot(item) == GearSlot.Cosmetic && (!ClassGear.CosmeticClothing(item) || definition.BaseCode == null))
        {
            if (state.Defense != 0 || state.Roll.Rarity != GearRarity.Normal || state.Roll.Affixes.Length != 0)
            {
                throw new InvalidDataException("Cosmetic UO slots cannot contribute gear stats.");
            }
            return;
        }
        var basis = EquipmentBalance.Base(state.BaseCode);
        int affixLevel = AffixBalance.Level(state.Roll.ItemLevel, basis.QualityLevel, basis.MagicLevel);
        if (state.NamedItemId != null)
        {
            NamedGear.Validate(item, state);
            return;
        }
        if (state.NamedProperties == null || state.NamedProperties.Length != 0 || state.NamedSockets != 0
            || state.Roll.Rarity is GearRarity.Set or GearRarity.Unique)
        { throw new InvalidDataException("Ordinary equipment contains named-item state."); }
        int limit = state.Roll.Rarity switch { GearRarity.Normal => 0, GearRarity.Magic => 2, _ => 6 };
        if (state.Defense < basis.MinDefense || state.Defense > basis.MaxDefense || state.Roll.AffixLevel != affixLevel
            || state.Roll.Affixes.Length > limit || state.Roll.Rarity != GearRarity.Normal && state.Roll.Affixes.Length == 0)
        {
            throw new InvalidDataException("Invalid converted base defence or affix count.");
        }
        var groups = new HashSet<int>();
        int prefixes = 0, suffixes = 0, requirement = Math.Max(basis.RequiredLevel, EquipmentCrafting.EquipFloor(state.Tier));
        foreach (var affix in state.Roll.Affixes)
        {
            var source = AffixBalance.Find(affix.Definition.Id);
            if (!groups.Add(source.Group) || !ClassGear.Implemented(source)
                || !AffixBalance.Eligible(source, basis.Type, affixLevel, state.Roll.Rarity)
                || source.Modifiers.Length != affix.Modifiers.Length)
            {
                throw new InvalidDataException("Illegal saved affix type, group, rarity, level or effect.");
            }
            prefixes += source.Kind == AffixKind.Prefix ? 1 : 0;
            suffixes += source.Kind == AffixKind.Suffix ? 1 : 0;
            requirement = Math.Max(requirement, source.RequiredLevel);
            for (int i = 0; i < source.Modifiers.Length; i++)
            {
                var mod = source.Modifiers[i];
                var rolled = affix.Modifiers[i];
                if (rolled.Code != mod.Code || rolled.Parameter != mod.Parameter || rolled.Mode != mod.Mode
                    || rolled.Min != mod.Min || rolled.Max != mod.Max)
                {
                    throw new InvalidDataException("Saved affix roll escaped its source range.");
                }
                OrdinaryAffixProperties.Projection(mod,rolled.Value);
            }
        }
        int perKind = state.Roll.Rarity == GearRarity.Magic ? 1 : 3;
        // Jewellery saves its craft-time equip floor in the roll. A later table
        // edit must not lower it or invalidate existing items when loading.
        bool savedJewelleryFloor = item is BaseJewel && state.CraftedEquipment;
        if (prefixes > perKind || suffixes > perKind || (savedJewelleryFloor
                ? state.Roll.RequiredLevel < requirement || state.Roll.RequiredLevel > 99 : state.Roll.RequiredLevel != requirement))
        {
            throw new InvalidDataException("Invalid affix prefix/suffix budget or required level.");
        }
    }
    // Saved quotes are independent of later price-band tuning.
    private static bool ValidOffer(int offer) => offer is >= 1 and <= 60000;
    public override void Serialize(IGenericWriter writer)
    {
        writer.Write(6);
        writer.Write(_starterMigrations.Count);
        foreach (var serial in _starterMigrations)
        {
            writer.Write(serial.Value);
        }
        writer.Write(_items.Count);
        foreach (var (serial, state) in _items)
        {
            writer.Write(serial.Value);
            writer.Write(state.BaseCode);
            writer.Write(state.Defense);
            writer.Write(state.StarterTeeth);
            writer.Write((byte)state.Roll.Rarity);
            writer.Write(state.Roll.ItemLevel);
            writer.Write(state.Roll.AffixLevel);
            writer.Write(state.Roll.RequiredLevel);
            writer.Write(state.Roll.Affixes.Length);
            foreach (var affix in state.Roll.Affixes)
            {
                writer.Write(affix.Definition.Id);
                foreach (var mod in affix.Modifiers)
                {
                    writer.Write(mod.Value ?? 0);
                }
            }
            writer.Write((byte)state.Tier);
            writer.Write((byte)state.Quality);
            writer.Write(state.NativeSockets);
            writer.Write(state.TinkerSockets);
            writer.Write(state.Identified);
            writer.Write(state.IdentifiedSaleOffer ?? 0);
            writer.Write(state.CraftedEquipment);
            writer.Write(state.DurabilityPercent);
            writer.Write(state.MaterialRolls.Length);
            foreach (var material in state.MaterialRolls) { writer.Write(material.Code); writer.Write(material.Value); }
            writer.Write(state.VendorPurchasePrice ?? -1);
            RuneSockets.Write(writer, state.Runes);
            GemSockets.Write(writer, state.Gems);
            writer.Write(state.NamedItemId);
            writer.Write(state.NamedSockets);
            writer.Write(state.NamedProperties.Length);
            foreach (var property in state.NamedProperties)
            { writer.Write(property.Key); writer.Write(property.Value); writer.Write(property.Maximum); }
        }
    }
    public override void Deserialize(IGenericReader reader)
    {
        int version = reader.ReadInt();
        if (version is < 0 or > 6)
        {
            throw new InvalidDataException("Unsupported equipment persistence version.");
        }
        int migrations = reader.ReadInt();
        if (migrations < 0)
        {
            throw new InvalidDataException("Invalid starter migration count.");
        }
        _starterMigrations.Clear();
        for (int i = 0; i < migrations; i++)
        {
            _starterMigrations.Add((Serial)reader.ReadUInt());
        }
        int count = reader.ReadInt();
        if (count < 0)
        {
            throw new InvalidDataException("Invalid equipment count.");
        }
        _items.Clear();
        for (int i = 0; i < count; i++)
        {
            var serial = (Serial)reader.ReadUInt();
            string basis = reader.ReadString();
            int defense = reader.ReadInt();
            bool starter = reader.ReadBool();
            var rarity = (GearRarity)reader.ReadByte();
            int itemLevel = reader.ReadInt(), affixLevel = reader.ReadInt(), requirement = reader.ReadInt();
            int length = reader.ReadInt();
            if (length is < 0 or > 6)
            {
                throw new InvalidDataException("Invalid saved affix count.");
            }
            var affixes = new RolledAffix[length];
            for (int j = 0; j < length; j++)
            {
                var source = AffixBalance.Find(reader.ReadString());
                var modifiers = new RolledModifier[source.Modifiers.Length];
                for (int k = 0; k < modifiers.Length; k++)
                {
                    var mod = source.Modifiers[k];
                    int stored=reader.ReadInt();
                    if(mod.Mode!=ModifierMode.Numeric && stored!=0){throw new InvalidDataException("Invalid ordinary range/parameter save sentinel");}
                    modifiers[k] = new(mod.Code, mod.Parameter, mod.Min, mod.Max, mod.Mode, mod.Mode==ModifierMode.Numeric?stored:null);
                }
                affixes[j] = new(source, modifiers);
            }
            var state = new GearState(basis, defense, starter, new(rarity, itemLevel, affixLevel, requirement, affixes));
            if (version >= 1)
            {
                var tier = (GearTier)reader.ReadByte();
                var quality = (CraftQuality)reader.ReadByte();
                int nativeSockets = reader.ReadInt(), tinkerSockets = reader.ReadInt();
                bool identified = reader.ReadBool();
                int offer = reader.ReadInt();
                state = state with { Tier = tier, Quality = quality, NativeSockets = nativeSockets,
                    TinkerSockets = tinkerSockets, Identified = identified, IdentifiedSaleOffer = offer == 0 ? null : offer };
            }
            if (version >= 2)
            {
                bool crafted = reader.ReadBool();
                int durability = reader.ReadInt(), countMaterials = reader.ReadInt();
                if (countMaterials is < 0 or > 2) { throw new InvalidDataException("Invalid material roll count."); }
                var materials = new MaterialRoll[countMaterials];
                for (int j = 0; j < materials.Length; j++) { materials[j] = new(reader.ReadString(), reader.ReadInt()); }
                state = state with { CraftedEquipment = crafted, DurabilityPercent = durability, MaterialRolls = materials };
            }
            if (version >= 3)
            {
                int paid = reader.ReadInt();
                if (paid < -1) { throw new InvalidDataException("Invalid saved purchase price."); }
                state = state with { VendorPurchasePrice = paid < 0 ? null : paid };
            }
            if (version >= 4) { state = state with { Runes = RuneSockets.Read(reader) }; }
            if (version >= 5) { state = state with { Gems = GemSockets.Read(reader) }; }
            if (version >= 6)
            {
                string named = reader.ReadString(); int sockets = reader.ReadInt(), countProperties = reader.ReadInt();
                if (countProperties is < 0 or > 64) { throw new InvalidDataException("Invalid named-property count."); }
                var properties = new NamedPropertyRoll[countProperties];
                for (int n = 0; n < properties.Length; n++) { properties[n] = new(reader.ReadString(), reader.ReadInt(), reader.ReadInt()); }
                state = state with { NamedItemId = named, NamedSockets = sockets, NamedProperties = properties };
            }
            _items.Add(serial, state);
        }
    }
    public override void PostDeserialize()
    {
        Prune();
        foreach (var (serial, state) in _items)
        {
            Validate(World.FindItem(serial), state);
            NamedMaintenance.Track(World.FindItem(serial));
        }
    }
}
