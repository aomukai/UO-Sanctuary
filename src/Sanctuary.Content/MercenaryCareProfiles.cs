// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using Server;

namespace Sanctuary;

public sealed class MercenaryCareProfiles : GenericPersistence
{
    private KeyValuePair<Serial, MercenaryCareState>[] _snapshot = [];
    public MercenaryCareProfiles() : base("SanctuaryMercenaryCare", 132) { }
    internal void Capture()
    {
        _snapshot = new KeyValuePair<Serial, MercenaryCareState>[MercenaryCare.States.Count]; int i = 0;
        foreach (var (serial, state) in MercenaryCare.States)
        { _snapshot[i++] = new(serial, state with { Healing = (long[])state.Healing.Clone() }); }
    }
    public override void Serialize() { Capture(); base.Serialize(); }
    public override void Serialize(IGenericWriter writer)
    {
        writer.Write(0); writer.Write(_snapshot.Length);
        foreach (var (serial, state) in _snapshot)
        {
            writer.Write(serial.Value); writer.Write(state.Owner.Value); writer.Write(state.Level); writer.Write(state.Retired);
            foreach (long amount in state.Healing) { writer.Write(amount); } writer.Write(state.NextDrinkUtc);
        }
    }
    public override void Deserialize(IGenericReader reader)
    {
        if (reader.ReadInt() != 0) { throw new InvalidDataException("Unsupported mercenary care version."); }
        int count = reader.ReadInt(); if (count is < 0 or > 1000000) { throw new InvalidDataException("Invalid mercenary count."); }
        var restored = new Dictionary<Serial, MercenaryCareState>(); var active = new HashSet<Serial>();
        for (int i = 0; i < count; i++)
        {
            var serial = (Serial)reader.ReadUInt(); var owner = (Serial)reader.ReadUInt(); int level = reader.ReadInt(); bool retired = reader.ReadBool();
            var healing = new long[5]; long total = 0;
            for (int tier = 0; tier < 5; tier++)
            {
                long amount = reader.ReadLong();
                if (amount < 0 || total > long.MaxValue - amount) { throw new InvalidDataException("Invalid mercenary reserve."); }
                healing[tier] = amount; total += amount;
            }
            var next = reader.ReadDateTime();
            if (serial == Serial.Zero || owner == Serial.Zero || level is < 1 or > 99 || retired && total != 0
                || !retired && !active.Add(owner) || !restored.TryAdd(serial, new(owner, level, retired, healing, next)))
            { throw new InvalidDataException("Invalid mercenary contract."); }
        }
        MercenaryCare.States.Clear(); foreach (var row in restored) { MercenaryCare.States.Add(row.Key, row.Value); }
    }
}
