using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Engines.Craft;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using Server.Regions;
using Server.Targeting;

namespace Sanctuary.Integration;

public static class SurveyChecks
{
    private static readonly ILogger _logger = LogFactory.GetLogger(typeof(SurveyChecks));
    private sealed record Saved(uint Owner, uint Item, string Id, DateTime Expiry, string Context);
    private static string Manifest => Path.Join(Core.BaseDirectory, "survey-checks.json");
    public static void Configure() { Register("survey-check", Check); Register("survey-reload-check", Reload); }
    private static void Register(string name, Action action) => ConsoleInputHandler.RegisterCommand(name, "Map crafting, vendor prices, native treasure search and named-dungeon effects", _ => Core.LoopContext.Post(() =>
    { try { action(); _logger.Information("{Command}: passed", name); } catch (Exception e) { _logger.Error(e, "{Command}: failed", name); } }));
    private static void Require(bool v, string why) => RuneChecks.Require(v, why);
    internal static PlayerMobile Player()
    {
        var p = ImbuingChecks.Player(SkillName.Cartography); FixtureSpace.Place(p,new Point3D(5650, 1100, 0), Map.Trammel);
        p.RawDex = 1; p.RawInt = 1; p.Skills.Cartography.SetLockNoRelay(SkillLock.Locked); CraftFacilities.Register(new(SkillName.Cartography, "Trammel", p.X+1,p.Y,p.Z,0xB4A)); return p;
    }
    internal static CraftTransactions.Request Ticket(SurveyRequest q) => new(q.Owner, DefCartography.CraftSystem, q.Recipe, null, q.Tool, 0) { Survey = q };
    internal static SurveyMap Use(PlayerMobile p, string id)
    { var m = new SurveyMap(id); p.Backpack.DropItem(m); Require(MapSurveys.Use(p, m) && m.Deleted, "Map not consumed " + id); return m; }
    internal static Point3D Inside(string name, int area = 0)
    {
        var r = Region.Regions.OfType<DungeonRegion>().First(r => r.Map == Map.Trammel && r.Name == name);
        var a = r.Area[area]; for (int x=0;x<30;x++)
        {
            var point=new Point3D(a.Start.X+5+x,a.Start.Y+5,0);
            bool occupied=false;
            foreach(var actor in Map.Trammel.GetMobilesInRange<Mobile>(point,0)) { if(actor.Alive && Math.Abs(actor.Z-point.Z)<16) { occupied=true; break; } }
            if(r.Contains(point) && !occupied) { return point; }
        }
        throw new InvalidOperationException("No independent survey region fixture");
    }
    private static void Check()
    {
        var saved = new List<Saved>();
        foreach (var row in MapSurveys.Recipes)
        {
            var p = Player(); var tool = new MapmakersPen(100); p.Backpack.DropItem(tool);
            for (int n = 0; n < 5; n++) { p.Backpack.DropItem(new BlankMap()); }
            if (row.Skill > 0) { p.Skills.Cartography.Base = row.Skill - .1; Require(MapDrawing.Quote(p, tool, row) == null, "Under-skilled recipe allowed"); }
            p.Skills.Cartography.Base = row.Maximum; var q = MapDrawing.Quote(p, tool, row); Require(q != null && q.Chance == 1, "Recipe band wrong");
            var ticket = Ticket(q); Require(CraftTransactions.Complete(ticket, new AlchemyChecks.Roll(0)) && !CraftTransactions.Complete(ticket), "Map craft rejected/replayed");
            var output = p.Backpack.Items.OfType<SurveyMap>().Single(); Require(output.RecipeId == row.Id && output.PlayerConstructed && output.Origin == ItemOrigin.Crafted && tool.UsesRemaining == 99 && p.Backpack.GetAmount(typeof(BlankMap)) == 4, "Wrong map output/cost");
            p.Skills.Cartography.Base = (row.Skill + row.Maximum) / 2; q = MapDrawing.Quote(p, tool, row);
            Require(q.Chance is > 0 and < 1 && CraftTransactions.Complete(Ticket(q), new AlchemyChecks.Roll()) && tool.UsesRemaining == 98 && p.Backpack.GetAmount(typeof(BlankMap)) == 3, "Map failure/gain band wrong");
            FoodEffects.Apply(p, Meals.Find("porter-flatbread")); CombatPotions.Apply(p, 5);
            var food = TimedEffects.Buff(p, TimedEffectCategory.Food); var potion = TimedEffects.Buff(p, TimedEffectCategory.Potion);
            var copy = new SurveyMap(row.Id); p.Backpack.DropItem(copy);
            if (row.Effect == "dungeon")
            {
                Require(!MapSurveys.Use(p, copy) && !copy.Deleted, "Dungeon map consumed outside dungeon"); p.MoveToWorld(Inside("Deceit"), Map.Trammel);
            }
            p.Skills.Cartography.Base = 0; ClassProfessions.Profiles.Select(p, SkillName.Alchemy);
            Require(MapSurveys.Use(p, copy) && !MapSurveys.Use(p, copy), "Map needed maker skill or replayed");
            var buff = TimedEffects.Buff(p, TimedEffectCategory.Map);
            Require(buff.Id == "survey-" + row.Effect && buff.Magnitude == row.Magnitude && (buff.ExpiresUtc - TimedEffects.Now).TotalSeconds is > 1799 and <= 1800
                && TimedEffects.Buff(p, TimedEffectCategory.Food) == food && TimedEffects.Buff(p, TimedEffectCategory.Potion) == potion, "Map duration or independent slots wrong");
            saved.Add(new(p.Serial.Value, output.Serial.Value, row.Id, buff.ExpiresUtc, buff.Context));
        }
        File.WriteAllText(Manifest, JsonSerializer.Serialize(saved));
        Transactions(); Dungeons(); Prices(); Search();
    }
    private static void Transactions()
    {
        var p = Player(); var tool = new MapmakersPen(100); p.Backpack.DropItem(tool); var blank = new BlankMap(); p.Backpack.DropItem(blank); var r = MapSurveys.Find("city");
        SurveyRequest Quote() => MapDrawing.Quote(p, tool, r);
        var q = Quote(); MapDrawing.Invalidate(p); Require(!CraftTransactions.Complete(Ticket(q)) && !blank.Deleted && tool.UsesRemaining == 100, "Cancelled craft consumed");
        q = Quote(); blank.Hue = 1; Require(!CraftTransactions.Complete(Ticket(q)), "Changed blank map accepted"); blank.Hue = 0;
        q = Quote(); ClassProfessions.Profiles.Select(p, SkillName.Alchemy); Require(!CraftTransactions.Complete(Ticket(q)), "Changed craft completed"); ClassProfessions.Profiles.Select(p, SkillName.Cartography);
        q = Quote(); p.MoveToWorld(new Point3D(5670, 1100, 0), p.Map); Require(!CraftTransactions.Complete(Ticket(q)), "Remote map crafting accepted"); FixtureSpace.Place(p,new Point3D(5650, 1100, 0), p.Map);
        q = Quote(); p.Backpack.MaxItems = 1; p.Backpack.DropItem(new Gold(1));
        // A whole blank map is replaced by one output, so a full pack remains a valid exchange.
        Require(CraftTransactions.Complete(Ticket(q), new AlchemyChecks.Roll(0)) && tool.UsesRemaining == 99, "Slot-neutral map exchange rejected"); p.Backpack.MaxItems = 125;
        var forged = new CraftItem(typeof(SurveyMap), "Forged", "Forged"); forged.AddSkill(SkillName.Cartography, 0, 30);
        Require(!CraftTransactions.Complete(new(p, DefCartography.CraftSystem, forged, null, tool, 0)), "Unbound map granted output");
        p.Backpack.DropItem(new BlankMap()); tool.Delete(); tool = ItemOrigins.Create(ItemOrigin.Crafted, () => new MapmakersPen(100)); p.Backpack.DropItem(tool); CraftTransactions.Profiles.Store(tool, new(CraftQuality.Superior, true, 100));
        Require(CraftTransactions.Complete(Ticket(Quote()), new AlchemyChecks.Roll(0, 0)) && tool.UsesRemaining == 100, "Crafted pen saving absent");
        var ground = new SurveyMap(); ground.MoveToWorld(p.Location, p.Map); Require(!MapSurveys.Use(p, ground), "Ground map consumed"); ground.Delete();
        Use(p, "city"); var before = TimedEffects.Buff(p, TimedEffectCategory.Map); TimedEffects.ApplyBuff(p, TimedEffectCategory.Map, "survey-city", 5, at: TimedEffects.Now.AddMinutes(-20));
        Use(p, "city"); Require(TimedEffects.Buff(p, TimedEffectCategory.Map).ExpiresUtc >= before.ExpiresUtc, "Map did not refresh");
        Use(p, "world"); Require(TimedEffects.Buff(p, TimedEffectCategory.Map).Id == "survey-world", "Maps stacked");
        TimedEffects.Settle(p, TimedEffects.Now.AddMinutes(31)); Require(!MapSurveys.WorldBonus(p), "Offline map time paused");
        var death = Player(); Use(death, "city"); death.Kill(); Require(TimedEffects.Buff(death, TimedEffectCategory.Map) == null, "Death retained map");
    }
    private static void Dungeons()
    {
        var p = Player(); p.MoveToWorld(Inside("Ice"), Map.Trammel); Use(p, "dungeon-3");
        Require(GearDrops.MagicFind(p) == 30 && ClassStats.Snapshot(p)[(int)StatField.MagicFind] == 30, "Map MF consumer/status missing");
        p.MoveToWorld(Inside("Ice", 1), Map.Trammel); Require(GearDrops.MagicFind(p) == 30, "Floor transition lost map");
        var parent = p.Region.GetRegion<DungeonRegion>(); var child = new BaseRegion("Survey child", p.Map, parent, new Rectangle3D(p.X - 1, p.Y - 1, -128, 4, 4, 256)); child.Register();
        Require(MapSurveys.Dungeon(p) == "Trammel/Ice", "Nested subregion changed dungeon"); child.Unregister();
        var victim = new Skeleton(); var corpse = GearDropChecks.Body(victim, p, p.Location, p.Map); var roll = new GearDropChecks.PathRoll(130, 0, 0, 0);
        RewardLife.Process(victim, corpse, roll); Require(roll.Bounds.Contains(GearDrops.Denominator(GearDrops.Base("hax"), CreatureProfiles.Level(victim), 30, DropQuality.Unique)), "Death reward missed map MF"); corpse.Delete(); victim.Delete();
        FixtureSpace.Place(p,new Point3D(5650, 1100, 0), p.Map); p.MoveToWorld(Inside("Ice"), p.Map);
        Require(TimedEffects.Buff(p, TimedEffectCategory.Map) == null, "Exit/re-entry revived map within one tick");
        Use(p, "dungeon-2"); p.MoveToWorld(p.Location, Map.Felucca); Require(TimedEffects.Buff(p, TimedEffectCategory.Map) == null, "Facet change retained map");
        p.MoveToWorld(Inside("Ice"), Map.Trammel); Use(p, "dungeon-1"); var buff = TimedEffects.Buff(p, TimedEffectCategory.Map); var point = p.Location;
        p.LogoutMap = p.Map; p.LogoutLocation = p.Location; p.Internalize(); Require(TimedEffects.Buff(p, TimedEffectCategory.Map) == buff, "Logout erased dungeon map");
        p.MoveToWorld(point, Map.Trammel); Require(TimedEffects.Buff(p, TimedEffectCategory.Map) == buff, "Login reset/lost map");
    }
    private static void Prices()
    {
        var p = Player(); p.AccessLevel = AccessLevel.Player; Use(p, "city");
        Require(MapSurveys.PurchasePrice(p, 1) == 1 && MapSurveys.PurchasePrice(p, 19) == 19 && MapSurveys.PurchasePrice(p, 20) == 19 && MapSurveys.PurchasePrice(p, 101) == 96, "Price rounding wrong");
        var v = new Blacksmith { Frozen = true, CantWalk = true }; v.MoveToWorld(CombatPractice.FindFloor(Map.Felucca, 1382, 1495), Map.Felucca); v.AIObject.AITimer.Stop(); p.MoveToWorld(v.Location, v.Map); var market = TownMarkets.Market(v);
        var offer = market.Offers.First(o => o.Equipment); p.Backpack.DropItem(new Gold(60000)); int gold = p.Backpack.GetAmount(typeof(Gold)); int price = MapSurveys.PurchasePrice(p, offer.Price);
        v.VendorBuy(p); Require(v.OnBuyItems(p, [new BuyItemResponse(offer.Item, 1)]) && p.Backpack.GetAmount(typeof(Gold)) == gold - price, "Discount payment wrong");
        var item = World.FindItem(offer.Item); Require(ClassGear.State(item).VendorPurchasePrice == price && TradePrices.Quote(item) == price / 10, "Vendor resale did not retain paid amount");
        offer = market.Offers.First(o => o.Equipment); v.VendorBuy(p); gold = p.Backpack.GetAmount(typeof(Gold)); TimedEffects.RemoveBuff(p, TimedEffectCategory.Map);
        Require(ClassEconomyHooks.PurchasePrice(v, p, 400) == 380, "Displayed batch quote changed after expiry");
        Require(!v.OnBuyItems(p, [new BuyItemResponse(offer.Item, 1)]) && p.Backpack.GetAmount(typeof(Gold)) == gold, "Expired discount silently increased charge");
        v.VendorBuy(p); Use(p, "city"); Require(!v.OnBuyItems(p, [new BuyItemResponse(offer.Item, 1)]), "Changed quote accepted");
    }
    private static void Search()
    {
        var p = Player(); p.MoveToWorld(CombatPractice.FindFloor(Map.Trammel, 5716, 1140), Map.Trammel); p.Skills.Mining.Base = 0;
        var map = new TreasureMap(1, p.Map) { Decoder = p, ChestLocation = new Point2D(5712, 1140) }; p.Backpack.DropItem(map); p.Backpack.DropItem(new Shovel());
        void Dig() { map.OnBeginDig(p); Require(p.Target != null, "No native dig target"); p.Target.Invoke(p, new LandTarget(new Point3D(5717, 1140, p.Z), p.Map)); p.Target = null; }
        Dig(); Require(p.CanBeginAction<TreasureMap>(), "Baseline search unexpectedly found chest"); Use(p, "world"); Dig();
        Require(!p.CanBeginAction<TreasureMap>() && ClassMapHooks.SearchRange(p, 4) == 9, "World map did not change actual native search");
        p.NextActionTime++; p.EndAction<TreasureMap>(); map.Delete();
    }
    private static void Reload()
    {
        foreach (var s in JsonSerializer.Deserialize<Saved[]>(File.ReadAllText(Manifest)))
        {
            var p = (PlayerMobile)World.FindMobile((Serial)s.Owner); var m = World.FindItem((Serial)s.Item) as SurveyMap; var b = TimedEffects.Buff(p, TimedEffectCategory.Map);
            Require(m is { PlayerConstructed: true, Origin: ItemOrigin.Crafted } && m.RecipeId == s.Id && m.Parent == p.Backpack && b.ExpiresUtc == s.Expiry && b.Context == s.Context, "Saved map identity/expiry/context changed");
            Require(b.Id != "survey-dungeon" || MapSurveys.MagicFind(p) == b.Magnitude, "Reloaded dungeon MF absent");
        }
    }
}
