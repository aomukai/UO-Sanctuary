using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Accounting;
using Server.Engines.Craft;
using Server.Engines.Harvest;
using Server.Items;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class ProcessingChecks
{
    private static readonly ILogger _logger = LogFactory.GetLogger(typeof(ProcessingChecks));
    private static string Manifest => Path.Join(Core.BaseDirectory, "processing-checks.json");
    private sealed record Saved(uint Item, int Amount, int Hue, ItemOrigin Origin, double Weight, string Type, int Art, CraftResource? Resource);
    private static CraftResource? Resource(Item item) => item switch
    { BaseOre ore => ore.Resource, BaseIngot ingot => ingot.Resource, Log log => log.Resource, Board board => board.Resource,
        BaseHides hides => hides.Resource, BaseLeather leather => leather.Resource, _ => null };
    private static void Require(bool value, string message) => CraftingChecks.Require(value, message);
    public static void Configure()
    {
        Register("processing-check", Setup); Register("processing-reload-check", Reload);
        Register("processing-comparison-check", Comparison);
        Register("processing-pending-check", Pending);
        ConsoleInputHandler.RegisterCommand("processing-spin-check", "Native delayed wheel completion", _ => Core.LoopContext.Post(Spinning));
    }
    private static void Register(string command, Action action) => ConsoleInputHandler.RegisterCommand(command,
        "Disposable material processing checks", _ => Core.LoopContext.Post(() =>
        {
            try { action(); _logger.Information("{Command}: passed", command); }
            catch (Exception error) { _logger.Error(error, "{Command}: failed", command); }
        }));
    internal static PlayerMobile Player(string name, SkillName? craft = null)
    {
        var player = new PlayerMobile { Body = 0x190, Player = true, Name = name, RawStr = 200 };
        new Account(name.ToLowerInvariant(), Guid.NewGuid().ToString("N"))[0] = player;
        ClassCreation.Profiles.Assign(player, DiabloClass.Necromancer);
        player.RawStr = 200; player.AddItem(new Backpack());
        FixtureSpace.Place(player,new Point3D(5550,1150,0), Map.Trammel);
        if(craft.HasValue) { CraftFacilities.Register(new(craft.Value,player.Map.Name,player.X+1,player.Y,player.Z,0xB4A)); }
        if (craft.HasValue) { ClassProfessions.Profiles.Confirm(player, craft.Value); }
        return player;
    }
    private static T Put<T>(PlayerMobile player, T item) where T : Item { player.Backpack.DropItem(item); return item; }
    private static void Clear(PlayerMobile player)
    { foreach (var item in player.Backpack.Items.ToArray()) { item.Delete(); } }
    private static void Setup()
    {
        Require(ClassProfessions.Enabled, "Processing feature disabled in test world");
        var player = Player("Processor");
        (Func<BaseTool> Tool, Func<Item> Source, Type Output, int Ratio)[] cases =
        [
            (() => new SmithHammer(), () => new IronIngot(11), typeof(MetalScraps), 10),
            (() => new TinkerTools(), () => new ShadowIronIngot(11), typeof(MetalScraps), 10),
            (() => new SmithHammer(), () => new ValoriteOre(11) { ItemID = 0x19B7 }, typeof(MetalScraps), 5),
            (() => new TinkerTools(), () => new IronOre(11) { ItemID = 0x19B8 }, typeof(MetalScraps), 10),
            (() => new SmithHammer(), () => new IronOre(11) { ItemID = 0x19B9 }, typeof(MetalScraps), 20),
            (() => new Saw(), () => new OakLog(11), typeof(WoodenChips), 10),
            (() => new FletcherTools(), () => new FrostwoodBoard(11), typeof(WoodenChips), 10),
            (() => new SewingKit(), () => new BarbedHides(11), typeof(LeatherStrips), 10),
            (() => new SewingKit(), () => new SpinedLeather(11), typeof(LeatherStrips), 10),
            (() => new SewingKit(), () => new Cloth(11) { Hue = 1234 }, typeof(ClothScraps), 10),
            (() => new SewingKit(), () => new UncutCloth(11), typeof(ClothScraps), 10),
            (() => new SewingKit(), () => new BoltOfCloth(11), typeof(ClothScraps), 500)
        ];
        foreach (var row in cases)
        {
            Clear(player); var tool = Put(player, row.Tool()); var source = Put(player, row.Source());
            int uses = tool.UsesRemaining;
            var request = MaterialProcessing.Quote(player, tool, source);
            Require(request != null && request.OutputPerUnit == row.Ratio && MaterialProcessing.Complete(request, 2), "Partial processing failed");
            Require(source.Amount == 9 && player.Backpack.GetAmount(row.Output) == 2 * row.Ratio
                && tool.UsesRemaining == uses && player.Skills[tool.CraftSystem.MainSkill].Base == 0
                && !ClassProfessions.Profiles.Find(player).Confirmed, "Processing charged/selected/gained or wrong count");
            Require(!MaterialProcessing.Complete(request, 2), "Replayed request consumed twice");
            var scraps = player.Backpack.Items.Single(i => i.GetType() == row.Output);
            Require(scraps.Origin == ItemOrigin.Crafted && scraps.Name is "Metal Scraps" or "Wooden Chips" or "Leather Strips" or "Cloth Scraps",
                "Generic identity/origin lost");
            Require(MaterialProcessing.Complete(MaterialProcessing.Quote(player, tool, source), 9)
                && source.Deleted && player.Backpack.GetAmount(row.Output) == 11 * row.Ratio, "All/merge produced wrong quantity");
        }
        Limits(player); Native(player); Gains(); Vendor(); GatheringAndSkin(player);
        // Explicit obsolete weights simulate stacks from older saves, not new constructors.
        Clear(player);
        var legacy = new Item[] { new IronIngot(23), new ShadowIronOre(7) { ItemID = 0x19B9 }, new BarbedHides(13),
            new OakLog(17), new Board(9), new SpinedLeather(31), new Cloth(42), new UncutCloth(35),
            new BoltOfCloth(3), new Wool(5), new DarkYarn(6), new MetalScraps(100), new WoodenChips(100),
            new LeatherStrips(100), new ClothScraps(100), new Bandage(12) };
        var saved = new List<Saved>();
        foreach (var item in legacy)
        {
            Put(player, item); double weight = MaterialProcessing.Weight(item) ?? item.Weight;
            item.Weight = item is Bandage ? .1 : 17;
            saved.Add(new(item.Serial.Value, item.Amount, item.Hue, item.Origin, weight, item.GetType().Name, item.ItemID, Resource(item)));
        }
        File.WriteAllText(Manifest, JsonSerializer.Serialize(saved));
        _logger.Information("Processing: 12 material/tool routes, exact partial/all counts, generic identity, limits, stale actions, native conversions, gains, vendor ban and gathering/skin passed");
    }
    private static void Limits(PlayerMobile player)
    {
        Clear(player); var sewing = Put(player, new SewingKit()); var bolts = Put(player, new BoltOfCloth(60000));
        Require(!MaterialProcessing.Complete(MaterialProcessing.Quote(player, sewing, bolts), 60000)
            && bolts.Amount == 60000 && !player.Backpack.Items.OfType<ClothScraps>().Any(), "Oversized output was clamped or partially consumed");
        Clear(player); var saw = Put(player, new Saw()); var logs = Put(player, new Log(60000));
        Require(MaterialProcessing.Complete(MaterialProcessing.Quote(player, saw, logs), 60000), "Encumbered batch could not reduce weight");
        var stacks = player.Backpack.Items.OfType<WoodenChips>().ToArray();
        Require(stacks.Length == 10 && stacks.All(i => i.Amount == 60000) && stacks.Sum(i => i.Amount) == 600000,
            "Maximum input silently clamped output or exceeded stack limit");
        Clear(player); saw = Put(player, new Saw()); logs = Put(player, new Log(2));
        while (player.Backpack.TotalItems < player.Backpack.MaxItems) { Put(player, new Bag { Weight = 0 }); }
        var full = MaterialProcessing.Quote(player, saw, logs);
        Require(!MaterialProcessing.Complete(full, 1) && logs.Amount == 2, "Full pack partial conversion lost inputs");
        Require(MaterialProcessing.Complete(MaterialProcessing.Quote(player, saw, logs), 2), "Full pack did not reuse consumed source slot");
        Clear(player); saw = Put(player, new Saw()); logs = Put(player, new Log(3));
        var merge = Put(player, ItemOrigins.Create(ItemOrigin.Crafted, () => new WoodenChips(59995)));
        Require(MaterialProcessing.Complete(MaterialProcessing.Quote(player, saw, logs), 1)
            && merge.Amount == 60000 && player.Backpack.Items.OfType<WoodenChips>().Sum(i => i.Amount) == 60005, "Partial merge boundary lost items");
        foreach (var change in new Action<Item>[] { i => i.Amount++, i => i.Hue++, i => i.Internalize(), i => i.Delete() })
        {
            Clear(player); saw = Put(player, new Saw()); logs = Put(player, new Log(3));
            var request = MaterialProcessing.Quote(player, saw, logs); change(logs);
            Require(!MaterialProcessing.Complete(request, 1) && !player.Backpack.Items.OfType<WoodenChips>().Any(), "Stale input committed");
        }
        Clear(player); saw = Put(player, new Saw()); logs = Put(player, new Log(3));
        var staleTool = MaterialProcessing.Quote(player, saw, logs); saw.Internalize();
        Require(!MaterialProcessing.Complete(staleTool, 1) && logs.Amount == 3, "Moved tool committed");
        Put(player, saw); var invalidQuantity = MaterialProcessing.Quote(player, saw, logs);
        Require(!MaterialProcessing.Complete(invalidQuantity, 0) && !MaterialProcessing.Complete(invalidQuantity, 4)
            && logs.Amount == 3, "Invalid quantity consumed inputs");
        var foreign = Player("ForeignProcessor");
        Require(MaterialProcessing.Quote(foreign, saw, logs) == null, "Foreign resources accepted");
        foreign.Delete();
    }
    private static void Native(PlayerMobile player)
    {
        Clear(player); player.Skills.Mining.Base = 120; player.Skills.Lumberjacking.Base = 120;
        var forge = new Forge(); forge.MoveToWorld(player.Location, player.Map);
        foreach (int art in new[] { 0x19B7, 0x19B8, 0x19BA, 0x19B9 })
        {
            var ore = Put(player, new ShadowIronOre(7) { ItemID = art });
            int before = player.Backpack.GetAmount(typeof(ShadowIronIngot));
            ore.OnDoubleClick(player); player.Target.Invoke(player, forge);
            int output = art == 0x19B7 ? 3 : art == 0x19B9 ? 14 : 7;
            Require(player.Backpack.GetAmount(typeof(ShadowIronIngot)) == before + output
                && (art == 0x19B7 ? ore.Amount == 1 : ore.Deleted), "Native smelt quantity/subtype changed");
        }
        Clear(player); var blockedOre = Put(player, new IronOre(3) { ItemID = 0x19B9 });
        while (player.Backpack.TotalItems < player.Backpack.MaxItems) { Put(player, new Bag { Weight = 0 }); }
        // All ore is consumed, so one output stack replaces it even at full capacity.
        blockedOre.OnDoubleClick(player); player.Target.Invoke(player, forge);
        Require(blockedOre.Deleted && player.Backpack.GetAmount(typeof(IronIngot)) == 6, "Native smelt source slot not reused");
        Clear(player); var failedOre = Put(player, new IronOre(7) { ItemID = 0x19B9 });
        var originalCheck = Mobile.SkillCheckTargetHandler;
        try
        {
            Mobile.SkillCheckTargetHandler = (_, _, _, _, _) => false;
            failedOre.OnDoubleClick(player); player.Target.Invoke(player, forge);
            Require(failedOre.Amount == 3 && player.Backpack.GetAmount(typeof(IronIngot)) == 0, "Native smelt failure loss changed");
            failedOre.Amount = 1; failedOre.OnDoubleClick(player); player.Target.Invoke(player, forge);
            Require(failedOre.ItemID == 0x19B8 && failedOre.Amount == 1 && failedOre.Weight == 2, "Failed singleton clump did not reweigh");
        }
        finally { Mobile.SkillCheckTargetHandler = originalCheck; }
        Clear(player); var small = Put(player, new IronOre(3) { ItemID = 0x19B7 });
        while (player.Backpack.TotalItems < player.Backpack.MaxItems) { Put(player, new Bag { Weight = 0 }); }
        small.OnDoubleClick(player); player.Target.Invoke(player, forge);
        Require(small.Amount == 3 && player.Backpack.GetAmount(typeof(IronIngot)) == 0, "Rejected small ore lost odd remainder");
        Clear(player); var ore1 = Put(player, new IronOre(5) { ItemID = 0x19B9 }); var ore2 = Put(player, new IronOre(7) { ItemID = 0x19B7 });
        ore1.OnDoubleClick(player); player.Target.Invoke(player, ore2);
        Require(ore1.Deleted && ore2.ItemID == 0x19B7 && ore2.Amount == 27 && ore2.PileWeight == 27, "Mixed clump combine changed content/weight");
        Clear(player); var axe = Put(player, new Hatchet()); var log = Put(player, new OakLog(7));
        Require(log.Axe(player, axe) && log.Deleted && player.Backpack.GetAmount(typeof(OakBoard)) == 7, "Native board subtype/quantity lost");
        var staleLog = Put(player, new OakLog(2)); axe.Internalize();
        Require(!staleLog.Axe(player, axe) && staleLog.Amount == 2, "Moved axe processed a log stack");
        Put(player, axe);
        var scissors = Put(player, new Scissors()); var hides = Put(player, new BarbedHides(8));
        Require(hides.Scissor(player, scissors) && hides.Deleted && player.Backpack.GetAmount(typeof(BarbedLeather)) == 8, "Native hides lost subtype");
        var bolt = Put(player, new BoltOfCloth(2) { Hue = 1234 });
        Require(bolt.Scissor(player, scissors) && bolt.Deleted && player.Backpack.Items.OfType<Cloth>().Single().Amount == 100
            && player.Backpack.Items.OfType<Cloth>().Single().Hue == 1234, "Native bolt cutting lost quantity/hue");
        var cloth = player.Backpack.Items.OfType<Cloth>().Single();
        Require(!cloth.Scissor(player, scissors) && cloth.Amount == 100 && player.Backpack.GetAmount(typeof(Bandage)) == 0,
            "New bandage creation still available");
        var oil = Put(player, new OilCloth());
        Require(!oil.Scissor(player, scissors) && !oil.Deleted, "Oil cloth still creates bandages");
        var uncut = Put(player, new UncutCloth(5));
        Require(!uncut.Scissor(player, scissors) && uncut.Amount == 5, "Uncut cloth created bandages");
        var loom = new LoomEastAddon(); loom.MoveToWorld(player.Location, player.Map);
        var yarn = Put(player, new DarkYarn(5) { Hue = 1234 });
        for (int i = 0; i < 5; ++i) { yarn.OnDoubleClick(player); player.Target.Invoke(player, loom.Components[0]); }
        Require(yarn.Deleted && loom.Phase == 0 && player.Backpack.Items.OfType<BoltOfCloth>().Single().Hue == 1234, "Native loom progress/output changed");
        Clear(player); loom.Phase = 4; yarn = Put(player, new DarkYarn(2));
        while (player.Backpack.TotalItems < player.Backpack.MaxItems) { Put(player, new Bag { Weight = 0 }); }
        yarn.OnDoubleClick(player); player.Target.Invoke(player, loom.Components[0]);
        Require(loom.Phase == 4 && yarn.Amount == 2, "Rejected loom lost phase/input");
        Clear(player); Put(player, new Bag { Weight = player.Backpack.MaxWeight });
        yarn = Put(player, new DarkYarn(2)); yarn.OnDoubleClick(player); player.Target.Invoke(player, loom.Components[0]);
        Require(loom.Phase == 4 && yarn.Amount == 2, "Overweight loom consumed input");
        loom.Delete(); forge.Delete();
    }
    private static void Gains()
    {
        var player = Player("GainProcessor", SkillName.Carpentry); var saw = Put(player, new Saw(50)); var logs = Put(player, new Log(200));
        player.Skills.Carpentry.Base = 0;
        for (int i = 0; i < 40; i++) { Require(MaterialProcessing.Complete(MaterialProcessing.Quote(player, saw, logs), 1), "Gain action rejected"); }
        Require(player.Skills.Carpentry.Base > 0 && player.Skills.Carpentry.Base <= 30 && saw.UsesRemaining == 50, "Active native processing gains/charges wrong");
        foreach (var skillLock in new[] { SkillLock.Locked, SkillLock.Down })
        {
            player.Skills.Carpentry.SetLockNoRelay(skillLock); int before = player.Skills.Carpentry.BaseFixedPoint;
            Require(MaterialProcessing.Complete(MaterialProcessing.Quote(player, saw, logs), 1)
                && player.Skills.Carpentry.BaseFixedPoint == before, "Processing ignored skill lock");
        }
        player.Skills.Carpentry.SetLockNoRelay(SkillLock.Up); player.Skills.Carpentry.Cap = 12; player.Skills.Carpentry.Base = 12;
        Require(MaterialProcessing.Complete(MaterialProcessing.Quote(player, saw, logs), 1) && player.Skills.Carpentry.Base == 12, "Processing ignored individual cap");
        player.Skills.Carpentry.Cap = 120; player.Skills.Carpentry.Base = 29.9;
        player.AcceleratedSkill = SkillName.Carpentry; player.AcceleratedStart = Core.Now + TimeSpan.FromMinutes(1);
        for (int i = 0; i < 100; i++) { MaterialProcessing.Complete(MaterialProcessing.Quote(player, saw, logs), 1); }
        Require(player.Skills.Carpentry.Base <= 30, "Accelerated gain overshot 30");
        player.Skills.Carpentry.Base = 29.9;
        int checks = 0;
        var originalCheck = Mobile.SkillCheckLocationHandler;
        try
        {
            Mobile.SkillCheckLocationHandler = (from, skill, min, max) =>
            {
                checks++; Server.Misc.SkillCheck.Gain(from, from.Skills[skill]); return false;
            };
            Require(MaterialProcessing.Complete(MaterialProcessing.Quote(player, saw, logs), 1)
                && checks == 1 && player.Skills.Carpentry.Base == 30, "Native accelerated gain ceiling/check count failed");
            Require(MaterialProcessing.Complete(MaterialProcessing.Quote(player, saw, logs), 1) && checks == 1,
                "Trivial processing still checked skill");
        }
        finally { Mobile.SkillCheckLocationHandler = originalCheck; }
        var fletcher = Put(player, new FletcherTools()); player.Skills.Fletching.Base = 0;
        Require(MaterialProcessing.Complete(MaterialProcessing.Quote(player, fletcher, logs), 1) && player.Skills.Fletching.Base == 0,
            "Inactive craft gained from processing");
        player.Delete();
    }
    private static void Vendor()
    {
        foreach (Type type in new[] { typeof(IronOre), typeof(ValoriteIngot), typeof(OakLog), typeof(Board), typeof(Hides),
            typeof(BarbedLeather), typeof(Bandage), typeof(MetalScraps), typeof(WoodenChips), typeof(LeatherStrips) })
        {
            var stock = new GenericBuyInfo(type, 5, 20, 1, 0);
            Require(stock.Amount == 0 && stock.GetEntity() == null, "NPC forbidden stock available: " + type.Name);
            stock.OnRestock(); Require(stock.Amount == 0, "Restock revived banned resource");
        }
        var buyer = Player("VendorProcessor"); Put(buyer, new Gold(1000));
        foreach (var row in new (BaseVendor Vendor, Item Supply)[]
            { (new Blacksmith(), new IronIngot(10)), (new Carpenter(), new Log(10)),
                (new Tanner(), new Leather(10)), (new FortuneTeller(), new Bandage(10)) })
        {
            row.Vendor.MoveToWorld(buyer.Location, buyer.Map); row.Vendor.AIObject.AITimer.Stop();
            row.Vendor.BuyPack.DropItem(row.Supply);
            int gold = buyer.Backpack.GetAmount(typeof(Gold));
            Require(!row.Vendor.OnBuyItems(buyer, [new BuyItemResponse(row.Supply.Serial, 1)])
                && buyer.Backpack.GetAmount(typeof(Gold)) == gold && row.Supply.Amount == 10
                && row.Supply.IsChildOf(row.Vendor.BuyPack), "Forged buyback purchase sold a forbidden supply");
            row.Vendor.Delete();
        }
        buyer.Delete();
        var cloth = new GenericBuyInfo(typeof(Cloth), 5, 20, 1, 0);
        Require(cloth.Amount == 20 && cloth.GetEntity() is Cloth, "Allowed textile supply blocked");
        var deed = new CommodityDeed(new IronIngot(10));
        Require(!MaterialProcessing.ResaleAllowed(deed), "Commodity buyback bypassed source restriction"); deed.Delete();
        var bag = new Bag(); bag.DropItem(new Bandage(5));
        Require(!MaterialProcessing.ResaleAllowed(bag), "Nested buyback bypassed bandage ban"); bag.Delete();
    }
    private static void GatheringAndSkin(PlayerMobile player)
    {
        var ore = Mining.System.OreAndStone;
        Require(ore.Skill == SkillName.Mining && ore.ConsumedPerHarvest == 1 && ore.ConsumedPerFeluccaHarvest == 2
            && ore.Resources[0].ReqSkill == 0 && ore.Resources[1].ReqSkill == 65 && ore.Resources[^1].ReqSkill == 99,
            "Native mining thresholds/yields changed");
        Require(ClassProfessions.Active(player, SkillName.Mining) && ClassProfessions.Active(player, SkillName.Lumberjacking)
            && ClassProfessions.Active(player, SkillName.Fishing), "Gathering required craft selection");
        foreach (var creature in new BaseCreature[] { new Cow(), new Lizardman(), new Dragon() })
        {
            var corpse = new Corpse(creature, new List<Item>()); corpse.MoveToWorld(player.Location, player.Map);
            var knife = new ButcherKnife(); double before = player.Skills.Total;
            creature.OnCarve(player, corpse, knife);
            var hides = corpse.Items.OfType<BaseHides>().Single();
            Require(hides.Amount == (Core.ML && player.Race == Race.Human ? (int)Math.Ceiling(creature.Hides * 1.1) : creature.Hides) && hides.Resource == creature.HideType switch
            { HideType.Spined => CraftResource.SpinedLeather, HideType.Horned => CraftResource.HornedLeather,
                HideType.Barbed => CraftResource.BarbedLeather, _ => CraftResource.RegularLeather }
                && player.Skills.Total == before, "Skinning changed native species/yield or gained skill: " + creature.GetType().Name + " " + hides.Amount + " " + hides.Resource);
            corpse.Delete(); creature.Delete(); knife.Delete();
        }
    }
    private static void Spinning()
    {
        try
        {
            var player = Player("SpinProcessor"); var stale = Player("StaleSpinProcessor");
            var wheel = new SpinningWheelEastAddon(); wheel.MoveToWorld(player.Location, player.Map);
            var wheel2 = new SpinningWheelSouthAddon(); wheel2.MoveToWorld(stale.Location, stale.Map);
            var wool = Put(player, new Wool(2) { Hue = 1234 }); var moved = Put(stale, new TaintedWool(2));
            wool.OnDoubleClick(player); player.Target.Invoke(player, wheel.Components[0]);
            moved.OnDoubleClick(stale); stale.Target.Invoke(stale, wheel2.Components[0]);
            Require(wool.Amount == 2 && moved.Amount == 2 && wheel.Spinning && wheel2.Spinning, "Spin consumed before completion");
            moved.Internalize();
            var textiles = new List<(PlayerMobile Owner, SpinningWheelEastAddon Wheel, Item Source)>();
            foreach (var input in new Item[] { new Cotton(2) { Hue = 321 }, new Flax(2) { Hue = 321 }, new TaintedWool(2) { Hue = 321 }, new Wool(2) })
            {
                var owner = Player("FiberProcessor" + textiles.Count);
                var equipment = new SpinningWheelEastAddon(); equipment.MoveToWorld(owner.Location, owner.Map);
                Put(owner, input); input.OnDoubleClick(owner); owner.Target.Invoke(owner, equipment.Components[0]);
                Require(input.Amount == 2 && equipment.Spinning, "Fiber input consumed before spinning completes");
                if (input.GetType() == typeof(Wool))
                {
                    while (owner.Backpack.TotalItems < owner.Backpack.MaxItems) { Put(owner, new Bag { Weight = 0 }); }
                }
                textiles.Add((owner, equipment, input));
            }
            Timer.DelayCall(TimeSpan.FromSeconds(4), () =>
            {
                try
                {
                    Require(wool.Amount == 1 && player.Backpack.Items.OfType<DarkYarn>().Single() is { Amount: 3, Hue: 1234 }
                        && moved.Amount == 2 && !stale.Backpack.Items.OfType<DarkYarn>().Any(), "Spin delayed output/stale source failed");
                    foreach (var row in textiles)
                    {
                        bool full = row.Source.GetType() == typeof(Wool);
                        Require(row.Source.Amount == (full ? 2 : 1), "Fiber/full-pack delayed consumption wrong");
                        if (row.Source is Cotton or Flax)
                        { Require(row.Owner.Backpack.Items.OfType<SpoolOfThread>().Single() is { Amount: 6, Hue: 321 }, "Native fiber yield/hue changed"); }
                        else if (row.Source is TaintedWool)
                        { Require(row.Owner.Backpack.Items.OfType<DarkYarn>().Single() is { Amount: 1, Hue: 321 }, "Native tainted wool yield changed"); }
                        else { Require(!row.Owner.Backpack.Items.OfType<DarkYarn>().Any(), "Full pack received spun material"); }
                    }
                    _logger.Information("processing-spin-check: passed");
                }
                catch (Exception error) { _logger.Error(error, "processing-spin-check: failed"); }
                finally
                {
                    foreach (var row in textiles) { row.Wheel.Delete(); row.Owner.Delete(); }
                    wheel.Delete(); wheel2.Delete(); player.Delete(); stale.Delete(); moved.Delete();
                }
            });
        }
        catch (Exception error) { _logger.Error(error, "processing-spin-check: failed"); }
    }
    private static void Comparison()
    {
        Require(!ClassProfessions.Enabled, "Comparison feature unexpectedly enabled");
        var player = Player("ComparisonProcessor");
        var ingot = Put(player, new IronIngot(3)); var hides = Put(player, new Hides(3));
        Require(ingot.Weight == .1 && hides.Weight == 5, "Disabled feature changed native weights");
        ingot.Weight = 17; MaterialProcessing.Normalize(ingot);
        Require(ingot.Weight == 17, "Disabled feature normalized explicit weight");
        var stock = new GenericBuyInfo(typeof(Bandage), 5, 20, 1, 0);
        Require(stock.Amount == 20 && stock.GetEntity() is Bandage, "Disabled feature changed vendor stock");
        var cloth = Put(player, new Cloth(5)); var scissors = Put(player, new Scissors());
        Require(cloth.Scissor(player, scissors) && cloth.Deleted && player.Backpack.GetAmount(typeof(Bandage)) == 5,
            "Disabled feature changed native bandage conversion");
        var saw = Put(player, new Saw()); var log = Put(player, new Log());
        Require(MaterialProcessing.Quote(player, saw, log) == null, "Disabled feature offered scraps");
        player.Delete();
    }
    private sealed record PendingSaved(uint Owner, uint Wool, uint Wheel, uint Yarn, uint Loom);
    private static void Pending()
    {
        var owner = Player("PendingProcessor");
        var wheel = new SpinningWheelEastAddon(); wheel.MoveToWorld(owner.Location, owner.Map);
        var wool = Put(owner, new Wool(2)); wool.OnDoubleClick(owner); owner.Target.Invoke(owner, wheel.Components[0]);
        var loom = new LoomEastAddon(); loom.MoveToWorld(owner.Location, owner.Map); loom.Phase = 4;
        var yarn = Put(owner, new DarkYarn(2));
        File.WriteAllText(Path.Join(Core.BaseDirectory, "processing-pending.json"), JsonSerializer.Serialize(new PendingSaved(
            owner.Serial.Value, wool.Serial.Value, wheel.Serial.Value, yarn.Serial.Value, loom.Serial.Value)));
        Require(wheel.Spinning && wool.Amount == 2, "Pending spin did not defer consumption");
    }
    private static void Reload()
    {
        var saved = JsonSerializer.Deserialize<List<Saved>>(File.ReadAllText(Manifest));
        foreach (var state in saved)
        {
            var item = World.FindItem((Serial)state.Item);
            Require(item is { Deleted: false } && item.Amount == state.Amount && item.Hue == state.Hue
                && item.Origin == state.Origin && item.Weight == state.Weight && item.GetType().Name == state.Type
                && item.ItemID == state.Art && Resource(item) == state.Resource, "Reload weight migration altered resource identity/count: " + state.Item);
        }
        var pending = JsonSerializer.Deserialize<PendingSaved>(File.ReadAllText(Path.Join(Core.BaseDirectory, "processing-pending.json")));
        var owner = World.FindMobile((Serial)pending.Owner) as PlayerMobile;
        Require(World.FindItem((Serial)pending.Wool) is Wool { Amount: 2 }
            && World.FindItem((Serial)pending.Wheel) is SpinningWheelEastAddon { Spinning: false }
            && owner.Backpack.GetAmount(typeof(DarkYarn)) == 2
            && World.FindItem((Serial)pending.Yarn) is DarkYarn { Amount: 2 }
            && World.FindItem((Serial)pending.Loom) is LoomEastAddon { Phase: 4 }, "Pending native textile work lost input or replayed after restart");
        ProcessingUiChecks.Reload();
    }
}
