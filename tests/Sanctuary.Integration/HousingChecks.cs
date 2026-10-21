using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Server;
using Server.Accounting;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using Server.Multis;
using Server.Regions;

namespace Sanctuary.Integration;

public static class HousingChecks
{
    private static readonly ILogger _log = LogFactory.GetLogger(typeof(HousingChecks));
    private static string Manifest => Path.Join(Core.BaseDirectory, "housing-checks.json");
    private sealed record Saved(uint House, uint Owner, uint Alt, uint Fixture, uint Wardrobe, uint EmptyHouse, uint HiddenFixture, int Receipt, int Living, int Garden, uint[] Architects);
    private static void Require(bool condition, string why) => RuneChecks.Require(condition, why);
    public static void Configure()
    {
        Register("housing-check", Check); Register("housing-reload-check", Reload);
    }
    private static void Register(string name, Action action) => ConsoleInputHandler.RegisterCommand(name, "Disposable housing/GM geometry/security/trade/replacement checks", _ => Core.LoopContext.Post(() =>
    { try { action(); _log.Information("{Command}: passed", name); } catch (Exception e) { _log.Error(e, "{Command}: failed", name); } }));
    private static PlayerMobile Player(Account account, int slot)
    {
        var player = new PlayerMobile { Body = 0x190, Player = true, Name = "HousingFixture", RawStr = 200 };
        player.AddItem(new Backpack()); ClassCreation.Profiles.Assign(player, DiabloClass.Necromancer);
        account[slot] = player; return player;
    }
    private static Account Account() => new("house-" + Guid.NewGuid().ToString("N"), Guid.NewGuid().ToString("N"));
    private static void Corners(HouseMeasurement measure, int floor, int x, int y, int width, int height, int z)
    {
        measure.Corners[floor][0] = new(x, y, z); measure.Corners[floor][1] = new(x + width - 1, y, z);
        measure.Corners[floor][2] = new(x + width - 1, y + height - 1, z); measure.Corners[floor][3] = new(x, y + height - 1, z);
    }
    private static void MoveToSign(PlayerMobile p, TownHouse house, int offset = 0) => p.MoveToWorld(new(house.Sign.X + offset, house.Sign.Y + 1, house.Sign.Z), house.Map);
    private static Item Contract(TownHouse house) => (Item)Activator.CreateInstance(typeof(BaseHouse).GetNestedType("TransferItem", BindingFlags.NonPublic),
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, [house], null);

    private static void Check()
    {
        Require(Housing.IndoorRate == 1000 && Housing.GardenRate == 250 && Housing.WardrobePrice == 1000 && !BaseHouse.DecayEnabled, "Housing defaults differ");
        var account = Account(); var owner = Player(account, 0); var alt = Player(account, 1);
        var outsider = Player(Account(), 0);
        Require(!Housing.HasRights(owner) && !HousingContentHooks.CanAcquire(alt), "Unrecognized account may acquire housing");
        TownRecognition.Profiles.Store(owner, new(RecognitionHonor.Hero, RecognitionHonor.Hero));
        Require(Housing.HasRights(alt) && account.GetTag("Sanctuary.HousingRecognized") == "true" && !TownRecognition.HasAccess(alt), "Housing did not share or incorrectly shared recognition");
        Housing.Grant(outsider);

        var map = Map.Trammel; var exit = CombatPractice.FindFloor(map, 5700, 1410);
        var measure = new HouseMeasurement(map) { FloorCount = 3, HasGarden = true, Exit = exit };
        Corners(measure, 0, 5700, 1400, 5, 5, exit.Z);
        Corners(measure, 2, 5700, 1400, 3, 3, exit.Z + 40);
        Corners(measure, 5, 5699, 1399, 7, 9, exit.Z);
        var definition = measure.Build("Three-floor fixture home");
        Require(definition.LivingTiles == 59 && definition.Floors[1].Inherited && !definition.Floors[2].Inherited
            && definition.GardenTiles == 38 && definition.SuggestedPrice == 68500, "Measured/inherited floors or garden overlap priced incorrectly");
        measure.Corners[1][0] = new Point3D(5700, 1400, exit.Z + 20);
        bool rejected = false;
        try { measure.Build("Partial override"); } catch (InvalidDataException) { rejected = true; }
        Require(rejected, "Partial upper floor silently inherited"); measure.Corners[1][0] = null;
        measure.Corners[0][3] = measure.Corners[0][0]; rejected = false;
        try { measure.Build("Duplicate corner"); } catch (InvalidDataException) { rejected = true; }
        Require(rejected, "Duplicate corners accepted"); Corners(measure, 0, 5700, 1400, 5, 5, exit.Z);

        var fixture = new WoodenChest { Movable = false }; fixture.MoveToWorld(new(5701, 1401, exit.Z), map);
        var underlying = Region.Find(new Point3D(5700, 1400, exit.Z), map);
        var house = new TownHouse(definition, map, exit, definition.SuggestedPrice, true, 244, 489, [fixture]);
        Require(house.Region.Parent == underlying, "Town home replaced the underlying town/world rules");
        MoveToSign(owner, house); MoveToSign(alt, house, 1); MoveToSign(outsider, house, -1);
        Require(BaseHouse.FindHouseAt(fixture) == house && house.GetAosMaxLockdowns() == 244 && house.GetAosMaxSecures() == 489, "Static home not found by native house lookup or wrong capacity");
        Require(!house.Buy(alt, house.Revision, house.ListPrice), "Unfunded town purchase succeeded");
        Require(Banker.Deposit(alt, 300000), "Could not fund purchaser");
        var native = new SmallOldHouse(alt, 0x64); native.MoveToWorld(new(5800, 1450, 0), map);
        Require(!house.Buy(alt, house.Revision, house.ListPrice), "Placed home did not block town purchase"); native.Delete();
        long oldQuote = house.Revision; int before = Banker.GetBalance(alt);
        Require(house.Buy(alt, oldQuote, house.ListPrice) && !house.Buy(alt, oldQuote, house.ListPrice)
            && Banker.GetBalance(alt) == before - definition.SuggestedPrice, "Purchase failed/replayed/charged incorrectly");
        Require(BaseHouse.HasAccountHouse(owner) && house.IsOwner(owner) && house.IsOwner(alt) && !Housing.CanBuy(owner), "Native account cap/full alt access missing");
        Require(house.DecayType == DecayType.Ageless, "Owned town home decays");
        house.ReleaseSecure(alt, fixture);
        Require(fixture.IsSecure && !fixture.Movable && house.HasSecureItem(fixture), "Original fixture could be released and removed");

        var region = (HouseRegion)house.Region;
        var inside = new Point3D(5702, 1402, exit.Z); var garden = new Point3D(5699, 1400, exit.Z);
        Require(!region.OnMoveInto(outsider, Direction.North, inside, outsider.Location)
            && region.OnMoveInto(outsider, Direction.North, garden, outsider.Location), "Private entry/open garden rules differ");
        house.Access.Add(outsider);
        Require(region.OnMoveInto(outsider, Direction.North, inside, outsider.Location), "Invited visitor could not enter");
        Require(!BaseHouse.CheckAccessible(outsider, fixture) && BaseHouse.CheckAccessible(owner, fixture), "Invitation granted storage or account owner denied storage");
        outsider.MoveToWorld(inside, map); house.Access.Remove(outsider); outsider.MoveToWorld(new(inside.X + 1, inside.Y, inside.Z), map);
        Require(!definition.Indoors(outsider.Location), "Revoked/forced entry was not ejected"); MoveToSign(outsider, house, -1);
        var loose = new Gold(17); loose.MoveToWorld(garden, map);
        before = Banker.GetBalance(alt);
        Require(!house.SellBack(alt, house.Revision, house.Price) && Banker.GetBalance(alt) == before && house.Owner == alt, "Garden belongings lost or buyback charged"); loose.Delete();
        var contents = new Gold(19); fixture.DropItem(contents);
        Require(!house.EmptyForBuyback(), "Original fixture contents ignored by empty check"); contents.Delete();

        var unqualified = Player(Account(), 0); MoveToSign(unqualified, house, -1);
        var contract = Contract(house);
        Require(!contract.AllowSecureTrade(alt, unqualified, unqualified, true), "Native trade accepted unqualified buyer");
        Housing.Grant(unqualified);
        Require(contract.AllowSecureTrade(alt, unqualified, unqualified, true), "Native house trade could not accept eligible buyer");
        house.Access.Add(outsider); house.Friends.Add(outsider);
        contract.OnSecureTrade(alt, unqualified, unqualified, true);
        Require(house.Owner == unqualified && !BaseHouse.HasAccountHouse(alt) && BaseHouse.HasAccountHouse(unqualified)
            && house.Access.Count == 0 && house.Friends.Count == 0 && !fixture.Deleted && house.Price == definition.SuggestedPrice,
            "Native trade lost property, leaked permissions or changed town receipt");
        before = Banker.GetBalance(unqualified); long saleQuote = house.Revision;
        Require(house.SellBack(unqualified, saleQuote, house.Price) && !house.SellBack(unqualified, saleQuote, house.Price)
            && Banker.GetBalance(unqualified) == before + definition.SuggestedPrice / 2 && house.Owner == null && house.Listed && !fixture.Deleted,
            "Buyback refund/replay/relisting/original fixture retention failed");
        MoveToSign(alt, house, 1);
        Require(house.Buy(alt, house.Revision, house.ListPrice), "Re-listed town home could not be purchased");

        string hall = RecognitionChecks.Place(owner);
        Require(Banker.Deposit(owner, 2000), "Could not fund replacement"); before = Banker.GetBalance(owner);
        Require(Housing.ReplaceWardrobe(owner, hall) && Banker.GetBalance(owner) == before - 1000, "Replacement cost/eligibility failed");
        var wardrobe = owner.Backpack.Items.OfType<EarnedWardrobe>().Single();
        Require(!Housing.ReplaceWardrobe(alt, hall), "Account housing rights incorrectly granted wardrobe recognition");
        wardrobe.MoveToWorld(new(5702, 1401, exit.Z), map); owner.MoveToWorld(wardrobe.Location, map);
        Require(WardrobeAccess.SourceValid(owner, wardrobe) && !WardrobeAccess.SourceValid(alt, wardrobe), $"Personal wardrobe/account house/recipient access differs: visible={owner.CanSee(wardrobe)}, los={owner.InLOS(wardrobe)}, house={BaseHouse.FindHouseAt(wardrobe)?.Serial}, owner={house.IsOwner(owner)}, range={owner.InRange(wardrobe, 2)}");
        wardrobe.Internalize(); Require(!WardrobeAccess.SourceValid(owner, wardrobe), "Removed wardrobe remained usable"); owner.Backpack.DropItem(wardrobe);

        var hidden = new Robe { Movable = false }; hidden.MoveToWorld(new(5689, 1401, exit.Z), map);
        var emptyDef = new HouseDefinition([new(5688, 1400, 4, 4, exit.Z)], null, "Empty fixture home", exit);
        var empty = new TownHouse(emptyDef, map, emptyDef.Exit, 16000, false, 244, 489, [hidden]);
        Require(hidden.Map == Map.Internal && hidden.Parent is TownFixtureStorage && !hidden.Deleted, "Empty listing destroyed or failed to hold original furnishing");

        // Removing a GM-authored listing restores existing town objects before Item.Delete clears its map.
        var restoredPoint = new Point3D(5681, 1401, exit.Z);
        var restored = new Robe { Movable = false }; restored.MoveToWorld(restoredPoint, map);
        var door = new MetalDoor(DoorFacing.SouthCW); door.MoveToWorld(new(5682, 1400, exit.Z), map);
        var cleanupDef = new HouseDefinition([new(5680, 1400, 4, 4, exit.Z)], null, "Removed draft home", exit);
        var cleanup = new TownHouse(cleanupDef, map, exit, 16000, false, 244, 489, [restored]);
        var fixtureStore = restored.Parent as TownFixtureStorage;
        cleanup.Delete();
        Require(!restored.Deleted && restored.Parent == null && restored.Map == map && restored.Location == restoredPoint
            && !restored.Movable && !door.Deleted && door.Map == map && fixtureStore is { Deleted: true }
            && BaseHouse.FindHouseAt(restoredPoint, map, 16) == null, "GM listing removal lost original furnishings/doors or left its region registered");
        restored.Delete(); door.Delete();
        var architects = TownRecognition.Halls.Select(h => Housing.Profiles.Architect(h.Id).Value).ToArray();
        Housing.Initialize();
        Require(architects.Length == 22 && architects.Distinct().Count() == 22
            && TownRecognition.Halls.Select(h => Housing.Profiles.Architect(h.Id).Value).SequenceEqual(architects), "Architect installation duplicated services");
        File.WriteAllText(Manifest, JsonSerializer.Serialize(new Saved(house.Serial.Value, owner.Serial.Value, alt.Serial.Value,
            fixture.Serial.Value, wardrobe.Serial.Value, empty.Serial.Value, hidden.Serial.Value, house.Price, definition.LivingTiles, definition.GardenTiles, architects)));
    }
    private static void Reload()
    {
        var saved = JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));
        var house = World.FindItem((Serial)saved.House) as TownHouse;
        var owner = World.FindMobile((Serial)saved.Owner) as PlayerMobile; var alt = World.FindMobile((Serial)saved.Alt) as PlayerMobile;
        var wardrobe = World.FindItem((Serial)saved.Wardrobe) as EarnedWardrobe;
        Require(house is { Deleted: false } && house.Owner == alt && house.IsOwner(owner) && BaseHouse.HasAccountHouse(owner)
            && Housing.HasRights(alt) && !BaseHouse.DecayEnabled && house.Price == saved.Receipt
            && house.Definition.LivingTiles == saved.Living && house.Definition.GardenTiles == saved.Garden, "House ownership/geometry/receipt/rights changed on reload");
        Require(BaseHouse.FindHouseAt(new Point3D(5701, 1401, house.Z), house.Map, 16) == house
            && World.FindItem((Serial)saved.Fixture) is WoodenChest { Deleted: false, IsSecure: true }
            && wardrobe is { Deleted: false } && wardrobe.Recipient == owner.Serial.Value, "Region/storage/wardrobe identity did not reload");
        Require(World.FindItem((Serial)saved.EmptyHouse) is TownHouse { Owner: null, Furnished: false }
            && World.FindItem((Serial)saved.HiddenFixture) is { Deleted: false, Map: var map, Parent: TownFixtureStorage } && map == Map.Internal, "Empty listing furnishings changed on reload");
        Require(TownRecognition.Halls.Select(h => Housing.Profiles.Architect(h.Id).Value).SequenceEqual(saved.Architects), "Architect identities changed on reload");
    }
}
