using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Server;
using Server.Engines.Craft;
using Server.Items;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class SalvageChecks
{
    private static readonly ILogger _logger = LogFactory.GetLogger(typeof(SalvageChecks));
    private static string Manifest => Path.Join(Core.BaseDirectory, "salvage-checks.json");
    private sealed record SavedItem(uint Serial, string Type, int Amount, ItemOrigin Origin);
    private sealed record Saved(uint Player, SavedItem[] Outputs, uint[] Destroyed, uint Remaining, int HammerUses, string Skills);
    private sealed class Roll(int essence = 0, int physical = 0, bool upper = false) : Random
    {
        private int _roll;
        public override int Next(int maximum) => Math.Min(_roll++ == 0 ? essence : physical, maximum - 1);
        public override int Next(int minimum, int maximum) => upper ? maximum - 1 : minimum;
    }
    public static void Configure()
    {
        Register("salvage-check", Check); Register("salvage-reload-check", Reload);
    }
    private static void Register(string command, Action action) => ConsoleInputHandler.RegisterCommand(command,
        "Disposable corresponding-tool salvage checks", _ => Core.LoopContext.Post(() =>
        {
            try { action(); _logger.Information("{Command}: passed", command); }
            catch (Exception error) { _logger.Error(error, "{Command}: failed", command); }
        }));
    private static void Require(bool condition, string message)
    { if (!condition) { throw new InvalidOperationException(message); } }
    private static PlayerMobile Player()
    {
        var p = new PlayerMobile { Body = 0x190, Player = true, Name = "SalvageCheck" };
        ClassCreation.Profiles.Assign(p, DiabloClass.Necromancer); p.AddItem(new Backpack());
        p.MoveToWorld(CombatPractice.FindFloor(Map.Trammel, 5450, 1160), Map.Trammel);
        return p;
    }
    internal static T Gear<T>(PlayerMobile p, T item, ItemOrigin origin = ItemOrigin.Drop, GearRarity rarity = GearRarity.Magic, int level = 30) where T : Item
    {
        var insured = item.Insured;
        if (item.Origin != origin)
        {
            var replacement = ItemOrigins.Create(origin, () => item.GetType().CreateInstance<T>());
            item.Delete(); item = replacement;
        }
        if (!ClassGear.Definition(item).AllowsAffixes || ClassGear.CosmeticClothing(item)) { rarity = GearRarity.Normal; }
        ClassGear.Profiles.Convert(item, level, rarity, new Random(42)); p.Backpack.DropItem(item);
        // Install the protected-item fixture after receipt, which cancels inherited insurance.
        item.Insured = insured;
        return item;
    }
    private static string Skills(PlayerMobile p) => JsonSerializer.Serialize(Enumerable.Range(0, p.Skills.Length)
        .Select(i => p.Skills[i]).Select(s => new { s.BaseFixedPoint, s.CapFixedPoint, s.Lock }));
    private static EssenceQuote Quote(PlayerMobile p, Item gear, BaseTool tool = null) => EquipmentEconomy.Quote(p, gear, EssenceAction.Salvage, tool);
    private static SalvageResult Complete(PlayerMobile p, Item item, BaseTool tool, Random roll)
    {
        var quote = Quote(p, item, tool);
        Require(quote != null && quote.Tool == tool.Serial && quote.Gold == 0 && quote.Essence == 0, "Missing/wrong salvage quote");
        Require(EquipmentEconomy.Commit(p, quote, out var result, roll) && item.Deleted, $"Salvage failed for {item.GetType().Name}");
        Require(!EquipmentEconomy.Commit(p, quote, out _, roll), "Salvage replay succeeded"); return result;
    }
    private static void Check()
    {
        Require(EquipmentEconomy.Settings.SalvageGoldCost == 0 && EquipmentEconomy.EssenceRange((GearRarity)99) == null,
            "Fee or unknown rarity recovery enabled");
        var p = Player();
        var tools = new BaseTool[] { new SmithHammer(77), new FletcherTools(77), new SewingKit(77), new TinkerTools(77), new ScribesPen(77) };
        foreach (var tool in tools) { p.Backpack.DropItem(tool); }
        TradeSkillHooks.ToolUsed(p, tools[4]);
        Require(ClassProfessions.Pending(p) == null, "Scribe salvage tool forced a first-craft choice");
        string skills = Skills(p); var profession = ClassProfessions.Profiles.Find(p);
        var destroyed = new List<uint>();
        foreach (var (item, material, tool) in new (Item, SalvageMaterial, BaseTool)[]
        {
            (new Dagger(), SalvageMaterial.Metal, tools[0]), (new ChainChest(), SalvageMaterial.Metal, tools[0]),
            (new RingmailChest(), SalvageMaterial.Metal, tools[0]), (new PlateArms(), SalvageMaterial.Metal, tools[0]),
            (new Bow(), SalvageMaterial.Wood, tools[1]), (new WoodenShield(), SalvageMaterial.Wood, tools[1]),
            (new WoodlandChest(), SalvageMaterial.Wood, tools[1]), (new QuarterStaff(), SalvageMaterial.Wood, tools[1]),
            (new MagicArrowWand(), SalvageMaterial.Wood, tools[1]), (new LeatherChest(), SalvageMaterial.Leather, tools[2]),
            (new Robe(), SalvageMaterial.Cloth, tools[2]), (new Boots(), SalvageMaterial.Leather, tools[2]),
            (new Spellbook { Content = ulong.MaxValue, LootType = LootType.Regular }, SalvageMaterial.None, tools[4]),
            (new GoldRing(), SalvageMaterial.Metal, tools[3]), (new SilverNecklace(), SalvageMaterial.Metal, tools[3]),
            (new BoneChest(), SalvageMaterial.None, tools[2]), (new BoneHarvester(), SalvageMaterial.None, tools[2]),
            (new GlassStaff(), SalvageMaterial.None, tools[1]), (new GlassSword(), SalvageMaterial.None, tools[0]),
            (new DragonChest(), SalvageMaterial.None, tools[0])
        })
        {
            Gear(p, item); Require(EquipmentEconomy.Material(item) == material, "Wrong physical material " + item.GetType().Name);
            foreach (var wrong in tools.Where(t => t != tool)) { Require(Quote(p, item, wrong) == null, "Wrong tool accepted for " + item.GetType().Name); }
            Require(Quote(p, item) is { } automatic && automatic.Tool == tool.Serial, "Common menu did not require corresponding tool: " + item.GetType().Name);
            int expectedEssence = EquipmentEconomy.EssenceRange(ClassGear.State(item).Roll.Rarity)?.Max ?? 0;
            var result = Complete(p, item, tool, new Roll(upper: true)); destroyed.Add(item.Serial.Value);
            Require(result.Essence == expectedEssence && result.Material == (material == SalvageMaterial.None ? 0 : 5), "Recovery material/range changed");
        }
        foreach (var origin in new[] { ItemOrigin.Drop, ItemOrigin.Crafted })
        {
            var item = Gear(p, new Dagger(), origin); Complete(p, item, tools[0], new Roll()); destroyed.Add(item.Serial.Value);
        }
        Require(EquipmentEconomy.Material(Gear(p, new PlateChest { Resource = CraftResource.Verite })) == SalvageMaterial.Metal,
            "Coloured plate became a different salvage family");
        var white = Gear(p, new Dagger(), rarity: GearRarity.Normal, level: 99);
        Require(Complete(p, white, tools[0], new Roll(upper: true)) == new SalvageResult(0, 12), "White/ilvl99 yield changed");
        var rare = Gear(p, new Dagger(), rarity: GearRarity.Rare, level: 1);
        Require(Complete(p, rare, tools[0], new Roll(upper: true)) == new SalvageResult(5, 3), "Rare/ilvl1 yield changed");
        var partial = Gear(p, new Dagger());
        Require(Complete(p, partial, tools[0], new Roll(99, 0)) == new SalvageResult(0, 3), "Essence/physical rolls were coupled");
        var essenceOnly = Gear(p, new Dagger());
        Require(Complete(p, essenceOnly, tools[0], new Roll(0, 99)) == new SalvageResult(1, 0), "Independent physical miss failed");
        var empty = Gear(p, new Dagger());
        Require(Complete(p, empty, tools[0], new Roll(99, 99)) == new SalvageResult(0, 0), "Empty recovery did not consume host");
        Contents(p, tools[0], tools[1]);
        Restrictions(p, tools[0]); Capacity();
        Require(Skills(p) == skills && ReferenceEquals(ClassProfessions.Profiles.Find(p), profession)
            && p.Backpack.GetAmount(typeof(Gold)) == 0 && tools.All(t => t.UsesRemaining == 77),
            "Salvage charged uses/gold, required a chosen craft or changed training");
        var remaining = Gear(p, new Dagger());
        var outstanding = Quote(p, remaining, tools[0]);
        EventSink.InvokeDisconnected(p); EventSink.InvokeConnected(p);
        Require(!EquipmentEconomy.Commit(p, outstanding, out _) && !remaining.Deleted, "Reconnect accepted an old quote");
        var outputs = p.Backpack.Items.Where(i => i is MagicEssence or MetalScraps or WoodenChips or LeatherStrips or ClothScraps)
            .Select(i => new SavedItem(i.Serial.Value, i.GetType().FullName, i.Amount, i.Origin)).ToArray();
        Require(outputs.Select(i => i.Type).Distinct().Count() == 5 && outputs.All(i => i.Origin == ItemOrigin.Crafted), "Output provenance/coverage wrong");
        File.WriteAllText(Manifest, JsonSerializer.Serialize(new Saved(p.Serial.Value, outputs, destroyed.ToArray(), remaining.Serial.Value, 77, skills)));
    }
    private static void Contents(PlayerMobile p, BaseTool hammer, BaseTool fletcher)
    {
        var plain = Gear(p, new Dagger());
        var filled = Gear(p, new Dagger()); var ruby = new Ruby(10); var money = new Gold(1000);
        filled.AddItem(ruby); filled.AddItem(money);
        var state = ClassGear.State(filled); ClassGear.Profiles.Store(filled, state with { NativeSockets = 1, Quality = CraftQuality.HighQuality });
        var filledResult = Complete(p, filled, hammer, new Roll(upper: true));
        Require(ruby.Deleted && money.Deleted && filledResult == Complete(p, plain, hammer, new Roll(upper: true)),
            "Contents were recovered, survived or increased host yield");
        var wand = Gear(p, new MagicArrowWand()); wand.Charges = 100;
        Require(Complete(p, wand, fletcher, new Roll(upper: true)) == new SalvageResult(2, 5), "Filled wand altered recovery");
        var changed = Gear(p, new Dagger()); var child = new Ruby(); changed.AddItem(child);
        var quote = Quote(p, changed, hammer); child.Amount++;
        Require(!EquipmentEconomy.Commit(p, quote, out _) && !changed.Deleted && !child.Deleted, "Changed contents used stale confirmation");
        changed.Delete();
        var host = Gear(p, new Dagger()); var nested = Gear(p, new Dagger()); host.AddItem(nested);
        quote = Quote(p, host, hammer); ClassGear.Profiles.Convert(nested, 30, GearRarity.Rare, new Random(17));
        Require(!EquipmentEconomy.Commit(p, quote, out _) && !host.Deleted && !nested.Deleted, "Changed nested gear reused quote");
        host.Delete();
        var charged = Gear(p, new MagicArrowWand()); charged.Charges = 20;
        quote = Quote(p, charged, fletcher); charged.Charges--;
        Require(!EquipmentEconomy.Commit(p, quote, out _) && !charged.Deleted, "Changed wand charges reused quote"); charged.Delete();
        var pen = p.Backpack.Items.OfType<ScribesPen>().Single();
        var book = Gear(p, new Spellbook { Content = ulong.MaxValue, LootType = LootType.Regular });
        quote = Quote(p, book, pen); book.Content--;
        Require(!EquipmentEconomy.Commit(p, quote, out _) && !book.Deleted, "Changed spellbook contents reused quote");
        Require(Complete(p, book, pen, new Roll(upper: true)) == new SalvageResult(2, 0), "Filled spellbook changed essence entitlement");
    }
    private static void Restrictions(PlayerMobile p, BaseTool tool)
    {
        var item = Gear(p, new Dagger());
        var before = Quote(p, item, tool); var latest = Quote(p, item, tool);
        Require(!EquipmentEconomy.Commit(p, before, out _) && !item.Deleted, "Replaced quote consumed gear");
        item.Hue++;
        Require(!EquipmentEconomy.Commit(p, latest, out _) && !item.Deleted, "Changed hue reused quote");
        latest = Quote(p, item, tool); ((BaseWeapon)item).HitPoints--;
        Require(!EquipmentEconomy.Commit(p, latest, out _), "Changed native durability reused quote");
        latest = Quote(p, item, tool); tool.UsesRemaining--;
        Require(!EquipmentEconomy.Commit(p, latest, out _), "Used tool reused quote"); tool.UsesRemaining++;
        var nested = new Bag(); p.Backpack.DropItem(nested);
        latest = Quote(p, item, tool); nested.DropItem(tool);
        Require(!EquipmentEconomy.Commit(p, latest, out _), "Moved tool reused quote"); p.Backpack.DropItem(tool);
        latest = Quote(p, item, tool); nested.DropItem(item);
        Require(!EquipmentEconomy.Commit(p, latest, out _), "Moved host reused quote"); p.Backpack.DropItem(item);
        latest = Quote(p, item, tool); tool.Internalize();
        Require(!EquipmentEconomy.Commit(p, latest, out _), "Absent tool reused quote"); p.Backpack.DropItem(tool);
        tool.UsesRemaining = 0; Require(Quote(p, item, tool) == null, "Broken tool accepted"); tool.UsesRemaining = 77;
        foreach (var protectedItem in new Item[]
        {
            Gear(p, new Dagger(), ItemOrigin.Vendor), Gear(p, new Dagger { LootType = LootType.Newbied }),
            Gear(p, new Dagger { LootType = LootType.Blessed }), Gear(p, new Dagger { Insured = true }),
            Gear(p, new Spellbook()),
            Gear(p, new Dagger { QuestItem = true }), Gear(p, new Dagger { Movable = false })
        }) { Require(Quote(p, protectedItem, tool) == null, "Protected/vendor item accepted"); protectedItem.Delete(); }
        var starter = Gear(p, new Dagger(), rarity: GearRarity.Normal); ClassGear.Profiles.MarkStarter(starter);
        Require(Quote(p, starter, tool) == null, "Starter state accepted"); starter.Delete();
        p.Backpack.DropItem(item); ClassCreation.Profiles.AdvanceLevel(p, 99); p.RawStr = p.RawDex = 200;
        Require(p.EquipItem(item) && Quote(p, item, tool) == null, "Equipped gear accepted"); p.Backpack.DropItem(item);
        p.Body = 0x192; Require(Quote(p, item, tool) == null, "Dead salvage accepted"); p.Body = 0x190;
        var foreign = Player(); foreign.Backpack.DropItem(item);
        Require(Quote(p, item, tool) == null && Quote(foreign, item) == null, "Foreign or tool-less salvage accepted");
        foreign.Delete(); nested.Delete();
        Legacy(p);
    }
    private static void Legacy(PlayerMobile p)
    {
        var scissors = new Scissors(); p.Backpack.DropItem(scissors);
        foreach (var origin in new[] { ItemOrigin.Drop, ItemOrigin.Crafted, ItemOrigin.Vendor })
        {
            var robe = Gear(p, new Robe(), origin); var leather = Gear(p, new LeatherChest(), origin);
            Require(!robe.Scissor(p, scissors) && !leather.Scissor(p, scissors) && !robe.Deleted && !leather.Deleted,
                "Scissors bypassed common salvage"); robe.Delete(); leather.Delete();
            var plate = Gear(p, new PlateChest(), origin);
            var type = typeof(Resmelt).GetNestedType("InternalTarget", BindingFlags.NonPublic);
            var target = Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null, [DefBlacksmithy.CraftSystem, null], null);
            Require((SmeltResult)type.GetMethod("Resmelt", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(target, [p, plate, CraftResource.Iron]) == SmeltResult.Invalid && !plate.Deleted, "Resmelt bypassed salvage");
            var bag = new SalvageBag(); p.Backpack.DropItem(bag); bag.DropItem(plate);
            Require(!(bool)typeof(SalvageBag).GetMethod("Resmelt", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(bag, [p, plate, CraftResource.Iron]) && !plate.Deleted, "Salvage bag bypassed common salvage"); bag.Delete();
        }
        scissors.Delete();
    }
    private static void Capacity()
    {
        var p = Player(); var hammer = new SmithHammer(20); p.Backpack.DropItem(hammer);
        var gear = Gear(p, new Dagger());
        p.Backpack.MaxItems = p.Backpack.TotalItems;
        var quote = Quote(p, gear, hammer);
        Require(quote != null && !EquipmentEconomy.Commit(p, quote, out _, new Roll()) && !gear.Deleted
            && p.Backpack.GetAmount(typeof(MagicEssence)) == 0 && p.Backpack.GetAmount(typeof(MetalScraps)) == 0, "Full pack destroyed gear or leaked output");
        p.Backpack.MaxItems = 125; var heavy = new Bag { Weight = 1000 }; p.Backpack.DropItem(heavy);
        quote = Quote(p, gear, hammer);
        Require(!EquipmentEconomy.Commit(p, quote, out _, new Roll()) && !gear.Deleted && hammer.UsesRemaining == 20, "Overweight delivery consumed inputs");
        heavy.Delete();
        Complete(p, gear, hammer, new Roll()); p.Delete();
    }
    private static void Reload()
    {
        var saved = JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest)); var p = (PlayerMobile)World.FindMobile((Serial)saved.Player);
        Require(Skills(p) == saved.Skills && !ClassProfessions.Profiles.Find(p).Confirmed, "Reload changed skills or confirmed a craft");
        foreach (var output in saved.Outputs)
        {
            var item = World.FindItem((Serial)output.Serial);
            Require(item is { Deleted: false } && item.GetType().FullName == output.Type && item.Amount == output.Amount
                && item.Origin == output.Origin && item.IsChildOf(p.Backpack), "Reload lost or duplicated salvage resources");
        }
        foreach (uint serial in saved.Destroyed) { Require(World.FindItem((Serial)serial) == null, "Destroyed host returned after reload"); }
        var gear = World.FindItem((Serial)saved.Remaining);
        Require(gear is { Deleted: false } && Quote(p, gear) != null, "Unconsumed host or tool access lost on reload");
        foreach (var tool in p.Backpack.Items.OfType<BaseTool>()) { Require(tool.UsesRemaining == saved.HammerUses, "Salvage refilled/spent uses on reload"); }
    }
}
