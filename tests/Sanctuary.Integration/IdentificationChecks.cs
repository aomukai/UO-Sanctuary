using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Server;
using Server.Engines.Craft;
using Server.Items;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class IdentificationChecks
{
    private static readonly ILogger _logger = LogFactory.GetLogger(typeof(IdentificationChecks));
    private static string Manifest => Path.Join(Core.BaseDirectory, "identification-checks.json");
    private sealed record Saved(uint Item, string State, NativeGearDetails Native, ItemOrigin Origin);
    private sealed class Roll(double success = 0) : Random
    {
        public override double NextDouble() => success;
        public override int Next(int max) => max - 1;
        public override int Next(int min, int max) => min;
    }
    public static void Configure()
    { Register("identification-check", Check); Register("identification-reload-check", Reload); }
    private static void Register(string name, Action action) => ConsoleInputHandler.RegisterCommand(name,
        "Disposable identification recipe, use, privacy and persistence checks", _ => Core.LoopContext.Post(() =>
        {
            try { action(); _logger.Information("{Command}: passed", name); }
            catch (Exception error) { _logger.Error(error, "{Command}: failed", name); }
        }));
    private static void Require(bool condition, string message) => ImbuingChecks.Require(condition, message);
    private static string State(Item item) => JsonSerializer.Serialize(ClassGear.State(item));
    private static T Put<T>(Mobile p, T item) where T : Item { p.Backpack.DropItem(item); return item; }
    internal static Item Unknown(PlayerMobile p, Item item)
    {
        ClassGear.Profiles.FromMonster(item, 99, GearRarity.Rare, new Random(42)); Put(p, item);
        Require(ClassGear.State(item) is { Identified: false }, "New magical drop was already identified"); return item;
    }
    internal static void Hidden(Item item)
    {
        using var opl = new ObjectPropertyList(item); item.GetProperties(opl); opl.Terminate();
        string text = Encoding.Unicode.GetString(opl.Buffer.AsSpan(1));
        Require(text.Contains("Unidentified") && !text.Contains("Base tier") && !text.Contains("item level")
            && !text.Contains("Required level") && !text.Contains("Empty sockets"), "Unidentified tooltip leaked saved properties");
        var state = ClassGear.State(item);
        foreach (var affix in state.Roll.Affixes)
        { Require(!text.Contains(affix.Definition.Name), "Hidden affix name leaked"); }
        // BaseWand appends a localized charge property after BaseWeapon.GetProperties.
        // Compare to an identical hidden weapon after changing charges to expose leaks.
        if (item is BaseWand wand)
        {
            byte[] before = opl.Buffer.ToArray(); int charges = wand.Charges; wand.Charges++;
            using var changed = new ObjectPropertyList(item); item.GetProperties(changed); changed.Terminate();
            Require(before.AsSpan().SequenceEqual(changed.Buffer), "Unknown wand tooltip exposed charges"); wand.Charges = charges;
        }
    }
    private static void Check()
    {
        var p = ImbuingChecks.Player(SkillName.Inscribe); var saved = new List<Saved>();
        foreach (var item in new Item[] { new Longsword(), new LeatherCap(), new RingmailChest(), new GoldRing(), new SilverNecklace(),
            new MagicArrowWand(), new Spellbook { LootType = LootType.Regular } })
        {
            Unknown(p, item); string stateBefore = State(item); var native = GearDetails.Read(item);
            int offer = TradePrices.Quote(item); var before = ClassGear.State(item); int price = before.IdentifiedSaleOffer.Value;
            Require(offer == price / 10 && !before.Identified, "Unknown valuation wrong");
            Hidden(item); item.MoveToWorld(p.Location, p.Map); Hidden(item);
            Put(p, item); var bag = Put(p, new Bag()); bag.DropItem(item); Hidden(item);
            Require(ClassGear.CanEquip(item, p) == false && !ClassGear.Active(p, item), "Unknown item usable");
            var scroll = Put(p, new IdentifyScroll()); scroll.OnDoubleClick(p); var target = p.Target;
            Require(target != null, "Scroll did not target"); target.Invoke(p, item);
            var after = ClassGear.State(item);
            Require(scroll.Deleted && after == (before with { Identified = true }) && after.Roll == before.Roll
                && after.IdentifiedSaleOffer == price && TradePrices.Quote(item) == price && GearDetails.Read(item) == native,
                "Identification consumed incorrectly or changed roll/price/native identity");
            var spare = Put(p, new IdentifyScroll()); Require(!Identification.Reveal(p, spare, item) && !spare.Deleted, "Already-known consumed a scroll");
            var copy = item.GetType().CreateInstance<Item>(); item.Dupe(copy); Put(p, copy);
            Require(State(copy) == State(item), "Copy lost identification/roll/price");
            saved.Add(new(item.Serial.Value, State(item), native, item.Origin)); saved.Add(new(copy.Serial.Value, State(copy), GearDetails.Read(copy), copy.Origin));
        }
        Rejects(p, saved); Supply(p); Recipe(p, saved);
        var known = Put(p, new Dagger()); ClassGear.Profiles.Convert(known, 35, GearRarity.Magic, new Random(42));
        Require(ClassGear.State(known).Identified, "Known authored/crafted gear became hidden");
        var white = Put(p, new Dagger()); ClassGear.Profiles.FromMonster(white, 35, GearRarity.Normal);
        Require(ClassGear.State(white).Identified, "White drop hidden");
        File.WriteAllText(Manifest, JsonSerializer.Serialize(saved));
    }
    private static void Rejects(PlayerMobile p, List<Saved> saved)
    {
        var item = Unknown(p, new Longsword()); var scroll = Put(p, new IdentifyScroll()); var junk = Put(p, new Ruby());
        scroll.OnDoubleClick(p); var target = p.Target; target.Invoke(p, junk);
        Require(!scroll.Deleted && !ClassGear.State(item).Identified, "Invalid target consumed");
        scroll.OnDoubleClick(p); target = p.Target; p.Target.Cancel(p, Server.Targeting.TargetCancelType.Canceled);
        Require(!scroll.Deleted && !ClassGear.State(item).Identified, "Cancel consumed");
        scroll.OnDoubleClick(p); target = p.Target; scroll.Internalize(); target.Invoke(p, item);
        Require(!scroll.Deleted && !ClassGear.State(item).Identified, "Remote scroll identified"); Put(p, scroll);
        item.MoveToWorld(p.Location, p.Map); Require(!Identification.Reveal(p, scroll, item), "Ground gear identified"); Put(p, item);
        var other = ImbuingChecks.Player(SkillName.Alchemy); Put(other, item);
        Require(!Identification.Reveal(p, scroll, item), "Foreign backpack identified"); Put(p, item);
        var foreign = Put(other, new IdentifyScroll()); Require(!Identification.Reveal(p, foreign, item), "Foreign scroll used");
        p.Frozen = true; Require(!Identification.Reveal(p, scroll, item), "Frozen identify accepted"); p.Frozen = false;
        p.Body = 0x192; Require(!Identification.Reveal(p, scroll, item), "Dead identify accepted"); p.Body = 0x190;
        scroll.OnDoubleClick(p); target = p.Target;
        // The successful first target spends this entity; a retained target cannot spend another scroll.
        p.Backpack.MaxItems = p.Backpack.TotalItems;
        Require(Identification.Reveal(p, scroll, item), "Full backpack blocked in-place identification");
        target.Invoke(p, item); p.Backpack.MaxItems = 125;
        Require(scroll.Deleted && !foreign.Deleted, "Retained target spent another scroll");
        var hidden = Unknown(p, new Longsword()); TradePrices.Quote(hidden); Hidden(hidden);
        var hammer = Put(p, new RepairHammer()); hidden.GetType(); ((BaseWeapon)hidden).HitPoints--;
        Require(EquipmentRepairs.Quote(p, hidden, hammer) == null, "Repair quote exposed hidden rarity");
        var copy = new Longsword(); hidden.Dupe(copy); Put(p, copy); Require(State(copy) == State(hidden), "Hidden copy lost state");
        saved.Add(new(hidden.Serial.Value, State(hidden), GearDetails.Read(hidden), hidden.Origin));
        saved.Add(new(copy.Serial.Value, State(copy), GearDetails.Read(copy), copy.Origin));
        saved.Add(new(foreign.Serial.Value, null, null, foreign.Origin));
    }
    private static void Supply(PlayerMobile p)
    {
        var vendors = TownMarkets.Sites.Where(s => s.Role == MarketRole.Scribe).Select(s => TownMarkets.Profiles.At(s.Id)).ToArray();
        Require(vendors.Length > 1, "No scribe acquisition route");
        foreach (var vendor in vendors)
        {
            var offer = TownMarkets.Market(vendor).Offers.Single(o => World.FindItem(o.Item) is IdentifyScroll);
            Require(offer.Price == 80 && offer.Supply != null && !offer.Equipment, "Scribe scroll supply/price missing");
        }
        var shop = vendors[0]; p.MoveToWorld(shop.Location, shop.Map); Put(p, new Gold(1000));
        var row = TownMarkets.Market(shop).Offers.Single(o => World.FindItem(o.Item) is IdentifyScroll);
        int before = p.Backpack.GetAmount(typeof(Gold));
        Require(ClassEconomyHooks.BuyItems(shop, p, [new(row.Item, 1)]) == true
            && p.Backpack.GetAmount(typeof(Gold)) == before - 80 && p.Backpack.Items.OfType<IdentifyScroll>().Any(s => s.Origin == ItemOrigin.Vendor),
            "Real merchant purchase failed");
        p.MoveToWorld(new Point3D(5550, 1151, 0), Map.Trammel);
    }
    private static void Recipe(PlayerMobile p, List<Saved> saved)
    {
        var pen = Put(p, new ScribesPen(30)); var blank = Put(p, new BlankScroll(10)); var pearl = Put(p, new BlackPearl(10));
        var recipe = Identification.Recipe; Require(recipe != null && recipe.Skills[0].MinSkill == 0 && recipe.Skills[0].MaxSkill == 50, "Scroll recipe missing");
        CraftTransactions.Request Ticket() => (CraftTransactions.Request)CraftTransactions.Begin(p, pen.CraftSystem, recipe, null, pen, 0);
        int count = p.Backpack.Items.OfType<IdentifyScroll>().Count();
        Require(CraftTransactions.Complete(Ticket(), new Roll()) && blank.Amount == 9 && pearl.Amount == 9 && pen.UsesRemaining == 29,
            "Scroll recipe did not charge one of each ingredient and a pen use");
        var output = p.Backpack.Items.OfType<IdentifyScroll>().Single(i => i.Origin == ItemOrigin.Crafted);
        saved.Add(new(output.Serial.Value, null, null, output.Origin));
        p.Skills.Inscribe.Base = 25; var ticket = Ticket();
        Require(CraftTransactions.Complete(ticket, new Roll(1)) && !CraftTransactions.Complete(ticket, new Roll())
            && p.Backpack.Items.OfType<IdentifyScroll>().Count() == count + 1 && blank.Amount == 8 && pearl.Amount == 8 && pen.UsesRemaining == 28,
            "Failure output/replay/input/pen cost wrong");
        p.Skills.Inscribe.Base = 120; ticket = Ticket(); var location = p.Location;
        p.MoveToWorld(new Point3D(5558, 1180, 0), p.Map);
        Require(!CraftTransactions.Complete(ticket, new Roll()) && blank.Amount == 8 && pen.UsesRemaining == 28, "Remote scribing accepted");
        p.MoveToWorld(location, p.Map); p.Backpack.MaxItems = p.Backpack.TotalItems; ticket = Ticket();
        Require(!CraftTransactions.Complete(ticket, new Roll()) && blank.Amount == 8 && pen.UsesRemaining == 28, "Full-pack scribing consumed inputs");
        p.Backpack.MaxItems = 125; ticket = Ticket(); ClassProfessions.Profiles.Select(p, SkillName.Tinkering);
        Require(!CraftTransactions.Complete(ticket, new Roll()) && blank.Amount == 8 && pen.UsesRemaining == 28, "Inactive scribing consumed");
    }
    private static void Reload()
    {
        foreach (var row in JsonSerializer.Deserialize<Saved[]>(File.ReadAllText(Manifest)))
        {
            var item = World.FindItem((Serial)row.Item);
            Require(item != null && item.Origin == row.Origin && (row.State == null ? item is IdentifyScroll
                : State(item) == row.State && GearDetails.Read(item) == row.Native), "Identification/scroll save state changed");
            if (ClassGear.State(item) is { Identified: false } state)
            { Hidden(item); Require(TradePrices.Quote(item) == state.IdentifiedSaleOffer / 10, "Unknown price changed on reload"); }
        }
    }
}
