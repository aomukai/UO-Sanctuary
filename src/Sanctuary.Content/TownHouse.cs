// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using ModernUO.Serialization;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Multis;
using Server.Network;

namespace Sanctuary;

[SerializationGenerator(0)]
public partial class TownHouse : BaseHouse
{
    [SerializableField(0, setter: "private")] private string _definitionJson;
    [SerializableField(1, setter: "private")] private int _listPrice;
    [SerializableField(2, setter: "private")] private bool _listed;
    [SerializableField(3, setter: "private")] private bool _furnished;
    [SerializableField(4, setter: "private")] private uint[] _fixtureIds = [];
    private List<Item> _fixtures = new();
    [SerializableField(5, setter: "private")] private Point3D[] _fixturePoints = [];
    [SerializableField(6, setter: "private")] private long _revision;
    private HouseDefinition _definition;
    public HouseDefinition Definition => _definition;
    public override bool IsAosRules => true;
    public override bool KeepUnowned => true;
    public override TimeSpan DecayPeriod => TimeSpan.Zero;
    public override Point3D BaseBanLocation => Point3D.Zero;
    public override Region HouseRegionParent => Map == null || Map == Map.Internal ? null : Server.Region.Find(Location, Map);
    public override Rectangle2D[] Area => _definition == null ? [] : _definition.Floors
        .Concat(_definition.Garden == null ? [] : new[] { _definition.Garden })
        .Select(f => new Rectangle2D(f.X - X, f.Y - Y, f.Width, f.Height)).ToArray();
    public override bool IsInside(Point3D p, int height) => !Deleted && _definition?.Inside(p, height) == true;
    public override bool IsPrivateAt(Point3D p) => Owner != null && !Public && _definition?.Indoors(p) == true;
    public int Refund => Price / 2;
    public override void ReleaseSecure(Mobile from, Item item)
    {
        if (_fixtures.Contains(item))
        {
            from.SendMessage("Original property fixtures stay with the home. You may change their storage access settings.");
            return;
        }
        base.ReleaseSecure(from, item);
    }
    internal void ChangedOwner(Mobile previous)
    {
        _revision++; this.MarkDirty();
        if (previous?.Account != Owner?.Account)
        {
            Friends.Clear(); CoOwners.Clear(); Bans.Clear(); Access.Clear();
            if (Map != null && Map != Map.Internal && _definition != null) { EjectVisitors(); }
        }
    }

    [Constructible]
    public TownHouse(HouseDefinition definition, Map map, Point3D sign, int price, bool furnished,
        int lockdowns, int storage, List<Item> fixtures, List<BaseDoor> entrances = null) : base(0x3FFF, null, lockdowns, 3)
    {
        definition.Validate(map);
        if (price < 1 || lockdowns < 1 || storage < 1 || fixtures == null || fixtures.Count > 256)
        { throw new ArgumentException("Invalid town-house quote or furnishing count."); }
        _definition = definition; _definitionJson = JsonSerializer.Serialize(definition);
        _listPrice = price; _furnished = furnished; _listed = true;
        MaxSecures = 10;
        MaxLockDowns = lockdowns;
        _storage = storage;
        Visible = false; Name = definition.Name; Public = false; RestrictDecay = true;
        MoveToWorld(new Point3D(definition.Floors[0].X, definition.Floors[0].Y, definition.Floors[0].Z), map);
        RelativeBanLocation = new Point3D(definition.Exit.X - X, definition.Exit.Y - Y, definition.Exit.Z - Z);
        Sign = new TownHouseSign(this) { Name = definition.Name };
        Sign.MoveToWorld(sign, map);
        foreach (var rect in Area)
        {
            foreach (var door in map.GetItemsInBounds<BaseDoor>(new Rectangle2D(X + rect.X, Y + rect.Y, rect.Width, rect.Height)))
            {
                if (IsInside(door) && !Doors.Contains(door)) { Doors.Add(door); }
            }
        }
        foreach (var fixture in fixtures)
        {
            if (!FixtureAllowed(fixture)) { throw new ArgumentException("Invalid original furnishing."); }
            _fixtures.Add(fixture);
        }
        if (entrances != null)
        {
            foreach (var door in entrances)
            { if (!Doors.Contains(door)) { Doors.Add(door); } }
        }
        _fixtureIds = _fixtures.Select(i => i.Serial.Value).ToArray();
        _fixturePoints = _fixtures.Select(i => i.Location).ToArray();
        ApplyFurnishings();
        UpdateRegion();
    }
    [SerializableField(7, setter: "private")] private int _storage;
    [SerializableField(8, setter: "private")] private uint _fixtureStorage;
    public override int GetAosMaxSecures() => _storage;
    public override int GetAosMaxLockdowns() => MaxLockDowns;

    public bool Near(Mobile from) => from is { Deleted: false, Alive: true } && Sign is { Deleted: false }
        && from.Map == Map && from.InRange(Sign, 3) && from.InLOS(Sign);

    public static bool FixtureAllowed(Item item) => item is { Deleted: false, Parent: null, Movable: false }
        && item is not (BaseDoor or BaseMulti or HouseSign or PublicRecognitionWardrobe or EarnedWardrobe or TownCraftStation)
        && (item is not Container container || container.TotalItems == 0)
        && !item.IsLockedDown && !item.IsSecure;

    public bool Buy(Mobile from, long revision, int quotedPrice)
    {
        if (!Near(from) || !_listed || Owner != null || _revision != revision || quotedPrice != _listPrice
            || !Housing.CanBuy(from)) { return false; }
        if (!EmptyForBuyback()) { from.SendMessage("This property still contains belongings. Ask its owner or a GM to clear it before purchase."); return false; }
        if (_fixtures.Any(i => i is Container { Deleted: false, TotalItems: > 0 })
            || _fixtures.Count(i => i is Container { Deleted: false } && i.Map == Map) > GetAosMaxLockdowns()) { return false; }
        if (!Banker.Withdraw(from, _listPrice)) { from.SendMessage("You need the full purchase price in your bank."); return false; }
        Owner = from; Price = _listPrice; _listed = false; Public = false;
        Friends.Clear(); CoOwners.Clear(); Bans.Clear(); Access.Clear();
        ChangeLocks(from);
        foreach (var item in _fixtures)
        {
            if (item is Container { Deleted: false } container && item.Map == Map)
            {
                Secures.Add(new SecureInfo(container, SecureLevel.Owner));
                container.IsSecure = true; container.IsLockedDown = false; container.Movable = false;
            }
        }
        _revision++; this.MarkDirty();
        Sign.InvalidateProperties();
        EjectVisitors();
        from.SendMessage($"Home purchased for {Price} gold. All characters on your account have full access.");
        return true;
    }

    public bool EmptyForBuyback()
    {
        if (MovingCrate != null || InternalizedVendors.Count != 0 || VendorInventories.Count != 0 || HasRentedVendors
            || Addons.Any(i => i is { Deleted: false } && !_fixtures.Contains(i))
            || LockDowns.Any(i => i is { Deleted: false } && !_fixtures.Contains(i))
            || Secures.Any(s => s.Item is { Deleted: false } && !_fixtures.Contains(s.Item))) { return false; }
        foreach (var fixture in _fixtures)
        {
            if (fixture is Container { Deleted: false, TotalItems: > 0 }) { return false; }
        }
        foreach (var rect in Area)
        {
            foreach (var item in Map.GetItemsInBounds(new Rectangle2D(X + rect.X, Y + rect.Y, rect.Width, rect.Height)))
            {
                if (item == this || item == Sign || Doors.Contains(item as BaseDoor) || _fixtures.Contains(item) || !IsInside(item)) { continue; }
                return false;
            }
        }
        return true;
    }

    public bool SellBack(Mobile from, long revision, int receipt)
    {
        if (!Near(from) || !IsOwner(from) || Owner == null || _listed || _revision != revision || Price != receipt) { return false; }
        if (!EmptyForBuyback()) { from.SendMessage("Remove all player-added belongings, including garden items and container contents, before selling."); return false; }
        int refund = Refund;
        if (!Banker.Deposit(from, refund)) { from.SendMessage("Your bank cannot receive the refund. The house remains yours."); return false; }
        RemoveKeys(from);
        foreach (var info in Secures) { info.Item.IsSecure = false; info.Item.IsLockedDown = false; }
        Secures.Clear(); LockDowns.Clear();
        Friends.Clear(); CoOwners.Clear(); Bans.Clear(); Access.Clear();
        Owner = null; _listed = true; _revision++; this.MarkDirty();
        Sign.InvalidateProperties();
        from.SendMessage($"Home sold back to town. {refund} gold refunded to your bank.");
        return true;
    }

    private void ApplyFurnishings()
    {
        TownFixtureStorage storage = null;
        if (!_furnished && _fixtures.Count > 0)
        {
            storage = new TownFixtureStorage(this);
            _fixtureStorage = storage.Serial.Value;
        }
        for (int i = 0; i < _fixtures.Count; i++)
        {
            var item = _fixtures[i];
            if (item?.Deleted != false) { continue; }
            if (_furnished) { item.MoveToWorld(_fixturePoints[i], Map); }
            else { storage.DropItem(item); }
        }
    }
    public void EjectVisitors()
    {
        var visitors = new HashSet<Mobile>();
        foreach (var rect in Area)
        {
            foreach (var mobile in Map.GetMobilesInBounds(new Rectangle2D(X + rect.X, Y + rect.Y, rect.Width, rect.Height)))
            {
                if (IsPrivateAt(mobile.Location) && !HasAccess(mobile)) { visitors.Add(mobile); }
            }
        }
        foreach (var mobile in visitors) { mobile.MoveToWorld(_definition.Exit, Map); }
    }

    [AfterDeserialization]
    private void RestoreProperty()
    {
        if (_definitionJson is not { Length: > 0 and <= 20000 } || _fixtureIds is not { Length: <= 256 } || _fixturePoints == null)
        { throw new InvalidDataException("Invalid town-house definition size."); }
        _fixtures = _fixtureIds.Select(id => World.FindItem((Serial)id)).ToList();
        _definition = JsonSerializer.Deserialize<HouseDefinition>(_definitionJson) ?? throw new InvalidDataException("Missing town-house definition.");
        _definition.Validate(Map, false);
        if (_listPrice < 1 || _storage < 1 || _fixtures.Count != _fixturePoints.Length || Owner != null && _listed
            || _fixtureIds.Any(id => !((Serial)id).IsItem) || new HashSet<uint>(_fixtureIds).Count != _fixtureIds.Length
            || _fixtureStorage != 0 && !((Serial)_fixtureStorage).IsItem)
        { throw new InvalidDataException("Invalid town-house save."); }
        Visible = false; RestrictDecay = true;
        UpdateRegion();
    }
    public override void OnDelete()
    {
        // Native house deletion owns its doors. Town doors are part of the existing town.
        Doors.Clear();
        for (int i = 0; i < _fixtures.Count; i++)
        {
            var fixture = _fixtures[i];
            if (fixture is not { Deleted: false }) { continue; }
            if (fixture.Map == Map.Internal && Map != null && Map != Map.Internal)
            { fixture.MoveToWorld(_fixturePoints[i], Map); }
            Secures.RemoveAll(s => s.Item == fixture); LockDowns.Remove(fixture);
            fixture.IsSecure = false; fixture.IsLockedDown = false; fixture.Movable = false;
        }
        _fixtures.Clear(); _fixtureIds = []; _fixturePoints = [];
        World.FindItem((Serial)_fixtureStorage)?.Delete(); _fixtureStorage = 0;
        base.OnDelete();
    }
}

[SerializationGenerator(0)]
public partial class TownFixtureStorage : Container
{
    [SerializableField(0, setter: "private")] private uint _house;
    [Constructible]
    public TownFixtureStorage() : base(0xE75) { Movable = Visible = false; Name = "Original town-property furnishings"; }
    internal TownFixtureStorage(TownHouse house) : this() => _house = house.Serial.Value;
    public override int DefaultMaxItems => 256;
    public override int DefaultMaxWeight => 0;
    public override double DefaultWeight => 0;
    public override bool CheckLift(Mobile from, Item item, ref LRReason reject) => false;
    public override bool CheckContentDisplay(Mobile from) => false;
    public override void OnDoubleClick(Mobile from) { }
    public override bool OnDragDrop(Mobile from, Item dropped) => false;
    public override bool OnDragDropInto(Mobile from, Item item, Point3D point) => false;
}

[SerializationGenerator(0)]
public partial class TownHouseSign : HouseSign
{
    public TownHouseSign(TownHouse house) : base(house) { }
    public override void OnDoubleClick(Mobile from)
    {
        if (Owner is TownHouse house) { TownHouseGump.DisplayTo(from, house); }
    }
    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);
        if (Owner is TownHouse house && house.Owner == null)
        { list.Add($"{"Purchase price"}\t{house.ListPrice} gold"); }
    }
}
