// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using ModernUO.Serialization;
using Server;
using Server.Mobiles;
using Server.Items;
using Server.Network;

namespace Sanctuary;

[SerializationGenerator(0)]
public partial class WardrobeSet : Container
{
    [SerializableField(0, setter: "private")]
    private uint _owner;
    [SerializableField(1, setter: "private")]
    private DiabloClass _characterClass;
    [SerializableField(2, setter: "private")]
    private uint[] _saved = [];
    [Constructible]
    public WardrobeSet() : base(0xE75) { Movable = Visible = false; Name = "Stored class equipment"; }
    internal WardrobeSet(PlayerMobile owner, DiabloClass characterClass) : this()
    { _owner = owner.Serial.Value; _characterClass = characterClass; }
    public override double DefaultWeight => 0;
    public override int DefaultMaxItems => WardrobeEquipment.Layers.Length;
    public override int DefaultMaxWeight => 0;
    public override bool CheckLift(Mobile from, Item item, ref LRReason reject) => false;
    public override bool CheckContentDisplay(Mobile from) => false;
    public override void OnDoubleClick(Mobile from) { }
    public override bool OnDragDrop(Mobile from, Item dropped) => false;
    public override bool OnDragDropInto(Mobile from, Item item, Point3D point) => false;
    internal void Remember(uint[] saved) { _saved = saved; this.MarkDirty(); }
    [AfterDeserialization]
    private void ValidateSaved()
    {
        if (!((Serial)_owner).IsMobile || !DiabloClasses.TryFromWire((int)_characterClass, out _)
            || _saved == null || _saved.Length > WardrobeEquipment.Layers.Length
            || new HashSet<uint>(_saved).Count != _saved.Length)
        { throw new InvalidDataException("Invalid wardrobe equipment identity."); }
        foreach (uint serial in _saved)
        { if (!((Serial)serial).IsItem) { throw new InvalidDataException("Invalid saved equipment serial."); } }
    }
}

public sealed class WardrobeEquipment : GenericPersistence
{
    internal static readonly Layer[] Layers = [Layer.OneHanded, Layer.TwoHanded, Layer.Shoes, Layer.Pants, Layer.Shirt,
        Layer.Helm, Layer.Gloves, Layer.Ring, Layer.Talisman, Layer.Neck, Layer.Waist, Layer.InnerTorso, Layer.Bracelet,
        Layer.MiddleTorso, Layer.Earrings, Layer.Arms, Layer.Cloak, Layer.OuterTorso, Layer.OuterLegs, Layer.InnerLegs];
    private Dictionary<(Serial Owner, DiabloClass Class), Serial> _sets = new();
    public static WardrobeEquipment Instance { get; private set; }
    public WardrobeEquipment() : base("SanctuaryWardrobeEquipment", 130) { }
    public static void Configure()
    {
        Instance = new(); EventSink.WorldSave += () => Instance.Prune();
    }
    internal static bool Equipment(Item item) => item != null && Array.IndexOf(Layers, item.Layer) >= 0 && item is not PotionBeltStorage;
    internal static bool Storable(Item item) => item is { Deleted: false, Movable: true, QuestItem: false }
        && Equipment(item) && item.Items is not { Count: > 0 };
    public WardrobeSet Find(Serial owner, DiabloClass characterClass) =>
        World.FindItem(_sets.GetValueOrDefault((owner, characterClass))) as WardrobeSet;
    internal bool HasReference(Serial owner, DiabloClass characterClass) => _sets.ContainsKey((owner, characterClass));
    internal WardrobeSet Create(PlayerMobile owner, DiabloClass characterClass)
    {
        if (HasReference(owner.Serial, characterClass)) { throw new InvalidOperationException("Wardrobe set already exists."); }
        var set = new WardrobeSet(owner, characterClass); _sets.Add((owner.Serial, characterClass), set.Serial); return set;
    }
    internal void RemoveCreated(WardrobeSet set)
    { _sets.Remove(((Serial)set.Owner, set.CharacterClass)); set.Delete(); }
    internal static bool Valid(PlayerMobile owner, DiabloClass characterClass, WardrobeSet set)
    {
        if (set == null) { return true; }
        if (set.Deleted || set.Owner != owner.Serial.Value || set.CharacterClass != characterClass || set.Parent != null || set.Map != Map.Internal) { return false; }
        var layers = new HashSet<Layer>(); var remembered = new HashSet<uint>(set.Saved);
        foreach (var item in set.Items)
        {
            if (!Storable(item) || !remembered.Contains(item.Serial.Value) || !layers.Add(item.Layer)) { return false; }
        }
        return set.Items.Count <= Layers.Length;
    }
    public void Prune()
    {
        var removed = new List<(Serial Owner, DiabloClass Class)>();
        foreach (var (key, serial) in _sets)
        {
            if (World.FindMobile(key.Owner) is not PlayerMobile { Deleted: false })
            { World.FindItem(serial)?.Delete(); removed.Add(key); }
        }
        foreach (var key in removed) { _sets.Remove(key); }
    }
    public override void Serialize(IGenericWriter writer)
    {
        writer.Write(0); writer.Write(_sets.Count);
        foreach (var (key, set) in _sets)
        { writer.Write(key.Owner.Value); writer.Write((byte)key.Class); writer.Write(set.Value); }
    }
    public override void Deserialize(IGenericReader reader)
    {
        if (reader.ReadInt() != 0) { throw new InvalidDataException("Unsupported wardrobe equipment index."); }
        int count = reader.ReadInt(); if (count is < 0 or > 7000000) { throw new InvalidDataException("Invalid wardrobe set count."); }
        var sets = new Dictionary<(Serial Owner, DiabloClass Class), Serial>(); var identities = new HashSet<Serial>();
        for (int i = 0; i < count; i++)
        {
            var owner = (Serial)reader.ReadUInt(); byte id = reader.ReadByte(); var set = (Serial)reader.ReadUInt();
            if (!owner.IsMobile || !set.IsItem || !DiabloClasses.TryFromWire(id, out var characterClass) || !identities.Add(set)
                || !sets.TryAdd((owner, characterClass), set)) { throw new InvalidDataException("Invalid wardrobe set identity."); }
        }
        _sets = sets;
    }
    public override void PostDeserialize()
    {
        Prune();
        foreach (var (key, serial) in _sets)
        {
            if (World.FindMobile(key.Owner) is not PlayerMobile owner || World.FindItem(serial) is not WardrobeSet set
                || !Valid(owner, key.Class, set) || !ClassCreation.Profiles.HasHistory(key.Owner, key.Class))
            { throw new InvalidDataException("Invalid wardrobe equipment ownership or contents."); }
        }
    }
}
