using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Engines.Craft;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using Server.Multis;

namespace Sanctuary.Integration;

public static class CarpentryChecks
{
    private static readonly ILogger _logger = LogFactory.GetLogger(typeof(CarpentryChecks));
    private sealed record Saved(uint Item, uint Parent, string Type, int Held, RestorationKind Kind, int Tier, int Weight, bool Constructed, ItemOrigin Origin);
    private static string Manifest => Path.Join(Core.BaseDirectory, "carpentry-checks.json");
    public static void Configure()
    {
        Register("carpentry-check", () => { var saved = new List<Saved>(); Recipes(saved); Kegs(saved); Placement(saved); File.WriteAllText(Manifest, JsonSerializer.Serialize(saved)); });
        Register("carpentry-reload-check", Reload);
    }
    private static void Register(string command, Action action) => ConsoleInputHandler.RegisterCommand(command, "Carpentry, house placement and identity-safe kegs", _ => Core.LoopContext.Post(() =>
    { try { action(); _logger.Information("{Command}: passed", command); } catch (Exception e) { _logger.Error(e, "{Command}: failed", command); } }));
    private static void Require(bool value, string why) => RuneChecks.Require(value, why);
    internal static PlayerMobile Player()
    {
        var p = ImbuingChecks.Player(SkillName.Carpentry); FixtureSpace.Place(p,new Point3D(5650, 1160, 0), Map.Trammel);
        p.Skills.Carpentry.SetLockNoRelay(SkillLock.Locked);
        CraftFacilities.Register(new(SkillName.Carpentry, "Trammel", p.X+1,p.Y,p.Z,0xB4A)); return p;
    }
    internal static void Inputs(PlayerMobile p, CarpentryRecipe row, int batches = 1)
    {
        foreach (var i in row.Ingredients)
        {
            var t = EquipmentCrafting.Resolve(i.Type); var first = t.CreateInstance<Item>();
            if (first.Stackable) { first.Amount = i.Amount * batches; p.Backpack.DropItem(first); }
            else { p.Backpack.DropItem(first); for (int n = 1; n < i.Amount * batches; n++) { p.Backpack.DropItem(t.CreateInstance<Item>()); } }
        }
    }
    internal static CraftTransactions.Request Ticket(CarpentryRequest q) => new(q.Owner, DefCarpentry.CraftSystem, q.Recipe, null, q.Tool, 0) { Carpentry = q };
    private static Saved Snapshot(Item i) => new(i.Serial.Value, (i.Parent as Item)?.Serial.Value ?? 0, i.GetType().Name,
        (i as SanctuaryPotionKeg)?.Held ?? 0, (i as SanctuaryPotionKeg)?.Kind ?? 0, (i as SanctuaryPotionKeg)?.Tier ?? 0, i.PileWeight, i.PlayerConstructed, i.Origin);
    private static void Recipes(List<Saved> saved)
    {
        foreach (var row in CarpentryCrafting.Recipes)
        {
            var p = Player(); var tool = new Saw(100); p.Backpack.DropItem(tool); Inputs(p, row, 3);
            if (row.Skill > 0) { p.Skills.Carpentry.Base = row.Skill - .1; Require(CarpentryCrafting.Quote(p, tool, row) == null, "Under-skilled Carpentry allowed"); }
            p.Skills.Carpentry.Base = 120; var q = CarpentryCrafting.Quote(p, tool, row); Require(q != null, "No quote for " + row.Type);
            var ticket = Ticket(q); Require(CraftTransactions.Complete(ticket, new AlchemyChecks.Roll(0)) && !CraftTransactions.Complete(ticket), "Carpentry failed/replayed " + row.Type);
            var output = p.Backpack.Items.Last(i => i.GetType() == EquipmentCrafting.Resolve(row.Type));
            Require(output.Origin == ItemOrigin.Crafted && output.PlayerConstructed && tool.UsesRemaining == 99, "Output origin/tool wrong");
            foreach (var i in row.Ingredients) { Require(p.Backpack.GetAmount(EquipmentCrafting.Resolve(i.Type)) == i.Amount * 2, "Wrong recipe cost " + row.Type); }
            saved.Add(Snapshot(output));
            p.Skills.Carpentry.Base = row.Skill; q = CarpentryCrafting.Quote(p, tool, row); Require(q != null, "Required skill gives no chance");
            Require(CraftTransactions.Complete(Ticket(q), new AlchemyChecks.Roll()) && tool.UsesRemaining == 98, "Failed attempt rejected");
            foreach (var i in row.Ingredients) { Require(p.Backpack.GetAmount(EquipmentCrafting.Resolve(i.Type)) == i.Amount * 2 - (i.Amount + 1) / 2, "Failure not rounded half " + row.Type); }
        }
        var owner = Player(); var saw = new Saw(100); owner.Backpack.DropItem(saw); var r = CarpentryCrafting.Recipes.First(i => i.Type == "SanctuaryPotionKeg"); Inputs(owner, r, 2);
        var barrel = owner.Backpack.Items.OfType<Keg>().First(); var gold = new Gold(5); barrel.DropItem(gold);
        var quote = CarpentryCrafting.Quote(owner, saw, r); Require(quote != null && quote.Inputs.All(i => i.Item != barrel), "Filled component selected");
        var clean = owner.Backpack.Items.OfType<Keg>().First(i => i.TotalItems == 0); clean.DropItem(new Gold(1));
        Require(!CraftTransactions.Complete(Ticket(quote)) && saw.UsesRemaining == 100 && !gold.Deleted, "Contents changed after quote accepted");
        Require(CarpentryCrafting.Quote(owner, saw, r) == null, "Filled components accepted"); clean.Items[0].Delete();
        var chest = new WoodenChest(); owner.Backpack.DropItem(chest); chest.DropItem(clean); chest.Locked = true; Require(CarpentryCrafting.Quote(owner, saw, r) == null, "Locked component accepted"); chest.Locked = false;
        chest.TrapType = TrapType.ExplosionTrap; Require(CarpentryCrafting.Quote(owner, saw, r) == null, "Trapped component accepted"); chest.TrapType = TrapType.None;
        quote = CarpentryCrafting.Quote(owner, saw, r); CarpentryCrafting.Invalidate(owner); Require(!CraftTransactions.Complete(Ticket(quote)), "Cancelled craft replayed");
        quote = CarpentryCrafting.Quote(owner, saw, r); ClassProfessions.Profiles.Select(owner, SkillName.Alchemy);
        Require(!CraftTransactions.Complete(Ticket(quote)), "Switched craft accepted"); ClassProfessions.Profiles.Select(owner, SkillName.Carpentry);
        quote = CarpentryCrafting.Quote(owner, saw, r); owner.MoveToWorld(new Point3D(owner.X + 30, owner.Y, 0), owner.Map);
        Require(!CraftTransactions.Complete(Ticket(quote)), "Remote Carpentry accepted"); FixtureSpace.Place(owner,new Point3D(5650, 1160, 0), Map.Trammel);
        var small = CarpentryCrafting.Recipes.First(i => i.Type == "FootStool"); Inputs(owner, small, 2); quote = CarpentryCrafting.Quote(owner, saw, small); owner.Backpack.MaxItems = owner.Backpack.TotalItems;
        Require(!CraftTransactions.Complete(Ticket(quote), new AlchemyChecks.Roll(0)) && saw.UsesRemaining == 100, "Full pack consumed materials"); owner.Backpack.MaxItems = 125;
        Require(!TradeSkillHooks.CheckRecipe(owner, DefTinkering.CraftSystem, DefTinkering.CraftSystem.CraftItems.SearchFor(typeof(PotionKeg)), new TinkerTools(5)), "Native potion keg recipe bypassed book");
        var native = DefCarpentry.CraftSystem.CraftItems.SearchFor(typeof(Keg)); Require(!TradeSkillHooks.CheckRecipe(owner, DefCarpentry.CraftSystem, native, saw), "Native Keg bypassed book");
        var fake = new CraftItem(typeof(Keg), "Forged", "Forged"); fake.AddSkill(SkillName.Carpentry, 0, 50);
        Require(!CraftTransactions.Complete(new(owner, DefCarpentry.CraftSystem, fake, null, saw, 0)), "Unbound output allowed");
        saw.Delete(); saw = ItemOrigins.Create(ItemOrigin.Crafted, () => new Saw(100)); owner.Backpack.DropItem(saw); CraftTransactions.Profiles.Store(saw, new(CraftQuality.Superior, true, 100));
        quote = CarpentryCrafting.Quote(owner, saw, r); Require(CraftTransactions.Complete(Ticket(quote), new AlchemyChecks.Roll(0, 0)) && saw.UsesRemaining == 100, "Crafted saw saving missing");
    }
    private static void Kegs(List<Saved> saved)
    {
        foreach (var row in BalanceCatalog.Read<RestorationBase>("restoration-potions"))
        {
            var p = Player(); var keg = new SanctuaryPotionKeg(); p.Backpack.DropItem(keg);
            foreach (var origin in Enum.GetValues<ItemOrigin>())
            {
                var pot = ItemOrigins.Create(origin, () => new RestorationPotion(row.Kind, row.Tier) { Amount = 2 }); p.Backpack.DropItem(pot);
                int before = p.Backpack.GetAmount(typeof(Bottle)); long revision = keg.Revision;
                Require(keg.PourIn(p, pot, revision) && pot.Deleted && p.Backpack.GetAmount(typeof(Bottle)) == before + 2, "Deposit bottle balance wrong");
                Require(!keg.FillBottle(p, revision), "Stale revision withdrew");
            }
            Require(keg.Held == 6 && keg.PileWeight == 25 && p.Backpack.TotalWeight >= keg.PileWeight, "Keg contents weight not propagated");
            var wrong = new RestorationPotion(row.Kind == RestorationKind.Healing ? RestorationKind.Mana : RestorationKind.Healing, 1); p.Backpack.DropItem(wrong);
            Require(!keg.PourIn(p, wrong, keg.Revision) && !wrong.Deleted, "Mixed kind accepted"); wrong.Delete();
            foreach (var origin in new[] { ItemOrigin.Vendor, ItemOrigin.Crafted, ItemOrigin.Drop })
            {
                Require(keg.FillBottle(p, keg.Revision), "Withdrawal rejected"); var pot = p.Backpack.Items.OfType<RestorationPotion>().Single();
                Require(pot.Kind == row.Kind && pot.Tier == row.Tier && pot.Amount == 1 && pot.Origin == origin, "Potion identity/origin changed"); pot.Delete();
                Require(keg.FillBottle(p, keg.Revision), "Second withdrawal rejected"); p.Backpack.Items.OfType<RestorationPotion>().Single().Delete();
            }
            Require(keg.Held == 0 && keg.PileWeight == 20 && p.Backpack.GetAmount(typeof(Bottle)) == 0 && !keg.FillBottle(p, keg.Revision), "Empty keg duplicated doses/bottles");
            var stored = ItemOrigins.Create(ItemOrigin.Crafted, () => new RestorationPotion(row.Kind, row.Tier) { Amount = 3 }); p.Backpack.DropItem(stored);
            Require(keg.PourIn(p, stored, keg.Revision), "Saved keg deposit failed"); saved.Add(Snapshot(keg));
            var native = new PotionKeg(); Require(!native.OnDragDrop(p, new RestorationPotion(row.Kind, row.Tier)) && native.Held == 0, "Native keg converted custom identity"); native.Delete();
        }
        var owner = Player(); var barrel = new SanctuaryPotionKeg(); owner.Backpack.DropItem(barrel);
        var stack = new RestorationPotion(RestorationKind.Healing, 1) { Amount = 105 }; owner.Backpack.DropItem(stack);
        Require(barrel.PourIn(owner, stack, barrel.Revision) && barrel.Held == 100 && stack.Amount == 5 && owner.Backpack.GetAmount(typeof(Bottle)) == 100 && barrel.PileWeight == 100, "Partial-capacity balance wrong");
        Require(!barrel.PourIn(owner, stack, barrel.Revision), "Full keg overflowed");
        var tier = new RestorationPotion(RestorationKind.Healing, 2); owner.Backpack.DropItem(tier); Require(!barrel.PourIn(owner, tier, barrel.Revision), "Mixed tier accepted");
        var other = Player(); Require(!barrel.FillBottle(other, barrel.Revision), "Other backpack keg accessible");
        stack.Delete(); tier.Delete(); var bottle = owner.Backpack.Items.OfType<Bottle>().Single(); bottle.Delete(); Require(!barrel.FillBottle(owner, barrel.Revision) && barrel.Held == 100, "Missing bottle spent dose");
        owner.Backpack.DropItem(new Bottle(2)); owner.Backpack.MaxItems = owner.Backpack.TotalItems;
        Require(!barrel.FillBottle(owner, barrel.Revision) && barrel.Held == 100 && owner.Backpack.GetAmount(typeof(Bottle)) == 2, "Full pack spent bottle/dose"); owner.Backpack.MaxItems = 125;
        var beltPot = new RestorationPotion(RestorationKind.Healing, 1); owner.Backpack.DropItem(beltPot); Require(PotionBelts.Load(owner, 0, beltPot), "Belt setup failed");
        Require(!barrel.PourIn(owner, PotionBelts.Cell(owner, 0), barrel.Revision), "Direct belt source accepted");
        barrel.MoveToWorld(owner.Location, owner.Map); Require(!barrel.CanUse(owner), "Unsecured ground keg accessible"); owner.Backpack.DropItem(barrel);
        owner.BankBox.DropItem(barrel); Require(!barrel.CanUse(owner), "Bank keg accessible"); owner.Backpack.DropItem(barrel);
        var locked = new WoodenChest(); owner.Backpack.DropItem(locked); locked.DropItem(barrel); locked.Locked = true;
        Require(!barrel.FillBottle(owner, barrel.Revision), "Keg in a locked container accessible"); locked.Locked = false; owner.Backpack.DropItem(barrel);
        owner.Frozen = true; Require(!barrel.FillBottle(owner, barrel.Revision), "Frozen actor used keg"); owner.Frozen = false;
        owner.Kill(); Require(!barrel.FillBottle(owner, barrel.Revision), "Dead actor used keg");
        var heavy = Player(); var keg2 = new SanctuaryPotionKeg(); heavy.Backpack.DropItem(keg2); var doses = new RestorationPotion(RestorationKind.Mana, 1) { Amount = 10 }; heavy.Backpack.DropItem(doses);
        heavy.Backpack.DropItem(new IronIngot(heavy.Backpack.MaxWeight - heavy.Backpack.TotalWeight)); int oldWeight = heavy.Backpack.TotalWeight;
        Require(!keg2.PourIn(heavy, doses, keg2.Revision) && doses.Amount == 10 && keg2.Held == 0 && heavy.Backpack.TotalWeight == oldWeight, "Filled keg weight bypassed capacity");
    }
    private static void Placement(List<Saved> saved)
    {
        var p = Player(); var deed = new SmallForgeDeed(); p.Backpack.DropItem(deed);
        var point = new Point3D(5700, 1200, 0); BaseHouse home = null; var addon = deed.Addon;
        Require(addon.CouldFit(point, Map.Trammel, p, ref home) != AddonFitResult.Valid && !deed.Deleted, "Deed placed outside house"); addon.Delete();
        var house = new SmallOldHouse(p, 0x64); house.MoveToWorld(new Point3D(5800, 1200, 0), Map.Trammel);
        point = new Point3D(5800, 1200, 7); p.MoveToWorld(new Point3D(point.X - 2, point.Y, point.Z), Map.Trammel); addon = deed.Addon;
        var result = addon.CouldFit(point, p.Map, p, ref home); Require(result == AddonFitResult.Valid, "Native house placement unavailable: " + result);
        var stranger = Player(); Require(!BaseAddon.CheckHouse(stranger, point, p.Map, 16, out _), "Another owner's house accepted");
        addon.MoveToWorld(point, p.Map); house.Addons.Add(addon); deed.Delete();
        Require(!CraftFacilities.Allows(p, SkillName.Blacksmith, false) && !CraftFacilities.Allows(p, SkillName.Carpentry, false), "Private addon registered a workshop");
        p.MoveToWorld(new Point3D(point.X - 1, point.Y, point.Z), p.Map);
        var keg = new SanctuaryPotionKeg(); keg.MoveToWorld(new Point3D(point.X + 1, point.Y, point.Z), p.Map); keg.IsLockedDown = true; keg.Movable = false;
        Require(keg.CanUse(p) && !keg.CanUse(stranger), "House keg permission wrong"); saved.Add(Snapshot(keg));
        saved.Add(Snapshot(addon));
    }
    private static void Reload()
    {
        foreach (var s in JsonSerializer.Deserialize<Saved[]>(File.ReadAllText(Manifest)))
        {
            var item = World.FindItem((Serial)s.Item); Require(item != null && item.GetType().Name == s.Type && item.PileWeight == s.Weight && item.PlayerConstructed == s.Constructed && item.Origin == s.Origin, "Object not restored " + s.Type);
            Require(((item.Parent as Item)?.Serial.Value ?? 0) == s.Parent, "Furniture parent changed");
            if (item is SanctuaryPotionKeg k)
            {
                Require(k.Held == s.Held && k.Kind == s.Kind && k.Tier == s.Tier, "Keg identity/count changed");
                if (k.Held > 0)
                {
                    var owner = (PlayerMobile)k.RootParent;
                    Require(k.FillBottle(owner, k.Revision), "Reloaded keg withdrawal failed");
                    var potion = owner.Backpack.Items.OfType<RestorationPotion>().Single();
                    Require(potion.Origin == ItemOrigin.Crafted && k.PourIn(owner, potion, k.Revision) && k.Held == s.Held, "Saved dose origin changed");
                }
            }
        }
    }
}
