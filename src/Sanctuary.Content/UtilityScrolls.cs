// SPDX-License-Identifier: GPL-3.0-or-later
using ModernUO.Serialization;
using Server;
using Server.Mobiles;

namespace Sanctuary;

public enum UtilityKind : byte { Identify, TownPortal }

[SerializationGenerator(0)]
public abstract partial class UtilityTome : Item
{
    [SerializableField(0, setter: "private")] private int _charges = 3;
    [SerializableField(1, setter: "private")] private int _capacity = 3;
    [SerializableField(2, setter: "private")] private CraftQuality _quality;
    public abstract UtilityKind Kind { get; }
    protected UtilityTome(int graphic, string name) : base(graphic)
    { Name = name; Hue = BeltProtocol.TeleportHue; Weight = 1; }
    internal void Finish(int charges, CraftQuality quality)
    { _charges = _capacity = charges + (int)quality; _quality = quality; PlayerConstructed = true; InvalidateProperties(); }
    internal void Spend()
    { if (_charges > 0 && --_charges == 0) { Delete(); } else { InvalidateProperties(); } }
    public override void Dupe(Item item)
    {
        base.Dupe(item);
        if (item is UtilityTome tome && tome.Kind == Kind)
        { tome._charges = _charges; tome._capacity = _capacity; tome._quality = _quality; }
    }
    public override void OnDoubleClick(Mobile from)
    { if (from is PlayerMobile p) { if (Kind == UtilityKind.Identify) { Identification.Use(p, this); } else { TownPortals.Open(p, this); } } }
    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list); list.Add($"{"Charges: "}{_charges}{" / "}{_capacity}");
        if (PlayerConstructed) { list.Add($"{CraftTransactions.QualityName(_quality)}"); }
        list.Add($"{"One charge per successful use; consumed when empty. Cannot be refilled."}");
        list.Add($"{"Keep in your backpack; right-click to use."}");
    }
}
[SerializationGenerator(0)]
public partial class IdentifyTome : UtilityTome
{
    public override UtilityKind Kind => UtilityKind.Identify;
    [Constructible] public IdentifyTome() : base(BeltProtocol.IdentifyTomeGraphic, "Tome of Identify") { }
}
[SerializationGenerator(0)]
public partial class TownPortalTome : UtilityTome
{
    public override UtilityKind Kind => UtilityKind.TownPortal;
    [Constructible] public TownPortalTome() : base(BeltProtocol.PortalTomeGraphic, "Tome of Town Portal") { }
}
[SerializationGenerator(0)]
public partial class TownPortalScroll : Item
{
    [Constructible] public TownPortalScroll() : base(BeltProtocol.PortalGraphic) { Name = "Town Portal scroll"; Hue = BeltProtocol.TeleportHue; Weight = .1; }
    public override void OnDoubleClick(Mobile from) { if (from is PlayerMobile p) { TownPortals.Open(p, this); } }
    public override void GetProperties(IPropertyList list)
    { base.GetProperties(list); list.Add($"{"Single use: open your two-way Town Portal"}"); }
}

public static class UtilityScrolls
{
    public static bool Ready(PlayerMobile p, Item item, int cell = -1) => EquipmentEconomy.Ready(p)
        && item is { Deleted: false, Movable: true, Amount: 1, IsLockedDown: false, IsSecure: false }
        && (item is not UtilityTome tome || tome.Charges is > 0 and <= 12 && tome.Capacity >= tome.Charges && tome.Capacity <= 12)
        && (cell < 0 ? item.IsChildOf(p.Backpack) && item.IsAccessibleTo(p) && item.VerifyMove(p)
            : item is not UtilityTome && cell < PotionBelts.Rows(p) * 4 && PotionBelts.Entry(p, cell) == item && item.Parent == PotionBelts.Storage(p));
    internal static void Spend(PlayerMobile p, Item item, int cell)
    {
        if (item is UtilityTome tome) { tome.Spend(); } else { item.Consume(); }
        if (cell >= 0) { PotionBelts.Compact(p, cell % 4); PotionBeltNetwork.Send(p); }
    }
}
