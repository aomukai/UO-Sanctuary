using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Server;
using Server.Accounting;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using NativeInsurance = Server.Engines.Insurance.Insurance;

namespace Sanctuary.Integration;

public static class InsuranceChecks
{
    private static readonly ILogger _log = LogFactory.GetLogger(typeof(InsuranceChecks));
    private static string Manifest => Path.Join(Core.BaseDirectory, "insurance-checks.json");
    private sealed record Saved(uint Player, uint Carried, uint Stored, uint Store, int Balance, string State);
    private static void Require(bool value, string why) => RuneChecks.Require(value, why);
    public static void Configure()
    { Register("insurance-check", Check); Register("insurance-reload-check", Reload); }
    private static void Register(string name, Action action) => ConsoleInputHandler.RegisterCommand(name,
        "Disposable insurance purchase/death/renewal/transfer/persistence checks", _ => Core.LoopContext.Post(() =>
        { try { action(); _log.Information("{Command}: passed", name); } catch (Exception e) { _log.Error(e, "{Command}: failed", name); } }));
    internal static PlayerMobile Player()
    {
        var p = new PlayerMobile { Player = true, Body = 0x190, Name = "InsuranceFixture" };
        var account = new Account("insurance-" + Guid.NewGuid().ToString("N"), Guid.NewGuid().ToString("N")); account[0] = p;
        ClassCreation.Profiles.Assign(p, DiabloClass.Necromancer);
        ClassCreation.Profiles.AdvanceLevel(p, 99); p.AddItem(new Backpack());
        p.MoveToWorld(CombatPractice.FindFloor(Map.Trammel, 1367, 1504), Map.Trammel);
        return p;
    }
    internal static Item Gear(PlayerMobile p, GearRarity rarity = GearRarity.Normal, GearTier tier = GearTier.Normal)
    {
        var item = ItemOrigins.Create(ItemOrigin.Drop, () => new Dagger { LootType = LootType.Regular });
        var code = EquipmentBalance.NormalBases.First(b => EquipmentCrafting.Identity(item.GetType(), tier, b.Code, ClassGear.Definition(item).BaseCode)).Code;
        var state = ClassGear.Profiles.FromMonster(item, 99, rarity, code, tier, new Random(43));
        ClassGear.Profiles.Store(item, state with { Identified = true }); p.Backpack.DropItem(item); return item;
    }
    private static void Insure(PlayerMobile p, params Item[] items) => Require(EquipmentInsurance.Apply(p,
        items.Select(EquipmentInsurance.Quote).ToArray(), items.Select(_ => true).ToArray()), "Could not insure fixture gear");
    private static void Check()
    {
        Require(NativeInsurance.Enabled, "Insurance is disabled");
        var pricing = Player();
        int[,] expected = { { 100, 200, 300 }, { 250, 500, 750 }, { 500, 1000, 1500 } };
        // Named definitions have their own factory and integration fixture.
        foreach (var rarity in new[] { GearRarity.Normal, GearRarity.Magic, GearRarity.Rare })
        {
            foreach (var tier in Enum.GetValues<GearTier>())
            {
                var item = Gear(pricing, rarity, tier);
                Require(EquipmentInsurance.Cost(item) == expected[(int)rarity, (int)tier]
                    && NativeInsurance.GetInsuranceCost(pricing, item) == expected[(int)rarity, (int)tier], "Rarity/base-tier insurance price differs");
                var state = ClassGear.State(item);
                ClassGear.Profiles.Store(item, state with { Identified = false });
                Require(!NativeInsurance.CanInsure(pricing, item), "Insurance leaked unidentified rarity through its premium");
                ClassGear.Profiles.Store(item, state);
                item.LootType = LootType.Blessed; Require(!NativeInsurance.CanInsure(pricing, item), "Already protected gear could be insured");
                item.Delete();
            }
        }
        foreach (var item in new Item[] { new Bag(), new Gold(7), new GreaterHealPotion(), new Arrow(5), new EarnedWardrobe(pricing) })
        { pricing.Backpack.DropItem(item); Require(!NativeInsurance.CanInsure(pricing, item), "Non-equipment could be insured"); item.Delete(); }

        var p = Player(); var white = Gear(p); var magic = Gear(p, GearRarity.Magic);
        var quotes = new[] { EquipmentInsurance.Quote(white), EquipmentInsurance.Quote(magic) };
        Require(Banker.Deposit(p, 349), "Bank funding failed");
        Require(!EquipmentInsurance.Apply(p, quotes, [true, true]) && Banker.GetBalance(p) == 349
            && !white.Insured && !magic.Insured, "Insufficient funds partially charged or insured gear");
        Require(Banker.Deposit(p, 1), "Bank funding failed");
        Require(EquipmentInsurance.Apply(p, quotes, [true, true]) && Banker.GetBalance(p) == 0, "Batch premium was not exact");
        Require(!EquipmentInsurance.Apply(p, quotes, [true, true]) && Banker.GetBalance(p) == 0, "Stale purchase was accepted");
        NativeInsurance.ToggleItemInsurance(p, white, false);
        Require(!white.Insured && white.PaidInsurance, "Cancellation lost unused credit");
        NativeInsurance.ToggleItemInsurance(p, white, false);
        Require(white.Insured && Banker.GetBalance(p) == 0, "Reactivation charged unused credit again");
        var receiver = Player(); receiver.Backpack.DropItem(white);
        Require(!white.Insured && !white.PaidInsurance && EquipmentInsurance.Profiles.Find(white.Serial) == null, "Ownership transfer carried a paid policy");
        p.Backpack.DropItem(white);
        Require(!white.Insured && !white.PaidInsurance, "Return to former owner restored transferred credit");
        var bag = new Bag(); p.Backpack.DropItem(bag); bag.DropItem(magic); receiver.Backpack.DropItem(bag);
        Require(!magic.Insured && !magic.PaidInsurance, "A transferred bag concealed a paid policy");

        var dying = Player(); var worn = Gear(dying); var nested = Gear(dying, GearRarity.Magic);
        var uninsured = Gear(dying); var inner = new Bag(); var outer = new Bag();
        inner.DropItem(nested); outer.DropItem(inner); dying.Backpack.DropItem(outer); dying.AddItem(worn);
        var carriedGold = new Gold(1001); dying.Backpack.DropItem(carriedGold);
        Require(Banker.Deposit(dying, 5000), "Death bank funding failed"); Insure(dying, worn, nested);
        Require(!dying.AutoRenewInsurance, "Automatic renewal did not default off");
        string stateBefore = JsonSerializer.Serialize(ClassGear.State(nested));
        var killer = Player(); Require(Banker.Deposit(killer, 1000), "Killer bank funding failed");
        dying.RegisterDamage(1, killer);
        dying.Kill(); var corpse = dying.Corpse as Corpse;
        Require(Banker.GetBalance(killer) == 1000, "A player killer received an insurance payout");
        Require(!dying.Alive && corpse != null && worn.Parent == dying.Backpack && nested.Parent == dying.Backpack
            && uninsured.IsChildOf(corpse) && Banker.GetBalance(dying) == 4650 && !worn.Insured && !nested.Insured
            && corpse.GetAmount(typeof(Gold)) == 801 && carriedGold.Amount == 801,
            "Death retention, nested protection, premium consumption or flat carried-gold penalty differs");
        Require(JsonSerializer.Serialize(ClassGear.State(nested)) == stateBefore && nested.Origin == ItemOrigin.Drop, "Insurance changed actual gear/roll/origin");
        NativeInsurance.CheckInsuranceOnDeath(dying); NativeInsurance.CheckInsuranceOnDeath(dying); dying.Kill();
        Require(Banker.GetBalance(dying) == 4650, "Death replay charged again");
        dying.Resurrect(); dying.Kill();
        Require(worn.IsChildOf(dying.Corpse) && nested.IsChildOf(dying.Corpse), "A spent policy protected a later death");

        var renewal = Player(); var renewing = Gear(renewal);
        Require(Banker.Deposit(renewal, 1500), "Renewal funding failed"); Insure(renewal, renewing); renewal.AutoRenewInsurance = true;
        renewal.Kill();
        Require(renewing.Parent == renewal.Backpack && renewing.Insured && renewing.PaidInsurance && Banker.GetBalance(renewal) == 1300,
            "Automatic renewal did not buy exactly the next death");
        Require(Banker.Withdraw(renewal, 1300), "Could not exhaust renewal funds"); renewal.Resurrect(); renewal.Kill();
        Require(renewing.Parent == renewal.Backpack && !renewing.Insured && Banker.GetBalance(renewal) == 0,
            "Failed renewal lost current protection or overdrew the bank");
        renewal.Resurrect(); renewal.Kill();
        Require(renewing.IsChildOf(renewal.Corpse), "Failed renewal still protected the following death");

        var beltWearer = Player(); var belt = new PotionPlatedBelt { LootType = LootType.Regular };
        ClassGear.Profiles.Convert(belt, 99, GearRarity.Normal, new Random(43));
        ClassGear.Profiles.Store(belt, ClassGear.State(belt) with { Identified = true }); beltWearer.AddItem(belt);
        var reserve = new GreaterHealPotion(); var beltStore = PotionBelts.Storage(beltWearer, true); beltStore.AddItem(reserve);
        Require(Banker.Deposit(beltWearer, 1000) && EquipmentInsurance.Cost(belt) > 0
            && EquipmentInsurance.Cost(reserve) == 0 && EquipmentInsurance.Cost(beltStore) == 0, "Belt equipment/reserve eligibility differs");
        Insure(beltWearer, belt); beltWearer.Kill();
        Require(belt.Parent == beltWearer.Backpack && reserve.IsChildOf(beltWearer.Corpse),
            "Insuring belt equipment also protected its potion reserve or lost the actual belt");

        var legacy = Player(); var oldPaid = Gear(legacy); oldPaid.Insured = oldPaid.PaidInsurance = true;
        EquipmentInsurance.AdoptCarried(legacy);
        Require(EquipmentInsurance.Profiles.Find(oldPaid.Serial) is { Paid: true, Active: true } && Banker.GetBalance(legacy) == 0,
            "A previously paid native policy was charged or lost during adoption");
        var copy = new Dagger(); Item.CopyProperties(oldPaid, copy); legacy.Backpack.DropItem(copy);
        Require(!copy.Insured && !copy.PaidInsurance, "Item copy inherited paid coverage");

        var saved = Player(); var carried = Gear(saved, GearRarity.Rare, GearTier.Elite); var stored = Gear(saved);
        Require(Banker.Deposit(saved, 10000), "Persistent fixture funding failed"); Insure(saved, carried, stored); saved.AutoRenewInsurance = true;
        var store = (WardrobeSet)Activator.CreateInstance(typeof(WardrobeSet), BindingFlags.NonPublic | BindingFlags.Instance,
            null, [saved, DiabloClass.Barbarian], null);
        store.DropItem(stored); int balance = Banker.GetBalance(saved);
        saved.Kill();
        Require(stored.Parent == store && stored.Insured && stored.PaidInsurance && Banker.GetBalance(saved) == balance - 1500,
            "Inactive wardrobe gear was consumed or charged by another class's death");
        saved.Resurrect();
        File.WriteAllText(Manifest, JsonSerializer.Serialize(new Saved(saved.Serial.Value, carried.Serial.Value, stored.Serial.Value,
            store.Serial.Value, Banker.GetBalance(saved), JsonSerializer.Serialize(ClassGear.State(carried)))));
    }
    private static void Reload()
    {
        var saved = JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));
        var p = World.FindMobile((Serial)saved.Player) as PlayerMobile;
        var carried = World.FindItem((Serial)saved.Carried); var stored = World.FindItem((Serial)saved.Stored);
        Require(p is { Alive: true, AutoRenewInsurance: true } && Banker.GetBalance(p) == saved.Balance
            && carried is { Insured: true, PaidInsurance: true } && carried.Parent == p.Backpack
            && stored is { Insured: true, PaidInsurance: true } && stored.Parent?.Serial == (Serial)saved.Store
            && EquipmentInsurance.Profiles.Find(carried.Serial) is { Active: true, Paid: true } policy && policy.Owner == p.Serial
            && JsonSerializer.Serialize(ClassGear.State(carried)) == saved.State, "Insurance identities, policies, credit, preference or actual gear changed on reload");
        EventSink.InvokeConnected(p); NativeInsurance.CheckInsuranceOnDeath(p);
        Require(!NativeInsurance.CheckItemInsuranceOnDeath(p, carried) && Banker.GetBalance(p) == saved.Balance,
            "Reload invented a pending death or charged existing coverage");
    }
}
