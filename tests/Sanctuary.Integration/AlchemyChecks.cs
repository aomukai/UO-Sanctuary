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

namespace Sanctuary.Integration;

public static class AlchemyChecks
{
    private static readonly ILogger _logger = LogFactory.GetLogger(typeof(AlchemyChecks));
    private static string Manifest => Path.Join(Core.BaseDirectory, "alchemy-checks.json");
    private sealed record Saved(uint Item, RestorationKind Kind, int Tier, int Amount, ItemOrigin Origin, uint Parent);
    internal sealed class Roll(double value = .999, int save = 99) : Random
    { public override double NextDouble() => value; public override int Next(int max) => Math.Min(save, max - 1); }
    public static void Configure() { Register("alchemy-check", Check); Register("alchemy-reload-check", Reload); }
    private static void Register(string name, Action action) => ConsoleInputHandler.RegisterCommand(name, "Restorative mixtures, source drops and frozen double restoration", _ => Core.LoopContext.Post(() =>
    { try { action(); _logger.Information("{Command}: passed", name); } catch (Exception e) { _logger.Error(e, "{Command}: failed", name); } }));
    private static void Require(bool value, string why) => RuneChecks.Require(value, why);
    private static T Put<T>(PlayerMobile p, T item) where T : Item { p.Backpack.DropItem(item); return item; }
    internal static PlayerMobile Player()
    {
        var p = ImbuingChecks.Player(SkillName.Alchemy); p.Skills.Alchemy.SetLockNoRelay(SkillLock.Locked); return p;
    }
    internal static RestorationPotion Potion(PlayerMobile p, RestorationKind kind, int tier, int amount = 1, ItemOrigin origin = ItemOrigin.Drop) =>
        Put(p, ItemOrigins.Create(origin, () => new RestorationPotion(kind, tier) { Amount = amount }));
    internal static void Inputs(PlayerMobile p, AlchemyRecipe r, int batches = 1)
    {
        if (r.Healing > 0) { Potion(p, RestorationKind.Healing, r.HealingTier, r.Healing * batches); }
        if (r.Mana > 0) { Potion(p, RestorationKind.Mana, r.ManaTier, r.Mana * batches); }
        Put(p, new Bottle(r.Bottles * batches));
        foreach (var ingredient in r.Reagents ?? [])
        {
            var item = EquipmentCrafting.Resolve(ingredient.Type).CreateInstance<Item>();
            item.Amount = ingredient.Amount * batches; Put(p, item);
        }
    }
    internal static CraftTransactions.Request Ticket(AlchemyRequest q) => new(q.Owner, DefAlchemy.CraftSystem, q.Recipe, null, q.Tool, 0) { Alchemy = q };
    private static void Empty(PlayerMobile p) { foreach (var i in p.Backpack.Items.ToArray()) { i.Delete(); } }
    private static void Check()
    {
        Formula(); Drinking(); Crafting(); Drops(); Vendors();
        _logger.Information("Alchemy coverage: seven class multipliers, ten source profiles, exact double probabilities/frozen dose, four mixtures/failure costs, stale/capacity/native bypass guards, 3-difficulty potion drops and unlimited vendor supply passed");
    }
    private static void Formula()
    {
        Require(PotionRestoration.DoubleChance(2) == 0 && Math.Abs(PotionRestoration.DoubleChance(3) - 1.0 / 300) < 1e-12
            && PotionRestoration.DoubleChance(200) == .495 && Math.Abs(PotionRestoration.DoubleChance(201) - 100.0 / 201) < 1e-12
            && PotionRestoration.DoubleChance(400) == .7475, "Double-restoration formula boundaries wrong");
        foreach (var c in Enum.GetValues<DiabloClass>())
        {
            var p = new PlayerMobile { Body = 0x190 }; ClassCreation.Profiles.Assign(p, c);
            foreach (var kind in new[] { RestorationKind.Healing, RestorationKind.Mana })
            {
                int halves = c is DiabloClass.Amazon or DiabloClass.Paladin or DiabloClass.Assassin ? 3
                    : c == DiabloClass.Barbarian ? kind == RestorationKind.Healing ? 4 : 2 : kind == RestorationKind.Healing ? 2 : 4;
                for (int t = 1; t <= 5; t++)
                { var r = PotionBeltCatalog.Potion(kind, t); Require(PotionRestoration.Amount(p, r) == r.Amount * halves / 2, "Class restoration amount changed"); }
            }
            p.Delete();
        }
    }
    private static void Drinking()
    {
        var p = Player(); p.Hits = 1; p.Mana = 0; p.RawInt = 1000;
        var r = PotionBeltCatalog.Potion(RestorationKind.Healing, 1);
        var potion = Potion(p, r.Kind, r.Tier, origin: ItemOrigin.Crafted);
        Require(PotionRestoration.Drink(p, potion, new Roll(0)), "Critical dose rejected");
        var state = TimedEffects.Snapshot().Single(s => s.Owner == p.Serial.Value);
        Require(state.Life.Units == 60000000 && state.Life.Duration == r.DurationMs, "Double amount/duration not frozen at drink");
        TimedEffects.Profiles.Capture(); var w = new BufferWriter(new byte[1024 * 1024], true); TimedEffects.Profiles.Serialize(w);
        TimedEffects.Profiles.Deserialize(new BufferReader(w.Buffer[..(int)w.Position]));
        Require(TimedEffects.Snapshot().Single(s => s.Owner == p.Serial.Value).Life == state.Life, "Reload rerolled double dose");
        TimedEffects.Settle(p, state.UpdatedUtc.AddMilliseconds(r.DurationMs)); Require(p.Hits == 61, "Critical dose did not pay exactly twice");
        TimedEffects.Clear(p); p.Hits = 1; p.Mana = 0;
        TimedEffects.AddRestoration(p, true, 100, 5000); TimedEffects.AddRestoration(p, false, 100, 5000);
        var heal = Potion(p, RestorationKind.InstantHealing, 1);
        Require(PotionRestoration.Drink(p, heal, new Roll()) && p.Hits == p.HitsMax, "Instant Heal did not fill life");
        var a = TimedEffects.Snapshot().Single(s => s.Owner == p.Serial.Value);
        Require(a.Life.Duration == 0 && a.Mana.Duration > 0, "Instant Heal retained overheal/cancelled mana");
        p.Hits -= 10; TimedEffects.Settle(p, a.UpdatedUtc.AddSeconds(6)); Require(p.Hits == p.HitsMax - 10 && p.Mana == 100, "Old healing survived full instant heal");
        TimedEffects.Clear(p); p.Hits = 1; p.Mana = 0;
        var rejuv = Potion(p, RestorationKind.Rejuvenation, 1);
        TimedEffects.ApplyBuff(p, TimedEffectCategory.Food, "light", 1); TimedEffects.Chill(p, 50, 2000, TimedEffects.Now);
        Require(PotionBelts.Load(p, 0, rejuv) && PotionBelts.Use(p, 0) && p.Hits == 1 + p.HitsMax * 35 / 100
            && p.Mana == p.ManaMax * 35 / 100 && TimedEffects.Buff(p, TimedEffectCategory.Food) != null
            && GearEffects.ChillPercent(p) == 50, "Belt rejuvenation amount/instant/no-cleanse wrong");
        var full = Potion(p, RestorationKind.Rejuvenation, 2);
        Require(PotionRestoration.Drink(p, full, new Roll()) && p.Hits == p.HitsMax && p.Mana == p.ManaMax, "Full Rejuvenation wrong");
        full = Potion(p, RestorationKind.Rejuvenation, 2); Require(!PotionRestoration.Drink(p, full, new Roll()) && !full.Deleted, "Full pools consumed mixture");
        p.Mana = 0; var recharge = Potion(p, RestorationKind.InstantMana, 1);
        Require(PotionRestoration.Drink(p, recharge, new Roll()) && p.Mana == p.ManaMax, "Instant Recharge wrong");
        p.Hits = 1; p.Backpack.MaxItems = 1;
        foreach (var b in p.Backpack.Items.OfType<Bottle>().ToArray()) { b.Delete(); }
        var direct = Potion(p, RestorationKind.InstantHealing, 1, 2);
        Require(PotionRestoration.Drink(p, direct, new Roll(0)) && direct.Amount == 1 && !p.Backpack.Items.OfType<Bottle>().Any(), "Full pack bottle recovery spilled or lost consumption");
        p.Backpack.MaxItems = 125; TimedEffects.Clear(p);
    }
    private static void Crafting()
    {
        var p = Player(); var saved = new List<Saved>();
        foreach (var r in AlchemyRestoratives.Recipes.Where(r => r.Kind != RestorationKind.CombatBuff))
        {
            Empty(p); var mortar = Put(p, new MortarPestle(100)); Inputs(p, r, 3);
            p.Skills.Alchemy.Base = r.Skill - .1; Require(AlchemyRestoratives.Quote(p, mortar, r) == null, "Under-skilled mixture allowed"); p.Skills.Alchemy.Base = 120;
            var q = AlchemyRestoratives.Quote(p, mortar, r); Require(q != null, "Mixture quote missing");
            var ticket = Ticket(q); Require(CraftTransactions.Complete(ticket, new Roll(0)) && !CraftTransactions.Complete(ticket, new Roll(0)), "Mixture failed/replayed");
            var output = p.Backpack.Items.OfType<RestorationPotion>().Single(i => i.Kind == r.Kind && i.Tier == r.Tier);
            Require(output.Amount == 1 && output.Origin == ItemOrigin.Crafted && output.PlayerConstructed && mortar.UsesRemaining == 99
                && p.Backpack.GetAmount(typeof(Bottle)) == 2, "Output/bottle/tool count wrong");
            foreach (var kind in new[] { RestorationKind.Healing, RestorationKind.Mana })
            { int need = kind == RestorationKind.Healing ? r.Healing : r.Mana; Require(p.Backpack.Items.OfType<RestorationPotion>().Where(i => i.Kind == kind).Sum(i => i.Amount) == need * 2, "Typed potion ingredients wrong"); }
            output.Stackable = false; saved.Add(new(output.Serial.Value, output.Kind, output.Tier, output.Amount, output.Origin, p.Backpack.Serial.Value));
            output.MoveToWorld(p.Location, p.Map);
            // Move saved outputs into a separate owner's backpack before the next fixture reset.
            var holder = Player(); holder.Backpack.DropItem(output);
            saved[^1] = saved[^1] with { Parent = holder.Backpack.Serial.Value };
            p.Skills.Alchemy.Base = r.Skill + 10;
            q = AlchemyRestoratives.Quote(p, mortar, r); Require(q != null && CraftTransactions.Complete(Ticket(q), new Roll(.999)), "Valid failed attempt rejected");
            Require(p.Backpack.GetAmount(typeof(Bottle)) == 1 && mortar.UsesRemaining == 98, "Failure did not lose bottle/tool");
            foreach (var kind in new[] { RestorationKind.Healing, RestorationKind.Mana })
            { int need = kind == RestorationKind.Healing ? r.Healing : r.Mana; Require(p.Backpack.Items.OfType<RestorationPotion>().Where(i => i.Kind == kind).Sum(i => i.Amount) == need * 2 - (need + 1) / 2, "Failure half ingredients wrong"); }
            p.Skills.Alchemy.Base = 120;
        }
        File.WriteAllText(Manifest, JsonSerializer.Serialize(saved));
        Empty(p); var tool = Put(p, new MortarPestle(100)); var row = AlchemyRestoratives.Recipes[0]; Inputs(p, row, 3);
        AlchemyRequest Quote() => AlchemyRestoratives.Quote(p, tool, row);
        var stale = Quote(); var changed = p.Backpack.Items.OfType<RestorationPotion>().First(); changed.Amount++;
        Require(!CraftTransactions.Complete(Ticket(stale), new Roll(0)) && tool.UsesRemaining == 100, "Changed input consumed");
        stale = Quote(); AlchemyRestoratives.Invalidate(p); Require(!CraftTransactions.Complete(Ticket(stale), new Roll(0)), "Cancelled request replayed");
        stale = Quote(); ClassProfessions.Profiles.Select(p, SkillName.Tinkering);
        Require(!CraftTransactions.Complete(Ticket(stale), new Roll(0)), "Craft switch completed old mixture"); ClassProfessions.Profiles.Select(p, SkillName.Alchemy);
        stale = Quote(); p.MoveToWorld(new Point3D(p.X + 30, p.Y, p.Z), p.Map);
        Require(!CraftTransactions.Complete(Ticket(stale), new Roll(0)), "Remote mixture completed"); p.MoveToWorld(new Point3D(5550, 1151, 0), Map.Trammel);
        stale = Quote(); p.Backpack.MaxItems = 1;
        Require(!CraftTransactions.Complete(Ticket(stale), new Roll(0)) && tool.UsesRemaining == 100, "Full pack mixture consumed inputs"); p.Backpack.MaxItems = 125;
        var forged = new CraftItem(typeof(RestorationPotion), "Forged", "Forged"); forged.AddSkill(SkillName.Alchemy, 0, 50);
        Require(!CraftTransactions.Complete(new(p, DefAlchemy.CraftSystem, forged, null, tool, 0), new Roll(0)), "Unbound native mixture bypassed quote");
        var state = new CraftToolState(CraftQuality.Superior, true, 100); tool.Delete(); tool = Put(p, ItemOrigins.Create(ItemOrigin.Crafted, () => new MortarPestle(100))); CraftTransactions.Profiles.Store(tool, state);
        stale = Quote(); Require(CraftTransactions.Complete(Ticket(stale), new Roll(0, 0)) && tool.UsesRemaining == 100, "Crafted mortar did not save use");
    }
    private static void Drops()
    {
        var p = Player();
        foreach (var d in Enum.GetValues<MonsterDifficulty>())
        {
            foreach (var kind in new[] { 'h', 'm' })
            {
                for (int tier = 1; tier <= 5; tier++)
                {
                    var item = AlchemyRestoratives.Drop($"{kind}p{tier}"); Require(item != null && item.Tier == tier && item.Origin == ItemOrigin.Drop, "Mapped restorative leaf failed"); item.Delete();
                }
            }
            for (int seed = 0; seed < 500; seed++)
            {
                var victim = new Skeleton(); MonsterVariants.Assign(victim, d); var corpse = GearDropChecks.Body(victim, p);
                string leaf = CreatureGold.Select(typeof(Skeleton), new Random(seed), d);
                GearDrops.Award(new(victim, corpse, p, CreatureProfiles.Level(victim), 0, seed % 2 * 500, d), new Random(seed));
                var potions = corpse.Items.OfType<RestorationPotion>().ToArray();
                Require(potions.Length == (leaf is { Length: 3 } && leaf[1] == 'p' && leaf[0] is 'h' or 'm' ? 1 : 0), "Treasure branch was rerolled or potion replaced by MF");
                corpse.Delete(); victim.Delete();
            }
        }
        Require(AlchemyRestoratives.Drop("rvs") == null && AlchemyRestoratives.Drop("rvl") == null && AlchemyRestoratives.Drop("hp6") == null, "Craft-only rejuvenation entered loot");
    }
    private static void Vendors()
    {
        var p = Player(); var vendor = TownMarkets.Profiles.At("Felucca/Britain/Alchemist"); p.MoveToWorld(vendor.Location, vendor.Map); Put(p, new Gold(60000));
        var market = TownMarkets.Market(vendor); var offers = market.Offers.Where(o => World.FindItem(o.Item) is RestorationPotion).ToArray();
        Require(offers.Length == 8 && offers.All(o => World.FindItem(o.Item) is RestorationPotion { Kind: RestorationKind.Healing or RestorationKind.Mana, Tier: <= 4 }), "Alchemist tiers or craft-only supply wrong");
        foreach (var offer in offers)
        {
            int before = p.Backpack.GetAmount(typeof(Gold));
            Require(vendor.OnBuyItems(p, [new BuyItemResponse(offer.Item, 2)]) && p.Backpack.GetAmount(typeof(Gold)) == before - 2 * offer.Price,
                "Unlimited potion purchase failed");
            Require(vendor.OnBuyItems(p, [new BuyItemResponse(offer.Item, 1)]), "Unlimited offer was depleted");
        }
        var potion = p.Backpack.Items.OfType<RestorationPotion>().First(); Require(potion.Origin == ItemOrigin.Vendor, "Bought potion origin wrong");
        p.Hits = 1; p.Mana = 0; Require(PotionRestoration.Drink(p, potion, new Roll()), "Bought potion unusable");
    }
    private static void Reload()
    {
        foreach (var s in JsonSerializer.Deserialize<Saved[]>(File.ReadAllText(Manifest)))
        {
            var item = World.FindItem((Serial)s.Item) as RestorationPotion;
            Require(item != null && item.Kind == s.Kind && item.Tier == s.Tier && item.Amount == s.Amount && item.Origin == s.Origin
                && (item.Parent as Item)?.Serial.Value == s.Parent && item.PlayerConstructed, "Crafted mixture changed on reload");
        }
    }
}
