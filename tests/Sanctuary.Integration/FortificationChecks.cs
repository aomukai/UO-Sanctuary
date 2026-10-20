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

public static class FortificationChecks
{
    private static readonly ILogger _logger = LogFactory.GetLogger(typeof(FortificationChecks));
    private static string Filepath => Path.Join(Core.BaseDirectory, "fortification-checks.json");
    private sealed record Saved(uint Item, int Current, int Maximum, int Ceiling, string State);
    private sealed record Manifest(Saved[] Items, uint Chitin, int Amount, uint Powder, int Powders, uint Corpse);
    private static Manifest _manifest;
    public static void Configure()
    {
        Register("fortification-check", Check, true); Register("fortification-reload-check", Reload);
    }
    private static void Register(string name, Action action, bool timed = false) => ConsoleInputHandler.RegisterCommand(name,
        "Chitin, Alchemy powder, durability ceilings, transactions and persistence", _ => Core.LoopContext.Post(() =>
        {
            try { action(); if (!timed) { _logger.Information("{Command}: passed", name); } }
            catch (Exception error) { _logger.Error(error, "{Command}: failed", name); }
        }));
    private static void Require(bool value, string why) => GearDropChecks.Require(value, why);
    private static T Put<T>(PlayerMobile p, T item) where T : Item { p.Backpack.DropItem(item); return item; }
    private static PlayerMobile Player() => AlchemyChecks.Player();
    private static CraftTransactions.Request Ticket(FortificationCraftRequest q) => new(q.Owner, DefAlchemy.CraftSystem, q.Recipe, null, q.Tool, 0) { Fortification = q };
    private sealed class Roll(int ticket = 0, double chance = 0, int save = 99) : Random
    { public override int Next(int max) => max == 10000 ? ticket : Math.Min(save, max - 1); public override double NextDouble() => chance; }
    private static void Check()
    {
        Drops(); FullRepairChain(); var p = Player(); var saved = new List<Saved>(); Durability(p, saved); Crafting(p);
        var chitin = Put(p, ItemOrigins.Create(ItemOrigin.Drop, () => new Chitin(35)));
        var powder = Put(p, ItemOrigins.Create(ItemOrigin.Crafted, () => new FortifyingPowder(7) { PlayerConstructed = true }));
        var victim = new GiantSpider(); var body = GearDropChecks.Body(victim, p);
        RewardLife.Process(victim, body, new Roll());
        Require(body.SanctuaryNoRewards && body.Items.OfType<Chitin>().Sum(c => c.Amount) == 1, "Kill omitted chitin"); victim.Delete();
        _manifest = new(saved.ToArray(), chitin.Serial.Value, chitin.Amount, powder.Serial.Value, powder.Amount, body.Serial.Value);
        File.WriteAllText(Filepath, JsonSerializer.Serialize(_manifest));
        TimedCraft();
    }
    private static void Drops()
    {
        var p = Player();
        foreach (string name in Fortification.Settings.Species)
        {
            var type = typeof(BaseCreature).Assembly.GetType(name);
            Require(Fortification.Species(type), "Declared chitin species missing");
            foreach (var difficulty in Enum.GetValues<MonsterDifficulty>())
            {
                var victim = type.CreateInstance<BaseCreature>();
                if (difficulty != MonsterDifficulty.Normal && MonsterVariants.Supported(type, difficulty)) { MonsterVariants.Assign(victim, difficulty); }
                var corpse = GearDropChecks.Body(victim, p);
                foreach (int ticket in name == Fortification.Settings.Species[0] && difficulty == MonsterDifficulty.Normal
                    ? Enumerable.Range(0, 10000) : new[] { 0, 999, 1000, 9999 })
                {
                    var life = new RewardLife(victim, corpse, p, CreatureProfiles.Level(victim), 0, ticket % 2 * 5000, difficulty);
                    int count = corpse.Items.OfType<Chitin>().Sum(c => c.Amount);
                    Fortification.Award(life, new Roll(ticket));
                    Require(corpse.Items.OfType<Chitin>().Sum(c => c.Amount) == count + (ticket < 1000 ? 1 : 0), "Chitin chance, quantity or MF changed");
                    foreach (var item in corpse.Items.ToArray()) { item.Delete(); }
                }
                corpse.Delete(); victim.Delete();
            }
        }
        foreach (var victim in new BaseCreature[] { new Skeleton(), new GiantSpider { Controlled = true }, new GiantSpider { Summoned = true }, new GiantSpider { NoKillAwards = true } })
        {
            var body = GearDropChecks.Body(victim, p); RewardLife.Process(victim, body, new Roll());
            Require(!body.Items.OfType<Chitin>().Any() && body.SanctuaryNoRewards, "Excluded life dropped chitin"); body.Delete(); victim.Delete();
        }
        var spider = new GiantBlackWidow(); var corpseOnce = GearDropChecks.Body(spider, p);
        RewardLife.Process(spider, corpseOnce, new Roll()); int amount = corpseOnce.Items.OfType<Chitin>().Sum(c => c.Amount);
        RewardLife.Process(spider, corpseOnce, new Roll());
        Require(amount == 1 && corpseOnce.Items.OfType<Chitin>().Sum(c => c.Amount) == 1, "Chitin life paid twice");
        corpseOnce.Delete(); spider.Delete(); p.Delete();
    }
    private static void Durability(PlayerMobile p, List<Saved> saved)
    {
        foreach (var type in new[] { typeof(Dagger), typeof(PlateChest), typeof(WoodenShield), typeof(LeatherGloves), typeof(LongPants), typeof(GoldRing) })
        {
            var item = Put(p, type.CreateInstance<Item>()); var powder = Put(p, new FortifyingPowder(100));
            int ceiling = Fortification.Ceiling(item);
            if (ClassGear.CosmeticClothing(item) || ceiling == 0)
            { Require(Fortification.Quote(p, powder, item) == null, "Naturally non-wearing/cosmetic item acquired durability"); item.Delete(); powder.Delete(); continue; }
            Set(item, 0, Math.Max(1, ceiling - 11)); var state = ClassGear.State(item);
            var q = Fortification.Quote(p, powder, item); Require(q != null && q.Added == 5 && Fortification.Commit(q), "Broken item could not be fortified");
            var native = GearDetails.Read(item);
            Require(native.Durability == 0 && native.MaximumDurability == ceiling - 6 && powder.Amount == 99
                && ReferenceEquals(state, ClassGear.State(item)) && !Fortification.Commit(q), "Fortification healed, rerolled gear or replayed");
            while ((q = Fortification.Quote(p, powder, item)) != null) { Require(Fortification.Commit(q), "Repeated fortification failed"); }
            Require(GearDetails.Read(item).MaximumDurability == ceiling && GearDetails.Read(item).Durability == 0 && powder.Amount == 97,
                "Ceiling/final partial dose wrong");
            saved.Add(new(item.Serial.Value, 0, ceiling, ceiling, JsonSerializer.Serialize(ClassGear.State(item)))); powder.Delete();
        }
        var dagger = Put(p, new Dagger()); var dose = Put(p, new FortifyingPowder(10)); int cap = Fortification.Ceiling(dagger);
        dagger.MaxHitPoints = cap - 10; dagger.HitPoints = 3;
        var stale = Fortification.Quote(p, dose, dagger); dagger.HitPoints++;
        Require(!Fortification.Commit(stale) && dose.Amount == 10, "Changed wear state consumed powder");
        stale = Fortification.Quote(p, dose, dagger); dagger.MoveToWorld(p.Location, p.Map);
        Require(!Fortification.Commit(stale) && dose.Amount == 10, "Ground item consumed powder"); p.Backpack.DropItem(dagger);
        stale = Fortification.Quote(p, dose, dagger); dose.MoveToWorld(p.Location, p.Map);
        Require(!Fortification.Commit(stale), "Ground powder consumed"); p.Backpack.DropItem(dose);
        var legacy = Put(p, new PowderOfTemperament(10)); legacy.OnDoubleClick(p);
        Require(p.Target != null, "Legacy native powder did not route through custom targeting"); p.Target.Cancel(p, Server.Targeting.TargetCancelType.Canceled);
        var use = Fortification.Quote(p, legacy, dagger); Require(use != null && Fortification.Commit(use)
            && legacy.UsesRemaining == 9 && dagger.HitPoints == 4 && dagger.MaxHitPoints == cap - 5, "Native powder bypassed five-max/no-heal rule");
        var profile = ClassGear.Profiles.Ensure(dagger); ClassGear.Profiles.Store(dagger, profile with { Identified = false });
        Require(Fortification.Quote(p, dose, dagger) == null, "Unidentified gear accepted"); ClassGear.Profiles.Store(dagger, profile);
        var crafted = Put(p, new Dagger()); var recipe = EquipmentCrafting.Recipes.First(r => r.Type == "Dagger" && r.Tier == GearTier.Normal);
        p.Skills.Blacksmith.Base = 100; EquipmentCrafting.Finish(crafted, p, recipe, CraftResource.Iron, 0, CraftQuality.Superior, new Random(7));
        Require(Fortification.Ceiling(crafted) == (int)Math.Ceiling(crafted.InitMaxHits * (100 + ClassGear.State(crafted).DurabilityPercent) / 100.0), "Crafted durability bonus omitted from ceiling");
        // Persist the saved equipment under an independent owner before the Alchemy fixture continues.
        var holder = Player(); foreach (var row in saved) { holder.Backpack.DropItem(World.FindItem((Serial)row.Item)); }
    }
    private static void Set(Item item, int current, int maximum)
    {
        switch (item) { case BaseWeapon w: w.MaxHitPoints = maximum; w.HitPoints = current; break;
            case BaseArmor a: a.MaxHitPoints = maximum; a.HitPoints = current; break;
            case BaseClothing c: c.MaxHitPoints = maximum; c.HitPoints = current; break; }
    }
    private static void Crafting(PlayerMobile unused)
    {
        var p = Player(); var tool = Put(p, new MortarPestle(100)); var chitin = Put(p, new Chitin(100));
        p.Skills.Alchemy.Base = 79.9; Require(FortificationCrafting.Quote(p, tool) == null, "Under-skilled powder allowed");
        p.Skills.Alchemy.Base = 90; var q = FortificationCrafting.Quote(p, tool);
        Require(q != null && q.Chance > 0 && CraftTransactions.Complete(Ticket(q), new Roll()), "Valid powder craft rejected");
        Require(chitin.Amount == 90 && tool.UsesRemaining == 99 && p.Backpack.Items.OfType<FortifyingPowder>().Sum(c => c.Amount) == 1
            && !CraftTransactions.Complete(Ticket(q), new Roll()), "Powder craft counts/replay wrong");
        q = FortificationCrafting.Quote(p, tool); Require(CraftTransactions.Complete(Ticket(q), new Roll(chance: .999))
            && chitin.Amount == 85 && tool.UsesRemaining == 98, "Powder failure losses wrong");
        q = FortificationCrafting.Quote(p, tool); chitin.Amount++;
        Require(!CraftTransactions.Complete(Ticket(q), new Roll()) && tool.UsesRemaining == 98, "Changed chitin accepted");
        q = FortificationCrafting.Quote(p, tool); p.MoveToWorld(new Point3D(p.X + 100, p.Y, p.Z), p.Map);
        Require(!CraftTransactions.Complete(Ticket(q), new Roll()), "Remote powder craft accepted");
        FixtureSpace.Place(p, new Point3D(5550, 1151, 0), Map.Trammel);
        q = FortificationCrafting.Quote(p, tool); ClassProfessions.Profiles.Select(p, SkillName.Tinkering);
        Require(!CraftTransactions.Complete(Ticket(q), new Roll()), "Craft switch accepted stale powder"); ClassProfessions.Profiles.Select(p, SkillName.Alchemy);
        foreach (var powder in p.Backpack.Items.OfType<FortifyingPowder>().ToArray()) { powder.Delete(); }
        q = FortificationCrafting.Quote(p, tool); p.Backpack.MaxItems = 1;
        Require(!CraftTransactions.Complete(Ticket(q), new Roll()) && chitin.Amount == 86 && tool.UsesRemaining == 98,
            "Undeliverable powder consumed ingredients or a tool use"); p.Backpack.MaxItems = 125;
        tool.Delete(); tool = Put(p, ItemOrigins.Create(ItemOrigin.Crafted, () => new MortarPestle(100)));
        CraftTransactions.Profiles.Store(tool, new(CraftQuality.Superior, true, 100));
        q = FortificationCrafting.Quote(p, tool);
        Require(CraftTransactions.Complete(Ticket(q), new Roll(save: 0)) && chitin.Amount == 76 && tool.UsesRemaining == 100,
            "Crafted mortar did not save its use");
        var forged = new CraftItem(typeof(FortifyingPowder), "forged", "forged"); forged.AddSkill(SkillName.Alchemy, 0, 50);
        Require(!CraftTransactions.Complete(new(p, DefAlchemy.CraftSystem, forged, null, tool, 0), new Roll()), "Unbound native recipe bypassed powder quote");
        p.Delete();
    }
    private static void FullRepairChain()
    {
        var p = Player(); var tool = Put(p, new MortarPestle(100));
        for (int i = 0; i < 10; i++)
        {
            var victim = new GiantSpider(); var corpse = GearDropChecks.Body(victim, p);
            RewardLife.Process(victim, corpse, new Roll());
            foreach (var piece in corpse.Items.OfType<Chitin>().ToArray()) { p.Backpack.DropItem(piece); }
            corpse.Delete(); victim.Delete();
        }
        Require(p.Backpack.GetAmount(typeof(Chitin)) == 10, "Ten eligible kills did not supply the powder recipe");
        var recipe = FortificationCrafting.Quote(p, tool);
        Require(recipe != null && CraftTransactions.Complete(Ticket(recipe), new Roll())
            && p.Backpack.GetAmount(typeof(Chitin)) == 0, "Dropped chitin could not become powder");
        var dose = p.Backpack.Items.OfType<FortifyingPowder>().Single();
        var item = Put(p, new Dagger()); int cap = Fortification.Ceiling(item);
        Set(item, 0, cap - 5);
        var smith = TownMarkets.Profiles.At("Felucca/Britain/Smith"); p.MoveToWorld(smith.Location, smith.Map);
        var staleRepair = EquipmentRepairs.Quote(p, item, vendor: smith);
        Require(staleRepair != null, "Ordinary repair quote unavailable before fortifying");
        var fortify = Fortification.Quote(p, dose, item);
        Require(fortify != null && Fortification.Commit(fortify) && item.MaxHitPoints == cap && item.HitPoints == 0,
            "Crafted powder did not restore maximum without healing");
        Require(!EquipmentRepairs.Commit(staleRepair, out _, new Roll()), "Pre-fortification repair quote remained valid");
        var repair = EquipmentRepairs.Quote(p, item, vendor: smith);
        Require(repair != null && repair.Gold == 100, "Post-fortification ordinary repair unavailable");
        foreach (var cost in repair.Costs.Append(new RepairCost(typeof(Gold), repair.Gold)))
        {
            if (cost.Amount <= 0) { continue; }
            var resource = cost.Type.CreateInstance<Item>(); resource.Amount = cost.Amount; Put(p, resource);
        }
        Require(EquipmentRepairs.Commit(repair, out _, new Roll()) && item.HitPoints == cap && item.MaxHitPoints == cap
            && p.Backpack.GetAmount(typeof(Gold)) == 0 && !EquipmentRepairs.Commit(repair, out _, new Roll()),
            "Fortified item could not be repaired normally or paid twice");
        p.Delete();
    }
    private static void TimedCraft()
    {
        var p = Player(); var tool = Put(p, new MortarPestle(100)); var chitin = Put(p, new Chitin(10));
        var q = FortificationCrafting.Quote(p, tool); Require(q != null && FortificationCrafting.Start(q), "Native timed powder craft did not start");
        Timer.StartTimer(TimeSpan.FromSeconds(8), () =>
        {
            try
            {
                int powder = p.Backpack.Items.OfType<FortifyingPowder>().Sum(i => i.Amount);
                Require(q.Claimed && tool.UsesRemaining == 99 && (powder == 1 && chitin.Deleted || powder == 0 && chitin.Amount == 5), "Native timed craft did not consume exactly one correct outcome");
                p.Delete(); _logger.Information("fortification-check: passed");
            }
            catch (Exception e) { p.Delete(); _logger.Error(e, "fortification-check: failed"); }
        });
    }
    private static void Reload()
    {
        var m = JsonSerializer.Deserialize<Manifest>(File.ReadAllText(Filepath));
        foreach (var row in m.Items)
        {
            var item = World.FindItem((Serial)row.Item); var actual = GearDetails.Read(item);
            Require(actual.Durability == row.Current && actual.MaximumDurability == row.Maximum && Fortification.Ceiling(item) == row.Ceiling
                && JsonSerializer.Serialize(ClassGear.State(item)) == row.State, "Fortified gear/current/ceiling/affixes changed on reload");
        }
        Require(World.FindItem((Serial)m.Chitin) is Chitin { Origin: ItemOrigin.Drop } c && c.Amount == m.Amount
            && World.FindItem((Serial)m.Powder) is FortifyingPowder { Origin: ItemOrigin.Crafted } f && f.Amount == m.Powders
            && World.FindItem((Serial)m.Corpse) is Corpse { SanctuaryNoRewards: true }, "Resource/powder/paid-life identity changed on reload");
    }
}
