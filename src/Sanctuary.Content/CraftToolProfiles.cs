// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using Server;
using Server.Items;

namespace Sanctuary;

public sealed record CraftToolState(CraftQuality Quality, bool Crafted, int Capacity);

public sealed class CraftToolProfiles : GenericPersistence
{
    private readonly Dictionary<Serial, CraftToolState> _tools = new();
    public CraftToolProfiles() : base("SanctuaryCraftTools", 112) { }
    public CraftToolState Find(Item item) => item == null ? null : _tools.GetValueOrDefault(item.Serial);
    public void Store(BaseTool tool, CraftToolState state)
    {
        if (!Enum.IsDefined(state.Quality) || state.Capacity < 1 || state.Capacity > 10000)
        { throw new InvalidDataException("Invalid craft tool state."); }
        _tools[tool.Serial] = state;
        tool.InvalidateProperties();
    }
    public void Copy(Item source, Item copy)
    {
        if (copy is BaseTool tool && Find(source) is { } state) { Store(tool, state); }
    }
    public void Prune()
    {
        var removed = new List<Serial>();
        foreach (var serial in _tools.Keys)
        {
            if (World.FindItem(serial) is not BaseTool { Deleted: false }) { removed.Add(serial); }
        }
        foreach (var serial in removed) { _tools.Remove(serial); }
    }
    public override void Serialize(IGenericWriter writer)
    {
        writer.Write(0); writer.Write(_tools.Count);
        foreach (var (serial, state) in _tools)
        {
            writer.Write(serial.Value); writer.Write((byte)state.Quality);
            writer.Write(state.Crafted); writer.Write(state.Capacity);
        }
    }
    public override void Deserialize(IGenericReader reader)
    {
        if (reader.ReadInt() != 0) { throw new InvalidDataException("Unsupported craft tool save."); }
        int count = reader.ReadInt();
        if (count < 0) { throw new InvalidDataException("Invalid craft tool count."); }
        _tools.Clear();
        for (int i = 0; i < count; i++)
        {
            var serial = (Serial)reader.ReadUInt();
            var state = new CraftToolState((CraftQuality)reader.ReadByte(), reader.ReadBool(), reader.ReadInt());
            if (!Enum.IsDefined(state.Quality) || state.Capacity < 1 || state.Capacity > 10000)
            { throw new InvalidDataException("Invalid saved craft tool."); }
            _tools.Add(serial, state);
        }
    }
    public override void PostDeserialize() => Prune();
}
