using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Accounting;
using Server.Gumps;
using Server.Items;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class SurveyUiChecks
{
    private static readonly ILogger _logger = LogFactory.GetLogger(typeof(SurveyUiChecks));
    private static PlayerMobile _p;
    private static MapmakersPen _pen;
    private static SurveyMap _map;
    private static Blacksmith _vendor;
    private static Item _stock;
    private static int _gold;
    public static void Configure()
    {
        Register("setup", () =>
        {
            _p = (PlayerMobile)Accounts.GetAccount("combatui")[0]; ClassCombat.Stop(_p); _p.Target = null;
            foreach (var i in _p.Backpack.Items.ToArray()) { i.Delete(); }
            ClassCreation.Profiles.AdvanceLevel(_p, 99); _p.RawStr = 1000; TimedEffects.Clear(_p);
            ClassProfessions.Profiles.Select(_p, SkillName.Cartography); _p.Skills.Cartography.Cap = 120; _p.Skills.Cartography.Base = 120; _p.Skills.Cartography.SetLockNoRelay(SkillLock.Locked);
            _p.MoveToWorld(CombatPractice.FindFloor(Map.Trammel, 5650, 1100), Map.Trammel);
            CraftFacilities.Register(new(SkillName.Cartography, "Trammel", _p.X + 1, _p.Y, _p.Z, 0xB4A));
            _pen = new MapmakersPen(20); _p.Backpack.DropItem(_pen); _p.Backpack.DropItem(new BlankMap()); _p.Backpack.DropItem(new BlankMap()); _pen.OnDoubleClick(_p);
        });
        Register("quoted", () => Require(_p.FindGump<SurveyGump>() != null, "Map confirmation missing"));
        Register("cancelled", () => { Require(_p.FindGump<SurveyGump>() == null && _pen.UsesRemaining == 20 && _p.Backpack.GetAmount(typeof(BlankMap)) == 2, "Cancel consumed resources"); _pen.OnDoubleClick(_p); });
        Register("crafted", () =>
        {
            _map = _p.Backpack.Items.OfType<SurveyMap>().Single();
            Require(_map.RecipeId == "city" && _map.Origin == ItemOrigin.Crafted && _pen.UsesRemaining == 19 && _p.Backpack.GetAmount(typeof(BlankMap)) == 1, "Native map timer/cost wrong");
            foreach (var i in _p.Backpack.Items) { i.Location = new Point3D(100, 120, 0); } _map.Location = new Point3D(45, 65, 0); _p.Backpack.OnDoubleClick(_p);
        });
        Register("used", () => Require(_map.Deleted && MapSurveys.City(_p) != null, "Inventory map double click did not consume/apply"));
        Register("shop", () =>
        {
            var point = CombatPractice.FindFloor(Map.Felucca, 1382, 1495); _p.MoveToWorld(point, Map.Felucca); _p.Backpack.DropItem(new Gold(5000));
            _vendor = new Blacksmith { Frozen = true, CantWalk = true, Name = "Survey shop" }; _vendor.MoveToWorld(point, Map.Felucca); _vendor.AIObject.AITimer.Stop(); Stock();
        });
        Register("bought", () => Require(_stock.Parent == _p.Backpack && Gold() == _gold - 380 && ClassGear.State(_stock).VendorPurchasePrice == 380, "Displayed map discount did not match payment"));
        Register("stale-shop", Stock);
        Register("expire", () => TimedEffects.RemoveBuff(_p, TimedEffectCategory.Map));
        Register("stale-rejected", () => Require(_stock.Parent is MarketShelf && Gold() == _gold, "Stale discount silently changed payment"));
        Register("reopen", () => _vendor.VendorBuy(_p));
        Register("final", () => Require(_stock.Parent == _p.Backpack && Gold() == _gold - 400 && ClassGear.State(_stock).VendorPurchasePrice == 400 && _pen.UsesRemaining == 19, "Reopened ordinary price/state wrong"));
    }
    private static int Gold() => _p.Backpack.GetAmount(typeof(Gold));
    private static void Stock()
    {
        var state = TownMarkets.Market(_vendor); foreach (var o in state.Offers) { World.FindItem(o.Item).Delete(); } state.Offers.Clear();
        var r = EquipmentCrafting.Recipes.First(r => r.Type == "Longsword" && r.Tier == GearTier.Normal); _stock = TownMarkets.Equipment(r, 70, GearRarity.Magic, new Random(17)); _stock.Name = "Survey shop blade";
        ((MarketShelf)World.FindItem(state.Shelf)).DropItem(_stock); state.Offers.Add(new(_stock.Serial, 400, true, null)); _gold = Gold(); _vendor.VendorBuy(_p);
    }
    private static void Require(bool v, string why) => RuneChecks.Require(v, why);
    private static void Register(string suffix, Action action)
    {
        string name = "survey-ui-" + suffix;
        ConsoleInputHandler.RegisterCommand(name, "Actual map book, consumption and discounted shop quote", _ => Core.LoopContext.Post(() =>
        { try { action(); File.WriteAllText(Environment.GetEnvironmentVariable("SANCTUARY_TEST_UI_STATE"), JsonSerializer.Serialize(new { uses = _pen.UsesRemaining, gold = Gold() })); _logger.Information("{Command}: passed", name); } catch (Exception e) { _logger.Error(e, "{Command}: failed", name); } }));
    }
}
