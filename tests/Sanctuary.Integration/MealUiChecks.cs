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
using Server.Misc;

namespace Sanctuary.Integration;

public static class MealUiChecks
{
    private static readonly ILogger _logger = LogFactory.GetLogger(typeof(MealUiChecks));
    private static PlayerMobile _p;
    private static Skillet _tool;
    private static SackFlour _flour;
    private static PreparedMeal _meal;
    private static Item _heavy;
    private static TimedBuff _potion;
    private static int _capacity, _lightOverride;
    public static void Configure()
    {
        Register("setup", () =>
        {
            _p = (PlayerMobile)Accounts.GetAccount("combatui")[0]; ClassCombat.Stop(_p); _p.Target = null;
            foreach (var item in _p.Backpack.Items.ToArray()) { item.Delete(); }
            ClassCreation.Profiles.AdvanceLevel(_p, 99); _p.RawStr = 40; TimedEffects.Clear(_p); _p.Hunger = 20;
            ClassProfessions.Profiles.Select(_p, SkillName.Cooking); _p.Skills.Cooking.Cap = 120; _p.Skills.Cooking.Base = 120;
            _p.Skills.Cooking.SetLockNoRelay(SkillLock.Locked);
            _p.MoveToWorld(CombatPractice.FindFloor(Map.Trammel, 5450, 1160), Map.Trammel);
            CraftFacilities.Register(new(SkillName.Cooking, "Trammel", _p.X + 1, _p.Y, _p.Z, 0x461));
            _capacity = _p.MaxWeight; _tool = new Skillet(20); _p.Backpack.DropItem(_tool);
            MealChecks.Inputs(_p, Meals.Find("porter-flatbread")); _flour = _p.Backpack.Items.OfType<SackFlour>().Single(); _tool.OnDoubleClick(_p);
        });
        Register("quoted", () => Require(_p.FindGump<MealGump>() != null, "Meal confirmation missing"));
        Register("cancelled", () =>
        {
            Require(_tool.UsesRemaining == 20 && _flour.Quantity == 20 && _p.Backpack.GetAmount(typeof(WoodenChips)) == 5, "Cancel consumed food/fuel");
            _tool.OnDoubleClick(_p);
        });
        Register("crafted", () =>
        {
            _meal = _p.Backpack.Items.OfType<PreparedMeal>().Single();
            Require(_meal.RecipeId == "porter-flatbread" && _meal.Amount == 1 && _meal.Origin == ItemOrigin.Crafted
                && _meal.PlayerConstructed && _tool.UsesRemaining == 19 && _flour.Quantity == 19 && _p.Backpack.GetAmount(typeof(WoodenChips)) == 0, "Native cooked meal output/cost wrong");
            CombatPotions.Apply(_p, 5); _potion = TimedEffects.Buff(_p, TimedEffectCategory.Potion);
            _meal.Location = new Point3D(45, 65, 0); _tool.Location = new Point3D(100, 105, 0); _flour.Location = new Point3D(90, 120, 0); _p.Backpack.OnDoubleClick(_p);
        });
        Register("eaten", () =>
        {
            Require(_meal.Deleted && _p.MaxWeight == _capacity + 25 && _p.Hunger == 20 && TimedEffects.Buff(_p, TimedEffectCategory.Potion) == _potion, $"Actual inventory eating: deleted={_meal.Deleted}, capacity={_p.MaxWeight}/{_capacity+25}, hunger={_p.Hunger}, potion={TimedEffects.Buff(_p, TimedEffectCategory.Potion)==_potion}");
            _heavy = new IronIngot(Math.Max(1, _capacity - Mobile.BodyWeight - (int)_p.TotalWeight + 10)); _p.Backpack.DropItem(_heavy);
            Require(!StaminaSystem.IsOverloaded(_p), "Carry food did not affect native encumbrance");
            FoodEffects.Apply(_p, Meals.Find("porter-flatbread"), TimedEffects.Now.AddSeconds(-3599));
        });
        Register("expired", () =>
        {
            Require(_p.MaxWeight == _capacity && StaminaSystem.IsOverloaded(_p) && !_heavy.Deleted && _heavy.Parent == _p.Backpack, "Food expiry spilled possessions or retained capacity");
            _heavy.Delete(); _p.MoveToWorld(CombatPractice.FindFloor(Map.Trammel, 5550, 1200), Map.Trammel);
            _lightOverride = LightCycle.LevelOverride; LightCycle.LevelOverride = 26; _p.LightLevel = 0; _p.CheckLightLevels(true); StatNetwork.Send(_p);
        });
        Register("light", () =>
        {
            Require(FoodEffects.Apply(_p, Meals.Find("dawnwatch-chowder")) && ClassStats.Snapshot(_p)[(int)StatField.FoodLight] == 3, "Light radius missing from status");
        });
        Register("light-expire", () => FoodEffects.Apply(_p, Meals.Find("dawnwatch-chowder"), TimedEffects.Now.AddSeconds(-3599)));
        Register("final", () =>
        {
            Require(FoodEffects.Bonus(_p, "light") == 0 && _tool.UsesRemaining == 19 && _flour.Quantity == 19, "Food post-client state wrong");
            LightCycle.LevelOverride = _lightOverride;
        });
    }
    private static void Require(bool value, string why) => RuneChecks.Require(value, why);
    private static void Register(string suffix, Action action)
    {
        string name = "meal-ui-" + suffix;
        ConsoleInputHandler.RegisterCommand(name, "Actual meal menu, quantities, food use and lighting", _ => Core.LoopContext.Post(() =>
        {
            try
            {
                action(); File.WriteAllText(Environment.GetEnvironmentVariable("SANCTUARY_TEST_UI_STATE"), JsonSerializer.Serialize(new
                { weight = _p.MaxWeight, flour = _flour.Quantity, uses = _tool.UsesRemaining, light = FoodEffects.Bonus(_p, "light") }));
                _logger.Information("{Command}: passed", name);
            }
            catch (Exception e) { _logger.Error(e, "{Command}: failed", name); }
        }));
    }
}
