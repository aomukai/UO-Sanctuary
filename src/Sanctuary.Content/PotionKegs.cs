// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using ModernUO.Serialization;
using Server;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Multis;
using Server.Network;
using Server.Targeting;

namespace Sanctuary;

[SerializationGenerator(0)]
public partial class SanctuaryPotionKeg : Item
{
    [SerializableField(0, setter: "private")] private RestorationKind _kind;
    [SerializableField(1, setter: "private")] private int _tier;
    [SerializableField(2, setter: "private")] private int _dropped;
    [SerializableField(3, setter: "private")] private int _bought;
    [SerializableField(4, setter: "private")] private int _crafted;
    public const int Capacity = 100;
    public int Held => _dropped + _bought + _crafted;
    internal long Revision { get; private set; }
    public override double DefaultWeight => 20 + Held * .8;
    public string Contents => Held == 0 ? "Empty potion keg" : PotionBeltCatalog.Potion(Kind, Tier).Name;
    [Constructible] public SanctuaryPotionKeg() : base(0x1940) { Name = "Potion keg"; }
    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);
        list.Add(1060658, $"{"Contents"}\t{Contents}");
        list.Add(1060659, $"{"Doses"}\t{Held}/{Capacity}");
        list.Add(1060660, $"{"Transfer"}\t{"Double-click: pour in, or fill one empty bottle"}");
    }
    public override void OnSingleClick(Mobile from) { base.OnSingleClick(from); LabelTo(from, $"{Contents}: {Held}/{Capacity}"); }
    public override void OnDoubleClick(Mobile from)
    {
        if (from is PlayerMobile p && CanUse(p)) { PotionKegGump.DisplayTo(p, this); }
        else { from.SendMessage("Use a keg in your backpack or a locked-down keg in a house you co-own, within two tiles."); }
    }
    public override bool OnDragDrop(Mobile from, Item dropped)
    { from.SendMessage("Double-click the keg and choose Pour in, then target a potion in your backpack."); return false; }
    public bool CanUse(PlayerMobile p) => !Deleted && ClassProfessions.Enabled && PotionBelts.Ready(p) && ClassProfessions.Ready(p)
        && (CarpentryCrafting.Component(p, this)
            || Parent == null && IsLockedDown && !Movable && p.Map == Map && p.InRange(Location, 2) && p.InLOS(this)
                && BaseHouse.FindHouseAt(this) is { } house && house.IsCoOwner(p)
                && house.CheckSecureAccess(p, this) != SecureAccessResult.Inaccessible);
    private int WeightChange(int newHeld) => (int)Math.Ceiling(Weight == DefaultWeight ? 20 + newHeld * .8 : Weight) - PileWeight;
    private void Changed(int oldWeight)
    {
        UpdateTotal(this, TotalType.Weight, PileWeight - oldWeight);
        Revision++; this.MarkDirty(); InvalidateProperties();
    }
    private void Add(ItemOrigin origin, int count)
    {
        switch (origin)
        {
            case ItemOrigin.Drop: _dropped += count; break;
            case ItemOrigin.Vendor: _bought += count; break;
            case ItemOrigin.Crafted: _crafted += count; break;
            default: throw new ArgumentOutOfRangeException(nameof(origin));
        }
    }
    public bool PourIn(PlayerMobile p, RestorationPotion potion, long revision)
    {
        if (revision != Revision || !CanUse(p) || potion is not { Amount: > 0, Stackable: true }
            || !CarpentryCrafting.Component(p, potion) || !Enum.IsDefined(potion.Origin) || Held >= Capacity
            || Held > 0 && (Kind != potion.Kind || Tier != potion.Tier)) { return false; }
        int count = Math.Min(Capacity - Held, potion.Amount);
        var bottles = ItemOrigins.Create(potion.Origin, () => new Bottle(count));
        var delivery = MarketDelivery.Create(p, [(potion, count)], [bottles], IsChildOf(p.Backpack) ? WeightChange(Held + count) : 0);
        if (delivery == null) { bottles.Delete(); p.SendMessage("Make room for the bottles and filled keg. Nothing was transferred."); return false; }
        var kind = potion.Kind; int tier = potion.Tier; var origin = potion.Origin; int oldWeight = PileWeight;
        delivery.Commit(); _kind = kind; _tier = tier; Add(origin, count); Changed(oldWeight);
        p.PlaySound(0x240); p.SendMessage($"Poured in {count} doses and recovered {count} empty bottles.");
        return true;
    }
    public bool FillBottle(PlayerMobile p, long revision)
    {
        if (revision != Revision || !CanUse(p) || Held <= 0) { return false; }
        Bottle bottle = null;
        foreach (var item in p.Backpack.FindItemsByType<Bottle>(true))
        { if (item.GetType() == typeof(Bottle) && CarpentryCrafting.Component(p, item)) { bottle = item; break; } }
        if (bottle == null) { p.SendMessage("You need an empty bottle in your backpack."); return false; }
        var origin = _bought > 0 ? ItemOrigin.Vendor : _crafted > 0 ? ItemOrigin.Crafted : ItemOrigin.Drop;
        var potion = ItemOrigins.Create(origin, () => new RestorationPotion(Kind, Tier));
        var delivery = MarketDelivery.Create(p, [(bottle, 1)], [potion], IsChildOf(p.Backpack) ? WeightChange(Held - 1) : 0);
        if (delivery == null) { potion.Delete(); p.SendMessage("Make room for the potion. Nothing was transferred."); return false; }
        int oldWeight = PileWeight; delivery.Commit(); Add(origin, -1); Changed(oldWeight);
        p.PlaySound(0x240); p.SendMessage("Filled one bottle and placed the potion in your backpack.");
        return true;
    }
}

public sealed class PotionKegGump : DynamicGump
{
    private readonly PlayerMobile _owner;
    private readonly SanctuaryPotionKeg _keg;
    private readonly NetState _connection;
    private readonly long _revision;
    private bool _used;
    public override bool Singleton => true;
    private PotionKegGump(PlayerMobile p, SanctuaryPotionKeg keg) : base(80, 60)
    { _owner = p; _keg = keg; _connection = p.NetState; _revision = keg.Revision; }
    public static void DisplayTo(PlayerMobile p, SanctuaryPotionKeg keg)
    { if (p.NetState != null && keg.CanUse(p)) { p.SendGump(new PotionKegGump(p, keg)); } }
    protected override void BuildLayout(ref DynamicGumpBuilder b)
    {
        b.AddPage(); b.AddBackground(0, 0, 500, 265, 5054);
        b.AddLabel(22, 20, 0x481, "Potion keg"); b.AddButton(450, 20, 4017, 4019, 0);
        b.AddLabel(22, 55, 0x481, $"{_keg.Contents}: {_keg.Held}/{SanctuaryPotionKeg.Capacity}");
        b.AddLabel(22, 90, 0x47E, "One kind and tier. Empty the keg before changing potions.");
        b.AddButton(22, 135, 4005, 4007, 1); b.AddLabel(57, 135, 0x481, "Pour in potions from your backpack");
        b.AddButton(22, 175, 4005, 4007, 2); b.AddLabel(57, 175, 0x481, "Fill one empty bottle");
        b.AddLabel(22, 225, 0x47E, "Transfers are manual. Potions go to your backpack.");
    }
    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (_used || sender.Mobile != _owner || !ReferenceEquals(sender, _connection)) { return; } _used = true;
        if (!_keg.CanUse(_owner) || _keg.Revision != _revision) { return; }
        if (info.ButtonID == 1 && _owner.Target == null)
        {
            _owner.Target = new PourTarget(_owner, _keg, _connection, _revision);
            _owner.SendMessage("Target a potion stack in your backpack. Doses that fit return one empty bottle each.");
        }
        else if (info.ButtonID == 2 && _keg.FillBottle(_owner, _revision)) { DisplayTo(_owner, _keg); }
    }
    private sealed class PourTarget(PlayerMobile owner, SanctuaryPotionKeg keg, NetState connection, long revision) : Target(2, false, TargetFlags.None)
    {
        private bool _used;
        protected override void OnTarget(Mobile from, object targeted)
        {
            if (_used || from != owner || !ReferenceEquals(from.NetState, connection)) { return; } _used = true;
            if (targeted is not RestorationPotion p || !keg.PourIn(owner, p, revision))
            { from.SendMessage("Nothing transferred. Check potion identity, capacity, access and backpack room."); }
            DisplayTo(owner, keg);
        }
    }
}
