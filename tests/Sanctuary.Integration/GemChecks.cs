using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Server;
using Server.Engines.Craft;
using Server.Engines.Harvest;
using Server.Items;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class GemChecks
{
    private static readonly ILogger _logger = LogFactory.GetLogger(typeof(GemChecks));
    private static string Manifest => Path.Join(Core.BaseDirectory, "gem-checks.json");
    private sealed record Saved(uint Serial, string Identity, uint Parent);
    private sealed class Tickets(int chance, int family = 0, int quality = 0) : Random
    { private int _calls; public int Calls => _calls; public override int Next(int max) => _calls++ switch { 0 => chance, 1 => family, _ => quality }; }
    public static void Configure() { Register("gem-check", Check); Register("gem-reload-check", Reload); }
    private static void Register(string name, Action action) => ConsoleInputHandler.RegisterCommand(name,
        "Mining gem families, effects, upgrade transactions and persistence", _ => Core.LoopContext.Post(() =>
        { try { action(); _logger.Information("{Command}: passed", name); } catch (Exception e) { _logger.Error(e, "{Command}: failed", name); } }));
    private static void Require(bool value, string message) => RuneChecks.Require(value, message);
    internal static string Identity(SocketGem gem) => JsonSerializer.Serialize(new
    { gem.Family, gem.Quality, gem.Effects, gem.Name, gem.ItemID, gem.Hue, gem.Amount, gem.Weight, gem.Origin, gem.Stackable });
    private static Saved Snapshot(SocketGem gem) => new(gem.Serial.Value, Identity(gem), (gem.Parent as Item)?.Serial.Value ?? 0);
    private static bool Act(GemUpgradeRequest q, Random random = null)
    {
        if (q == null) { return false; }
        var ticket = new CraftTransactions.Request(q.Owner, DefTinkering.CraftSystem, q.Recipe, null, q.Tool, 0) { GemUpgrade = q };
        bool success = CraftTransactions.Complete(ticket, random); Require(!CraftTransactions.Complete(ticket), "Gem transaction replayed"); return success;
    }
    private static void Check()
    {
        var p = ImbuingChecks.Player(SkillName.Tinkering); p.Skills.Tinkering.SetLockNoRelay(SkillLock.Locked);
        var other = ImbuingChecks.Player(SkillName.Alchemy); p.Backpack.MaxItems = other.Backpack.MaxItems = 0;
        var tool = new TinkerTools { UsesRemaining = 500 }; p.Backpack.DropItem(tool); var saved = new List<Saved>();
        for (int skill = 0; skill <= 1200; skill++)
        { Require(SocketGems.Unlocked(skill / 10.0) == Math.Clamp((skill - 300) / 100, 0, 5), "Mining Base threshold drift"); }
        int wins = 0;
        for (int ticket = 0; ticket < 100; ticket++)
        { var random = new Tickets(ticket); var gem = SocketGems.Roll(80, random); if (gem != null) { wins++; gem.Delete(); } Require(random.Calls == (ticket < 5 ? 3 : 1), "Extra gem chance roll"); }
        Require(wins == 5, "Gem chance not 5%");
        var none = new Tickets(0); Require(SocketGems.Roll(39.9, none) == null && none.Calls == 0, "Locked gem made a roll");
        foreach (var family in SocketGems.Settings.Families)
        {
            for (int grade = 0; grade < 5; grade++)
            {
                var gem = SocketGems.Roll(40 + grade * 10, new Tickets(0, Array.IndexOf(SocketGems.Settings.Families, family), grade));
                Require(gem.Family == family.Id && (int)gem.Quality == grade && gem.Amount == 1 && gem.Origin == ItemOrigin.Drop, "Uniform selection ticket changed identity");
                for (int low = 0; low <= grade; low++)
                { var selected = SocketGems.Roll(40 + grade * 10, new Tickets(0, 0, low)); Require((int)selected.Quality == low, "Unlocked quality absent"); selected.Delete(); }
                Require(gem.EffectIdentity == JsonSerializer.Serialize(family.Effects[grade]) && gem.SellPrice == SocketGems.Settings.Prices[grade], "Gem effects or price drift");
                gem.Amount = 5; var copy = new SocketGem(); gem.Dupe(copy); Require(Identity(copy) == Identity(gem), "Gem copy lost frozen effects"); copy.Delete();
                var remainder = Mobile.LiftItemDupe(gem, 2) as SocketGem;
                Require(remainder != null && remainder.Amount == 3 && gem.Amount == 2 && remainder.EffectIdentity == gem.EffectIdentity, "Gem split lost identity");
                Require(gem.StackWith(p, remainder) && gem.Amount == 5, "Matching gem stacks did not merge");
                var wrong = new SocketGem(family.Id, (GemQuality)((grade + 1) % 5)); wrong.Name = gem.Name; wrong.Hue = gem.Hue;
                Require(!gem.StackWith(p, wrong), "Different quality merged"); wrong.Delete();
                other.Backpack.DropItem(gem); saved.Add(Snapshot(gem));
            }
            for (int grade = 0; grade < 4; grade++)
            {
                var gem = new SocketGem(family.Id, (GemQuality)grade, 4); p.Backpack.DropItem(gem);
                double required = SocketGems.Settings.UpgradeSkills[grade]; p.Skills.Tinkering.Base = required - .1;
                Require(GemUpgrading.Quote(p, tool, gem) == null, "Upgrade below Base gate"); p.Skills.Tinkering.Base = required;
                int uses = tool.UsesRemaining; Require(Act(GemUpgrading.Quote(p, tool, gem)), "Valid upgrade rejected at exact gate");
                var output = p.Backpack.Items.OfType<SocketGem>().Last(g => g != gem);
                Require(gem.Amount == 1 && output.Family == family.Id && output.Quality == (GemQuality)(grade + 1)
                    && output.Amount == 1 && output.Origin == ItemOrigin.Crafted && tool.UsesRemaining == uses - 1, "Upgrade costs/output drift");
                other.Backpack.DropItem(output); saved.Add(Snapshot(output)); gem.Delete();
            }
        }
        p.Skills.Tinkering.Base = 120; Rejections(p, tool, other); HarvestAndLegacy(p); Sales(p);
        File.WriteAllText(Manifest, JsonSerializer.Serialize(saved)); p.Delete();
        _logger.Information("Gem coverage: 45 identities, 36 upgrades, exact chance/selection/gates, effects, stacks, failure guards, legacy decoration and purchase-only sales passed");
    }
    private static void Rejections(PlayerMobile p, TinkerTools tool, PlayerMobile other)
    {
        var gem = new SocketGem("Ruby", GemQuality.Chipped, 10); p.Backpack.DropItem(gem); int uses = tool.UsesRemaining;
        void Reject(Action mutate, Action restore, string why)
        { var q = GemUpgrading.Quote(p, tool, gem); Require(q != null, "Missing quote for " + why); mutate(); Require(!Act(q), "Accepted " + why); restore(); }
        Reject(() => gem.Amount--, () => gem.Amount++, "changed stack");
        Reject(() => other.Backpack.DropItem(gem), () => p.Backpack.DropItem(gem), "foreign stack");
        Reject(() => tool.UsesRemaining--, () => tool.UsesRemaining++, "changed tool");
        Reject(() => ClassProfessions.Profiles.Select(p, SkillName.Alchemy), () => ClassProfessions.Profiles.Select(p, SkillName.Tinkering), "wrong craft");
        Reject(() => p.Backpack.MaxItems = p.Backpack.TotalItems, () => p.Backpack.MaxItems = 0, "full pack partial input");
        var stale = GemUpgrading.Quote(p, tool, gem); var fresh = GemUpgrading.Quote(p, tool, gem);
        Require(!Act(stale), "Stale upgrade replayed"); GemUpgrading.End(p, fresh.Session); Require(!Act(fresh), "Cancelled upgrade spent");
        Require(gem.Amount == 10 && tool.UsesRemaining == uses, "Invalid upgrade consumed");
        gem.Amount = 3; p.Backpack.MaxItems = p.Backpack.TotalItems;
        Require(Act(GemUpgrading.Quote(p, tool, gem)), "Full pack replacement failed"); p.Backpack.MaxItems = 0;
        var perfect = new SocketGem("Ruby", GemQuality.Perfect, 3); p.Backpack.DropItem(perfect);
        Require(GemUpgrading.Quote(p, tool, perfect) == null, "Perfect upgraded"); perfect.Delete();
        var crafted = ItemOrigins.Create(ItemOrigin.Crafted, () => new TinkerTools()); CraftTransactions.FinishTool(crafted, p, CraftQuality.Superior); p.Backpack.DropItem(crafted);
        gem = new SocketGem("Amber", GemQuality.Flawless, 3); p.Backpack.DropItem(gem); uses = crafted.UsesRemaining;
        Require(Act(GemUpgrading.Quote(p, crafted, gem), new SocketChecks.Rolls(0)) && crafted.UsesRemaining == uses, "Upgrade tool saving failed");
    }
    private static void HarvestAndLegacy(PlayerMobile p)
    {
        var ignored = new Tickets(0);
        foreach (var type in new[] { typeof(Sand), typeof(Granite) })
        { RunebarkHarvesting.Successful(p, Mining.System.OreAndStone, 120, type, ignored); }
        RunebarkHarvesting.Successful(p, new HarvestDefinition { Skill = SkillName.Fishing }, 120, typeof(IronOre), ignored);
        Require(ignored.Calls == 0, "Non-ore gathering rolled usable gems");
        p.Skills.Mining.Cap = 120; p.Skills.Mining.Base = 39.9;
        var bonus = new DefaultSkillMod(SkillName.Mining, "GemTests", true, 60); p.AddSkillMod(bonus);
        Require(SocketGems.Roll(p.Skills.Mining.Base, new Tickets(0)) == null, "Modified skill bypassed Base gate"); p.RemoveSkillMod(bonus);
        p.Backpack.MaxItems = p.Backpack.TotalItems;
        SocketGems.Award(p, 80, new Tickets(0, 0, 4)); SocketGem ground = null;
        foreach (var item in p.GetItemsAt()) { if (item is SocketGem found) { ground = found; } }
        Require(ground is { Quality: GemQuality.Perfect, Parent: null }, "Full pack gem not delivered at feet"); ground.Delete(); p.Backpack.MaxItems = 0;
        foreach (var type in new[] { typeof(Amber), typeof(Amethyst), typeof(Citrine), typeof(Diamond), typeof(Emerald), typeof(Ruby), typeof(Sapphire), typeof(StarSapphire), typeof(Tourmaline) })
        {
            var legacy = type.CreateInstance<Item>();
            Require(legacy.PropertyList.Buffer.AsSpan().IndexOf(Encoding.Unicode.GetBytes("Decorative legacy gem")) >= 0, "Legacy tooltip not decorative");
            var recipe = new CraftItem(typeof(GoldRing), "legacy", "legacy"); recipe.AddSkill(SkillName.Tinkering, 0, 50); recipe.AddRes(type, "gem", 1, "missing");
            Require(!TradeSkillHooks.CheckRecipe(p, DefTinkering.CraftSystem, recipe, null), "Decorative gem entered crafting"); legacy.Delete();
        }
        Require(!MaterialProcessing.StockAllowed(typeof(SocketGem)), "Usable gem entered merchant supplies");
    }
    private static void Sales(PlayerMobile p)
    {
        var vendor = new Carpenter(); vendor.MoveToWorld(p.Location, p.Map); vendor.GetBuyInfo();
        foreach (var quality in Enum.GetValues<GemQuality>())
        {
            var gem = new SocketGem("Diamond", quality, 3); p.Backpack.DropItem(gem); int gold = p.Backpack.GetAmount(typeof(Gold));
            Require(vendor.OnSellItems(p, [new SellItemResponse(gem, 2)]) && gem.Amount == 1
                && p.Backpack.GetAmount(typeof(Gold)) == gold + 2 * SocketGems.Settings.Prices[(int)quality], "Gem sale price wrong");
            Require(vendor.OnSellItems(p, [new SellItemResponse(gem, 1)]) && gem.Deleted, "Remaining gem sale failed");
        }
        Require(TownMarkets.Profiles.Find(vendor.Serial).Offers.All(o => World.FindItem(o.Item) is not SocketGem), "Sold gem entered stock"); vendor.Delete();
    }
    private static void Reload()
    {
        foreach (var row in JsonSerializer.Deserialize<Saved[]>(File.ReadAllText(Manifest)))
        { Require(World.FindItem((Serial)row.Serial) is SocketGem gem && Snapshot(gem) == row, "Saved gem changed"); }
    }
}
