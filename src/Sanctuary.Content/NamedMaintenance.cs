// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using Server;
using Server.Items;

namespace Sanctuary;

// Serial-only registration follows the equipment ledger, including stored and traded items.
public static class NamedMaintenance
{
    internal static readonly Dictionary<Serial, DateTime> Clocks = new();
    private static readonly List<Serial> _keys = new();
    private static readonly DateTime _paused = new(DateTime.MaxValue.Ticks, DateTimeKind.Utc);
    public static NamedMaintenanceProfiles Profiles { get; private set; }
    public static void Configure() => Profiles = new();
    public static void Initialize() => Timer.StartTimer(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1), () => Tick(TimedEffects.Now));
    public static int Rate(GearState state)
    {
        if (state?.NamedItemId == null) { return OrdinaryAffixProperties.FixedValue(state,"rep-dur"); }
        if (NamedGear.Catalog == null) { return 0; }
        foreach (var property in NamedGear.Catalog.Item(state.NamedItemId).Properties)
        {
            if (property.SourceEnabled && property.Code == "rep-dur")
            {
                foreach (var roll in state.NamedProperties)
                { if (roll.Key == "p/" + property.Slot) { return roll.Value; } }
            }
        }
        return 0;
    }
    public static int PeriodMs(int rate) => rate <= 0 ? 0 : Math.Max(125, 2500 / rate + 1) * ActionTiming.FrameMs;
    public static void Track(Item item) => Track(item, TimedEffects.Now);
    public static void Track(Item item, DateTime now)
    {
        if (item == null) { return; }
        NamedAmmunition.Track(item);
        int rate = Rate(ClassGear.State(item));
        var native = GearDetails.Read(item);
        if (item.Deleted || rate <= 0 || native.Durability == null || native.MaximumDurability == null)
        { Clocks.Remove(item.Serial); return; }
        if (native.Durability <= 0 || native.Durability >= native.MaximumDurability)
        { Clocks[item.Serial] = _paused; return; }
        if (!Clocks.TryGetValue(item.Serial, out var due) || due == _paused)
        { Clocks[item.Serial] = now.AddMilliseconds(PeriodMs(rate)); }
    }
    public static void Copy(Item source, Item destination)
    {
        if (Clocks.TryGetValue(source.Serial, out var due)) { Clocks[destination.Serial] = due; }
    }
    public static void Tick(DateTime now)
    {
        _keys.Clear(); foreach (var serial in Clocks.Keys) { _keys.Add(serial); }
        foreach (var serial in _keys)
        {
            if (World.FindItem(serial) is { Deleted: false } item) { Settle(item, now); }
            else { Clocks.Remove(serial); }
        }
    }
    public static void Settle(Item item, DateTime now)
    {
        if (!Clocks.TryGetValue(item.Serial, out var due) || due > now) { return; }
        int rate = Rate(ClassGear.State(item)); var native = GearDetails.Read(item);
        if (rate <= 0 || item.Deleted || native.Durability == null || native.MaximumDurability == null)
        { Clocks.Remove(item.Serial); return; }
        if (native.Durability <= 0 || native.Durability >= native.MaximumDurability)
        { Clocks[item.Serial] = _paused; return; }
        long period = PeriodMs(rate) * TimeSpan.TicksPerMillisecond;
        long intervals = (now.Ticks - due.Ticks) / period + 1;
        int current = native.Durability.Value, maximum = native.MaximumDurability.Value;
        int repaired = (int)Math.Min(maximum - current, intervals);
        Clocks[item.Serial] = repaired == maximum - current ? _paused : due.AddTicks(intervals * period);
        // Self-repair is an item property, including on ethereal gear; it never restores lost maximum durability.
        switch (item)
        {
            case BaseWeapon weapon: weapon.HitPoints += repaired; break;
            case BaseArmor armor: armor.HitPoints += repaired; break;
            case BaseClothing clothing: clothing.HitPoints += repaired; break;
            case BaseJewel jewel: jewel.HitPoints += repaired; break;
        }
    }
}

public sealed class NamedMaintenanceProfiles : GenericPersistence
{
    private KeyValuePair<Serial, DateTime>[] _snapshot = [];
    public NamedMaintenanceProfiles() : base("SanctuaryNamedMaintenance", 130) { }
    public override void Serialize() { Capture(); base.Serialize(); }
    internal void Capture()
    {
        _snapshot = new KeyValuePair<Serial, DateTime>[NamedMaintenance.Clocks.Count];
        int i = 0; foreach (var clock in NamedMaintenance.Clocks) { _snapshot[i++] = clock; }
    }
    public override void Serialize(IGenericWriter writer)
    {
        writer.Write(0); writer.Write(_snapshot.Length);
        foreach (var (serial, due) in _snapshot) { writer.Write(serial.Value); writer.Write(due); }
    }
    public override void Deserialize(IGenericReader reader)
    {
        if (reader.ReadInt() != 0) { throw new InvalidDataException("Unsupported named maintenance version."); }
        int count = reader.ReadInt();
        if (count is < 0 or > 1000000) { throw new InvalidDataException("Invalid named maintenance count."); }
        var restored = new Dictionary<Serial, DateTime>();
        for (int i = 0; i < count; i++)
        {
            var serial = (Serial)reader.ReadUInt(); var due = reader.ReadDateTime();
            if (serial == Serial.Zero || !restored.TryAdd(serial, due)) { throw new InvalidDataException("Invalid named maintenance entry."); }
        }
        NamedMaintenance.Clocks.Clear(); foreach (var pair in restored) { NamedMaintenance.Clocks.Add(pair.Key, pair.Value); }
    }
    public override void PostDeserialize() => NamedMaintenance.Tick(TimedEffects.Now);
}
