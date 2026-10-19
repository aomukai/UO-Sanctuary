// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using Server;
using Server.Items;
using Server.Mobiles;

namespace Sanctuary;

public sealed record GearState(string BaseCode, int Defense, bool StarterTeeth, GearRoll Roll);

public sealed class GearProfiles : GenericPersistence
{
    private readonly Dictionary<Serial, GearState> _items = new();
    private readonly HashSet<Serial> _starterMigrations = new();
    public bool BeginStarterMigration(Serial player) => _starterMigrations.Add(player);
    public GearProfiles() : base("SanctuaryEquipment", 110) { }
    public GearState Find(Serial serial) => _items.GetValueOrDefault(serial);
    public GearState Convert(Item item, int itemLevel = 1, GearRarity rarity = GearRarity.Normal, Random random = null)
    {
        if (!ClassGear.Converted(item) || itemLevel is < 1 or > 99 || !Enum.IsDefined(rarity))
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
        var state = new GearState(definition.BaseCode, defense, false, roll);
        Validate(item, state);
        _items[item.Serial] = state;
        item.InvalidateProperties();
        if (item.Parent is PlayerMobile player)
        {
            ClassGear.Changed(player);
        }
        return state;
    }
    public GearState FromMonster(Item item, int monsterLevel, GearRarity rarity, Random random = null)
        => Convert(item, monsterLevel, rarity, random);
    internal GearState PrepareImbue(Item item, GearRarity rarity, Random random)
    {
        var basis = ClassGear.Base(item);
        var before = Find(item.Serial);
        if (basis == null || before is { StarterTeeth: true }
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
        var state = new GearState(ClassGear.Definition(item).BaseCode, before?.Defense ?? ClassGear.Defense(item), false, roll);
        Validate(item, state);
        return state;
    }
    internal void ApplyImbue(Item item, GearState state)
    {
        _items[item.Serial] = state;
        item.InvalidateProperties();
    }
    public void MarkStarter(Dagger dagger)
    {
        var state = Find(dagger.Serial) ?? Convert(dagger);
        if (state.Roll.Rarity != GearRarity.Normal)
        {
            throw new InvalidOperationException("Only the normal starter base may receive the starter skill grant.");
        }
        _items[dagger.Serial] = state with { StarterTeeth = true };
        dagger.InvalidateProperties();
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
        var definition = ClassGear.Definition(item) ?? throw new InvalidDataException("Unknown saved gear type.");
        if (state.BaseCode != definition.BaseCode || state.Roll.ItemLevel is < 1 or > 99 || !Enum.IsDefined(state.Roll.Rarity)
            || state.StarterTeeth && (item is not Dagger || state.Roll.Rarity != GearRarity.Normal))
        {
            throw new InvalidDataException("Invalid converted equipment identity or level.");
        }
        if (ClassGear.Slot(item) == GearSlot.Cosmetic)
        {
            if (state.Defense != 0 || state.Roll.Rarity != GearRarity.Normal || state.Roll.Affixes.Length != 0)
            {
                throw new InvalidDataException("Cosmetic UO slots cannot contribute gear stats.");
            }
            return;
        }
        var basis = EquipmentBalance.Base(state.BaseCode);
        int affixLevel = AffixBalance.Level(state.Roll.ItemLevel, basis.QualityLevel, basis.MagicLevel);
        int limit = state.Roll.Rarity switch { GearRarity.Normal => 0, GearRarity.Magic => 2, _ => 6 };
        if (state.Defense < basis.MinDefense || state.Defense > basis.MaxDefense || state.Roll.AffixLevel != affixLevel
            || state.Roll.Affixes.Length > limit || state.Roll.Rarity != GearRarity.Normal && state.Roll.Affixes.Length == 0)
        {
            throw new InvalidDataException("Invalid converted base defence or affix count.");
        }
        var groups = new HashSet<int>();
        int prefixes = 0, suffixes = 0, requirement = basis.RequiredLevel;
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
                    || rolled.Min != mod.Min || rolled.Max != mod.Max || rolled.Value == null
                    || rolled.Value < mod.Min || rolled.Value > mod.Max)
                {
                    throw new InvalidDataException("Saved affix roll escaped its source range.");
                }
            }
        }
        int perKind = state.Roll.Rarity == GearRarity.Magic ? 1 : 3;
        if (prefixes > perKind || suffixes > perKind || state.Roll.RequiredLevel != requirement)
        {
            throw new InvalidDataException("Invalid affix prefix/suffix budget or required level.");
        }
    }
    public override void Serialize(IGenericWriter writer)
    {
        writer.Write(0);
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
        }
    }
    public override void Deserialize(IGenericReader reader)
    {
        if (reader.ReadInt() != 0)
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
                    modifiers[k] = new(mod.Code, mod.Parameter, mod.Min, mod.Max, mod.Mode, reader.ReadInt());
                }
                affixes[j] = new(source, modifiers);
            }
            _items.Add(serial, new(basis, defense, starter, new(rarity, itemLevel, affixLevel, requirement, affixes)));
        }
    }
    public override void PostDeserialize()
    {
        Prune();
        foreach (var (serial, state) in _items)
        {
            Validate(World.FindItem(serial), state);
        }
    }
}
