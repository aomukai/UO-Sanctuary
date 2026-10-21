// SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
using System.IO;
using Server;

namespace Sanctuary;

public sealed class HousingProfiles : GenericPersistence
{
    private Dictionary<string, Serial> _architects = new();
    public HousingProfiles() : base("SanctuaryHousing", 133) { }
    public Serial Architect(string hall) => _architects.GetValueOrDefault(hall);
    public void Architect(string hall, Serial architect) => _architects[hall] = architect;
    public override void Serialize(IGenericWriter writer)
    {
        writer.Write(0); writer.Write(_architects.Count);
        foreach (var (hall, architect) in _architects) { writer.Write(hall); writer.Write(architect.Value); }
    }
    public override void Deserialize(IGenericReader reader)
    {
        if (reader.ReadInt() != 0) { throw new InvalidDataException("Unknown housing persistence version."); }
        int count = reader.ReadInt();
        if (count is < 0 or > 1000) { throw new InvalidDataException("Invalid architect count."); }
        var decoded = new Dictionary<string, Serial>();
        for (int i = 0; i < count; i++)
        {
            string hall = reader.ReadString(); var serial = (Serial)reader.ReadUInt();
            if (string.IsNullOrWhiteSpace(hall) || !serial.IsMobile || !decoded.TryAdd(hall, serial))
            { throw new InvalidDataException("Invalid saved architect identity."); }
        }
        _architects = decoded;
    }
}
