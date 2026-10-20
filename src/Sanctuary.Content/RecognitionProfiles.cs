// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using Server;

namespace Sanctuary;

[Flags]
public enum RecognitionHonor : byte { None = 0, Hero = 1, Pillar = 2, Both = Hero | Pillar }
public sealed record RecognitionState(RecognitionHonor Eligible = 0, RecognitionHonor Earned = 0,
    RecognitionHonor Certificates = 0, bool Wardrobe = false);
public sealed record RecognitionOffice(Serial Registrar, Serial Wardrobe);

public sealed class RecognitionProfiles : GenericPersistence
{
    private readonly Dictionary<Serial, RecognitionState> _players = new();
    private readonly Dictionary<string, RecognitionOffice> _offices = new(StringComparer.Ordinal);
    private static readonly RecognitionState _initial = new();
    public RecognitionProfiles() : base("SanctuaryRecognition", 123) { }
    public RecognitionState Find(Mobile player) => _players.GetValueOrDefault(player.Serial) ?? _initial;
    public void Store(Mobile player, RecognitionState state) => _players[player.Serial] = state;
    public RecognitionOffice Office(string site) => _offices.GetValueOrDefault(site);
    public void Office(string site, RecognitionOffice office) => _offices[site] = office;
    public void Prune()
    {
        var removed = new List<Serial>();
        foreach (var serial in _players.Keys)
        {
            if (World.FindMobile(serial) is not { Deleted: false }) { removed.Add(serial); }
        }
        foreach (var serial in removed) { _players.Remove(serial); }
    }
    public override void Serialize(IGenericWriter writer)
    {
        writer.Write(0); writer.Write(_players.Count);
        foreach (var (serial, state) in _players)
        {
            writer.Write(serial.Value); writer.Write((byte)state.Eligible); writer.Write((byte)state.Earned);
            writer.Write((byte)state.Certificates); writer.Write(state.Wardrobe);
        }
        writer.Write(_offices.Count);
        foreach (var (id, office) in _offices)
        { writer.Write(id); writer.Write(office.Registrar.Value); writer.Write(office.Wardrobe.Value); }
    }
    public override void Deserialize(IGenericReader reader)
    {
        if (reader.ReadInt() != 0) { throw new InvalidDataException("Unknown recognition persistence version."); }
        _players.Clear(); _offices.Clear();
        int count = Count(reader);
        for (int i = 0; i < count; i++)
        {
            var serial = (Serial)reader.ReadUInt();
            var state = new RecognitionState((RecognitionHonor)reader.ReadByte(), (RecognitionHonor)reader.ReadByte(),
                (RecognitionHonor)reader.ReadByte(), reader.ReadBool());
            if ((state.Eligible & ~RecognitionHonor.Both) != 0 || (state.Earned & ~state.Eligible) != 0
                || (state.Certificates & ~state.Earned) != 0 || state.Wardrobe && state.Earned == 0)
            { throw new InvalidDataException("Invalid recognition claim."); }
            _players.Add(serial, state);
        }
        count = Count(reader);
        for (int i = 0; i < count; i++)
        { _offices.Add(reader.ReadString(), new((Serial)reader.ReadUInt(), (Serial)reader.ReadUInt())); }
    }
    private static int Count(IGenericReader reader)
    {
        int count = reader.ReadInt();
        if (count is < 0 or > 10000000) { throw new InvalidDataException("Invalid recognition record count."); }
        return count;
    }
    public override void PostDeserialize() => Prune();
}
