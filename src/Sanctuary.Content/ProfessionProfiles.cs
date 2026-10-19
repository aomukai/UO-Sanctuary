// SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
using System.IO;
using Server;

namespace Sanctuary;

public sealed record ProfessionState(SkillName Craft, bool Confirmed);

public sealed class ProfessionProfiles : GenericPersistence
{
    private readonly Dictionary<Serial, ProfessionState> _profiles = new();
    private static readonly ProfessionState _initial = new(SkillName.Alchemy, false);
    public ProfessionProfiles() : base("SanctuaryProfessions", 111) { }
    public ProfessionState Find(Mobile owner) => _profiles.GetValueOrDefault(owner.Serial) ?? _initial;
    public bool Confirm(Mobile owner, SkillName skill)
    {
        if (!TradeSkills.IsCraft((int)skill) || Find(owner).Confirmed)
        {
            return false;
        }
        _profiles[owner.Serial] = new(skill, true);
        return true;
    }
    public void Prune()
    {
        var removed = new List<Serial>();
        foreach (var serial in _profiles.Keys)
        {
            if (World.FindMobile(serial) is not { Deleted: false }) { removed.Add(serial); }
        }
        foreach (var serial in removed) { _profiles.Remove(serial); }
    }
    public override void Serialize(IGenericWriter writer)
    {
        writer.Write(0);
        writer.Write(_profiles.Count);
        foreach (var (owner, state) in _profiles)
        {
            writer.Write(owner.Value);
            writer.Write((byte)state.Craft);
            writer.Write(state.Confirmed);
        }
    }
    public override void Deserialize(IGenericReader reader)
    {
        if (reader.ReadInt() != 0) { throw new InvalidDataException("Unsupported profession save version."); }
        int count = reader.ReadInt();
        if (count < 0) { throw new InvalidDataException("Invalid profession count."); }
        _profiles.Clear();
        for (int i = 0; i < count; i++)
        {
            var owner = (Serial)reader.ReadUInt();
            var craft = (SkillName)reader.ReadByte();
            bool confirmed = reader.ReadBool();
            if (!TradeSkills.IsCraft((int)craft) || !confirmed)
            {
                throw new InvalidDataException("Invalid saved profession selection.");
            }
            _profiles.Add(owner, new(craft, confirmed));
        }
    }
    public override void PostDeserialize() => Prune();
}
