// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using Server;
using Server.Items;

namespace Sanctuary;

public sealed record RepairTier(GearTier Tier, int Min, int Max, int Scraps, int Gold);
public sealed record RepairSkill(double Minimum, int LossChance, int LossMin, int LossMax, int SavingChance);
public sealed record RepairSettings(RepairTier[] Tiers, RepairSkill[] Skills, int RareEssenceMultiplier,
    int UniqueEssenceMultiplier, int MinimumMaximumDurability, int EssencePerCharge, double NpcSkill,
    double ToolRequiredSkill, int HammerIngots, int HammerScraps, int KitIngots, int KitStrips);
public sealed record RepairPrice(int Hits, int Maximum, GearMaterial Material, CraftResource Resource,
    GearTier Tier, GearRarity Rarity, int Processed, int Essence);

public sealed class RepairProfiles : GenericPersistence
{
    private readonly Dictionary<Serial, RepairPrice> _prices = new();
    private readonly Dictionary<Serial, int> _capacities = new();
    public RepairProfiles() : base("SanctuaryRepairs", 122) { }
    public RepairPrice Price(Item item) => _prices.GetValueOrDefault(item.Serial);
    public void Store(Item item, RepairPrice price) => _prices[item.Serial] = price;
    public void Forget(Item item) => _prices.Remove(item.Serial);
    public int? Capacity(Item item) => _capacities.TryGetValue(item.Serial, out int count) ? count : null;
    public void Capture(BaseWand item, int count)
    {
        if (count is < 1 or > 60000) { return; }
        _capacities[item.Serial] = count;
    }
    public void Copy(Item source, Item copy)
    {
        if (copy is BaseWand wand && source is BaseWand)
        {
            Capture(wand, EquipmentRepairs.Capacity((BaseWand)source));
            wand.Charges = ((BaseWand)source).Charges;
        }
        if (Price(source) is { } price) { Store(copy, price); }
    }
    public void Prune()
    {
        var removed = new List<Serial>();
        foreach (var serial in _prices.Keys)
        {
            if (World.FindItem(serial) is not { Deleted: false }) { removed.Add(serial); }
        }
        foreach (var serial in removed) { _prices.Remove(serial); }
        removed.Clear();
        foreach (var serial in _capacities.Keys)
        {
            if (World.FindItem(serial) is not BaseWand { Deleted: false }) { removed.Add(serial); }
        }
        foreach (var serial in removed) { _capacities.Remove(serial); }
    }
    public override void Serialize(IGenericWriter writer)
    {
        writer.Write(0); writer.Write(_prices.Count);
        foreach (var (serial, price) in _prices)
        {
            writer.Write(serial.Value); writer.Write(price.Hits); writer.Write(price.Maximum);
            writer.Write((byte)price.Material); writer.Write((int)price.Resource); writer.Write((byte)price.Tier);
            writer.Write((byte)price.Rarity); writer.Write(price.Processed); writer.Write(price.Essence);
        }
        writer.Write(_capacities.Count);
        foreach (var (serial, capacity) in _capacities) { writer.Write(serial.Value); writer.Write(capacity); }
    }
    public override void Deserialize(IGenericReader reader)
    {
        if (reader.ReadInt() != 0) { throw new InvalidDataException("Unknown repair persistence version."); }
        _prices.Clear(); _capacities.Clear();
        int count = Count(reader);
        for (int i = 0; i < count; i++)
        {
            var serial = (Serial)reader.ReadUInt();
            var price = new RepairPrice(reader.ReadInt(), reader.ReadInt(), (GearMaterial)reader.ReadByte(),
                (CraftResource)reader.ReadInt(), (GearTier)reader.ReadByte(), (GearRarity)reader.ReadByte(), reader.ReadInt(), reader.ReadInt());
            if (!Enum.IsDefined(price.Material) || !Enum.IsDefined(price.Resource) || !Enum.IsDefined(price.Tier)
                || !Enum.IsDefined(price.Rarity) || price.Maximum is < 1 or > 65535 || price.Hits < 0 || price.Hits >= price.Maximum
                || price.Processed is < 1 or > 60000 || price.Essence is < 0 or > 60000)
            { throw new InvalidDataException("Invalid saved repair price."); }
            _prices.Add(serial, price);
        }
        count = Count(reader);
        for (int i = 0; i < count; i++)
        {
            var serial = (Serial)reader.ReadUInt(); int capacity = reader.ReadInt();
            if (capacity is < 1 or > 60000) { throw new InvalidDataException("Invalid saved charge capacity."); }
            _capacities.Add(serial, capacity);
        }
    }
    private static int Count(IGenericReader reader)
    {
        int count = reader.ReadInt();
        if (count is < 0 or > 10000000) { throw new InvalidDataException("Invalid repair record count."); }
        return count;
    }
    public override void PostDeserialize() => Prune();
}
