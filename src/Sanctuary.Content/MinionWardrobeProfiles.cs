// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using Server;
using Server.Mobiles;

namespace Sanctuary;

public sealed record ClassMinionState(uint Owner, DiabloClass Class, int Slots, bool Parked, DateTime? ExpiresUtc);
public sealed class MinionWardrobeProfiles : GenericPersistence
{
    private Dictionary<Serial, ClassMinionState> _pets = new();
    public MinionWardrobeProfiles() : base("SanctuaryClassMinions", 131) { }
    internal ClassMinionState Find(BaseCreature pet) => _pets.GetValueOrDefault(pet.Serial);
    internal ClassMinionState Track(BaseCreature pet)
    {
        if (PersistentMinions.Behavior(pet) is not{} minion || pet.ControlMaster is not PlayerMobile owner) { return null; }
        if (!_pets.TryGetValue(pet.Serial, out var state))
        {
            state = new(owner.Serial.Value, minion.SummonClass, pet.ControlSlots, false, minion.ExpiresUtc);
            _pets.Add(pet.Serial, state);
        }
        if (state.Owner != owner.Serial.Value || state.Class != minion.SummonClass) { return null; }
        if (!state.Parked && state.Slots != pet.ControlSlots) { _pets[pet.Serial] = state = state with { Slots = pet.ControlSlots }; }
        return state;
    }
    internal void Store(BaseCreature pet, ClassMinionState state) => _pets[pet.Serial] = state;
    internal void Sweep(DateTime now)
    {
        var removed = new List<Serial>();
        foreach (var (serial, state) in _pets)
        {
            var pet = World.FindMobile(serial) as BaseCreature;
            if (pet is not { Deleted: false, Alive: true }) { removed.Add(serial); continue; }
            if (World.FindMobile((Serial)state.Owner) is not PlayerMobile { Deleted: false, Alive: true } owner
                || pet.ControlMaster != owner || !ClassCreation.Profiles.HasHistory(owner.Serial, state.Class)
                || state.ExpiresUtc is { } expires && now >= expires)
            { pet.Delete(); removed.Add(serial); }
        }
        foreach (var serial in removed) { _pets.Remove(serial); }
    }
    public override void Serialize(IGenericWriter writer)
    {
        writer.Write(0); writer.Write(_pets.Count);
        foreach (var (serial, state) in _pets)
        {
            writer.Write(serial.Value); writer.Write(state.Owner); writer.Write((byte)state.Class); writer.Write(state.Slots); writer.Write(state.Parked);
            writer.Write(state.ExpiresUtc.HasValue); if (state.ExpiresUtc is { } expires) { writer.Write(expires.Ticks); }
        }
    }
    public override void Deserialize(IGenericReader reader)
    {
        if (reader.ReadInt() != 0) { throw new InvalidDataException("Unsupported class minion data."); }
        int count = reader.ReadInt(); if (count is < 0 or > 1000000) { throw new InvalidDataException("Invalid class minion count."); }
        var pets = new Dictionary<Serial, ClassMinionState>();
        for (int i = 0; i < count; i++)
        {
            var serial = (Serial)reader.ReadUInt(); uint owner = reader.ReadUInt(); byte id = reader.ReadByte();
            int slots = reader.ReadInt(); bool parked = reader.ReadBool(); DateTime? expires = reader.ReadBool() ? new DateTime(reader.ReadLong(), DateTimeKind.Utc) : null;
            if (!serial.IsMobile || !((Serial)owner).IsMobile || !DiabloClasses.TryFromWire(id, out var characterClass) || slots is < 0 or > 1000
                || !pets.TryAdd(serial, new(owner, characterClass, slots, parked, expires))) { throw new InvalidDataException("Invalid class minion identity."); }
        }
        _pets = pets;
    }
    public override void PostDeserialize()
    {
        Sweep(DateTime.UtcNow);
        foreach (var (serial, state) in new Dictionary<Serial, ClassMinionState>(_pets))
        {
            if (World.FindMobile(serial) is not BaseCreature pet || PersistentMinions.Behavior(pet) is not{} minion || minion.SummonClass != state.Class
                || state.Parked && pet.ControlSlots != 0) { throw new InvalidDataException("Invalid saved parked minion."); }
            PersistentMinions.Restore(pet);
        }
    }
}
