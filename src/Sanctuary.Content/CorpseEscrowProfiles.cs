// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using Server;

namespace Sanctuary;

public sealed record CorpseClaim(Serial Corpse, Serial Owner, DateTime Death, Point3D Location, int Facet, Serial Storage);

public sealed class CorpseEscrowProfiles : GenericPersistence
{
    private Dictionary<Serial, CorpseClaim> _claims = new();
    public CorpseEscrowProfiles() : base("SanctuaryCorpseEscrow", 135) { }
    public IEnumerable<CorpseClaim> All => _claims.Values;
    public CorpseClaim Find(Serial corpse) => _claims.GetValueOrDefault(corpse);
    public void Store(CorpseClaim claim) => _claims[claim.Corpse] = claim;
    public void Remove(Serial corpse) => _claims.Remove(corpse);
    public override void Serialize(IGenericWriter writer)
    {
        writer.Write(0); writer.Write(_claims.Count);
        foreach (var claim in _claims.Values)
        {
            writer.Write(claim.Corpse.Value); writer.Write(claim.Owner.Value); writer.Write(claim.Death);
            writer.Write(claim.Location); writer.Write(claim.Facet); writer.Write(claim.Storage.Value);
        }
    }
    public override void Deserialize(IGenericReader reader)
    {
        if (reader.ReadInt() != 0) { throw new InvalidDataException("Unknown corpse escrow version."); }
        int count = reader.ReadInt();
        if (count is < 0 or > 1000000) { throw new InvalidDataException("Invalid corpse claim count."); }
        var decoded = new Dictionary<Serial, CorpseClaim>();
        for (int i = 0; i < count; i++)
        {
            var corpse = (Serial)reader.ReadUInt(); var owner = (Serial)reader.ReadUInt(); var death = reader.ReadDateTime();
            var location = reader.ReadPoint3D(); int facet = reader.ReadInt(); var storage = (Serial)reader.ReadUInt();
            if (!corpse.IsItem || !owner.IsMobile || storage.Value != 0 && !storage.IsItem || facet is < 0 or > 255
                || death == DateTime.MinValue || death > DateTime.MaxValue - TimeSpan.FromDays(1)
                || !decoded.TryAdd(corpse, new(corpse, owner, death, location, facet, storage)))
            { throw new InvalidDataException("Invalid corpse escrow claim."); }
        }
        _claims = decoded;
    }
}
