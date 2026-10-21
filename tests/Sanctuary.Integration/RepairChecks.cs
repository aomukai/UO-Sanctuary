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
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class RepairChecks
{
    private static readonly ILogger _logger = LogFactory.GetLogger(typeof(RepairChecks));
    private sealed class Rolls(bool save, bool lose, bool saveTool = false) : Random
    {
        private int _roll;
        public override int Next(int maximum) => (_roll++ switch { 0 => save, 1 => lose, _ => saveTool }) ? 0 : maximum - 1;
        public override int Next(int minimum, int maximum) => maximum - 1;
    }
    private sealed record Saved(uint Player, uint Item, uint Wand, string Quote, int Capacity, uint Tool, uint Repaired, int RepairedMaximum);
    private static string Pathname => Path.Join(Core.BaseDirectory, "repair-check.json");
    public static void Configure()
    {
        Register("repair-check", Check);
        Register("repair-reload-check", Reload);
    }
    private static void Register(string command, Action action) => ConsoleInputHandler.RegisterCommand(command,
        "Disposable T12 repair/recharge checks", _ => Core.LoopContext.Post(() =>
        {
            try { action(); _logger.Information("{Command}: passed", command); }
            catch (Exception error) { _logger.Error(error, "{Command}: failed", command); }
        }));
    private static void Require(bool value, string message)
    {
        if (!value) { throw new InvalidOperationException(message); }
    }
    private static PlayerMobile Player(SkillName skill)
    {
        var player = new PlayerMobile { Body = 0x190, Player = true, Name = "RepairCheck" };
        player.AddItem(new Backpack());
        new Account("repair-" + skill, Guid.NewGuid().ToString("N"))[0] = player;
        ClassCreation.Profiles.Assign(player, DiabloClass.Necromancer);
        ClassCreation.Profiles.AdvanceLevel(player, 99);
        player.RawStr = 200; player.RawDex = 200;
        ClassProfessions.Profiles.Confirm(player, skill);
        player.Skills[skill].Cap = 120; player.Skills[skill].Base = 120;
        Workshop(player);
        return player;
    }
    internal static void Workshop(PlayerMobile player)
    {
        FixtureSpace.Place(player,new Point3D(5550,1151,0),Map.Trammel);
        var skill=ClassProfessions.Profiles.Find(player).Craft;
        CraftFacilities.Register(new(skill,player.Map.Name,player.X+1,player.Y,player.Z,0xFAF));
        if(skill==SkillName.Blacksmith)
        { new Static(0xFAF).MoveToWorld(new Point3D(player.X+1,player.Y,player.Z),player.Map);
          new Static(0xFB1).MoveToWorld(new Point3D(player.X+1,player.Y+1,player.Z),player.Map); }
    }
    private static void Fund(PlayerMobile player, RepairRequest request)
    {
        foreach (var cost in request.Costs.Append(new(typeof(Gold), request.Gold)))
        {
            if (cost.Amount == 0) { continue; }
            var item = cost.Type.CreateInstance<Item>(); item.Amount = cost.Amount;
            player.Backpack.DropItem(item);
        }
    }
    private static string Inventory(PlayerMobile player)
    {
        var items = new List<Item>();
        foreach (var item in player.Backpack.FindItemsByType<Item>(true)) { items.Add(item); }
        return JsonSerializer.Serialize(items.Select(i => new { i.Serial, i.Amount, i.Hue }).OrderBy(i => i.Serial.Value));
    }
    private static void ClearResources(PlayerMobile player)
    {
        var removed = new List<Item>();
        foreach (var item in player.Backpack.FindItemsByType<Item>(true))
        {
            if (item is BaseIngot or Board or BaseLeather or Bone or Cloth or MetalScraps or LeatherStrips or ClothScraps or MagicEssence or Gold)
            { removed.Add(item); }
        }
        foreach (var item in removed) { item.Delete(); }
    }
    private static void Damage(Item item, int hits, int maximum = 20)
    {
        switch (item)
        {
            case BaseWeapon weapon: weapon.MaxHitPoints = maximum; weapon.HitPoints = hits; break;
            case BaseArmor armor: armor.MaxHitPoints = maximum; armor.HitPoints = hits; break;
            case BaseClothing clothing: clothing.MaxHitPoints = maximum; clothing.HitPoints = hits; break;
            case BaseJewel jewel: jewel.MaxHitPoints = maximum; jewel.HitPoints = hits; break;
        }
    }
    private static BaseTool Tool(PlayerMobile player, SkillName skill)
    {
        BaseTool tool = skill switch
        {
            SkillName.Blacksmith => new RepairHammer(), SkillName.Tailoring => new TailoringRepairKit(),
            SkillName.Fletching => new FletcherTools(), _ => new TinkerTools()
        };
        tool.UsesRemaining = 1000; player.Backpack.DropItem(tool); return tool;
    }
    private static void Approach(PlayerMobile player, BaseVendor vendor)
    {
        for (int x = -3; x <= 3; x++)
        {
            for (int y = -3; y <= 3; y++)
            {
                var point = new Point3D(vendor.X + x, vendor.Y + y, vendor.Z);
                if (vendor.Map.CanFit(point,16,false,false,true) && FixtureSpace.ClearOfAllies(player,point,vendor.Map) && vendor.Map.LineOfSight(point, vendor.Location))
                { player.MoveToWorld(point, vendor.Map); return; }
            }
        }
        throw new InvalidOperationException("Cannot approach repair vendor at "+vendor.Location);
    }
    private static void Check()
    {
        new Static(0xFAF).MoveToWorld(new Point3D(5551, 1150, 0), Map.Trammel);
        new Static(0xFB1).MoveToWorld(new Point3D(5551, 1151, 0), Map.Trammel);
        var players = new Dictionary<SkillName, PlayerMobile>();
        var tools = new Dictionary<SkillName, BaseTool>();
        foreach (var skill in new[] { SkillName.Blacksmith, SkillName.Tailoring, SkillName.Fletching, SkillName.Tinkering })
        { var player = Player(skill); players.Add(skill, player); tools.Add(skill, Tool(player, skill)); }

        int repaired = 0;
        foreach (var row in EquipmentCrafting.Recipes)
        {
            var player = players[row.Skill]; var tool = tools[row.Skill];
            var item = ItemOrigins.Create(ItemOrigin.Crafted, () => EquipmentCrafting.Resolve(row.Type).CreateInstance<Item>());
            var material = EquipmentCrafting.PhysicalMaterial(item.GetType());
            var resource = material switch
            {
                GearMaterial.Metal => CraftResource.Copper, GearMaterial.Wood => CraftResource.YewWood,
                GearMaterial.Leather => CraftResource.BarbedLeather, _ => CraftResource.None
            };
            EquipmentCrafting.Finish(item, player, row, resource, 0, CraftQuality.Average, new Random(17));
            player.Backpack.DropItem(item);
            // Natural non-wearing footwear and cosmetic cloth remain non-wearing/cosmetic.
            if (GearDetails.Read(item).MaximumDurability is not > 0 || ClassGear.CosmeticClothing(item))
            { item.Delete(); continue; }
            Damage(item, 0);
            var request = EquipmentRepairs.Quote(player, item, tool);
            Require(request != null, "No repair route: " + row.Id);
            Fund(player, request);
            int uses = tool.UsesRemaining;
            Require(EquipmentRepairs.Commit(request, out var result, new Rolls(false, false)) && result.LostMaximum == 0
                && GearDetails.Read(item).Durability == 20 && tool.UsesRemaining == uses - 1, "Recipe repair failed: " + row.Id);
            Require(!EquipmentRepairs.Commit(request, out _), "Repair replay succeeded");
            ClearResources(player); item.Delete(); repaired++;
        }
        Require(repaired > 300, "Repair cohort unexpectedly small");

        var smith = players[SkillName.Blacksmith]; var hammer = tools[SkillName.Blacksmith];
        var sword = ItemOrigins.Create(ItemOrigin.Crafted, () => new Longsword { Resource = CraftResource.Copper }); smith.Backpack.DropItem(sword);
        ClassGear.Profiles.Ensure(sword); Damage(sword, 3);
        var first = EquipmentRepairs.Quote(smith, sword, hammer);
        Require(first.Costs[0].Type == typeof(CopperIngot) && first.Costs[1] == new RepairCost(typeof(MetalScraps), 1), "Exact material/scrap cost wrong");
        var reopened = EquipmentRepairs.Quote(smith, sword, hammer, random: new Random(999));
        Require(first.Costs.SequenceEqual(reopened.Costs), "Reopen rerolled repair cost");
        string before = Inventory(smith);
        Require(!EquipmentRepairs.Commit(first, out _, new Rolls(true, false)) && Inventory(smith) == before, "Missing resources permitted saving-roll fishing");
        var rejected = EquipmentRepairs.Quote(smith, sword, hammer); Fund(smith, rejected);
        sword.HitPoints--;
        before = Inventory(smith);
        Require(!EquipmentRepairs.Commit(rejected, out _) && before == Inventory(smith), "Stale damage consumed resources");
        rejected = EquipmentRepairs.Quote(smith, sword, hammer); var parent = sword.Parent;
        var bag = new Bag(); smith.Backpack.DropItem(bag); bag.DropItem(sword);
        Require(!EquipmentRepairs.Commit(rejected, out _), "Moved item accepted stale request");
        smith.Backpack.DropItem(sword); bag.Delete();
        ClearResources(smith);
        var exact = EquipmentRepairs.Quote(smith, sword, hammer);
        smith.Backpack.DropItem(new IronIngot(20) { Hue = CraftResources.GetHue(CraftResource.Copper) });
        smith.Backpack.DropItem(new MetalScraps(20));
        before = Inventory(smith);
        Require(!EquipmentRepairs.Commit(exact, out _) && Inventory(smith) == before, "Matching hue bypassed exact material subtype");
        ClearResources(smith);
        var changedTool = EquipmentRepairs.Quote(smith, sword, hammer); Fund(smith, changedTool);
        hammer.UsesRemaining--;
        before = Inventory(smith);
        Require(!EquipmentRepairs.Commit(changedTool, out _) && Inventory(smith) == before, "Changed tool use count accepted");
        var moved = EquipmentRepairs.Quote(smith, sword, hammer);
        smith.MoveToWorld(new Point3D(5570, 1151, 0), Map.Trammel);
        Require(!EquipmentRepairs.Commit(moved, out _) && EquipmentRepairs.Quote(smith, sword, hammer) == null, "Remote workshop accepted");
        Workshop(smith);
        Require(EquipmentRepairs.Quote(players[SkillName.Tailoring], sword, tools[SkillName.Tailoring]) == null,
            "Foreign item/wrong profession accepted");
        var nativeTool = new SmithHammer(); smith.Backpack.DropItem(nativeTool);
        Repair.Do(smith, DefBlacksmithy.CraftSystem, nativeTool);
        Require(smith.Target == null, "Native smithing hammer bypassed separate repair tool");
        Repair.Do(smith, DefBlacksmithy.CraftSystem, new RepairDeed());
        Require(smith.Target == null, "Repair deed bypassed repair resources"); nativeTool.Delete();
        ClearResources(smith);
        var save = EquipmentRepairs.Quote(smith, sword, hammer); Fund(smith, save); before = Inventory(smith);
        int oldUses = hammer.UsesRemaining;
        Require(EquipmentRepairs.Commit(save, out var savedResult, new Rolls(true, false)) && savedResult.SavedResources
            && hammer.UsesRemaining == oldUses - 1, "Saving did not preserve resources/spend tool");
        Require(smith.Backpack.GetAmount(typeof(CopperIngot)) == save.Costs[0].Amount, "Saved materials consumed");
        ClearResources(smith);
        Damage(sword, 0);
        var craftedHammer = ItemOrigins.Create(ItemOrigin.Crafted, () => new RepairHammer());
        CraftTransactions.FinishTool(craftedHammer, smith, CraftQuality.Superior); smith.Backpack.DropItem(craftedHammer);
        var savedTool = EquipmentRepairs.Quote(smith, sword, craftedHammer); Fund(smith, savedTool);
        Require(EquipmentRepairs.Commit(savedTool, out _, new Rolls(true, false, true)) && craftedHammer.UsesRemaining == 120,
            "Crafted repair tool did not retain use-saving perk"); craftedHammer.Delete(); ClearResources(smith);
        foreach (double skill in new[] { 0d, 79.9, 80, 99.9, 100, 119.9, 120 })
        {
            smith.Skills[SkillName.Blacksmith].Base = skill; Damage(sword, 0, 2);
            var request = EquipmentRepairs.Quote(smith, sword, hammer); Fund(smith, request);
            double initial = smith.Skills[SkillName.Blacksmith].Base;
            Require(EquipmentRepairs.Commit(request, out var result, new Rolls(false, true)) && result.LostMaximum == 1
                && sword.MaxHitPoints == 1 && sword.HitPoints == 1 && smith.Skills[SkillName.Blacksmith].Base == initial,
                "Skill bracket/floor/gain incorrect");
            Damage(sword, 0, 1); request = EquipmentRepairs.Quote(smith, sword, hammer); Fund(smith, request);
            Require(EquipmentRepairs.Commit(request, out result, new Rolls(false, true)) && result.LostMaximum == 0 && sword.MaxHitPoints == 1,
                "Minimum maximum-durability floor breached"); ClearResources(smith);
        }
        smith.Skills[SkillName.Blacksmith].Base = 120;
        foreach (var tier in Enum.GetValues<GearTier>())
        {
            var row = EquipmentCrafting.Recipes.First(r => r.Type == nameof(Longsword) && r.Tier == tier);
            EquipmentCrafting.Finish(sword, smith, row, CraftResource.Copper, 0, CraftQuality.Average, new Random(17));
            foreach (var rarity in new[] { GearRarity.Normal, GearRarity.Magic, GearRarity.Rare })
            {
                ClassGear.Profiles.Convert(sword, 99, rarity, new Random(17)); Damage(sword, 0);
                var request = EquipmentRepairs.Quote(smith, sword, hammer, random: new Random(11));
                var band = EquipmentRepairs.Tier(tier);
                Require(request.Costs[0].Amount >= band.Min && request.Costs[0].Amount <= band.Max
                    && request.Costs[1].Amount == band.Scraps, "Tier material/scrap bounds wrong");
                int essence = request.Costs.FirstOrDefault(c => c.Type == typeof(MagicEssence))?.Amount ?? 0;
                Require(rarity == GearRarity.Normal ? essence == 0 : essence >= band.Min * (rarity == GearRarity.Rare ? 2 : 1)
                    && essence <= band.Max * (rarity == GearRarity.Rare ? 2 : 1), "Rarity essence cost incorrect");
                Fund(smith, request); Require(EquipmentRepairs.Commit(request, out _, new Rolls(false, false)), "Tier/rarity repair failed");
                ClearResources(smith);
            }
        }
        // Public services accept the same resources without selecting that profession.
        foreach (var (role, skill, item) in new (MarketRole, SkillName, Item)[]
        {
            (MarketRole.Smith, SkillName.Blacksmith, new Longsword()),
            (MarketRole.Tailor, SkillName.Tailoring, new LeatherCap()),
            (MarketRole.Bowyer, SkillName.Fletching, new Bow()),
            (MarketRole.Jeweller, SkillName.Tinkering, new GoldRing())
        })
        {
            var vendor = TownMarkets.Profiles.At($"Felucca/Britain/{role}");
            if (role is MarketRole.Smith or MarketRole.Tailor)
            {
                var market = TownMarkets.Market(vendor);
                string toolType = role == MarketRole.Smith ? nameof(RepairHammer) : nameof(TailoringRepairKit);
                var added = market.Offers.Single(o => !o.Equipment && World.FindItem(o.Item).GetType().Name == toolType);
                var gear = market.Offers.Where(o => o.Equipment).ToArray(); var deadline = market.NextRefresh;
                World.FindItem(added.Item).Delete(); market.Offers.Remove(added);
                TownMarkets.Market(vendor);
                Require(market.NextRefresh == deadline && market.Offers.Where(o => o.Equipment).SequenceEqual(gear)
                    && market.Offers.Any(o => !o.Equipment && World.FindItem(o.Item).GetType().Name == toolType),
                    "Existing shop failed to gain repair supplies or rerolled saved gear");
            }
            Approach(smith, vendor); smith.Backpack.DropItem(item); Damage(item, 0);
            var request = EquipmentRepairs.Quote(smith, item, vendor: vendor);
            Require(request != null && request.Skill == skill && request.SkillValue == 80 && request.Gold == 100,
                "NPC repair route/skill/fee wrong: " + role);
            Fund(smith, request); before = Inventory(smith);
            smith.MoveToWorld(new Point3D(vendor.X + 10, vendor.Y, vendor.Z), vendor.Map);
            Require(!EquipmentRepairs.Commit(request, out _) && before == Inventory(smith), "Distant NPC charged");
            Approach(smith, vendor); request = EquipmentRepairs.Quote(smith, item, vendor: vendor);
            Require(EquipmentRepairs.Commit(request, out var result, new Rolls(true, true)) && result.SavedResources
                && result.LostMaximum == 2 && smith.Backpack.GetAmount(typeof(Gold)) == 0, "NPC saving/loss/gold incorrect");
            item.Delete(); ClearResources(smith);
        }
        Workshop(smith);
        var wood = players[SkillName.Fletching]; var fletcher = tools[SkillName.Fletching];
        ThrowingRefills(wood, fletcher);
        var wand = new HealWand(); wood.Backpack.DropItem(wand);
        int capacity = EquipmentRepairs.Capacity(wand); wand.ConsumeCharge(wood);
        Require(wand.Charges == capacity - 1, "Native charge consumer did not spend charge");
        Damage(wand, 0);
        var combined = EquipmentRepairs.Quote(wood, wand, fletcher, charges: -1); Fund(wood, combined);
        oldUses = fletcher.UsesRemaining;
        Require(combined.RestoreCharges == 1 && combined.Costs.Any(c => c.Type == typeof(Board))
            && EquipmentRepairs.Commit(combined, out _, new Rolls(false, false)) && wand.Charges == capacity
            && fletcher.UsesRemaining == oldUses - 1, "Combined work failed or spent two uses");
        ClearResources(wood); wand.Charges = Math.Max(0, capacity - 3);
        int maximumBefore = wand.MaxHitPoints;
        var recharge = EquipmentRepairs.Quote(wood, wand, fletcher, durability: false, charges: 1); Fund(wood, recharge);
        Require(recharge.Costs.SequenceEqual(new[] { new RepairCost(typeof(MagicEssence), 1) })
            && EquipmentRepairs.Commit(recharge, out var rechargeResult, new Rolls(false, true))
            && rechargeResult.LostMaximum == 0 && wand.MaxHitPoints == maximumBefore, "Charge-only billed materials or lost durability");
        Require(EquipmentRepairs.Quote(wood, wand, fletcher, durability: false, charges: 60001) == null, "Over-cap recharge accepted");
        var copy = new HealWand(); EquipmentRepairs.Profiles.Copy(wand, copy);
        Require(EquipmentRepairs.Capacity(copy) == capacity && copy.Charges == wand.Charges, "Charge copy lost capacity/count"); copy.Delete();
        var npcBowyer = TownMarkets.Profiles.At("Felucca/Britain/Bowyer"); Approach(wood, npcBowyer);
        var insufficientFee = EquipmentRepairs.Quote(wood, wand, vendor: npcBowyer, durability: false, charges: -1);
        foreach (var cost in insufficientFee.Costs)
        { var input = cost.Type.CreateInstance<Item>(); input.Amount = cost.Amount; wood.Backpack.DropItem(input); }
        before = Inventory(wood);
        Require(!EquipmentRepairs.Commit(insufficientFee, out _, new Rolls(true, false)) && Inventory(wood) == before,
            "Insufficient NPC fee consumed essence");
        Workshop(wood);
        ClearResources(wood);
        var retained = new WoodenShield(); wood.Backpack.DropItem(retained); Damage(retained, 0);
        var loseMaximum = EquipmentRepairs.Quote(wood, retained, fletcher); Fund(wood, loseMaximum);
        Require(EquipmentRepairs.Commit(loseMaximum, out var lost, new Rolls(false, true)) && lost.LostMaximum == 1,
            "Persistent-loss fixture did not actually lose maximum durability");
        foreach (var type in new[] { typeof(RepairHammer), typeof(TailoringRepairKit) })
        {
            var vendorTool = ItemOrigins.Create(ItemOrigin.Vendor, () => type.CreateInstance<BaseTool>());
            CraftTransactions.Supply(vendorTool);
            Require(vendorTool.UsesRemaining is >= 20 and <= 30 && DefTinkering.CraftSystem.CraftItems.SearchFor(type) != null,
                "New repair tool recipe/vendor capacity missing"); vendorTool.Delete();
        }
        // Save an unaccepted quote and partially spent wand for all later boots.
        Damage(sword, 4, 15);
        var persisted = EquipmentRepairs.Quote(smith, sword, hammer);
        Damage(wand, 0, 12);
        File.WriteAllText(Pathname, JsonSerializer.Serialize(new Saved(smith.Serial.Value, sword.Serial.Value, wand.Serial.Value,
            JsonSerializer.Serialize(persisted.Costs.Select(c => new { Type = c.Type.Name, c.Amount })), capacity, hammer.Serial.Value,
            retained.Serial.Value, 19)));
        File.WriteAllText(Path.Join(Core.BaseDirectory, "Data/Sanctuary/Balance/repairs.json"),
            JsonSerializer.Serialize(new[] { EquipmentRepairs.Settings with
            { Tiers = EquipmentRepairs.Settings.Tiers.Select(t => t with { Min = t.Min + 10, Max = t.Max + 10 }).ToArray() } }));
        _logger.Information("Repair cohort: {Count} durable recipe identities; all tiers, rarities, skill boundaries, NPC routes and charge transactions passed", repaired);
    }
    private static void ThrowingRefills(PlayerMobile worker, BaseTool tool)
    {
        ClearResources(worker);
        var item = (BaseWeapon)NamedGear.Create("unique/335", 99, new Random(43)); worker.Backpack.DropItem(item);
        int capacity = NamedAmmunition.Maximum(item), hits = item.HitPoints, maximum = item.MaxHitPoints;
        var state = ClassGear.State(item); var now = TimedEffects.Now;
        for (int i = 0; i < capacity; i++) { Require(NamedAmmunition.Consume(item, now), "Could not empty refill fixture"); }
        var quote = EquipmentRepairs.Quote(worker, item, tool, durability: false, charges: -1);
        Require(quote is { Durability: false } && quote.RestoreCharges == capacity && quote.Costs.Any(c => c.Type == EquipmentRepairs.Processed(GearDetails.Read(item)))
            && quote.Costs.Any(c => c.Type == typeof(MagicEssence)), "Throwing refill bypassed the named repair resource bundle");
        var before = Inventory(worker);
        Require(!EquipmentRepairs.Commit(quote, out _) && Inventory(worker) == before && NamedAmmunition.Remaining(item) == 0,
            "Insufficient refill resources consumed or restored anything");
        quote = EquipmentRepairs.Quote(worker, item, tool, durability: false, charges: 2); Fund(worker, quote);
        Require(EquipmentRepairs.Commit(quote, out var result, new Rolls(false, true)) && result.Charges == 2 && result.LostMaximum == 0
            && NamedAmmunition.Remaining(item) == 2 && item.HitPoints == hits && item.MaxHitPoints == maximum
            && ReferenceEquals(ClassGear.State(item), state) && !EquipmentRepairs.Commit(quote, out _),
            "Partial refill changed durability/identity or allowed replay");
        ClearResources(worker);
        quote = EquipmentRepairs.Quote(worker, item, tool, durability: false, charges: -1); Fund(worker, quote);
        NamedAmmunition.Consume(item, now); before = Inventory(worker);
        Require(!EquipmentRepairs.Commit(quote, out _) && Inventory(worker) == before, "Changed throwing quantity accepted stale refill");
        ClearResources(worker);
        var vendor = TownMarkets.Profiles.At("Felucca/Britain/Bowyer"); Approach(worker, vendor);
        quote = EquipmentRepairs.Quote(worker, item, vendor: vendor, durability: false, charges: -1); Fund(worker, quote);
        Require(quote.Gold == EquipmentRepairs.Tier(state.Tier).Gold
            && EquipmentRepairs.Commit(quote, out result, new Rolls(false, true)) && NamedAmmunition.Remaining(item) == capacity
            && item.HitPoints == hits && item.MaxHitPoints == maximum, "NPC refill failed or changed durability");
        Require(EquipmentRepairs.Quote(worker, item, vendor: vendor, durability: false, charges: -1) == null, "Full throwing weapon accepted refill");
        item.Delete(); ClearResources(worker); Workshop(worker);
        var ethereal = NamedGear.Create("unique/386", 99, new Random(2)); worker.Backpack.DropItem(ethereal);
        Require(NamedGear.Ethereal(ethereal), "Ethereal refill fixture changed");
        NamedAmmunition.Consume(ethereal, now);
        Require(EquipmentRepairs.Quote(worker, ethereal, tool, durability: false, charges: -1) == null,
            "Ethereal throwing weapon bypassed ordinary repair restrictions"); ethereal.Delete();
    }
    private static void Reload()
    {
        var saved = JsonSerializer.Deserialize<Saved>(File.ReadAllText(Pathname));
        var player = (PlayerMobile)World.FindMobile((Serial)saved.Player);
        var item = World.FindItem((Serial)saved.Item);
        var tool = (BaseTool)World.FindItem((Serial)saved.Tool);
        Workshop(player);
        var quote = EquipmentRepairs.Quote(player, item, tool, random: new Random(54321));
        Require(quote != null && JsonSerializer.Serialize(quote.Costs.Select(c => new { Type = c.Type.Name, c.Amount })) == saved.Quote,
            $"Unaccepted quote changed after reboot: quote={quote != null}; map={player.Map}; ready={EquipmentRepairs.Ready(player)}; tool={tool?.UsesRemaining}; skill={player.Skills[SkillName.Blacksmith].Base}; active={ClassProfessions.Active(player, SkillName.Blacksmith)}; native={GearDetails.Read(item)}; price={EquipmentRepairs.Profiles.Price(item)}");
        var wand = (BaseWand)World.FindItem((Serial)saved.Wand);
        Require(EquipmentRepairs.Capacity(wand) == saved.Capacity && wand.Charges == saved.Capacity - 2
            && GearDetails.Read(World.FindItem((Serial)saved.Repaired)).MaximumDurability == saved.RepairedMaximum
            && GearDetails.Read(World.FindItem((Serial)saved.Repaired)).Durability == saved.RepairedMaximum,
            "Saved charge capacity/durability changed");
    }
}
