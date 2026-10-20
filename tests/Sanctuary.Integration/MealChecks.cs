using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Engines.Craft;
using Server.Engines.Harvest;
using Server.Items;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class MealChecks
{
    private static readonly ILogger _logger = LogFactory.GetLogger(typeof(MealChecks));
    private sealed record Saved(uint Item, string Id, uint Parent, uint Owner, int Magnitude, DateTime Expiry);
    private static string Manifest => Path.Join(Core.BaseDirectory, "meal-checks.json");
    public static void Configure()
    {
        Register("meal-check", done => { Recipes(); Effects(); Carving(); Aggression(); NativeHarvest(done); });
        Register("meal-reload-check", done => { Reload(); done(); });
    }
    private static void Register(string name, Action<Action> action) => ConsoleInputHandler.RegisterCommand(name, "Meal recipes, effects and real gathering", _ => Core.LoopContext.Post(() =>
    { try { action(() => _logger.Information("{Command}: passed", name)); } catch (Exception e) { _logger.Error(e, "{Command}: failed", name); } }));
    private static void Require(bool value, string why) => RuneChecks.Require(value, why);
    internal static PlayerMobile Player()
    {
        var p = ImbuingChecks.Player(SkillName.Cooking); p.MoveToWorld(new Point3D(5450, 1160, 0), Map.Trammel);
        p.Skills.Cooking.SetLockNoRelay(SkillLock.Locked);
        CraftFacilities.Register(new(SkillName.Cooking, "Trammel", 5451, 1160, 0, 0x461)); return p;
    }
    internal static void Inputs(PlayerMobile p, MealRecipe r, int batches = 1)
    {
        foreach (var ingredient in r.Ingredients)
        {
            var item = EquipmentCrafting.Resolve(ingredient.Type).CreateInstance<Item>();
            if (item is not SackFlour) { item.Amount = ingredient.Amount * batches; }
            p.Backpack.DropItem(item);
        }
        p.Backpack.DropItem(new WoodenChips(r.Chips * batches));
    }
    internal static CraftTransactions.Request Ticket(MealRequest q) => new(q.Owner, DefCooking.CraftSystem, q.Recipe, null, q.Tool, 0) { Meal = q };
    private static int Available(PlayerMobile p, Type type)
    { int count = 0; foreach (var i in p.Backpack.FindItemsByType<Item>(true)) { if (i.GetType() == type) { count += MealInput.Available(i); } } return count; }
    private static void Recipes()
    {
        var saved = new List<Saved>();
        foreach (var r in Meals.Recipes)
        {
            var p = Player(); var tool = new Skillet(100); p.Backpack.DropItem(tool); Inputs(p, r, 3);
            if (r.Skill > 0) { p.Skills.Cooking.Base = r.Skill - .1; Require(MealCooking.Quote(p, tool, r) == null, "Under-skilled meal allowed"); }
            p.Skills.Cooking.Base = 120; var q = MealCooking.Quote(p, tool, r);
            Require(q != null && Math.Abs(q.Chance - Math.Min(1, (120 - r.Skill) / 50)) < 1e-9, "Meal curve wrong");
            var ticket = Ticket(q); Require(CraftTransactions.Complete(ticket, new AlchemyChecks.Roll(0)) && !CraftTransactions.Complete(ticket), "Cook failed/replayed");
            var meal = p.Backpack.Items.OfType<PreparedMeal>().Single();
            Require(meal.RecipeId == r.Id && meal.Amount == 1 && meal.Origin == ItemOrigin.Crafted && meal.PlayerConstructed
                && tool.UsesRemaining == 99 && p.Backpack.GetAmount(typeof(WoodenChips)) == r.Chips * 2, "Wrong output/fuel/tool");
            foreach (var input in r.Ingredients)
            {
                int remaining = Available(p, EquipmentCrafting.Resolve(input.Type));
                Require(remaining == (input.Type == "SackFlour" ? 20 : input.Amount * 3) - input.Amount, "Wrong ingredient portions");
            }
            p.Skills.Cooking.Base = Math.Min(120, r.Skill + 10); q = MealCooking.Quote(p, tool, r);
            Require(q != null && CraftTransactions.Complete(Ticket(q), new AlchemyChecks.Roll()), "Failed attempt rejected");
            Require(tool.UsesRemaining == 98 && p.Backpack.GetAmount(typeof(WoodenChips)) == r.Chips * 2 - (r.Chips + 1) / 2
                && p.Backpack.Items.OfType<PreparedMeal>().Sum(i => i.Amount) == 1, "Failure fuel/tool/output wrong");
            foreach (var input in r.Ingredients)
            {
                int remaining = Available(p, EquipmentCrafting.Resolve(input.Type));
                Require(remaining == (input.Type == "SackFlour" ? 20 : input.Amount * 3) - input.Amount - (input.Amount + 1) / 2, "Failure not half each ingredient");
            }
            Require(FoodEffects.Apply(p, r), "Saved meal buff rejected"); var buff = TimedEffects.Buff(p, TimedEffectCategory.Food);
            saved.Add(new(meal.Serial.Value, meal.RecipeId, p.Backpack.Serial.Value, p.Serial.Value, r.Magnitude, buff.ExpiresUtc));
        }
        File.WriteAllText(Manifest, JsonSerializer.Serialize(saved));
        var owner = Player(); var skillet = new Skillet(100); owner.Backpack.DropItem(skillet); var row = Meals.Find("porter-flatbread"); Inputs(owner, row, 3);
        MealRequest Quote() => MealCooking.Quote(owner, skillet, row);
        var flour = owner.Backpack.Items.OfType<SackFlour>().Single(); var quote = Quote(); flour.Quantity--;
        Require(!CraftTransactions.Complete(Ticket(quote), new AlchemyChecks.Roll(0)) && skillet.UsesRemaining == 100, "Changed flour portions accepted");
        quote = Quote(); MealCooking.Invalidate(owner); Require(!CraftTransactions.Complete(Ticket(quote)), "Cancelled meal replayed");
        quote = Quote(); owner.Backpack.MaxItems = owner.Backpack.TotalItems;
        Require(!CraftTransactions.Complete(Ticket(quote), new AlchemyChecks.Roll(0)) && skillet.UsesRemaining == 100 && flour.Quantity == 19, "Full pack spent food/fuel/tool"); owner.Backpack.MaxItems = 125;
        quote = Quote(); ClassProfessions.Profiles.Select(owner, SkillName.Alchemy);
        Require(!CraftTransactions.Complete(Ticket(quote)), "Craft change completed meal"); ClassProfessions.Profiles.Select(owner, SkillName.Cooking);
        quote = Quote(); owner.MoveToWorld(new Point3D(owner.X + 30, owner.Y, owner.Z), owner.Map);
        Require(!CraftTransactions.Complete(Ticket(quote)), "Remote cook completed"); owner.MoveToWorld(new Point3D(5450, 1160, 0), Map.Trammel);
        skillet.Delete(); skillet = ItemOrigins.Create(ItemOrigin.Crafted, () => new Skillet(100)); owner.Backpack.DropItem(skillet); CraftTransactions.Profiles.Store(skillet, new(CraftQuality.Superior, true, 100));
        quote = Quote(); Require(CraftTransactions.Complete(Ticket(quote), new AlchemyChecks.Roll(0, 0)) && skillet.UsesRemaining == 100, "Crafted skillet saving missing");
        var forged = new CraftItem(typeof(PreparedMeal), "Meal", "Forged"); forged.AddSkill(SkillName.Cooking, 0, 50);
        Require(!CraftTransactions.Complete(new(owner, DefCooking.CraftSystem, forged, null, skillet, 0), new AlchemyChecks.Roll(0)), "Unbound meal bypassed quote");
    }
    private static void Effects()
    {
        var p = Player(); p.Hunger = 20; p.Stam = 1; int capacity = p.MaxWeight;
        var apple = new Apple(); p.Backpack.DropItem(apple);
        Require(apple.Eat(p) && apple.Deleted && p.Hunger == 20 && p.Stam == 1, "Legacy food retained a hunger requirement/restoration");
        CombatPotions.Apply(p, 5); var potion = TimedEffects.Buff(p, TimedEffectCategory.Potion);
        foreach (var r in Meals.Recipes)
        {
            var meal = new PreparedMeal(r.Id); p.Backpack.DropItem(meal);
            long action = ClassCombat.For(p).NextAction = Core.TickCount + 5000, recovery = ClassCombat.For(p).RecoveryUntil = Core.TickCount + 2000;
            Require(meal.Eat(p) && meal.Deleted && p.Hunger == 20 && p.Stam == 1 && ClassCombat.For(p).NextAction == action
                && ClassCombat.For(p).RecoveryUntil == recovery && TimedEffects.Buff(p, TimedEffectCategory.Potion) == potion, "Eating changed hunger/stamina/action/potion");
            var buff = TimedEffects.Buff(p, TimedEffectCategory.Food);
            Require(buff.Id == "food-" + r.Effect && buff.Magnitude == r.Magnitude && TimedEffects.Snapshot().Single(s => s.Owner == p.Serial.Value).Buffs.Length == 2, "Food stacked/magnitude wrong");
            Require(p.MaxWeight == capacity + (r.Effect == "capacity" ? r.Magnitude : 0) && ClassStats.Snapshot(p)[(int)StatField.FoodLight] == (r.Effect == "light" ? r.Magnitude : 0), "Capacity/light consumer wrong");
        }
        TimedEffects.Clear(p); DateTime now = TimedEffects.Now; var carry = Meals.Find("giant-hearthpot");
        FoodEffects.Apply(p, carry, now); FoodEffects.Apply(p, carry, now.AddMinutes(15));
        Require(TimedEffects.Buff(p, TimedEffectCategory.Food, now).ExpiresUtc == now.AddMinutes(75), "Food did not refresh one hour");
        var heavy = new IronIngot(1000); p.Backpack.DropItem(heavy); int count = p.Backpack.TotalItems;
        TimedEffects.Settle(p, now.AddMinutes(76)); Require(p.MaxWeight == capacity && !heavy.Deleted && heavy.Parent == p.Backpack && p.Backpack.TotalItems == count, "Expiry spilled inventory");
        TimedEffects.Clear(p); FoodEffects.Apply(p, carry); ClassProfessions.Profiles.Select(p, SkillName.Alchemy);
        Require(p.MaxWeight == capacity + 74, "Craft switch cleared food");
        p.Kill(); Require(FoodEffects.Bonus(p, "capacity") == 0, "Death retained meal");
        var other = Player(); var ground = new PreparedMeal(carry.Id); ground.MoveToWorld(other.Location, other.Map);
        Require(!ground.Eat(other) && !ground.Deleted, "Ground meal consumed"); ground.Delete();
    }
    private static void Carving()
    {
        var p = Player(); FoodEffects.Apply(p, Meals.Find("master-hunter-roast")); var knife = new ButcherKnife(); p.Backpack.DropItem(knife);
        foreach (var c in new BaseCreature[] { new Cow(), new Bird(), new Lizardman(), new VampireBat(), new Mongbat(), new Daemon() })
        {
            var corpse = GearDropChecks.Body(c, p, p.Location); corpse.Carve(p, knife);
            Require(corpse.Carved, "Eligible corpse did not carve");
            if (c.Meat > 0) { Require(corpse.Items.Where(i => i is RawRibs or RawBird or RawLambLeg).Sum(i => i.Amount) == c.Meat + 3, "Carving meat bonus wrong"); }
            if (c.Feathers > 0) { Require(corpse.GetAmount(typeof(Feather)) == c.Feathers + 3, "Feather bonus wrong"); }
            if (c.Hides > 0) { Require(corpse.Items.OfType<BaseHides>().Sum(i => i.Amount) == (p.Race == Race.Human ? (int)Math.Ceiling(c.Hides * 1.1) : c.Hides) + 3, "Hide bonus wrong"); }
            if (c is VampireBat or Mongbat) { Require(corpse.GetAmount(typeof(BatWing)) == 1, "Bat wings unavailable from bats"); }
            if (c is Daemon) { Require(corpse.GetAmount(typeof(DaemonBlood)) == 1, "Daemon blood unavailable"); }
            int count = corpse.Items.Sum(i => i.Amount); corpse.Carve(p, knife); Require(corpse.Items.Sum(i => i.Amount) == count, "Repeated carve yielded again");
            corpse.Delete(); c.Delete();
        }
        var vendor = TownMarkets.Profiles.At("Felucca/Britain/Alchemist"); var market = TownMarkets.Market(vendor);
        foreach (var t in new[] { typeof(BatWing), typeof(DaemonBlood) }) { Require(market.Offers.Any(o => World.FindItem(o.Item)?.GetType() == t), "Unusual ingredient has no shop fallback"); }
    }
    private static void Aggression()
    {
        var p = Player(); p.AccessLevel = AccessLevel.Player; var c = new Ogre(); c.MoveToWorld(p.Location, p.Map); c.AIObject.AITimer.Stop();
        FoodEffects.Apply(p, Meals.Find("ghostwalk-supper")); long now = Core.TickCount;
        Require(!FoodEffects.CanAcquire(c, p, now) && !FoodEffects.CanAcquire(c, p, now + 2999) && FoodEffects.CanAcquire(c, p, now + 3000), "Aggro delay length wrong");
        FoodEffects.Apply(p, Meals.Find("ghostwalk-supper")); Require(FoodEffects.CanAcquire(c, p, now + 4000), "Re-eating reset encounter delay");
        p.MoveToWorld(new Point3D(p.X + 50, p.Y, p.Z), p.Map); FoodEffects.Observe(now + 5000); FoodEffects.Observe(now + 14999);
        p.MoveToWorld(c.Location, c.Map); Require(FoodEffects.CanAcquire(c, p, now + 14999), "Short absence reset encounter");
        p.MoveToWorld(new Point3D(p.X + 50, p.Y, p.Z), p.Map); FoodEffects.Observe(now + 15000); FoodEffects.Observe(now + 25000);
        p.MoveToWorld(c.Location, c.Map); Require(!FoodEffects.CanAcquire(c, p, now + 25000), "Ten-second absence failed to reset");
        p.DoHarmful(c); Require(FoodEffects.CanAcquire(c, p, now + 25001), "Attacking did not cancel delay"); c.Delete();
        p.MoveToWorld(new Point3D(6060, 1000, 0), Map.Trammel);
        c = new Ogre(); c.MoveToWorld(p.Location, p.Map); c.AIObject.AITimer.Stop(); c.NextReacquireTime = Core.TickCount;
        Require(!c.AIObject.AcquireFocusMob(c.RangePerception, FightMode.Closest, true, false, true), "Native AI bypassed food delay");
        Require(c.NextReacquireTime - Core.TickCount is > 0 and <= 3000, "Native reacquire ignored food deadline"); c.Delete();
    }
    private sealed class Harvest : HarvestSystem
    {
        internal HarvestDefinition Definition;
        internal readonly List<long> Times = [];
        internal int Delivered;
        public override bool GetHarvestDetails(Mobile from, Item tool, object target, out int tile, out Map map, out Point3D location, out bool land)
        { tile = 1; map = from.Map; location = from.Location; land = false; return true; }
        public override bool Give(Mobile from, Item item, bool feet) { Delivered += item.Amount; Times.Add(Core.TickCount); return base.Give(from, item, feet); }
        internal Harvest(HarvestDefinition def) { Definition = def; Definitions = [def]; }
    }
    private static void NativeHarvest(Action done)
    {
        var p = Player(); var r = new HarvestResource(0, -100, -1, null, typeof(Log));
        var def = new HarvestDefinition { Skill = SkillName.Lumberjacking, StaticTiles = [1], BankWidth = 1, BankHeight = 1,
            MinTotal = 10000, MaxTotal = 10000, MinRespawn = TimeSpan.FromMinutes(1), MaxRespawn = TimeSpan.FromMinutes(1),
            MaxRange = 2, ConsumedPerHarvest = 1, ConsumedPerFeluccaHarvest = 1, EffectCounts = [3], EffectActions = [], EffectSounds = [],
            EffectDelay = TimeSpan.FromSeconds(1.6), EffectSoundDelay = TimeSpan.FromSeconds(.9),
            Veins = [new HarvestVein(1000, 0, r, null)], Resources = [r], BonusResources = [] };
        var harvest = new Harvest(def); var tool = new Hatchet { UsesRemaining = 1000 }; p.Backpack.DropItem(tool); FoodEffects.Apply(p, Meals.Find("fleetfinger-feast"));
        long began = Core.TickCount; harvest.StartHarvesting(p, tool, null);
        Timer.DelayCall(TimeSpan.FromMilliseconds(1400), () =>
        {
            try
            {
                Require(harvest.Times.Count == 1 && harvest.Times[0] - began is >= 1050 and <= 1300, "Native multi-effect harvest not reduced4.1s to1.1s");
                TimedEffects.Clear(p); FoodEffects.Apply(p, Meals.Find("red-harvest-ragout"));
                foreach (var item in new Item[] { new Log(10), new IronOre(1), new Fish(1), new Runebark(1), new SocketGem("Ruby", GemQuality.Chipped) })
                {
                    var d = new HarvestDefinition { Skill = item is Log or Runebark ? SkillName.Lumberjacking : item is Fish ? SkillName.Fishing : SkillName.Mining };
                    int before = item.Amount; FoodEffects.HarvestExtra(p, d, item, new AlchemyChecks.Roll(0));
                    Require(item.Amount == before + (item is Log or BaseOre or Fish ? 3 : 0), "Primary yield/byproduct boundary wrong"); item.Delete();
                }
                int callbacks = 0; var original = ClassHarvestHooks.Successful;
                try
                {
                    ClassHarvestHooks.Successful = (m, d, trained, type) => { callbacks++; original(m, d, trained, type); };
                    for (int i = 0; i < 100; i++) { harvest.FinishHarvesting(p, tool, def, null, harvest); }
                    Require(callbacks == 100 && harvest.Delivered is >= 101 and <= 401, "Food repeated byproduct callbacks");
                }
                finally { ClassHarvestHooks.Successful = original; }
                TimedEffects.Clear(p); done();
            }
            catch (Exception e) { _logger.Error(e, "meal-check: failed"); }
        });
    }
    private static void Reload()
    {
        foreach (var s in JsonSerializer.Deserialize<Saved[]>(File.ReadAllText(Manifest)))
        {
            Require(World.FindItem((Serial)s.Item) is PreparedMeal m && m.RecipeId == s.Id && m.Amount == 1 && m.Origin == ItemOrigin.Crafted
                && m.PlayerConstructed && (m.Parent as Item)?.Serial.Value == s.Parent, "Saved meal changed");
            var b = TimedEffects.Buff(World.FindMobile((Serial)s.Owner), TimedEffectCategory.Food);
            Require(b != null && b.Magnitude == s.Magnitude && b.ExpiresUtc == s.Expiry, "Food reload reset expiry/magnitude");
        }
    }
}
