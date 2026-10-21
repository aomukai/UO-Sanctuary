// SPDX-License-Identifier: GPL-3.0-or-later
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

public static class ProfessionCoverageChecks
{
    private static readonly ILogger _log = LogFactory.GetLogger(typeof(ProfessionCoverageChecks));
    private sealed record Saved(uint Player, int Skill, int Base, int Cap);
    private static string Filename => Path.Join(Core.BaseDirectory, "profession-coverage-checks.json");
    public static void Configure() { Register("profession-coverage-check", Check); Register("profession-coverage-reload-check", Reload); }
    private static void Register(string name, Action action) => ConsoleInputHandler.RegisterCommand(name, "T47 complete profession gain paths", _ => Core.LoopContext.Post(() =>
    { try { action(); _log.Information("{Name}: passed", name); } catch (Exception e) { _log.Error(e, "{Name}: failed", name); } }));
    private static void Require(bool value, string why) => RuneChecks.Require(value, why);
    private static void Raise(PlayerMobile player, SkillName skill, Action attempt)
    {
        var trained = player.Skills[skill]; trained.Cap = 120; trained.Base = 119.9; trained.SetLockNoRelay(SkillLock.Up);
        int other = player.Skills[skill == SkillName.Tailoring ? SkillName.Alchemy : SkillName.Tailoring].BaseFixedPoint;
        int count = 0;
        while (trained.BaseFixedPoint < 1200 && count++ < 300) { attempt(); }
        Require(trained.BaseFixedPoint == 1200, "Actual production/extraction did not train to120: " + skill);
        attempt(); Require(trained.BaseFixedPoint == 1200 && trained.CapFixedPoint == 1200, "Production exceeded cap: " + skill);
        Require(player.Skills[skill == SkillName.Tailoring ? SkillName.Alchemy : SkillName.Tailoring].BaseFixedPoint == other,
            "Independent skill gain erased another profession");
    }
    private static void Check()
    {
        var saved = new List<Saved>();
        foreach (var skill in new[] { SkillName.Blacksmith, SkillName.Fletching, SkillName.Tailoring, SkillName.Tinkering })
        {
            var rows = skill == SkillName.Tinkering ? JewelleryCrafting.Recipes : EquipmentCrafting.Recipes;
            var row = rows.Where(r => r.Skill == skill && r.RequiredSkill < 119.9 && r.MaxSkill > 120).OrderByDescending(r => r.RequiredSkill).First();
            var player = EquipmentCraftingChecks.Player("Gain" + skill, row); player.Backpack.MaxItems = 2000;
            BaseTool tool = skill switch { SkillName.Blacksmith => new SmithHammer(1000), SkillName.Fletching => new FletcherTools(1000),
                SkillName.Tailoring => new SewingKit(1000), _ => new TinkerTools(1000) }; player.Backpack.DropItem(tool);
            Raise(player, skill, () =>
            {
                Item primary = null;
                foreach (var ingredient in row.Ingredients)
                { var input = EquipmentCrafting.Resolve(ingredient.Type).CreateInstance<Item>(); input.Amount = ingredient.Amount; player.Backpack.DropItem(input); primary ??= input; }
                var ticket = EquipmentCraftingChecks.Ticket(player, tool, row, primary);
                Require(CraftTransactions.Complete(ticket, new AlchemyChecks.Roll(0)), "Production failed: " + row.Id);
                foreach (var item in player.Backpack.Items.Where(i => i != tool).ToArray()) { item.Delete(); }
            });
            saved.Add(new(player.Serial.Value, (int)skill, 1200, 1200));
        }
        {
            var player = CarpentryChecks.Player(); player.Backpack.MaxItems = 2000; var tool = new Saw(1000); player.Backpack.DropItem(tool);
            var row = CarpentryCrafting.Recipes.OrderByDescending(r => r.Skill).First();
            Raise(player, SkillName.Carpentry, () => { CarpentryChecks.Inputs(player, row); var q = CarpentryCrafting.Quote(player, tool, row);
                Require(q != null && CraftTransactions.Complete(CarpentryChecks.Ticket(q), new AlchemyChecks.Roll(0)), "High Carpentry route failed"); });
            saved.Add(new(player.Serial.Value, (int)SkillName.Carpentry, 1200, 1200));
        }
        {
            var player = AlchemyChecks.Player(); var tool = new MortarPestle(1000); player.Backpack.DropItem(tool);
            var row = AlchemyRestoratives.Recipes.First(r => r.Skill == 110);
            Raise(player, SkillName.Alchemy, () => { AlchemyChecks.Inputs(player, row); var q = AlchemyRestoratives.Quote(player, tool, row);
                Require(q != null && CraftTransactions.Complete(AlchemyChecks.Ticket(q), new AlchemyChecks.Roll(0)), "High Alchemy route failed"); });
            saved.Add(new(player.Serial.Value, (int)SkillName.Alchemy, 1200, 1200));
        }
        {
            var player = MealChecks.Player(); TreasureChecks.LearnAll(player); var tool = new Skillet(1000); player.Backpack.DropItem(tool);
            var row = Meals.Recipes.OrderByDescending(r => r.Skill).First();
            Raise(player, SkillName.Cooking, () => { MealChecks.Inputs(player, row); var q = MealCooking.Quote(player, tool, row);
                Require(q != null && CraftTransactions.Complete(MealChecks.Ticket(q), new AlchemyChecks.Roll(0)), "High Cooking route failed"); });
            saved.Add(new(player.Serial.Value, (int)SkillName.Cooking, 1200, 1200));
        }
        {
            var player = InscriptionChecks.Player(); var tool = new ScribesPen(1000); player.Backpack.DropItem(tool);
            var row = InscriptionCrafting.Recipes.First(r => r.Skill == 100);
            Raise(player, SkillName.Inscribe, () => { InscriptionChecks.Inputs(player, row); var q = InscriptionCrafting.Quote(player, tool, row);
                Require(q != null && CraftTransactions.Complete(InscriptionChecks.Ticket(q), new AlchemyChecks.Roll(0)), "High Inscription route failed"); });
            saved.Add(new(player.Serial.Value, (int)SkillName.Inscribe, 1200, 1200));
        }
        Cartography(saved); Imbuing(saved); Gathering(saved);
        File.WriteAllText(Filename, JsonSerializer.Serialize(saved));
        foreach (var row in saved) { World.FindMobile((Serial)row.Player)?.Internalize(); }
    }
    private static void Cartography(List<Saved> saved)
    {
        var player = SurveyChecks.Player(); var tool = new MapmakersPen(1000); player.Backpack.DropItem(tool);
        player.Skills.Cartography.Cap = 120; player.Backpack.DropItem(new BlankMap { Amount = 1000 });
        foreach (double boundary in new[] { 0d, 30, 60, 80, 100 })
        {
            player.Skills.Cartography.Base = boundary; var row = MapSurveys.Recipes.Single(r => r.Skill == boundary);
            var quote = MapDrawing.Quote(player, tool, row);
            Require(quote != null && quote.Chance > 0 && CraftTransactions.Complete(SurveyChecks.Ticket(quote), new AlchemyChecks.Roll(0)),
                "Exact Cartography boundary has no production action: " + boundary);
        }
        Raise(player, SkillName.Cartography, () => { var q = MapDrawing.Quote(player, tool, MapSurveys.Recipes[^1]);
            Require(q != null && CraftTransactions.Complete(SurveyChecks.Ticket(q), new AlchemyChecks.Roll(0)), "High Cartography route failed"); });
        saved.Add(new(player.Serial.Value, (int)SkillName.Cartography, 1200, 1200));
    }
    private static void Imbuing(List<Saved> saved)
    {
        var player = ImbuingChecks.Player(SkillName.Imbuing); player.Backpack.MaxItems = 2000;
        var tool = new ImbuingLadle { UsesRemaining = 1000 }; player.Backpack.DropItem(tool);
        player.Backpack.DropItem(new MagicEssence(10000)); player.Backpack.DropItem(new Gold(60000));
        Raise(player, SkillName.Imbuing, () =>
        {
            var item = ItemOrigins.Create(ItemOrigin.Drop, () => new Longsword()); player.Backpack.DropItem(item);
            ClassGear.Profiles.Convert(item, 35, GearRarity.Normal, new Random(5));
            var quote = ImbuingChecks.Quote(player, item);
            Require(quote != null && EquipmentEconomy.Commit(player, quote, out _, new AlchemyChecks.Roll(0)), "High Imbuing route failed"); item.Delete();
        });
        saved.Add(new(player.Serial.Value, (int)SkillName.Imbuing, 1200, 1200));
    }
    private static void Gathering(List<Saved> saved)
    {
        foreach (var skill in new[] { SkillName.Fishing, SkillName.Mining, SkillName.Lumberjacking })
        {
            var player = ImbuingChecks.Player(SkillName.Alchemy); var definition = skill switch
            { SkillName.Fishing => Fishing.System.Definitions[0], SkillName.Mining => Mining.System.OreAndStone, _ => Lumberjacking.System.Definitions[0] };
            Type output = skill switch { SkillName.Fishing => typeof(Fish), SkillName.Mining => typeof(IronOre), _ => typeof(Log) };
            foreach (double start in new[] { 100d, 110, 119.9 })
            {
                var trained = player.Skills[skill]; trained.Cap = 120; trained.Base = start; trained.SetLockNoRelay(SkillLock.Up);
                int before = trained.BaseFixedPoint, attempts = 0;
                while (trained.BaseFixedPoint == before && attempts++ < 300)
                { ClassHarvestHooks.Successful(player, definition, trained.Base, output); }
                Require(trained.BaseFixedPoint > before && trained.BaseFixedPoint <= 1200, "Actual extraction gain callback stopped: " + skill + "/" + start);
            }
            player.Skills[skill].Base = 120; ClassHarvestHooks.Successful(player, definition, 120, output);
            Require(player.Skills[skill].BaseFixedPoint == 1200, "Harvest escaped120 cap");
            player.Skills[skill].Base = 110; player.Skills[skill].SetLockNoRelay(SkillLock.Locked);
            for (int i = 0; i < 50; i++) { ClassHarvestHooks.Successful(player, definition, 110, output); }
            Require(player.Skills[skill].BaseFixedPoint == 1100, "Locked gathering skill gained");
            player.Skills[skill].Base = 100; player.Skills[skill].Cap = 100; player.Skills[skill].SetLockNoRelay(SkillLock.Up);
            for (int i = 0; i < 50; i++) { ClassHarvestHooks.Successful(player, definition, 100, output); }
            Require(player.Skills[skill].BaseFixedPoint == 1000, "Harvest bypassed Power Scroll cap");
            player.Skills[skill].Cap = 120; player.Skills[skill].Base = 120;
            saved.Add(new(player.Serial.Value, (int)skill, 1200, 1200));
        }
    }
    private static void Reload()
    {
        var rows = JsonSerializer.Deserialize<Saved[]>(File.ReadAllText(Filename)); Require(rows.Length == 13, "Missing retained profession");
        foreach (var row in rows)
        {
            var player = (PlayerMobile)World.FindMobile((Serial)row.Player); var skill = player.Skills[(SkillName)row.Skill];
            Require(skill.BaseFixedPoint == row.Base && skill.CapFixedPoint == row.Cap, "Profession120 lost on restart");
        }
    }
}
