using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Accounting;
using Server.Items;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class MarketUiChecks
{
    private static readonly ILogger _logger = LogFactory.GetLogger(typeof(MarketUiChecks));
    private static PlayerMobile _player;
    private static Blacksmith _vendor;
    private static Item _stock, _hidden;
    private static int _before;
    public static void Configure()
    {
        Register("setup", Setup);
        Register("bought", () => Require(_stock.Parent == _player.Backpack && GoldCount() == _before - 400
            && ClassGear.State(_stock).VendorPurchasePrice == 400, "Client purchase did not deliver the displayed item/price"));
        Register("sell", () =>
        {
            _stock.Delete();
            using var removed = Server.Collections.PooledRefList<Item>.Create();
            foreach (var item in _player.Backpack.Items) { if (item is not Gold) { removed.Add(item); } }
            foreach (var item in removed) { item.Delete(); }
            _hidden = ItemOrigins.Create(ItemOrigin.Crafted, () => new Longsword { Name = "Hidden blade" });
            _player.Backpack.DropItem(_hidden);
            var state = ClassGear.Profiles.Convert(_hidden, 70, GearRarity.Magic, new Random(17));
            ClassGear.Profiles.Store(_hidden, state with { Identified = false, IdentifiedSaleOffer = 300 });
            _before = GoldCount();
            _vendor.VendorSell(_player);
        });
        Register("sold", () => Require(_hidden.Deleted && GoldCount() == _before + 30 && !_vendor.BuyPack.Items.Contains(_hidden),
            "Client sale did not pay the unidentified quote or left buyback stock"));
        Register("final", () => Require(_stock.Deleted && _hidden.Deleted && GoldCount() == _before + 30, "Market UI final state changed"));
    }
    private static int GoldCount() => _player.Backpack.GetAmount(typeof(Gold));
    private static void Setup()
    {
        _player = (PlayerMobile)Accounts.GetAccount("combatui")[0];
        ClassCombat.Stop(_player);
        ClassCreation.Profiles.AdvanceLevel(_player, 99);
        _player.RawStr = 200; _player.RawDex = 200;
        var point = CombatPractice.FindFloor(Map.Felucca, 1382, 1495);
        _player.MoveToWorld(point, Map.Felucca);
        using var removed = Server.Collections.PooledRefList<Item>.Create();
        foreach (var item in _player.Backpack.Items) { removed.Add(item); }
        foreach (var item in removed) { item.Delete(); }
        _player.Backpack.DropItem(new Gold(5000));
        _vendor = new Blacksmith { Frozen = true, CantWalk = true, Name = "Market UI seller" };
        _vendor.MoveToWorld(point, Map.Felucca); _vendor.AIObject.AITimer.Stop();
        var market = TownMarkets.Market(_vendor);
        foreach (var offer in market.Offers) { World.FindItem(offer.Item).Delete(); }
        market.Offers.Clear();
        var recipe = EquipmentCrafting.Recipes.First(r => r.Type == nameof(Longsword) && r.Tier == GearTier.Normal);
        _stock = TownMarkets.Equipment(recipe, 70, GearRarity.Magic, new Random(17));
        _stock.Name = "Shop blade";
        ((MarketShelf)World.FindItem(market.Shelf)).DropItem(_stock);
        market.Offers.Add(new(_stock.Serial, 400, true, null));
        _before = GoldCount();
        _vendor.VendorBuy(_player);
    }
    private static void Register(string command, Action action)
    {
        string name = "market-ui-" + command;
        ConsoleInputHandler.RegisterCommand(name, "Disposable actual shop UI assertions", _ => Core.LoopContext.Post(() =>
        {
            try
            {
                action();
                File.WriteAllText(Environment.GetEnvironmentVariable("SANCTUARY_TEST_UI_STATE"), JsonSerializer.Serialize(new
                { player = _player.Serial.Value, vendor = _vendor.Serial.Value, stock = _stock?.Serial.Value, gold = GoldCount() }));
                _logger.Information("{Command}: passed", name);
            }
            catch (Exception error) { _logger.Error(error, "{Command}: failed", name); }
        }));
    }
    private static void Require(bool value, string message)
    {
        if (!value) { throw new InvalidOperationException(message); }
    }
}
