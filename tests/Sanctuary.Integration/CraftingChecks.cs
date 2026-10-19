using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Accounting;
using Server.Engines.Craft;
using Server.Items;
using Server.Logging;
using Server.Misc;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class CraftingChecks
{
    private static readonly ILogger _logger = LogFactory.GetLogger(typeof(CraftingChecks));
    private static string Manifest => Path.Join(Core.BaseDirectory, "crafting-checks.json");
    private sealed record Saved(uint Owner, uint Tool, int Uses, CraftToolState State, ItemOrigin Origin, string Crafter);
    private sealed class Roll(double success = 0, int roll = 99) : Random
    {
        public override double NextDouble() => success;
        public override int Next(int maxValue) => Math.Min(roll, maxValue - 1);
    }
    private static readonly Type[] _types = [typeof(MortarPestle), typeof(SmithHammer), typeof(FletcherTools), typeof(Saw),
        typeof(MapmakersPen), typeof(Skillet), typeof(ScribesPen), typeof(SewingKit), typeof(TinkerTools), typeof(ImbuingLadle)];
    public static void Configure()
    {
        Register("crafting-check", Setup);
        Register("crafting-reload-check", Reload);
    }
    private static void Register(string name, Action action) => ConsoleInputHandler.RegisterCommand(name,
        "Disposable native crafting transaction check", _ => Core.LoopContext.Post(() =>
        {
            try { action(); _logger.Information("{Command}: passed", name); }
            catch (Exception error) { _logger.Error(error, "{Command}: failed", name); }
        }));
    internal static void Require(bool value, string message)
    { if (!value) { throw new InvalidOperationException(message); } }
    private static PlayerMobile Player(string name, SkillName craft = SkillName.Tinkering)
    {
        var player = new PlayerMobile { Body = 0x190, Player = true, Name = name, RawStr = 200 };
        new Account(name.ToLowerInvariant(), Guid.NewGuid().ToString("N"))[0] = player;
        ClassCreation.Profiles.Assign(player, DiabloClass.Necromancer);
        player.RawStr = 200; player.AddItem(new Backpack());
        player.MoveToWorld(CombatPractice.FindFloor(Map.Trammel, 5550, 1150), Map.Trammel);
        ClassProfessions.Profiles.Confirm(player, craft);
        player.Skills.Tinkering.Cap = 120; player.Skills.Tinkering.Base = 120;
        return player;
    }
    private static CraftItem Recipe(Type type) => DefTinkering.CraftSystem.CraftItems.SearchFor(type);
    private static CraftTransactions.Request Ticket(PlayerMobile player, BaseTool tool, CraftItem recipe, Type material = null, int hue = -1) =>
        (CraftTransactions.Request)CraftTransactions.Begin(player, DefTinkering.CraftSystem, recipe, material, tool, hue);
    private static void Setup()
    {
        Require(ClassProfessions.Enabled, "Explicit test-world feature missing");
        Require(Server.Engines.Craft.T2A.TinkeringMenu.Tools().Any(e => e.Name.StartsWith("imbuing ladle"))
            && Server.Engines.Craft.T2A.TinkeringMenu.Tools().Any(e => e.Name.StartsWith("fletcher tools")),
            "Native Tinkering tool menu hides first-cohort recipes");
        CraftFacilities.Initialize(); CraftFacilities.Initialize();
        var player = Player("CraftTransactions");
        var tool = new TinkerTools(500);
        player.Backpack.DropItem(tool);
        player.Backpack.DropItem(new IronIngot(500));
        player.Backpack.DropItem(new Gold(321));
        player.AcquireRecipe(9876001);
        var saved = new List<Saved>();
        foreach (var type in _types)
        {
            var recipe = Recipe(type);
            Require(recipe != null && recipe.Resources.Count == 1, "Missing bounded tool recipe " + type.Name);
            int before = player.Backpack.GetAmount(typeof(IronIngot));
            int uses = tool.UsesRemaining;
            var ticket = Ticket(player, tool, recipe, typeof(IronIngot));
            Require(CraftTransactions.Complete(ticket, new Roll()), "Valid tool transaction rejected " + type.Name);
            var output = player.Backpack.Items.OfType<BaseTool>().Last(t => t.GetType() == type && t != tool);
            Require(output.Origin == ItemOrigin.Crafted && output.UsesRemaining == 100
                && output.Crafter == player.RawName && output.PlayerConstructed
                && CraftTransactions.Profiles.Find(output) == new CraftToolState(CraftQuality.Average, true, 100),
                "Crafted tool state/capacity/origin wrong " + type.Name);
            Require(before - player.Backpack.GetAmount(typeof(IronIngot)) == recipe.Resources[0].Amount
                && tool.UsesRemaining == uses - 1 && player.Backpack.GetAmount(typeof(Gold)) == 321,
                "Craft did not consume once or charged station fee " + type.Name);
            Require(!CraftTransactions.Complete(ticket, new Roll()) && tool.UsesRemaining == uses - 1,
                "Duplicate completion consumed twice");
            saved.Add(Snapshot(player, output));
        }
        QualityAndSaving(player, saved);
        CommittedSaving(player, tool);
        Rejections(player, tool);
        MaterialsAndFailure(player, tool);
        Stations(player);
        TownSites(player);
        Imbuing();
        var second = Player("CraftConcurrent");
        var secondTool = new TinkerTools(10);
        second.Backpack.DropItem(secondTool); second.Backpack.DropItem(new IronIngot(10));
        var first = Ticket(player, tool, Recipe(typeof(SewingKit)));
        var other = Ticket(second, secondTool, Recipe(typeof(SewingKit)));
        Require(CraftTransactions.Complete(first, new Roll()) && CraftTransactions.Complete(other, new Roll()),
            "Concurrent station users reserved/blocked each other");
        var gated = new CraftItem(typeof(SewingKit), "tests", "learned tool");
        gated.AddSkill(SkillName.Tinkering, 10, 60); gated.AddRes(typeof(IronIngot), "iron", 2, "missing iron");
        gated.AddRecipe(9876002, DefTinkering.CraftSystem);
        Require(!CraftTransactions.Complete(Ticket(player, tool, gated)), "Unlearned recipe bypassed gate");
        player.AcquireRecipe(9876002);
        Require(CraftTransactions.Complete(Ticket(player, tool, gated), new Roll()), "Learned recipe failed");
        var legacy = new TinkerTools(17) { Quality = ToolQuality.Exceptional };
        legacy.UsesRemaining = 17; player.Backpack.DropItem(legacy);
        Require(CraftTransactions.Profiles.Find(legacy) == null, "Legacy tool inferred new provenance");
        saved.Add(Snapshot(player, legacy));
        File.WriteAllText(Manifest, JsonSerializer.Serialize(saved));
        _logger.Information("Crafting coverage: ten native tool recipes, three qualities, saving, failure, exact material/hue, capacity, rejection/replay, public/portable stations, concurrent users, recipe unlocks and persisted provenance");
    }
    private static Saved Snapshot(PlayerMobile owner, BaseTool tool) => new(owner.Serial.Value, tool.Serial.Value,
        tool.UsesRemaining, CraftTransactions.Profiles.Find(tool), tool.Origin, tool.Crafter);
    private static void QualityAndSaving(PlayerMobile player, List<Saved> saved)
    {
        Require(CraftTransactions.Quality(34.9, 25, 0) == CraftQuality.Average
            && CraftTransactions.Quality(35, 25, 0) == CraftQuality.Superior
            && CraftTransactions.Quality(35, 25, 1) == CraftQuality.HighQuality
            && CraftTransactions.Quality(35, 25, 10) == CraftQuality.HighQuality
            && CraftTransactions.Quality(35, 25, 11) == CraftQuality.Average
            && CraftTransactions.Quality(120, 120, 0) == CraftQuality.Superior,
            "Quality boundaries/120 ceiling wrong");
        foreach (var quality in Enum.GetValues<CraftQuality>())
        {
            var crafted = ItemOrigins.Create(ItemOrigin.Crafted, () => new TinkerTools());
            CraftTransactions.FinishTool(crafted, player, quality);
            player.Backpack.DropItem(crafted);
            int capacity = quality == CraftQuality.Average ? 100 : quality == CraftQuality.HighQuality ? 110 : 120;
            Require(crafted.UsesRemaining == capacity, "Quality capacity wrong");
            CraftTransactions.SpendUse(player, crafted, new Roll(0, 9));
            Require(crafted.UsesRemaining == capacity, "Crafted tool missed 10% saving");
            CraftTransactions.SpendUse(player, crafted, new Roll(0, 10));
            Require(crafted.UsesRemaining == capacity - 1, "Crafted tool saving exceeds10%");
            var copy = new TinkerTools(); crafted.Dupe(copy); player.Backpack.DropItem(copy);
            Require(CraftTransactions.Profiles.Find(copy) == CraftTransactions.Profiles.Find(crafted)
                && copy.UsesRemaining == crafted.UsesRemaining && copy.Origin == crafted.Origin, "Tool copy lost provenance");
            saved.Add(Snapshot(player, crafted)); saved.Add(Snapshot(player, copy));
        }
        foreach (var type in _types)
        {
            var buy = new GenericBuyInfo(type, 999, 10, 0x1EB8, 0);
            Require(buy.Price == 10, "Vendor tool price not configurable shared price");
            var vendor = (BaseTool)buy.GetEntity();
            Require(vendor.Origin == ItemOrigin.Vendor && vendor.UsesRemaining is >= 20 and <= 30,
                "Native vendor supply did not assign20–30uses");
            int uses = vendor.UsesRemaining;
            CraftTransactions.Supply(vendor);
            Require(vendor.UsesRemaining == uses, "Resupply refilled existing tool");
            CraftTransactions.SpendUse(player, vendor, new Roll(0, 0));
            Require(vendor.UsesRemaining == uses - 1, "Vendor tool received saving");
            player.Backpack.DropItem(vendor); saved.Add(Snapshot(player, vendor));
        }
    }
    private static void CommittedSaving(PlayerMobile player, BaseTool vendorTool)
    {
        var recipe = Recipe(typeof(SewingKit));
        foreach (int roll in new[] { 0, 1 })
        {
            Require(CraftTransactions.Complete(Ticket(player, vendorTool, recipe), new Roll(0, roll)), "Quality craft failed");
            var output = player.Backpack.Items.OfType<SewingKit>().Last();
            Require(CraftTransactions.Profiles.Find(output).Quality == (roll == 0 ? CraftQuality.Superior : CraftQuality.HighQuality)
                && output.UsesRemaining == (roll == 0 ? 120 : 110), "Committed quality did not set capacity");
        }
        var crafted = ItemOrigins.Create(ItemOrigin.Crafted, () => new TinkerTools());
        CraftTransactions.FinishTool(crafted, player, CraftQuality.Average);
        player.Backpack.DropItem(crafted);
        int ingots = player.Backpack.GetAmount(typeof(IronIngot));
        Require(CraftTransactions.Complete(Ticket(player, crafted, recipe), new Roll(0, 0))
            && crafted.UsesRemaining == 100 && player.Backpack.GetAmount(typeof(IronIngot)) == ingots - 2,
            "Committed production saving skipped ingredients or spent use");
        player.Skills.Tinkering.Base = 11;
        int outputs = player.Backpack.Items.OfType<SewingKit>().Count();
        ingots = player.Backpack.GetAmount(typeof(IronIngot));
        Require(CraftTransactions.Complete(Ticket(player, crafted, recipe), new Roll(.999, 0))
            && crafted.UsesRemaining == 100 && player.Backpack.Items.OfType<SewingKit>().Count() == outputs
            && player.Backpack.GetAmount(typeof(IronIngot)) == ingots - (Core.UOTD ? 2 : 1),
            "Failed production saving changed native failure loss/output");
        player.Skills.Tinkering.Base = 30; player.NextSkillTime = Core.TickCount;
        ingots = player.Backpack.GetAmount(typeof(IronIngot));
        Require(ProfessionPractice.Attempt(player, crafted, 0, out bool gained, new Roll(0, 0)) && gained
            && player.Skills.Tinkering.BaseFixedPoint == 301 && crafted.UsesRemaining == 100
            && player.Backpack.GetAmount(typeof(IronIngot)) == ingots - 2, "Practice saving/gain/input transaction wrong");
        player.Skills.Tinkering.Base = 120;
        crafted.Delete();
    }
    private static void Rejections(PlayerMobile player, TinkerTools tool)
    {
        var recipe = Recipe(typeof(SewingKit));
        int uses = tool.UsesRemaining, ingots = player.Backpack.GetAmount(typeof(IronIngot));
        int skill = player.Skills.Tinkering.BaseFixedPoint;
        recipe.CompleteCraft(2, true, player, DefTinkering.CraftSystem, null, tool, null);
        recipe.CompleteCraft(2, true, player, DefTinkering.CraftSystem, null, tool, null, 0);
        Require(tool.UsesRemaining == uses && player.Backpack.GetAmount(typeof(IronIngot)) == ingots,
            "Direct native completion bypassed ticket");
        var missing = Ticket(player, tool, recipe); tool.Internalize();
        Require(!CraftTransactions.Complete(missing), "Stale missing-tool request committed");
        player.Backpack.DropItem(tool);
        var moved = Ticket(player, tool, recipe);
        var point = player.Location; player.MoveToWorld(new Point3D(point.X + 10, point.Y, point.Z), player.Map);
        Require(!CraftTransactions.Complete(moved), "Out-of-station request committed");
        player.MoveToWorld(point, player.Map);
        var full = Ticket(player, tool, recipe);
        var fillers = new List<Item>();
        while (player.Backpack.TotalItems < player.Backpack.MaxItems)
        { var filler = new Bag { Weight = 0 }; player.Backpack.DropItem(filler); fillers.Add(filler); }
        Require(!CraftTransactions.Complete(full), "Full pack accepted craft");
        foreach (var filler in fillers) { filler.Delete(); }
        var heavy = new Bag { Weight = player.MaxWeight }; player.Backpack.DropItem(heavy);
        Require(!CraftTransactions.Complete(Ticket(player, tool, recipe)), "Overweight pack accepted output");
        heavy.Delete();
        Require(player.Skills.Tinkering.BaseFixedPoint == skill && tool.UsesRemaining == uses
            && player.Backpack.GetAmount(typeof(IronIngot)) == ingots, "Rejected craft gained/consumed");
        var depleted = Ticket(player, tool, recipe); tool.UsesRemaining = 0;
        Require(!CraftTransactions.Complete(depleted), "Depleted tool request committed"); tool.UsesRemaining = uses;
        var dead = Ticket(player, tool, recipe); player.Frozen = true;
        Require(!CraftTransactions.Complete(dead), "Frozen request committed"); player.Frozen = false;
    }
    private static void MaterialsAndFailure(PlayerMobile player, TinkerTools tool)
    {
        var recipe = Recipe(typeof(SewingKit));
        var ordinary = new List<IronIngot>();
        foreach (var ingot in player.Backpack.FindItemsByType<IronIngot>(true)) { ordinary.Add(ingot); }
        foreach (var item in ordinary) { item.Internalize(); }
        var copper = new CopperIngot(50); player.Backpack.DropItem(copper);
        int uses = tool.UsesRemaining, skill = player.Skills.Tinkering.BaseFixedPoint;
        Require(!CraftTransactions.Complete(Ticket(player, tool, recipe, typeof(IronIngot)), new Roll())
            && copper.Amount == 50 && tool.UsesRemaining == uses && player.Skills.Tinkering.BaseFixedPoint == skill,
            "Missing ordinary material consumed colored stack/gained");
        foreach (var item in ordinary) { player.Backpack.DropItem(item); }
        var cloth = new IronIngot(10) { Hue = 1234 }; player.Backpack.DropItem(cloth);
        int iron = player.Backpack.GetAmount(typeof(IronIngot));
        Require(CraftTransactions.Complete(Ticket(player, tool, recipe, typeof(IronIngot), 1234), new Roll())
            && cloth.Amount == 8 && player.Backpack.GetAmount(typeof(IronIngot)) == iron - 2,
            "Hue selection consumed wrong resource");
        var stale = Ticket(player, tool, recipe, typeof(IronIngot), 1234); cloth.Internalize();
        Require(!CraftTransactions.Complete(stale), "Missing selected hue fell back to plain resources");
        player.Backpack.DropItem(cloth);
        player.Skills.Tinkering.Base = 11;
        iron = player.Backpack.GetAmount(typeof(IronIngot)); uses = tool.UsesRemaining;
        Require(CraftTransactions.Complete(Ticket(player, tool, recipe), new Roll(.999))
            && tool.UsesRemaining == uses - 1 && player.Backpack.GetAmount(typeof(IronIngot)) == iron - (Core.UOTD ? 2 : 1),
            "Failure did not use native material loss/one charge");
        player.Skills.Tinkering.Base = 120;
    }
    private static void Imbuing()
    {
        var player = Player("CraftImbuing", SkillName.Imbuing);
        var gear = ItemOrigins.Create(ItemOrigin.Crafted, () => new Longsword());
        player.Backpack.DropItem(gear);
        ClassGear.Profiles.Convert(gear, 35);
        Require(EquipmentEconomy.Quote(player, gear, EssenceAction.Imbue) == null, "Imbuing bypassed missing ladle");
        var ladle = new ImbuingLadle(); player.Backpack.DropItem(ladle);
        var unpaid = EquipmentEconomy.Quote(player, gear, EssenceAction.Imbue);
        Require(unpaid != null && !EquipmentEconomy.Commit(player, unpaid, out _) && ladle.UsesRemaining == 25,
            "Unpaid Imbuing used ladle");
        player.Backpack.DropItem(new MagicEssence(100)); player.Backpack.DropItem(new Gold(1000));
        var stale = EquipmentEconomy.Quote(player, gear, EssenceAction.Imbue);
        var point = player.Location; player.MoveToWorld(new Point3D(point.X + 10, point.Y, point.Z), player.Map);
        Require(!EquipmentEconomy.Commit(player, stale, out _) && ladle.UsesRemaining == 25,
            "Imbuing stale station request consumed");
        player.MoveToWorld(point, player.Map);
        stale = EquipmentEconomy.Quote(player, gear, EssenceAction.Imbue); ladle.Internalize();
        Require(!EquipmentEconomy.Commit(player, stale, out _) && ladle.UsesRemaining == 25,
            "Imbuing missing-ladle quote consumed");
        player.Backpack.DropItem(ladle);
        var quote = EquipmentEconomy.Quote(player, gear, EssenceAction.Imbue);
        Require(quote != null && EquipmentEconomy.Commit(player, quote, out _, new Random(42)) && ladle.UsesRemaining == 24
            && player.Backpack.GetAmount(typeof(MagicEssence)) == 100 - quote.Essence
            && player.Backpack.GetAmount(typeof(Gold)) == 1000 - quote.Gold
            && !EquipmentEconomy.Commit(player, quote, out _) && ladle.UsesRemaining == 24,
            "Imbuing did not consume exactly one ladle use/cost");
    }
    private static void TownSites(PlayerMobile player)
    {
        var point = player.Location; var oldMap = player.Map;
        var rows = BalanceCatalog.Read<CraftFacility>("craft-facilities");
        int checkedSites = 0;
        foreach (var site in rows)
        {
            if (site.Map != "Felucca") { continue; }
            int stationCount = 0;
            foreach (var station in Map.Felucca.GetItemsAt<TownCraftStation>(new Point3D(site.X, site.Y, site.Z)))
            { if (station.Skill == site.Skill && station.ItemID == site.ItemID) { stationCount++; } }
            Require(stationCount == 1, "Facility initialization duplicated/lost public station");
            bool accessible = false;
            for (int dx = -2; dx <= 2 && !accessible; dx++)
            {
                for (int dy = -2; dy <= 2 && !accessible; dy++)
                {
                    var stand = new Point3D(site.X + dx, site.Y + dy, site.Z);
                    if (!Map.Felucca.CanSpawnMobile(stand)) { continue; }
                    player.MoveToWorld(stand, Map.Felucca);
                    accessible = CraftFacilities.Allows(player, site.Skill, false);
                    if (accessible && site.Skill == SkillName.Blacksmith)
                    {
                        DefBlacksmithy.CheckAnvilAndForge(player, 2, out bool anvil, out bool forge);
                        accessible = anvil && forge;
                    }
                }
            }
            Require(accessible, "No usable standing position at town facility " + site.Skill + " " + site.X + "," + site.Y);
            checkedSites++;
        }
        Require(checkedSites == 11, "Initial Britain facility registry missing entries");
        player.MoveToWorld(point, oldMap);
    }
    private static void Stations(PlayerMobile player)
    {
        foreach (byte skill in TradeSkills.Crafting)
        { Require(CraftFacilities.Allows(player, (SkillName)skill, false), "Missing registered facility"); }
        var point = player.Location;
        player.MoveToWorld(new Point3D(point.X + 15, point.Y, point.Z), player.Map);
        var fake = new TownCraftStation(SkillName.Tinkering, 0xB4A);
        fake.MoveToWorld(player.Location, player.Map);
        Require(!CraftFacilities.Allows(player, SkillName.Tinkering, false), "Private/unregistered station accepted");
        Require(CraftFacilities.AllowsRecipe(player, SkillName.Carpentry, typeof(Board)), "Portable processing blocked");
        var fire = new Campfire(); fire.MoveToWorld(player.Location, player.Map);
        Require(CraftFacilities.AllowsRecipe(player, SkillName.Cooking, typeof(FishSteak))
            && !CraftFacilities.AllowsRecipe(player, SkillName.Cooking, typeof(BreadLoaf)), "Campfire exception too broad/narrow");
        fire.Delete(); fake.Delete(); player.MoveToWorld(point, player.Map);
    }
    private static void Reload()
    {
        CraftingUiChecks.Reload();
        foreach (var row in JsonSerializer.Deserialize<Saved[]>(File.ReadAllText(Manifest)))
        {
            var player = World.FindMobile((Serial)row.Owner) as PlayerMobile;
            var tool = World.FindItem((Serial)row.Tool) as BaseTool;
            Require(player != null && tool != null && tool.IsChildOf(player.Backpack) && tool.UsesRemaining == row.Uses
                && tool.Origin == row.Origin && tool.Crafter == row.Crafter && CraftTransactions.Profiles.Find(tool) == row.State,
                "Tool quality/uses/provenance changed across reload");
            Require(player.HasRecipe(9876001) && player.HasRecipe(9876002), "Native recipe unlocks lost on reload");
        }
        _logger.Information("Crafting persisted tool state and native recipe unlocks survived restart");
    }
}
