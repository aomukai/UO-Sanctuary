// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using Server.Network;

namespace Sanctuary.Integration;

public static class MercenaryCareChecks
{
    private static readonly ILogger _log = LogFactory.GetLogger(typeof(MercenaryCareChecks));
    private sealed record Saved(uint Owner, uint Mercenary, uint Gear, uint Nested, long Stock, DateTime Next, int Level);
    private static string Filename => Path.Join(Core.BaseDirectory, "mercenary-care-checks.json");
    public static void Configure()
    {
        Register("mercenary-care-check", Check);
        Register("mercenary-care-reload-check", Reload);
        Register("mercenary-care-loyalty-check", Loyalty);
        Register("mercenary-inn-check", RecallAfterLevelling);
        Register("mercenary-ghost-login-check", GhostLogin);
    }
    private static void Register(string name, Action action) => ConsoleInputHandler.RegisterCommand(name, "Disposable mercenary preparation checks", _ => Core.LoopContext.Post(() =>
    { try { action(); _log.Information("{Name}: passed", name); } catch (Exception e) { _log.Error(e, "{Name}: failed", name); } }));
    private static void Require(bool value, string message) { if (!value) { throw new InvalidOperationException(message); } }
    internal static BaseCreature Companion(PlayerMobile owner)
    {
        var merc = new Dog { Body = 0x190, Name = "Mercenary care fixture", CantWalk = true };
        merc.SetControlMaster(owner); merc.RawStr = merc.RawDex = 200; merc.SetHits(1000); merc.Hits = 1000;
        merc.MoveToWorld(CombatPractice.FindFloor(owner.Map, owner.X + 1, owner.Y), owner.Map); merc.AIObject.AITimer.Stop();
        if (merc.Backpack == null) { merc.AddItem(new Backpack()); } return merc;
    }

    private static void RecallAfterLevelling()
    {
        var owner = new PlayerMobile { Body = 0x190, Player = true, Name = "Inn roster fixture" };
        owner.AddItem(new Backpack());
        Wolfgang companion = null;
        try
        {
            var map = Map.Trammel;
            ClassCreation.Profiles.Assign(owner, DiabloClass.Necromancer);
            ClassCreation.Profiles.Discover(owner.Serial, StoryDiscoveries.IoloRetired);
            owner.MoveToWorld(new Point3D(1594, 1588, 20), map);
            companion = Wolfgang.Join(owner);
            Require(companion != null && companion.BlockChance == 50 && MercenaryCare.ParkAtInn(owner, companion),
                "Wolfgang should have 50% block at level one with his starter shield");

            owner.Backpack.DropItem(new Gold(AyilaInn.CallFee));
            Require(ClassCreation.Profiles.AdvanceLevel(owner, 12), "Could not level while Wolfgang was parked");
            Require(PersistentMinions.FindPosition(map, AyilaInn.Reunion, point => point.Z == AyilaInn.Reunion.Z,
                out var alongside), "No floor beside the reunion tile");
            Require(MercenaryCare.CallFromInn(owner, companion, map, AyilaInn.Reunion, alongside, AyilaInn.CallFee)
                && MercenaryCare.ActiveStoryCompanion(owner) == companion && MercenaryCare.CompanionLevel(companion) == 12
                && !MercenaryCare.ParkedCompanions(owner).Any() && owner.Location == AyilaInn.Reunion
                && owner.Backpack.GetAmount(typeof(Gold)) == 0, "Recall at a higher level lost the active contract or fee");

            var stats = CompanionCharacterStats.Snapshot(companion, companion, 12);
            Require(companion.RawDex == 20 && companion.DefenseBonus == 105 && stats[(int)CompanionStatField.BlockChance] == 56
                && ClassStats.CheckHit(owner, companion, 0, () => 0.55, guaranteed: true, notifyMelee: false) == false
                && ClassStats.CheckHit(owner, companion, 0, () => 0.56, guaranteed: true, notifyMelee: false) == true,
                "Wolfgang's passive Holy Shield and live block chance should scale with his level");
            var shield = companion.Items.OfType<WoodenShield>().Single();
            owner.Backpack.DropItem(shield);
            Require(companion.BlockChance == 0 && companion.DefenseBonus == 50
                && ClassStats.CheckHit(owner, companion, 0, () => 0.49, guaranteed: true, notifyMelee: false) == true,
                "Wolfgang kept Holy Shield without an equipped shield");
            Require(companion.EquipItem(new MetalShield()) && companion.BlockChance == 63,
                "A better shield should increase Wolfgang's block chance");
        }
        finally
        {
            if (companion != null)
            {
                MercenaryCare.States.Remove(companion.Serial);
                companion.Delete();
            }
            owner.Delete();
        }
    }

    private static void CheckAvoidance(PlayerMobile owner, Merydwen companion, int[] stats)
    {
        Require(stats[(int)CompanionStatField.DodgeChance] == 18 && stats[(int)CompanionStatField.AvoidChance] == 24
            && stats[(int)CompanionStatField.EvadeChance] == 18, "The companion sheet did not show the Amazon passives");
        var restoreRoll = AmazonAvoidance.Roll;
        long restoreMove = companion.LastMoveTime;
        try
        {
            companion.LastMoveTime = 0;
            AmazonAvoidance.Roll = _ => 20;
            Require(ClassStats.CheckHit(owner, companion, 0, guaranteed: true, notifyMelee: false) == true
                && ClassStats.CheckHit(owner, companion, 0, projectile: true, guaranteed: true) == false
                && ClassAvoidance.Missile(companion), "Standing companions should use Dodge for melee and Avoid for missiles");
            companion.LastMoveTime = Core.TickCount;
            Require(!ClassAvoidance.Missile(companion), "Moving companions should use Evade in place of Avoid");
            AmazonAvoidance.Roll = _ => 17;
            Require(ClassAvoidance.Missile(companion), "Evade should avoid a missile below its roll threshold");
        }
        finally
        {
            AmazonAvoidance.Roll = restoreRoll;
            companion.LastMoveTime = restoreMove;
        }
    }

    private static void GhostLogin()
    {
        var owner = new PlayerMobile { Body = 0x190, Player = true, Name = "Ghost login fixture" };
        owner.AddItem(new Backpack());
        Merydwen companion = null;
        BaseCreature ordinaryPet = null;
        try
        {
            ClassCreation.Profiles.Assign(owner, DiabloClass.Necromancer);
            owner.MoveToWorld(new Point3D(1613, 1588, 0), Map.Trammel);
            owner.Backpack.DropItem(new Gold(5000));
            companion = Merydwen.Hire(owner, out _);
            Require(companion != null, "Could not hire Merydwen");
            var bow = companion.Weapon;
            var bone = new BoneChest();
            owner.Backpack.DropItem(bone);
            Require(!companion.EquipItem(bone) && bone.Parent == owner.Backpack
                && CompanionBuild.ArcherGearRefusal(bone, "bows") == "I can only wear light armour.",
                "Bone armour bypassed Merydwen's light-armour rule or lost the reason for refusal");
            var build = (IClassCompanion)companion;
            var before = CompanionCharacterStats.Snapshot(companion, build, 1);
            var armour = companion.Items.OfType<LeatherChest>().Single();
            owner.Backpack.DropItem(armour);
            var after = CompanionCharacterStats.Snapshot(companion, build, 1);
            Require(build.EquipmentStyle == CompanionEquipmentStyle.Bow && before.Length == CompanionStatProtocol.Values
                && before[(int)CompanionStatField.DamageMax] > 0 && before[(int)CompanionStatField.Defense] > after[(int)CompanionStatField.Defense]
                && after[(int)CompanionStatField.Defense] == ClassStats.Defense(companion)
                && companion.EquipItem(armour), "Companion sheet did not reflect the armour and combat stats");
            CheckAvoidance(owner, companion, before);
            var lantern = new BeltLantern();
            owner.Backpack.DropItem(lantern);
            Require(companion.AllowEquipFrom(owner) && companion.EquipItem(lantern)
                && companion.FindItemOnLayer(Layer.Talisman) == lantern && lantern.Burning && companion.Weapon == bow,
                "Merydwen could not light a belt lantern while keeping her bow equipped");
            ordinaryPet = Companion(owner);
            owner.Kill();
            Require(!owner.Alive && !companion.IsDeadPet, "The companion should outlive the Avatar");
            owner.AutoStablePets();
            Require(owner.AutoStabled.Contains(companion) && owner.AutoStabled.Contains(ordinaryPet),
                "Logout did not keep both followers in temporary storage");

            owner.ClaimAutoStabledPets();
            Require(companion.ControlMaster == owner && companion.Map == owner.Map && companion.InRange(owner, 4)
                && !companion.IsStabled && !companion.IsDeadPet && companion.Weapon == bow
                && companion.FindItemOnLayer(Layer.Talisman) == lantern && lantern.Burning
                && companion.ControlOrder == OrderType.Guard && !owner.AutoStabled.Contains(companion)
                && owner.AutoStabled.Contains(ordinaryPet) && ordinaryPet.Map == Map.Internal,
                "Ghost login did not restore the living story companion independently of ordinary pets");

            companion.Kill();
            owner.AutoStablePets();
            owner.ClaimAutoStabledPets();
            Require(companion.IsDeadPet && companion.Weapon == bow && companion.ControlMaster == owner
                && companion.Map == owner.Map && companion.ControlOrder == OrderType.Follow,
                "Ghost login did not preserve the companion's death state");
        }
        finally
        {
            if (companion != null)
            {
                MercenaryCare.States.Remove(companion.Serial);
                companion.Delete();
            }
            ordinaryPet?.Delete();
            owner.Delete();
        }
    }

    private static void Check()
    {
        var owner = ImbuingChecks.Player(SkillName.Blacksmith); var other = ImbuingChecks.Player(SkillName.Alchemy);
        var merc = Companion(owner); Require(MercenaryCare.Register(owner, merc, 12), "Registration failed");
        var sword = new Longsword(); merc.Backpack.DropItem(sword); Require(merc.EquipItem(sword), "Cannot equip retained fixture gear");
        var bag = new Bag(); merc.Backpack.DropItem(bag); var ring = new GoldRing(); bag.DropItem(ring);
        var mana = new RestorationPotion(RestorationKind.Mana, 1); owner.Backpack.DropItem(mana);
        Require(!MercenaryCare.Deposit(owner, merc, mana) && !mana.Deleted, "Mana entered healing-only reserve"); mana.Delete();
        var health = new RestorationPotion(RestorationKind.Healing, 1) { Amount = 10 }; owner.Backpack.DropItem(health);
        Require(!MercenaryCare.Deposit(other, merc, health) && health.Amount == 10, "Foreign owner deposited potions");
        Require(MercenaryCare.Deposit(owner, merc, health) && health.Deleted && MercenaryCare.Stock(merc) == 10, "Deposit failed");
        var now = TimedEffects.Now; merc.Hits = 501;
        Require(!MercenaryCare.TryDrink(merc, now), "Drank above 50 percent"); merc.Hits = 500;
        Require(MercenaryCare.TryDrink(merc, now) && MercenaryCare.Stock(merc) == 9 && merc.Hits == 500, "Dose was instant or not consumed at 50 percent");
        Require(!MercenaryCare.TryDrink(merc, now.AddMilliseconds(9999)), "Drank before ten seconds");
        TimedEffects.Settle(merc, now.AddMilliseconds(3840)); Require(merc.Hits == 515, "Mercenary potion did not tick like its healing tier");
        TimedEffects.Clear(merc); merc.Hits = 400;
        Require(MercenaryCare.TryDrink(merc, now.AddSeconds(10)), "Ten-second boundary remained blocked");
        TimedEffects.Clear(merc);
        for (int i = 0; i < 7; i++) { merc.Hits = 400; Require(MercenaryCare.TryDrink(merc, now.AddSeconds(20 + i * 10)), "Could not consume warning thresholds"); TimedEffects.Clear(merc); }
        Require(MercenaryCare.Stock(merc) == 1, "Warning threshold fixture count changed");
        for (int i = 0; i < 20; i++)
        {
            health = new RestorationPotion(RestorationKind.Healing, 5) { Amount = 60000 }; owner.Backpack.DropItem(health);
            Require(MercenaryCare.Deposit(owner, merc, health), "Reserve imposed a carry-capacity limit");
        }
        Require(MercenaryCare.Stock(merc) == 1200001, "Million-dose stock overflowed");
        var healer = new WanderingHealer { CantWalk = true }; healer.MoveToWorld(CombatPractice.FindFloor(owner.Map, owner.X, owner.Y + 1), owner.Map);
        healer.AIObject.AITimer.Stop(); var quote = MercenaryCare.Quote(owner, merc, healer);
        Require(quote is { Gold: 0, Resurrection: false } && MercenaryCare.Commit(quote) && merc.Hits == merc.HitsMax
            && !MercenaryCare.Commit(quote), "Living healing/once-only guard failed");
        var townHealer = new Healer(); var guildHealer = new HealerGuildmaster();
        Require(MercenaryCare.Provider(townHealer) && MercenaryCare.Provider(guildHealer), "Town healer providers missing"); townHealer.Delete(); guildHealer.Delete();
        var pet = Companion(owner); var foreign = Companion(other); var deadPet = Companion(owner); var farPet = Companion(owner);
        deadPet.IsBonded = true; deadPet.Kill(); farPet.MoveToWorld(new Point3D(owner.X + 30, owner.Y, owner.Z), owner.Map);
        var merchant = new Blacksmith { CantWalk = true }; merchant.MoveToWorld(CombatPractice.FindFloor(owner.Map, owner.X, owner.Y + 1), owner.Map); merchant.AIObject.AITimer.Stop();
        merc.Hits = pet.Hits = foreign.Hits = farPet.Hits = 200;
        merchant.VendorBuy(owner);
        Require(merc.Hits == merc.HitsMax && pet.Hits == pet.HitsMax && foreign.Hits == 200 && farPet.Hits == 200 && deadPet.IsDeadPet,
            "Merchant trade missed owned living companions or healed foreign/distant/dead pets");
        pet.Hits = 200; healer.OnSpeech(new SpeechEventArgs(owner, "hello", MessageType.Regular, 0, []));
        Require(pet.Hits == pet.HitsMax, "Healer conversation did not heal a living pet");
        pet.Hits = 200; pet.AggressiveAction(foreign, false); MercenaryCare.HealFollowers(owner, healer);
        Require(pet.Hits == 200, "Town healing healed a fighting companion"); pet.Combatant = null;
        pet.Delete(); foreign.Delete(); deadPet.Delete(); farPet.Delete(); merchant.Delete();
        merc.ControlOrder = OrderType.Release; Require(!MercenaryCare.States[merc.Serial].Retired && merc.ControlMaster == owner, "System release destroyed a mercenary contract");
        merc.Kill(); Require(!merc.Deleted && merc.IsDeadPet && sword.IsChildOf(merc.Backpack) && ring.IsChildOf(merc.Backpack), "Death lost real equipment/inventory");
        merc.ResurrectPet(); Require(merc.IsDeadPet, "Native veterinary resurrection bypassed the mercenary fee");
        quote = MercenaryCare.Quote(owner, merc, healer);
        Require(quote is { Gold: 1200, Resurrection: true } && !MercenaryCare.Commit(quote) && merc.IsDeadPet, "Insufficient funds resurrected");
        owner.BankBox.DropItem(new Gold(1300)); quote = MercenaryCare.Quote(owner, merc, healer);
        var point = healer.Location; healer.MoveToWorld(new Point3D(point.X + 20, point.Y, point.Z), healer.Map);
        Require(!MercenaryCare.Commit(quote) && owner.BankBox.GetAmount(typeof(Gold)) == 1300, "Remote healer charged");
        healer.MoveToWorld(point, owner.Map); quote = MercenaryCare.Quote(owner, merc, healer);
        Require(MercenaryCare.Commit(quote) && !merc.IsDeadPet && owner.BankBox.GetAmount(typeof(Gold)) == 100
            && sword.IsChildOf(merc.Backpack) && ring.IsChildOf(merc.Backpack) && MercenaryCare.Stock(merc) == 1200001
            && !MercenaryCare.Commit(quote), "Paid resurrection lost gear/stock or charged twice");
        merc.IssueOrder(OrderType.Release, owner);
        Require(MercenaryCare.States[merc.Serial].Retired && MercenaryCare.Stock(merc) == 0 && sword.IsChildOf(merc.Backpack) && merc.ControlMaster == null
            && ring.IsChildOf(merc.Backpack) && !MercenaryCare.RecoverRetiredGear(other, merc, sword), "Retirement lost gear or leaked ownership");
        var fillers = new System.Collections.Generic.List<Item>();
        while (owner.Backpack.TotalItems <= owner.Backpack.MaxItems) { var filler = new Bag(); owner.Backpack.DropItem(filler); fillers.Add(filler); }
        Require(!MercenaryCare.RecoverRetiredGear(owner, merc, sword) && sword.IsChildOf(merc.Backpack), "Full pack lost retired gear");
        foreach (var filler in fillers) { filler.Delete(); }
        Require(MercenaryCare.RecoverRetiredGear(owner, merc, sword) && MercenaryCare.RecoverRetiredGear(owner, merc, ring), "Retired gear could not return");
        var next = Companion(owner); Require(MercenaryCare.Register(owner, next, 99), "Retired contract blocked replacement");
        next.Backpack.DropItem(sword); next.EquipItem(sword); var nested = new Bag(); next.Backpack.DropItem(nested); nested.DropItem(ring);
        health = new RestorationPotion(RestorationKind.Healing, 4) { Amount = 37 }; owner.Backpack.DropItem(health); MercenaryCare.Deposit(owner, next, health);
        next.Hits = 400; Require(MercenaryCare.TryDrink(next, TimedEffects.Now), "Reload fixture cannot drink"); TimedEffects.Clear(next); next.Kill();
        var state = MercenaryCare.States[next.Serial];
        File.WriteAllText(Filename, JsonSerializer.Serialize(new Saved(owner.Serial.Value, next.Serial.Value, sword.Serial.Value, ring.Serial.Value,
            MercenaryCare.Stock(next), state.NextDrinkUtc, state.Level)));
        owner.Internalize(); other.Internalize(); next.Internalize();
    }
    private static void Reload()
    {
        var row = JsonSerializer.Deserialize<Saved>(File.ReadAllText(Filename)); var owner = (PlayerMobile)World.FindMobile((Serial)row.Owner);
        var merc = (BaseCreature)World.FindMobile((Serial)row.Mercenary); var gear = World.FindItem((Serial)row.Gear); var nested = World.FindItem((Serial)row.Nested);
        Require(merc.IsDeadPet && gear.IsChildOf(merc.Backpack) && nested.IsChildOf(merc.Backpack) && MercenaryCare.Stock(merc) == row.Stock
            && MercenaryCare.States[merc.Serial].NextDrinkUtc == row.Next && MercenaryCare.States[merc.Serial].Level == row.Level,
            "Save/reload lost death state, gear, stock, level or drink deadline");
        merc.ResurrectPet(); Require(merc.IsDeadPet && MercenaryCare.ResurrectionPrice(row.Level) == 9900, "Restart bypassed fee policy");
    }
    private sealed class LoyaltyProbe : LoyaltyTimer
    {
        internal void Run()
        {
            typeof(LoyaltyTimer).GetField("m_NextHourlyCheck", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(this, DateTime.MinValue);
            base.OnTick();
        }
    }
    private static void Loyalty()
    {
        var row = JsonSerializer.Deserialize<Saved>(File.ReadAllText(Filename)); var owner = (PlayerMobile)World.FindMobile((Serial)row.Owner);
        var merc = (BaseCreature)World.FindMobile((Serial)row.Mercenary);
        var ordinary = new Dog { IsBonded = true }; ordinary.SetControlMaster(owner);
        ordinary.MoveToWorld(new Point3D(5550, 1151, 0), Map.Trammel); ordinary.Kill();
        ordinary.OwnerAbandonTime = merc.OwnerAbandonTime = Core.Now.AddDays(-2); merc.Loyalty = 0;
        new LoyaltyProbe().Run();
        Require(ordinary.Deleted && !merc.Deleted && merc.IsDeadPet && merc.Loyalty == BaseCreature.MaxLoyalty
            && merc.OwnerAbandonTime == DateTime.MinValue && World.FindItem((Serial)row.Gear).IsChildOf(merc.Backpack),
            "Native abandonment deleted registered gear or changed ordinary pet cleanup");
    }
}
