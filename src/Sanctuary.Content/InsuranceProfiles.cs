// SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
using System.IO;
using Server;

namespace Sanctuary;

public sealed record EquipmentPolicy(Serial Owner, bool Paid, bool Active);

public sealed class InsuranceProfiles : GenericPersistence
{
    private Dictionary<Serial, EquipmentPolicy> _policies = new();
    public InsuranceProfiles() : base("SanctuaryInsurance", 134) { }
    public EquipmentPolicy Find(Serial item) => _policies.GetValueOrDefault(item);
    public void Store(Item item, EquipmentPolicy policy)
    {
        if (!policy.Paid) { _policies.Remove(item.Serial); }
        else { _policies[item.Serial] = policy; }
        item.PaidInsurance = policy.Paid;
        item.Insured = policy.Active && policy.Paid;
    }
    public void Remove(Item item)
    {
        _policies.Remove(item.Serial);
        item.Insured = item.PaidInsurance = false;
    }
    public override void Serialize(IGenericWriter writer)
    {
        var saved = new List<KeyValuePair<Serial, EquipmentPolicy>>();
        foreach (var pair in _policies)
        {
            if (World.FindItem(pair.Key) is { Deleted: false } && World.FindMobile(pair.Value.Owner) is { Deleted: false })
            { saved.Add(pair); }
        }
        writer.Write(0); writer.Write(saved.Count);
        foreach (var (serial, policy) in saved)
        { writer.Write(serial.Value); writer.Write(policy.Owner.Value); writer.Write(policy.Paid); writer.Write(policy.Active); }
    }
    public override void Deserialize(IGenericReader reader)
    {
        if (reader.ReadInt() != 0) { throw new InvalidDataException("Unknown insurance persistence version."); }
        int count = reader.ReadInt();
        if (count is < 0 or > 1000000) { throw new InvalidDataException("Invalid insurance policy count."); }
        var decoded = new Dictionary<Serial, EquipmentPolicy>();
        for (int i = 0; i < count; i++)
        {
            var item = (Serial)reader.ReadUInt(); var owner = (Serial)reader.ReadUInt();
            bool paid = reader.ReadBool(), active = reader.ReadBool();
            if (!item.IsItem || !owner.IsMobile || !paid || !decoded.TryAdd(item, new(owner, paid, active)))
            { throw new InvalidDataException("Invalid saved insurance policy."); }
        }
        _policies = decoded;
    }
}
