using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Engines.Harvest;
using Server.Items;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class RunebarkChecks
{
    private static readonly ILogger _logger = LogFactory.GetLogger(typeof(RunebarkChecks));
    private static string Manifest => Path.Join(Core.BaseDirectory, "runebark-checks.json");
    private sealed record Saved(uint Serial, int Tier, int Amount, uint Parent, string Name, ItemOrigin Origin);
    private sealed class Tickets(int chance, int tier = 0) : Random
    {
        public int Calls;
        public override int Next(int max) => ++Calls == 1 ? chance : tier;
    }
    private static void Require(bool value, string message) => MouldChecks.Require(value, message);
    public static void Configure()
    {
        Register("runebark-check", Check);
        Register("runebark-reload-check", Reload);
    }
    private static void Register(string name, Action action) => ConsoleInputHandler.RegisterCommand(name,
        "Runebark harvest, stacks, vendor sales and persistence", _ => Core.LoopContext.Post(() =>
        {
            try { action(); _logger.Information("{Command}: passed", name); }
            catch (Exception error) { _logger.Error(error, "{Command}: failed", name); }
        }));
    private static Saved Snapshot(Runebark bark) => new(bark.Serial.Value, bark.Tier, bark.Amount,
        (bark.Parent as Item)?.Serial.Value ?? 0, bark.Name, bark.Origin);
    private static void Check()
    {
        for (int skill = 0; skill <= 1200; skill++)
        {
            int unlocked = Math.Clamp((skill - 400) / 50, 0, 8);
            if (skill < 450) { unlocked = 0; }
            Require(RunebarkHarvesting.Unlocked(skill / 10.0) == unlocked, "Trained skill threshold drift");
        }
        foreach (var row in RunebarkHarvesting.Tiers)
        {
            int wins = 0;
            for (int ticket = 0; ticket < 100; ticket++)
            {
                var random = new Tickets(ticket, row.Tier - 1);
                var item = RunebarkHarvesting.Roll(row.MinimumSkill, random);
                if (item != null)
                {
                    wins++; Require(item.Tier == row.Tier && item.Amount == 1 && item.Origin == ItemOrigin.Drop,
                        "Runebark identity, quantity or origin drift"); item.Delete();
                }
                Require(random.Calls == (ticket < 5 ? 2 : 1), "More than one chance roll was used");
            }
            Require(wins == 5, "Runebark chance is not 5%");
            for (int tier = 0; tier < row.Tier; tier++)
            {
                var bark = RunebarkHarvesting.Roll(row.MinimumSkill, new Tickets(0, tier));
                Require(bark.Tier == tier + 1, "Not all unlocked tiers have equal selection tickets"); bark.Delete();
            }
        }
        var noRoll = new Tickets(0);
        Require(RunebarkHarvesting.Roll(44.9, noRoll) == null && noRoll.Calls == 0, "Locked tiers used RNG");
        var player = GearDropChecks.Player(); var other = GearDropChecks.Player();
        player.Skills.Lumberjacking.Cap = 120; player.Skills.Lumberjacking.Base = 44.9;
        var bonus = new DefaultSkillMod(SkillName.Lumberjacking, "RunebarkTest", true, 75); player.AddSkillMod(bonus);
        Require(player.Skills.Lumberjacking.Value > 80 && RunebarkHarvesting.Roll(player.Skills.Lumberjacking.Base, noRoll) == null,
            "Skill bonus bypasses Base threshold"); player.RemoveSkillMod(bonus);
        player.Skills.Lumberjacking.Base = 80;
        int before = player.Backpack.GetAmount(typeof(Runebark));
        RunebarkHarvesting.Award(player, 80, new Tickets(0, 7));
        Require(player.Backpack.GetAmount(typeof(Runebark)) == before + 1, "Find not delivered");
        var saved = new List<Saved>();
        for (int tier = 1; tier <= 8; tier++)
        {
            var bark = ItemOrigins.Create(ItemOrigin.Drop, () => new Runebark(tier, 3));
            var same = ItemOrigins.Create(ItemOrigin.Drop, () => new Runebark(tier, 2));
            Require(bark.StackWith(player, same) && bark.Amount == 5, "Same-tier stacking failed");
            var wrong = ItemOrigins.Create(ItemOrigin.Drop, () => new Runebark(tier % 8 + 1));
            wrong.Name = bark.Name; wrong.Hue = bark.Hue;
            Require(!bark.StackWith(player, wrong), "Cross-tier stack accepted"); wrong.Delete();
            var copy = new Runebark(); Item.CopyProperties(bark, copy);
            Require(copy.Tier == tier && copy.Amount == 5 && copy.SellPrice == tier * 10, "Copy lost tier or quote");
            var split = Mobile.LiftItemDupe(copy, 2) as Runebark;
            Require(split != null && split.Tier == tier && split.Amount == 3 && copy.Amount == 2,
                "Native split lost tier or quantities");
            other.Backpack.DropItem(copy); other.Backpack.DropItem(split);
            saved.Add(Snapshot(copy)); saved.Add(Snapshot(split)); bark.Delete();
            Require(!MaterialProcessing.StockAllowed(typeof(Runebark)) && !ClassGear.Converted(copy)
                && ClassGear.State(copy) == null && !EquipmentCrafting.Recipes.Any(r => r.Type == nameof(Runebark)),
                "Runebark entered gear or supply/crafting paths");
        }
        // Full pack: the primary wood can succeed while its independent bark lands safely at the player's feet.
        player.Backpack.MaxItems = player.Backpack.TotalItems;
        RunebarkHarvesting.Award(player, 80, new Tickets(0, 0));
        Runebark ground = null;
        foreach (var item in player.GetItemsAt()) { if (item is Runebark bark) { Require(ground == null, "Duplicate ground bark"); ground = bark; } }
        Require(ground != null && ground.Tier == 1 && ground.Amount == 1 && ground.Parent == null, "Full pack lost bark");
        ground.Delete(); player.Backpack.MaxItems = 125;
        CheckNative(player);
        CheckSales(player);
        File.WriteAllText(Manifest, JsonSerializer.Serialize(saved));
        player.Delete();
    }
    private sealed class Harvest : HarvestSystem
    {
        public bool RejectDelivery;
        public override bool GetHarvestDetails(Mobile from, Item tool, object target, out int tileID, out Map map,
            out Point3D loc, out bool isLand)
        { tileID = 1; map = from.Map; loc = from.Location; isLand = false; return true; }
        public override bool Give(Mobile from, Item item, bool feet) => !RejectDelivery && base.Give(from, item, feet);
    }
    private static void CheckNative(PlayerMobile player)
    {
        var resource = new HarvestResource(0, -100, -1, null, typeof(Log));
        var def = new HarvestDefinition { Skill = SkillName.Lumberjacking, StaticTiles = [1], BankWidth = 1,
            BankHeight = 1, MinTotal = 1000, MaxTotal = 1000, MinRespawn = TimeSpan.FromMinutes(1),
            MaxRespawn = TimeSpan.FromMinutes(1), MaxRange = 2, ConsumedPerHarvest = 1, ConsumedPerFeluccaHarvest = 1,
            Veins = [new HarvestVein(1000, 0, resource, null)], Resources = [resource], BonusResources = [] };
        var harvest = new Harvest(); var tool = new Hatchet(); player.Backpack.DropItem(tool);
        var original = ClassHarvestHooks.Successful; int calls = 0;
        try
        {
            ClassHarvestHooks.Successful = (from, definition, trained, resourceType) =>
            { calls++; Require(trained == 80, "Hook changed trained skill snapshot"); original(from, definition, trained, resourceType); };
            harvest.FinishHarvesting(player, tool, def, null, harvest);
            Require(calls == 1, "Successful harvest did not raise exactly one callback");
            harvest.RejectDelivery = true;
            harvest.FinishHarvesting(player, tool, def, null, harvest);
            Require(calls == 2, "Full backpack suppressed a successful extraction"); harvest.RejectDelivery = false;
            resource.ReqSkill = 121;
            harvest.FinishHarvesting(player, tool, def, null, harvest);
            Require(calls == 2, "Failed harvest awarded bark");
            int count = player.Backpack.GetAmount(typeof(Runebark));
            original(player, new HarvestDefinition { Skill = SkillName.Mining }, 120, typeof(IronOre));
            Require(player.Backpack.GetAmount(typeof(Runebark)) == count, "Mining awarded bark");
        }
        finally { ClassHarvestHooks.Successful = original; }
    }
    private static void CheckSales(PlayerMobile player)
    {
        var vendor = new Carpenter(); vendor.MoveToWorld(player.Location, player.Map);
        try
        {
            vendor.GetBuyInfo();
            foreach (int tier in Enumerable.Range(1, 8))
            {
                var bark = ItemOrigins.Create(ItemOrigin.Drop, () => new Runebark(tier, 3)); player.Backpack.DropItem(bark);
                var info = vendor.GetSellInfo().First(i => i.IsSellable(bark));
                Require(BaseVendor.GetSellPriceFor(bark, info) == tier * 10 && !info.IsResellable(bark), "Wrong vendor quote");
                int gold = player.Backpack.GetAmount(typeof(Gold));
                Require(vendor.OnSellItems(player, [new SellItemResponse(bark, 2)]) && bark.Amount == 1
                    && player.Backpack.GetAmount(typeof(Gold)) == gold + tier * 20, "Partial stack sale failed");
                Require(vendor.OnSellItems(player, [new SellItemResponse(bark, 1)]) && bark.Deleted, "Remaining sale failed");
                Require(!vendor.OnSellItems(player, [new SellItemResponse(bark, 1)]), "Deleted sale replayed");
            }
            Require(TownMarkets.Profiles.Find(vendor.Serial).Offers.All(o => World.FindItem(o.Item) is not Runebark), "Sold bark entered stock");
        }
        finally { vendor.Delete(); }
    }
    private static void Reload()
    {
        var rows = JsonSerializer.Deserialize<Saved[]>(File.ReadAllText(Manifest));
        foreach (var row in rows)
        {
            var bark = World.FindItem((Serial)row.Serial) as Runebark;
            Require(bark != null && Snapshot(bark) == row, "Saved tier/stack/trade metadata changed");
        }
    }
}
