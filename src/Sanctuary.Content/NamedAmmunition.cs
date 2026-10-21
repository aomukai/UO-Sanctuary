// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using Server;

namespace Sanctuary;

public sealed record NamedQuantity(int Remaining, DateTime DueUtc);

// A named throwing weapon stays one native item; its charges cannot be split into duplicate named weapons.
public static class NamedAmmunition
{
    internal static readonly Dictionary<Serial, NamedQuantity> Quantities = new();
    private static readonly List<Serial> _keys = new();
    private static readonly DateTime _paused = new(DateTime.MaxValue.Ticks, DateTimeKind.Utc);
    public static NamedAmmunitionProfiles Profiles { get; private set; }
    public static void Configure() => Profiles = new();
    public static void Initialize() => Timer.StartTimer(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1), () => Tick(TimedEffects.Now));
    public static int Maximum(Item item)
    {
        if (ClassGear.State(item) is not { NamedItemId: not null } state) { return 0; }
        var basis = NamedGear.Catalog.Base(state.BaseCode);
        if (basis.SourceTable != "weapons" || !basis.SourceFields.TryGetProperty("maxstack", out var cap)
            || !cap.TryGetInt32(out int maximum) || maximum <= 0) { return 0; }
        return maximum + Property(state, "stack");
    }
    private static int Property(GearState state, string code)
    {
        foreach (var property in NamedGear.Catalog.Item(state.NamedItemId).Properties)
        {
            if (!property.SourceEnabled || property.Code != code) { continue; }
            foreach (var roll in state.NamedProperties) { if (roll.Key == "p/" + property.Slot) { return roll.Value; } }
        }
        return 0;
    }
    public static int Rate(Item item) => ClassGear.State(item) is { NamedItemId: not null } state ? Property(state, "rep-quant") : 0;
    public static int Remaining(Item item) => item == null ? 0 : Quantities.GetValueOrDefault(item.Serial)?.Remaining ?? Maximum(item);
    public static bool CanAttack(Item item) => item == null || Maximum(item) == 0 || Remaining(item) > 0;
    public static void Track(Item item)
    {
        int maximum = Maximum(item);
        if (item.Deleted || maximum <= 0) { Quantities.Remove(item.Serial); return; }
        Quantities.TryAdd(item.Serial, new(maximum, _paused));
    }
    public static bool Consume(Item item, DateTime now)
    {
        if (item == null || Maximum(item) == 0) { return true; }
        Track(item); Settle(item, now);
        var quantity = Quantities[item.Serial];
        if (quantity.Remaining == 0) { return false; }
        int rate = Rate(item);
        var due = quantity.DueUtc == _paused && rate > 0 ? now.AddMilliseconds(NamedMaintenance.PeriodMs(rate)) : quantity.DueUtc;
        Quantities[item.Serial] = new(quantity.Remaining - 1, due); item.InvalidateProperties(); return true;
    }
    public static void Copy(Item source, Item destination)
    {
        if (Quantities.TryGetValue(source.Serial, out var quantity)) { Quantities[destination.Serial] = quantity with { }; }
    }
    // Called only after the repair transaction validates and pays its quoted resources.
    internal static void Restore(Item item, int amount)
    {
        Track(item);
        var quantity = Quantities[item.Serial]; int remaining = quantity.Remaining + amount;
        if (amount <= 0 || remaining > Maximum(item)) { throw new InvalidOperationException("Invalid throwing refill."); }
        Quantities[item.Serial] = quantity with { Remaining = remaining, DueUtc = remaining == Maximum(item) ? _paused : quantity.DueUtc };
        item.InvalidateProperties();
    }
    public static void Tick(DateTime now)
    {
        _keys.Clear(); foreach (var serial in Quantities.Keys) { _keys.Add(serial); }
        foreach (var serial in _keys)
        {
            if (World.FindItem(serial) is { Deleted: false } item) { Settle(item, now); }
            else { Quantities.Remove(serial); }
        }
    }
    public static void Settle(Item item, DateTime now)
    {
        if (!Quantities.TryGetValue(item.Serial, out var quantity) || quantity.DueUtc > now) { return; }
        int maximum = Maximum(item), rate = Rate(item);
        if (maximum <= 0 || item.Deleted) { Quantities.Remove(item.Serial); return; }
        if (rate <= 0) { Quantities[item.Serial] = quantity with { DueUtc = _paused }; return; }
        long period = NamedMaintenance.PeriodMs(rate) * TimeSpan.TicksPerMillisecond;
        long intervals = (now.Ticks - quantity.DueUtc.Ticks) / period + 1;
        int remaining = quantity.Remaining + (int)Math.Min(maximum - quantity.Remaining, intervals);
        Quantities[item.Serial] = new(remaining, remaining == maximum ? _paused : quantity.DueUtc.AddTicks(period * intervals));
        item.InvalidateProperties();
    }
}

public sealed class NamedAmmunitionProfiles : GenericPersistence
{
    private KeyValuePair<Serial, NamedQuantity>[] _snapshot = [];
    public NamedAmmunitionProfiles() : base("SanctuaryNamedAmmunition", 131) { }
    internal void Capture()
    {
        _snapshot = new KeyValuePair<Serial, NamedQuantity>[NamedAmmunition.Quantities.Count];
        int i = 0; foreach (var row in NamedAmmunition.Quantities) { _snapshot[i++] = row; }
    }
    public override void Serialize() { Capture(); base.Serialize(); }
    public override void Serialize(IGenericWriter writer)
    {
        writer.Write(0); writer.Write(_snapshot.Length);
        foreach (var (serial, quantity) in _snapshot)
        { writer.Write(serial.Value); writer.Write(quantity.Remaining); writer.Write(quantity.DueUtc); }
    }
    public override void Deserialize(IGenericReader reader)
    {
        if (reader.ReadInt() != 0) { throw new InvalidDataException("Unsupported named ammunition version."); }
        int count = reader.ReadInt(); if (count is < 0 or > 1000000) { throw new InvalidDataException("Invalid named ammunition count."); }
        var restored = new Dictionary<Serial, NamedQuantity>();
        for (int i = 0; i < count; i++)
        {
            var serial = (Serial)reader.ReadUInt(); int remaining = reader.ReadInt(); var due = reader.ReadDateTime();
            if (serial == Serial.Zero || remaining is < 0 or > 10000 || !restored.TryAdd(serial, new(remaining, due)))
            { throw new InvalidDataException("Invalid named ammunition entry."); }
        }
        NamedAmmunition.Quantities.Clear(); foreach (var row in restored) { NamedAmmunition.Quantities.Add(row.Key, row.Value); }
    }
    public override void PostDeserialize()
    {
        foreach (var (serial, quantity) in NamedAmmunition.Quantities)
        {
            if (World.FindItem(serial) is { Deleted: false } item && (NamedAmmunition.Maximum(item) <= 0 || quantity.Remaining > NamedAmmunition.Maximum(item)))
            { throw new InvalidDataException("Named ammunition escaped its source capacity."); }
        }
        NamedAmmunition.Tick(TimedEffects.Now);
    }
}
