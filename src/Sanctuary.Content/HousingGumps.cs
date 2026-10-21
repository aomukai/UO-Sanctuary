// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Server;
using Server.Gumps;
using Server.Items;
using Server.Multis;
using Server.Network;
using Server.Regions;
using Server.Targeting;

namespace Sanctuary;

public abstract class HousingGump : DynamicGump
{
    private static readonly Dictionary<Serial, long> _sessions = new();
    private static long _sequence;
    protected readonly Mobile User;
    private readonly NetState _connection;
    private readonly long _session;
    private bool _used;
    public override bool Singleton => true;
    protected HousingGump(Mobile user) : base(30, 30)
    {
        User = user; _connection = user.NetState;
        _session = ++_sequence; _sessions[user.Serial] = _session;
    }
    public static void Configure() => EventSink.Disconnected += from => _sessions.Remove(from.Serial);
    protected bool Current => !User.Deleted && User.NetState == _connection && _sessions.GetValueOrDefault(User.Serial) == _session;
    protected bool Accept(NetState state) => !_used && (_used = true) && ReferenceEquals(state, _connection)
        && state.Mobile == User && !User.Deleted && _sessions.GetValueOrDefault(User.Serial) == _session;
    protected static void Button(ref DynamicGumpBuilder b, int x, int y, int id, string label)
    { b.AddButton(x, y, 4005, 4007, id); b.AddLabel(x + 35, y, 0x481, label); }
}

public sealed class HouseMeasureGump : HousingGump
{
    private readonly HouseMeasurement _measure;
    private readonly int _floor;
    private HouseMeasureGump(Mobile user, HouseMeasurement measure, int floor) : base(user)
    { _measure = measure; _floor = floor; }
    public static void Start(Mobile user)
    {
        if (user.AccessLevel < AccessLevel.GameMaster || user.Map == null || user.Map == Map.Internal || user.NetState == null) { return; }
        user.SendMessage("Walk to each corner and check it. Mark an outside exit before saving.");
        Show(user, new HouseMeasurement(user.Map), 0);
    }
    private static void Show(Mobile user, HouseMeasurement measure, int floor) => user.SendGump(new HouseMeasureGump(user, measure, floor));
    protected override void BuildLayout(ref DynamicGumpBuilder b)
    {
        b.AddPage(); b.AddBackground(0, 0, 555, 435, 5054);
        b.AddLabel(22, 18, 0x481, "GM town-house measurement");
        b.AddLabel(22, 48, 0x47E, _floor == 5 ? "Garden — measured once, open to visitors" : $"Living space — floor {_floor + 1}");
        b.AddLabel(22, 78, 0x481, "Floor count (1–5):");
        b.AddBackground(170, 74, 50, 25, 3000); b.AddTextEntry(178, 77, 35, 20, 0, 0, _measure.FloorCount.ToString());
        Button(ref b, 245, 77, 20, "Apply");
        for (int i = 0; i < 4; i++)
        {
            var point = _measure.Corners[_floor][i];
            b.AddButton(22, 116 + i * 32, point.HasValue ? 211 : 210, point.HasValue ? 210 : 211, 1 + i);
            b.AddLabel(53, 116 + i * 32, 0x481, $"Corner {i + 1}: {(point.HasValue ? point.Value.ToString() : "walk here and check")}");
        }
        try
        {
            bool inherited = _floor > 0 && _floor < 5 && _measure.Corners[_floor].All(p => p == null);
            var area = HouseMeasurement.Rectangle(_measure.Corners[inherited ? 0 : _floor]);
            b.AddLabel(22, 249, 0x47E, $"Area: {area.Area} tiles{(inherited ? " (inherited from floor 1)" : " (measured)")}");
        }
        catch (InvalidDataException)
        { b.AddLabel(22, 249, 0x47E, "Complete corners; blank upper floors inherit floor 1"); }
        Button(ref b, 22, 282, 30, _measure.Exit.HasValue ? $"Outside exit: {_measure.Exit.Value}" : "Mark outside exit at your feet");
        b.AddButton(22, 319, _measure.HasGarden ? 211 : 210, _measure.HasGarden ? 210 : 211, 31);
        b.AddLabel(53, 319, 0x481, "Has garden / measure garden");
        if (_measure.HasGarden) { Button(ref b, 350, 319, 45, "Garden"); }
        for (int i = 0; i < _measure.FloorCount; i++)
        { Button(ref b, 22 + i * 100, 354, 40 + i, $"Floor {i + 1}"); }
        Button(ref b, 22, 395, 50, "Save settings and exit");
        b.AddButton(510, 18, 4017, 4019, 0);
    }
    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (!Accept(sender) || User.AccessLevel < AccessLevel.GameMaster || User.Map != _measure.Map || info.ButtonID == 0) { return; }
        int page = _floor;
        try
        {
            if (!int.TryParse(info.GetTextEntry(0), out int count) || count is < 1 or > 5)
            { throw new InvalidDataException("Floor count must be between one and five."); }
            _measure.FloorCount = count;
            if (info.ButtonID is >= 1 and <= 4)
            {
                int index = info.ButtonID - 1;
                _measure.Corners[_floor][index] = _measure.Corners[_floor][index].HasValue ? null : User.Location;
            }
            else if (info.ButtonID == 30) { _measure.Exit = User.Location; }
            else if (info.ButtonID == 31)
            { _measure.HasGarden = !_measure.HasGarden; page = _measure.HasGarden ? 5 : 0; }
            else if (info.ButtonID == 45 && _measure.HasGarden) { page = 5; }
            else if (info.ButtonID is >= 40 and <= 44) { page = Math.Min(info.ButtonID - 40, count - 1); }
            else if (info.ButtonID == 50)
            {
                var definition = _measure.Build("Town home");
                User.SendGump(new HouseAuthorSaleGump(User, definition, _measure.Map, _measure.Exit.Value));
                return;
            }
        }
        catch (Exception e) when (e is InvalidDataException or OverflowException)
        { User.SendMessage(e.Message); }
        Show(User, _measure, page == 5 ? 5 : Math.Min(page, _measure.FloorCount - 1));
    }
}

public sealed class HouseAuthorSaleGump : HousingGump
{
    private readonly HouseDefinition _definition;
    private readonly Map _map;
    private readonly Point3D _sign;
    private readonly List<Item> _fixtures;
    private readonly List<BaseDoor> _entrances;
    private readonly int _price, _lockdowns, _storage;
    private readonly bool _furnished;
    public HouseAuthorSaleGump(Mobile user, HouseDefinition definition, Map map, Point3D sign,
        List<Item> fixtures = null, int price = 0, int lockdowns = 0, int storage = 0, bool furnished = true, List<BaseDoor> entrances = null) : base(user)
    {
        _definition = definition; _map = map; _sign = sign; _fixtures = fixtures ?? new(); _furnished = furnished;
        _entrances = entrances ?? new();
        var native = HousePlacementEntry.ClassicHouses.FirstOrDefault(e => e.Cost >= definition.SuggestedPrice)
            ?? HousePlacementEntry.ClassicHouses[^1];
        _price = price > 0 ? price : definition.SuggestedPrice;
        _lockdowns = lockdowns > 0 ? lockdowns : native.Lockdowns;
        _storage = storage > 0 ? storage : native.Storage;
    }
    protected override void BuildLayout(ref DynamicGumpBuilder b)
    {
        b.AddPage(); b.AddBackground(0, 0, 555, 435, 5054);
        b.AddLabel(22, 18, 0x481, "GM property sale setup");
        Entry(ref b, 55, "Property name", 0, _definition.Name);
        b.AddLabel(22, 90, 0x47E, $"Living {_definition.LivingTiles} tiles; garden {_definition.GardenTiles} tiles");
        b.AddLabel(22, 115, 0x47E, $"Suggested {_definition.SuggestedPrice} gold; {_definition.Floors.Length} floors");
        Entry(ref b, 145, "Final price", 1, _price.ToString());
        Entry(ref b, 180, "Lockdowns", 2, _lockdowns.ToString());
        Entry(ref b, 215, "Stored items", 3, _storage.ToString());
        b.AddButton(22, 253, _furnished ? 211 : 210, _furnished ? 210 : 211, 1);
        b.AddLabel(53, 253, 0x481, _furnished ? "Furnished" : "Empty (original furnishings retained safely)");
        Button(ref b, 22, 288, 2, $"Mark original furnishing ({_fixtures.Count})");
        Button(ref b, 22, 323, 3, $"Sign at your feet (currently {_sign})");
        Button(ref b, 22, 358, 5, $"Mark entrance door ({_entrances.Count})");
        Button(ref b, 22, 390, 4, "List property for sale");
        b.AddButton(510, 18, 4017, 4019, 0);
    }
    private static void Entry(ref DynamicGumpBuilder b, int y, string label, int id, string value)
    { b.AddLabel(22, y, 0x481, label); b.AddBackground(180, y - 3, 345, 27, 3000); b.AddTextEntry(190, y, 325, 20, 0, id, value); }
    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (!Accept(sender) || User.AccessLevel < AccessLevel.GameMaster || User.Map != _map || info.ButtonID == 0) { return; }
        var definition = _definition with { Name = info.GetTextEntry(0)?.Trim() };
        if (!int.TryParse(info.GetTextEntry(1), out int price) || price is < 1 or > 1000000000
            || !int.TryParse(info.GetTextEntry(2), out int lockdowns) || lockdowns is < 1 or > 100000
            || !int.TryParse(info.GetTextEntry(3), out int storage) || storage is < 1 or > 100000)
        { User.SendMessage("Enter valid positive price and storage limits."); User.SendGump(new HouseAuthorSaleGump(User, _definition, _map, _sign, _fixtures, _price, _lockdowns, _storage, _furnished, _entrances)); return; }
        var next = new HouseAuthorSaleGump(User, definition, _map, info.ButtonID == 3 ? User.Location : _sign,
            _fixtures, price, lockdowns, storage, info.ButtonID == 1 ? !_furnished : _furnished, _entrances);
        if (info.ButtonID == 2)
        { User.Target = new FurnishingTarget(next, sender); return; }
        if (info.ButtonID == 5)
        { User.Target = new EntranceTarget(next, sender); return; }
        if (info.ButtonID == 4)
        {
            try
            {
                definition.Validate(_map);
                if (definition.Inside(_sign) || User.GetDistanceToSqrt(_sign) > 18 || !_map.CanFit(_sign, 0, false, false, false))
                { throw new InvalidDataException("Place the sale sign outside the property, within 18 tiles."); }
                if (Region.Find(_sign, _map).IsPartOf<HouseRegion>() || Region.Find(definition.Exit, _map).IsPartOf<HouseRegion>())
                { throw new InvalidDataException("The sign and outside exit must be outside other homes."); }
                foreach (var floor in definition.Floors.Concat(definition.Garden == null ? [] : new[] { definition.Garden }))
                {
                    foreach (var mobile in _map.GetMobilesInBounds(floor.Bounds))
                    {
                        if (mobile != User && !mobile.Player && floor.Contains(mobile.Location, 16))
                        { throw new InvalidDataException("An NPC or creature occupies this property. Preserve public services and NPC homes."); }
                    }
                    foreach (var item in _map.GetItemsInBounds(floor.Bounds))
                    {
                        if (item is TownCraftStation or PublicRecognitionWardrobe && floor.Contains(item.Location, item.ItemData.Height))
                        { throw new InvalidDataException("This area contains a public civic or crafting facility."); }
                    }
                    for (int x = floor.X; x < floor.X + floor.Width; x++)
                    {
                        for (int y = floor.Y; y < floor.Y + floor.Height; y++)
                        {
                            if (BaseHouse.FindHouseAt(new Point3D(x, y, floor.Z), _map, 16) != null
                                || Region.Find(new Point3D(x, y, floor.Z), _map).IsPartOf<HouseRegion>())
                            { throw new InvalidDataException("This property overlaps existing housing."); }
                            if (Region.Find(new Point3D(x, y, floor.Z), _map).Priority >= HouseRegion.HousePriority)
                            { throw new InvalidDataException("This building is reserved by another world area and cannot be listed."); }
                        }
                    }
                }
                if (_fixtures.Any(i => !TownHouse.FixtureAllowed(i) || i.Map != _map || !definition.Inside(i.Location, i.ItemData.Height)))
                { throw new InvalidDataException("A marked original furnishing moved, filled or became unavailable."); }
                if (_entrances.Any(d => !EntranceAllowed(d, definition, _map)))
                { throw new InvalidDataException("A marked entrance moved or belongs to another property."); }
                var house = new TownHouse(definition, _map, _sign, price, _furnished, lockdowns, storage, _fixtures, _entrances);
                User.SendMessage($"{house.Name} listed for {price} gold. No ownership was granted.");
                return;
            }
            catch (Exception e) when (e is InvalidDataException or ArgumentException or OverflowException)
            { User.SendMessage(e.Message); }
        }
        User.SendGump(next);
    }
    private sealed class FurnishingTarget(HouseAuthorSaleGump next, NetState connection) : Target(18, false, TargetFlags.None)
    {
        protected override void OnTarget(Mobile from, object target)
        {
            if (!next.Current || from != next.User || from.NetState != connection || from.AccessLevel < AccessLevel.GameMaster || from.Map != next._map) { return; }
            if (target is Item item && TownHouse.FixtureAllowed(item) && item.Map == next._map && next._definition.Inside(item.Location, item.ItemData.Height))
            {
                if (!next._fixtures.Remove(item))
                {
                    if (next._fixtures.Count < 256) { next._fixtures.Add(item); }
                    else { from.SendMessage("A property supports at most 256 original furnishings."); }
                }
            }
            else { from.SendMessage("Select an empty, immovable original furnishing inside the property. Doors are attached automatically."); }
            from.SendGump(next);
        }
        protected override void OnTargetCancel(Mobile from, TargetCancelType cancelType)
        { if (next.Current && from == next.User && from.NetState == connection) { from.SendGump(next); } }
    }
    private static bool EntranceAllowed(BaseDoor door, HouseDefinition definition, Map map) => door is { Deleted: false, Parent: null }
        && door.Map == map && BaseHouse.FindHouseAt(door) == null && definition.Floors.Any(f =>
            new Rectangle2D(f.X - 1, f.Y - 1, f.Width + 2, f.Height + 2).Contains(door.Location) && Math.Abs(door.Z - f.Z) <= 16);
    private sealed class EntranceTarget(HouseAuthorSaleGump next, NetState connection) : Target(18, false, TargetFlags.None)
    {
        protected override void OnTarget(Mobile from, object target)
        {
            if (!next.Current || from != next.User || from.NetState != connection || from.AccessLevel < AccessLevel.GameMaster) { return; }
            if (target is BaseDoor door && EntranceAllowed(door, next._definition, next._map))
            { if (!next._entrances.Remove(door)) { next._entrances.Add(door); } }
            else { from.SendMessage("Choose an unclaimed door on the edge of a measured room."); }
            from.SendGump(next);
        }
        protected override void OnTargetCancel(Mobile from, TargetCancelType cancelType)
        { if (next.Current && from == next.User && from.NetState == connection) { from.SendGump(next); } }
    }
}

public sealed class TownHouseGump : HousingGump
{
    private readonly TownHouse _house;
    private readonly long _revision;
    private readonly int _price;
    private readonly Mobile _owner;
    private readonly bool _confirm;
    private TownHouseGump(Mobile user, TownHouse house, bool confirm) : base(user)
    { _house = house; _revision = house.Revision; _price = house.Owner == null ? house.ListPrice : house.Price; _owner = house.Owner; _confirm = confirm; }
    public static void DisplayTo(Mobile user, TownHouse house, bool confirm = false)
    { if (user.NetState != null && house.Near(user)) { user.SendGump(new TownHouseGump(user, house, confirm)); } }
    protected override void BuildLayout(ref DynamicGumpBuilder b)
    {
        b.AddPage(); b.AddBackground(0, 0, 555, 330, 5054);
        b.AddLabel(22, 18, 0x481, _house.Name);
        b.AddLabel(22, 60, 0x47E, $"{_house.Definition.LivingTiles} living tiles; {_house.Definition.GardenTiles} garden tiles");
        b.AddLabel(22, 90, 0x47E, $"{(_house.Furnished ? "Furnished" : "Empty")}; storage {_house.GetAosMaxSecures()}; lockdowns {_house.GetAosMaxLockdowns()}");
        b.AddLabel(22, 120, 0x47E, "Private home; garden open to visitors. One home per account.");
        if (_owner == null)
        {
            b.AddLabel(22, 160, 0x481, $"Purchase: {_price} gold from your bank");
            Button(ref b, 22, 215, 1, "Confirm purchase");
        }
        else if (_house.IsOwner(User))
        {
            b.AddLabel(22, 160, 0x481, $"Town buyback: {_house.Refund} gold to your bank");
            if (_confirm)
            { b.AddLabel(22, 195, 0x47E, "All player-added belongings must be removed first."); Button(ref b, 22, 245, 3, "Confirm sale back to town"); }
            else
            { Button(ref b, 22, 210, 2, "House management / native transfer"); Button(ref b, 22, 255, 3, "Sell back to town"); }
        }
        else { b.AddLabel(22, 170, 0x47E, "Privately owned. Ask the owner for an invitation."); }
        b.AddButton(510, 18, 4017, 4019, 0);
    }
    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (!Accept(sender) || _house.Deleted || !_house.Near(User) || _house.Revision != _revision || _house.Owner != _owner || info.ButtonID == 0) { return; }
        if (info.ButtonID == 1 && _owner == null) { _house.Buy(User, _revision, _price); }
        else if (info.ButtonID == 2 && _house.IsOwner(User)) { HouseGumpAOS.DisplayTo(User, _house, HouseGumpPageAOS.Information); }
        else if (info.ButtonID == 3 && _house.IsOwner(User))
        {
            if (!_confirm) { DisplayTo(User, _house, true); return; }
            _house.SellBack(User, _revision, _price);
        }
    }
}
