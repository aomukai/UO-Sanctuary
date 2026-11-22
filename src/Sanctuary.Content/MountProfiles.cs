// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using Server;

namespace Sanctuary;

internal sealed record MountRecord(Serial Owner, string Stable, int Price, DateTime AbandonAt, DateTime BondAt);
internal sealed class MountProfiles() : GenericPersistence("SanctuaryMounts", 134)
{
    internal readonly Dictionary<Serial, MountRecord> Mounts = new();
    internal readonly Dictionary<Serial, long> Proceeds = new();
    private KeyValuePair<Serial, MountRecord>[] _mounts = [];
    private KeyValuePair<Serial, long>[] _proceeds = [];

    public override void Serialize()
    {
        _mounts = [.. Mounts];
        _proceeds = [.. Proceeds];
        base.Serialize();
    }

    public override void Serialize(IGenericWriter writer)
    {
        writer.Write(0);
        writer.Write(_mounts.Length);
        foreach (var (id, record) in _mounts)
        {
            writer.Write(id.Value);
            writer.Write(record.Owner.Value);
            writer.Write(record.Stable);
            writer.Write(record.Price);
            writer.Write(record.AbandonAt);
            writer.Write(record.BondAt);
        }
        writer.Write(_proceeds.Length);
        foreach (var (owner, gold) in _proceeds)
        {
            writer.Write(owner.Value);
            writer.Write(gold);
        }
    }

    public override void Deserialize(IGenericReader reader)
    {
        if (reader.ReadInt() != 0)
            throw new InvalidOperationException("Unknown mount save version.");

        int count = reader.ReadInt();
        for (int i = 0; i < count; i++)
        {
            var id = (Serial)reader.ReadUInt();
            Mounts.Add(id, new((Serial)reader.ReadUInt(), reader.ReadString(), reader.ReadInt(),
                reader.ReadDateTime(), reader.ReadDateTime()));
        }
        count = reader.ReadInt();
        for (int i = 0; i < count; i++)
            Proceeds.Add((Serial)reader.ReadUInt(), reader.ReadLong());
    }
}
