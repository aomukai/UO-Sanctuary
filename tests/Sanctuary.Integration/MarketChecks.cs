using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Server;
using Server.Accounting;
using Server.Items;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class MarketChecks
{
    private static readonly ILogger _logger = LogFactory.GetLogger(typeof(MarketChecks));
    private static string Pathname => Path.Join(Core.BaseDirectory, "market-check.json");
    private sealed record Stock(uint Vendor, string State, string[] Gear);
    private sealed record Saved(uint Player, uint Bought, int Paid, uint[] Quotes, int[] Prices, Stock[] Stocks, uint[] Towns);
    public static void Configure()
    {
        Register("market-check", Check);
        Register("market-reload-check", Reload);
    }
    private static void Register(string command, Action action) => ConsoleInputHandler.RegisterCommand(command,
        "Disposable T11 stock, sale, capacity and town-service checks", _ => Core.LoopContext.Post(() =>
        {
            try { action(); _logger.Information("{Command}: passed", command); }
            catch (Exception error) { _logger.Error(error, "{Command}: failed", command); }
        }));
    private static void Require(bool value, string message)
    {
        if (!value) { throw new InvalidOperationException(message); }
    }
    private static string Json(object value) => JsonSerializer.Serialize(value);
    private static PlayerMobile Player()
    {
        var player = new PlayerMobile { Body = 0x190, Player = true, Name = "MarketCheck" };
        player.AddItem(new Backpack());
        ClassCreation.Profiles.Assign(player, DiabloClass.Necromancer);
        ClassCreation.Profiles.AdvanceLevel(player, 99);
        player.RawStr = 200; player.RawDex = 200;
        return player;
    }
    private static int Gold(PlayerMobile player) => player.Backpack.GetAmount(typeof(Gold));
    private static void Funds(PlayerMobile player, int amount)
    {
        using var removed = Server.Collections.PooledRefList<Item>.Create();
        foreach (var item in player.Backpack.FindItemsByType(typeof(Gold), true)) { removed.Add(item); }
        foreach (var item in removed) { item.Delete(); }
        if (amount > 0) { player.Backpack.DropItem(new Gold(amount)); }
    }
    private static BaseVendor Vendor(string town, MarketRole role) => TownMarkets.Profiles.At($"Felucca/{town}/{role}");
    private static void Approach(PlayerMobile player, BaseVendor vendor)
    {
        for (int dx = -3; dx <= 3; dx++)
        {
            for (int dy = -3; dy <= 3; dy++)
            {
                var point = new Point3D(vendor.X + dx, vendor.Y + dy, vendor.Z);
                if (vendor.Map.CanFit(point,16,false,false,true) && FixtureSpace.ClearOfAllies(player,point,vendor.Map) && vendor.Map.LineOfSight(point, vendor.Location))
                { player.MoveToWorld(point, vendor.Map); return; }
            }
        }
        throw new InvalidOperationException("Merchant cannot be approached: " + vendor.Title);
    }
    private static Item Display(IBuyItemInfo info) => ((GenericBuyInfo)info).GetDisplayEntity() as Item;
    private static Stock Snapshot(BaseVendor vendor)
    {
        var state = TownMarkets.Market(vendor);
        return new(vendor.Serial.Value, Json(state), state.Offers.Select(o => Json(ClassGear.State(World.FindItem(o.Item)))).ToArray());
    }
    private static void Check()
    {
        Require(TownMarkets.Sites.Length == 476 && TownMarkets.Sites.Select(s => (s.Town, s.Map)).Distinct().Count() == 34,
            "Town/facet service coverage is incomplete");
        foreach (var site in TownMarkets.Sites)
        {
            var vendor = TownMarkets.Profiles.At(site.Id);
            Require(vendor is { Deleted: false, CantWalk: true } && vendor.Map.Name == site.Map
                && vendor.Map.CanFit(vendor.Location, 16, false, false, true), "Merchant floor/location invalid: " + site.Id);
            if (TownMarkets.Skill(site.Role) is { } skill)
            {
                Require(vendor.Skills[skill].Base == 100, "Missing trained profession: " + site.Id);
                if (TradeSkills.IsCraft((int)skill))
                {
                    var station = World.FindItem(TownMarkets.Profiles.Find(vendor.Serial).Station) as TownCraftStation;
                    Require(station != null && station.Skill == skill && station.Map == vendor.Map && !station.Movable,
                        "Missing public workstation: " + site.Id);
                }
            }
        }
        // Every supported base/tier enters a legal stock generator, not only a sample sword.
        foreach (int level in new[] { 41, 70 })
        {
            foreach (var role in Enum.GetValues<MarketRole>())
            {
                foreach (var rarity in new[] { GearRarity.Normal, GearRarity.Magic })
                {
                    foreach (var row in TownMarkets.Candidates(role, level, rarity))
                    {
                        var item = TownMarkets.Equipment(row, level, rarity, new Random(17));
                        var state = ClassGear.State(item);
                        Require(item.Origin == ItemOrigin.Vendor && !state.CraftedEquipment && state.NativeSockets == 0
                            && state.Roll.ItemLevel == level && state.Tier != GearTier.Elite
                            && (level == 70 || state.Tier == GearTier.Normal)
                            && (rarity == GearRarity.Normal || state.Roll.Affixes.Length > 0), "Illegal shop equipment: " + row.Id);
                        item.Delete();
                    }
                }
            }
        }
        foreach (var site in TownMarkets.Sites.Where(s => s.Map == "Felucca" && s.Town == "Britain" && TownMarkets.Skill(s.Role) != null))
        {
            var student = Player();
            var trainer = TownMarkets.Profiles.At(site.Id);
            var skill = TownMarkets.Skill(site.Role).Value;
            Approach(student, trainer);
            if (TradeSkills.IsCraft((int)skill)) { Require(ClassProfessions.Profiles.Confirm(student, skill), "Trainer fixture could not choose profession"); }
            student.Skills[skill].Base = 0;
            Require(trainer.Teach(skill, student, 0, false), "Actual town trainer did not offer training: " + skill);
            var fee = new Gold(300); student.Backpack.DropItem(fee);
            Require(trainer.OnGoldGiven(student, fee) && fee.Deleted && student.Skills[skill].BaseFixedPoint == 300,
                "Actual town training price/ceiling differed: " + skill);
            student.Delete();
        }
        var player = Player();
        new Account("market-fixture", Guid.NewGuid().ToString("N"))[0] = player;
        var smith = Vendor("Britain", MarketRole.Smith);
        var other = Vendor("Trinsic", MarketRole.Smith);
        var alchemist = Vendor("Britain", MarketRole.Alchemist);
        foreach (var merchant in new[] { smith, other })
        {
            var state = TownMarkets.Market(merchant);
            Require(state.Offers.Count(o => o.Equipment && ClassGear.State(World.FindItem(o.Item)).Roll.Rarity == GearRarity.Normal) == 9
                && state.Offers.Count(o => o.Equipment && ClassGear.State(World.FindItem(o.Item)).Roll.Rarity == GearRarity.Magic) == 3,
                "Equipment ratio is not nine White/three Magic");
            string before = Json(state);
            merchant.GetBuyInfo(); merchant.GetBuyInfo();
            Require(Json(state) == before, "Reopening rerolled stock or moved the refresh deadline");
            Require(state.Offers.Where(o => o.Equipment).All(o => ClassGear.State(World.FindItem(o.Item)).Roll.ItemLevel
                == (merchant == smith ? 70 : 41)), "Britain/ordinary item level differed");
        }
        Approach(player, smith); Funds(player, 10000);
        var info = smith.GetBuyInfo().First(i => ClassGear.State(Display(i))?.Roll.Rarity == GearRarity.Magic);
        var shown = Display(info); string gearBefore = Json(ClassGear.State(shown)); int asking = info.Price;
        int goldBefore = Gold(player);
        Require(!other.OnBuyItems(player, [new BuyItemResponse(shown.Serial, 1)]) && Gold(player) == goldBefore, "Foreign vendor fulfilled another shop's offer");
        Require(!smith.OnBuyItems(player, [new BuyItemResponse(shown.Serial, 1), new BuyItemResponse(shown.Serial, 1)])
            && Gold(player) == goldBefore && shown.Parent is MarketShelf, "Duplicate rows charged or duplicated equipment");
        var stock = TownMarkets.Market(smith);
        int limit = player.Backpack.MaxItems;
        player.Backpack.MaxItems = player.Backpack.TotalItems;
        Require(!smith.OnBuyItems(player, [new BuyItemResponse(shown.Serial, 1)]) && Gold(player) == goldBefore
            && shown.Parent is MarketShelf, "Full pack charged for gear or removed stock");
        player.Backpack.MaxItems = limit;
        Require(smith.OnBuyItems(player, [new BuyItemResponse(shown.Serial, 1)]) && shown.Parent == player.Backpack
            && Gold(player) == goldBefore - asking && !stock.Offers.Any(o => o.Item == shown.Serial), "Purchase failed or bought a replacement roll");
        Require(Json(ClassGear.State(shown) with { VendorPurchasePrice = null }) == gearBefore
            && ClassGear.State(shown).VendorPurchasePrice == asking && TradePrices.Quote(shown) == asking / 10,
            "Purchase changed displayed stats or vendor resale basis");
        goldBefore = Gold(player);
        Require(!smith.OnBuyItems(player, [new BuyItemResponse(shown.Serial, 1)]) && Gold(player) == goldBefore,
            "A bought stock item was replayed");
        var copy = shown.GetType().CreateInstance<Item>();
        shown.Dupe(copy);
        Require(copy.Origin == ItemOrigin.Vendor && ClassGear.State(copy).VendorPurchasePrice == asking,
            "Copy lost vendor purchase price/origin"); copy.Delete();
        var foreign = Player(); foreign.Backpack.DropItem(shown);
        Require(TradePrices.Quote(shown) == asking / 10 && EquipmentEconomy.Quote(foreign, shown, EssenceAction.Salvage) == null,
            "Trading changed vendor resale or made it salvageable");
        player.Backpack.DropItem(shown); foreign.Delete();
        var purchased = shown;
        var stale = stock.Offers.First(o => o.Equipment);
        stock.NextRefresh = Core.Now - TimeSpan.FromSeconds(1);
        smith.GetBuyInfo(); goldBefore = Gold(player);
        Require(!smith.OnBuyItems(player, [new BuyItemResponse(stale.Item, 1)]) && Gold(player) == goldBefore,
            "Expired stock request charged gold");
        var bankOffer = stock.Offers.First(o => o.Equipment);
        var priced = bankOffer with { Price = 2000 };
        stock.Offers[stock.Offers.IndexOf(bankOffer)] = priced;
        Funds(player, 0);
        Require(Banker.Deposit(player, 3000), "Bank fixture could not be funded");
        int bankBefore = Banker.GetBalance(player);
        Require(smith.OnBuyItems(player, [new BuyItemResponse(bankOffer.Item, 1)]) && Banker.GetBalance(player) == bankBefore - 2000
            && World.FindItem(bankOffer.Item).Parent == player.Backpack && Gold(player) == 0, "Bank-funded purchase did not settle once");
        World.FindItem(bankOffer.Item).Delete(); Funds(player, 5000);
        var quotes = new List<Item>(); var prices = new List<int>();
        foreach (var tier in Enum.GetValues<GearTier>())
        {
            var row = EquipmentCrafting.Recipes.First(r => r.Type == nameof(Longsword) && r.Tier == tier);
            var item = ItemOrigins.Create(ItemOrigin.Crafted, () => new Longsword());
            EquipmentCrafting.Finish(item, player, row, CraftResource.Iron, 0, CraftQuality.Average, new Random(3));
            player.Backpack.DropItem(item);
            int price = TradePrices.Quote(item);
            var band = TradePrices.Band(tier);
            Require(price >= band.Min && price <= band.Max && TradePrices.Quote(item, 1) == price && TradePrices.Quote(item, 500000) == price,
                "Sale band or persistent first quote failed");
            ClassGear.Profiles.Convert(item, 99, GearRarity.Magic, new Random(8));
            var saved = ClassGear.State(item);
            ClassGear.Profiles.Store(item, saved with { Identified = false });
            Require(TradePrices.Quote(item) == price / 10, "Unidentified sale was repriced/identified");
            using (var opl = new ObjectPropertyList(item))
            {
                item.GetProperties(opl); opl.Terminate();
                Require(opl.Buffer.AsSpan().IndexOf(Encoding.Unicode.GetBytes("properties hidden")) >= 0
                    && !ClassGear.State(item).Identified, "Unidentified tooltip revealed properties or identified the item");
            }
            quotes.Add(item); prices.Add(price);
        }
        var toSell = ItemOrigins.Create(ItemOrigin.Crafted, () => new Dagger());
        player.Backpack.DropItem(toSell); ClassGear.Profiles.Ensure(toSell);
        int payout = TradePrices.Quote(toSell); goldBefore = Gold(player);
        Require(!smith.OnSellItems(player, [new SellItemResponse(toSell, 1), new SellItemResponse(toSell, 1)]) && !toSell.Deleted
            && Gold(player) == goldBefore, "Duplicate sale paid/consumed items");
        Require(smith.OnSellItems(player, [new SellItemResponse(toSell, 1)]) && toSell.Deleted && Gold(player) == goldBefore + payout,
            "Sale payment differed from saved quote");
        Require(!smith.OnSellItems(player, [new SellItemResponse(toSell, 1)]) && Gold(player) == goldBefore + payout,
            "Sale replay paid again");
        Require(!smith.BuyPack.Items.Any(i => i.Serial == toSell.Serial)
            && !smith.OnBuyItems(player, [new BuyItemResponse(toSell.Serial, 1)]), "Sold gear became buyback stock");
        var starter = new Dagger { LootType = LootType.Newbied };
        player.Backpack.DropItem(starter); ClassGear.Profiles.MarkStarter(starter);
        goldBefore = Gold(player);
        Require(smith.OnSellItems(player, [new SellItemResponse(starter, 1)]) && Gold(player) == goldBefore + 1 && starter.Deleted,
            "Newbie starter could not sell for one gold");
        var legacy = ItemOrigins.Create(ItemOrigin.Vendor, () => new Dagger());
        player.Backpack.DropItem(legacy);
        Require(TradePrices.Quote(legacy) == 1, "Legacy vendor item received a tier reward"); legacy.Delete();
        Approach(player, alchemist); Funds(player, 5000);
        var supply = alchemist.GetBuyInfo().First(i => Display(i) is Bottle);
        var toolOffer = alchemist.GetBuyInfo().First(i => Display(i) is MortarPestle);
        var template = (BaseTool)Display(toolOffer);
        Require(template.UsesRemaining is >= 20 and <= 30, "Tool supply has wrong charges");
        Require(alchemist.OnBuyItems(player, [new BuyItemResponse(template.Serial, 1)]), "Tool supply purchase failed");
        var suppliedTool = player.Backpack.FindItemByType<MortarPestle>();
        Require(suppliedTool.Origin == ItemOrigin.Vendor && suppliedTool.UsesRemaining == template.UsesRemaining
            && CraftTransactions.Profiles.Find(suppliedTool).Capacity == template.UsesRemaining, "Bought tool differed from its displayed charges");
        Funds(player, 0);
        Require(!alchemist.OnBuyItems(player, [new BuyItemResponse(Display(supply).Serial, 1)])
            && player.Backpack.GetAmount(typeof(Bottle)) == 0 && supply.Amount == 999, "Insufficient gold consumed stock or granted goods");
        Funds(player, 5000);
        int count = player.Backpack.GetAmount(typeof(Bottle));
        Require(alchemist.OnBuyItems(player, [new BuyItemResponse(Display(supply).Serial, 3)])
            && player.Backpack.GetAmount(typeof(Bottle)) == count + 3 && Gold(player) == 5000 - 3 * supply.Price,
            "Supply price/quantity incorrect");
        Require(alchemist.GetBuyInfo().First(i => Display(i) is Bottle).Amount == 999, "Essential supplies depleted");
        Require(alchemist.GetBuyInfo().Count(i => Display(i) is RestorationPotion) == 8
            && alchemist.GetBuyInfo().All(i => Display(i) is not RestorationPotion { Tier: 5 }), "Restorative stock tiers incorrect");
        Require(TownMarkets.Supplies.All(s => MaterialProcessing.StockAllowed(EquipmentCrafting.Resolve(s.Type))), "Forbidden material supply");
        // Partial stack sales keep their source slot; a full pack must reject gold delivery atomically.
        var tailor = Vendor("Britain", MarketRole.Tailor); Approach(player, tailor); Funds(player, 0);
        var cloth = ItemOrigins.Create(ItemOrigin.Crafted, () => new Cloth(20)); player.Backpack.DropItem(cloth);
        limit = player.Backpack.MaxItems; player.Backpack.MaxItems = player.Backpack.TotalItems;
        Require(!tailor.OnSellItems(player, [new SellItemResponse(cloth, 5)]) && cloth.Amount == 20 && Gold(player) == 0,
            "Full-pack sale consumed inputs without delivering gold");
        player.Backpack.MaxItems = limit; cloth.Delete();
        Approach(player, smith); Funds(player, 5000);
        File.WriteAllText(Pathname, Json(new Saved(player.Serial.Value, purchased.Serial.Value, asking,
            quotes.Select(i => i.Serial.Value).ToArray(), prices.ToArray(), [Snapshot(smith), Snapshot(other), Snapshot(alchemist)],
            TownMarkets.Sites.Select(s => TownMarkets.Profiles.At(s.Id).Serial.Value).ToArray())));
        File.WriteAllText(Path.Join(Core.BaseDirectory, "Data/Sanctuary/Balance/market-settings.json"),
            Json(new[] { TownMarkets.Settings with { NormalMin = 1000, NormalMax = 1500 } }));
        ClassGear.Profiles.Prune();
    }
    private static void Reload()
    {
        var saved = JsonSerializer.Deserialize<Saved>(File.ReadAllText(Pathname));
        var player = (PlayerMobile)World.FindMobile((Serial)saved.Player);
        var bought = World.FindItem((Serial)saved.Bought);
        Require(bought?.RootParent == player && bought.Origin == ItemOrigin.Vendor && ClassGear.State(bought).VendorPurchasePrice == saved.Paid
            && TradePrices.Quote(bought) == saved.Paid / 10, "Purchased item's identity or price did not persist");
        for (int i = 0; i < saved.Quotes.Length; i++)
        {
            var item = World.FindItem((Serial)saved.Quotes[i]);
            Require(ClassGear.State(item) is { Identified: false } state && state.IdentifiedSaleOffer == saved.Prices[i]
                && TradePrices.Quote(item) == saved.Prices[i] / 10, "Unidentified quote rerolled on restart");
        }
        foreach (var stock in saved.Stocks)
        {
            var vendor = (BaseVendor)World.FindMobile((Serial)stock.Vendor);
            Require(Json(Snapshot(vendor)) == Json(stock), "Shop stock, rolls or refresh deadline changed on restart");
        }
        Require(TownMarkets.Sites.Select(s => TownMarkets.Profiles.At(s.Id).Serial.Value).SequenceEqual(saved.Towns),
            "Town merchants duplicated or changed identity after reload");
    }
}
