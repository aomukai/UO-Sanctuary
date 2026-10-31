// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class FullWardrobeChecks
{
    private static readonly ILogger _log = LogFactory.GetLogger(typeof(FullWardrobeChecks));
    private static string Manifest => Path.Join(Core.BaseDirectory, "full-wardrobe-checks.json");
    private sealed record Saved(uint Owner, uint Dagger, uint Robe, uint Pet, uint Ordinary, string Gear, int PetLife, uint Expiring, DateTime Until);
    private static void Require(bool value, string why) => GearDropChecks.Require(value, why);
    public static void Configure()
    { Register("full-wardrobe-check", Check); Register("full-wardrobe-reload-check", Reload); }
    private static void Register(string name, Action check) => ConsoleInputHandler.RegisterCommand(name, "Real wardrobe transactions and parked minion persistence", _ => Core.LoopContext.Post(() =>
    { try { check(); _log.Information("{Command}: passed", name); } catch (Exception error) { _log.Error(error, "{Command}: failed", name); } }));
    private static CharacterProfile Profile(PlayerMobile p) { ClassCreation.Profiles.TryGet(p.Serial, out var result); return result; }
    private static Item Wardrobe(PlayerMobile p) => TownRecognition.PublicWardrobe(RecognitionChecks.Place(p));
    private static bool Swap(PlayerMobile p, DiabloClass c, SkillName? craft = null, string quote = null)
    {
        var wardrobe = Wardrobe(p);
        return WardrobeSwitch.Commit(p, wardrobe, TownRecognition.BeginSession(p), Profile(p), ClassProfessions.Profiles.Find(p),
            c, craft ?? ClassProfessions.Profiles.Find(p).Craft, quote);
    }
    private static PlayerMobile Player()
    {
        var p = new PlayerMobile { Player = true, Body = 0x190, Name = "FullWardrobe" };
        p.AddItem(new Backpack()); ClassCreation.Profiles.Assign(p, DiabloClass.Necromancer);
        ClassCreation.Profiles.AdvanceLevel(p, 80); string site = RecognitionChecks.Place(p);
        Require(TownRecognition.Claim(p, site, RecognitionHonor.Hero), "Wardrobe right was not earned");
        ClassProfessions.Profiles.Confirm(p, SkillName.Alchemy);
        Require(ClassCreation.Profiles.TryTrain(p.Serial, "necromancer.raiseskeleton", out _), "Summon allocation failed");
        return p;
    }
    private static void RatioSwap(PlayerMobile p, DiabloClass c, SkillName? craft = null)
    {
        int life = p.Hits, mana = p.Mana, stam = p.Stam, maxLife = p.HitsMax, maxMana = p.ManaMax, maxStam = p.StamMax;
        var s = ClassCombat.For(p); var deadlines = (s.NextAction, s.RecoveryUntil, p.NextSkillTime, p.NextSpellTime);
        Require(Swap(p, c, craft), $"Class switch to {c} failed");
        Require(p.Hits == (long)life * p.HitsMax / maxLife && p.Mana == (long)mana * p.ManaMax / maxMana
            && p.Stam == (long)stam * p.StamMax / maxStam
            && deadlines == (s.NextAction, s.RecoveryUntil, p.NextSkillTime, p.NextSpellTime), "Gear swap refilled resources or reset action deadlines");
    }
    private static void Check()
    {
        Guards(); StatDependencies();
        var p = Player();
        Require(ClassCreation.Profiles.TryTrain(p.Serial, "necromancer.raiseskeleton", out _), "Second summon allowance failed");
        var dagger = new Dagger(); ClassGear.Profiles.MarkStarter(dagger);
        var robe = new Robe(); Require(p.EquipItem(dagger) && p.EquipItem(robe), "Initial set equip failed");
        string gear = JsonSerializer.Serialize(ClassGear.State(dagger)); var bank = p.BankBox; var pack = p.Backpack;
        p.Backpack.DropItem(new Gold(1234)); p.Skills.Tinkering.Base = 73.4;
        SkillBindings.Instance.Assign(p, false, CombatAbility.RaiseSkeleton);
        SkillBindings.Instance.AssignQuick(p, 11, CombatAbility.RaiseSkeleton);
        var pet = new SkeletonGuardian(); pet.Initialize(p, 1, 0); pet.SetControlMaster(p);
        pet.Hits = pet.HitsMax / 2; PersistentMinions.Profiles.Track(pet);
        var ordinary = new Dog(); ordinary.SetControlMaster(p); ordinary.MoveToWorld(new Point3D(p.X + 2, p.Y, p.Z), p.Map);
        p.Hits = p.HitsMax * 3 / 4; p.Mana = p.ManaMax / 2; p.Stam = p.StamMax / 2;
        RatioSwap(p, DiabloClass.Sorceress, SkillName.Tinkering);
        var set = WardrobeEquipment.Instance.Find(p.Serial, DiabloClass.Necromancer);
        Require(dagger.Parent == set && robe.Parent == set && set.Parent == null && set.Map == Map.Internal
            && !set.CheckLift(p, dagger, ref _reason) && pet.Map == Map.Internal && pet.ControlMaster == p
            && pet.ControlSlots == 0 && PersistentMinions.Profiles.Find(pet).Parked
            && !PersistentMinions.CanThink(pet) && ordinary.Map == p.Map && ordinary.ControlMaster == p
            && p.Backpack == pack && p.BankBox == bank && p.Backpack.GetAmount(typeof(Gold)) == 1234 && p.Skills.Tinkering.Base == 73.4,
            "Stored equipment, minions or shared belongings violated ownership rules");
        Require(Profile(p) == new CharacterProfile(DiabloClass.Sorceress, 1, 0)
            && SkillBindings.Instance.GetQuick(p.Serial, DiabloClass.Sorceress, 11) == CombatAbility.None, "Fresh class inherited progress or quick bindings");
        int beforeLife = pet.Hits;
        var extra = new SkullCap(); Require(p.EquipItem(extra), "Second class cosmetic equip failed");
        RatioSwap(p, DiabloClass.Necromancer);
        Require(dagger.Parent == p && robe.Parent == p && extra.Parent == WardrobeEquipment.Instance.Find(p.Serial, DiabloClass.Sorceress)
            && pet.Hits == beforeLife && !PersistentMinions.Profiles.Find(pet).Parked
            && SkillBindings.Instance.Get(p).Right == CombatAbility.RaiseSkeleton
            && SkillBindings.Instance.GetQuick(p.Serial, DiabloClass.Necromancer, 11) == CombatAbility.RaiseSkeleton
            && JsonSerializer.Serialize(ClassGear.State(dagger)) == gear, "Class restoration cloned, rerolled, healed or lost saved state");
        Require(Swap(p, DiabloClass.Necromancer) && Swap(p, DiabloClass.Necromancer, SkillName.Alchemy), "Unchanged or craft-only selection failed");
        // Invalid storage and stale full quotes fail before moving any real object.
        string quote = WardrobeTransaction.Quote(p); p.Backpack.DropItem(robe);
        Require(!Swap(p, DiabloClass.Sorceress, quote: quote) && dagger.Parent == p, "Stale equipment quote switched class");
        Require(p.EquipItem(robe), "Restore robe failed");
        robe.Movable = false; Require(!Swap(p, DiabloClass.Sorceress) && dagger.Parent == p, "Immovable equipped item was stored"); robe.Movable = true;
        RatioSwap(p, DiabloClass.Sorceress);
        var junk = new Gold(1); set.AddItem(junk);
        Require(!Swap(p, DiabloClass.Necromancer) && Profile(p).Class == DiabloClass.Sorceress, "Unlisted storage contents accepted"); junk.Delete();
        // An owned but now unidentified item falls back to the pack. No space means full rollback.
        var state = ClassGear.State(dagger); ClassGear.Profiles.Store(dagger, state with { Identified = false });
        int oldMaxItems = p.Backpack.MaxItems; p.Backpack.MaxItems = p.Backpack.TotalItems;
        var oldProfile = Profile(p); var oldProfession = ClassProfessions.Profiles.Find(p);
        var oldPools = (p.Hits, p.Mana, p.Stam); int followers = p.Followers;
        Require(!Swap(p, DiabloClass.Necromancer, SkillName.Imbuing) && Profile(p) == oldProfile
            && ClassProfessions.Profiles.Find(p) == oldProfession && dagger.Parent == set && extra.Parent == p
            && oldPools == (p.Hits, p.Mana, p.Stam) && p.Followers == followers && PersistentMinions.Profiles.Find(pet).Parked,
            "Full backpack left a partial gear/class/craft/summon swap");
        p.Backpack.MaxItems = oldMaxItems;
        RatioSwap(p, DiabloClass.Necromancer);
        Require(dagger.Parent == p.Backpack && robe.Parent == p, "Ineligible item was not put in backpack");
        ClassGear.Profiles.Store(dagger, state); Require(p.EquipItem(dagger), "Identified dagger did not equip");
        RatioSwap(p, DiabloClass.Sorceress);
        // Missing/moved items are never remotely reclaimed from another character.
        var stranger = Player(); stranger.Backpack.DropItem(robe);
        RatioSwap(p, DiabloClass.Necromancer);
        Require(robe.Parent == stranger.Backpack && dagger.Parent == p, "Wardrobe reclaimed an item from another owner");
        stranger.Backpack.RemoveItem(robe); p.Backpack.DropItem(robe); stranger.Delete(); Require(p.EquipItem(robe), "Robe recovery fixture failed");
        RatioSwap(p, DiabloClass.Sorceress);
        long token = TownRecognition.BeginSession(p); var wardrobe = Wardrobe(p);
        p.Backpack.MaxItems = p.Backpack.TotalItems;
        Require(!WardrobeTransaction.Retrieve(p, wardrobe, token, set, robe) && robe.Parent == set, "Full backpack retrieval removed stored item");
        p.Backpack.MaxItems = oldMaxItems;
        Require(WardrobeTransaction.Retrieve(p, wardrobe, token, set, robe) && robe.Parent == p.Backpack
            && !set.Saved.Contains(robe.Serial.Value) && !WardrobeTransaction.Retrieve(p, wardrobe, token, set, dagger), "Retrieval replay moved another item");
        RatioSwap(p, DiabloClass.Necromancer); Require(p.EquipItem(robe), "Retrieved gear was not shareable");
        // All fresh classes retain empty quick bars and create no new starter equipment.
        foreach (var c in Enum.GetValues<DiabloClass>().Where(c => c != DiabloClass.Necromancer))
        {
            p.Hits = p.HitsMax; p.Mana = p.ManaMax; p.Stam = p.StamMax;
            RatioSwap(p, c); Require(Profile(p).Level == 1 && Profile(p).Experience == 0, "Fresh class wasn't level one");
        }
        p.Hits = p.HitsMax; RatioSwap(p, DiabloClass.Necromancer);
        // Nonzero follower costs are released and restored; ordinary pets consume their normal slots.
        pet.ControlSlots = 2; p.Followers += 2; PersistentMinions.Profiles.Track(pet);
        p.Hits = p.HitsMax; RatioSwap(p, DiabloClass.Sorceress);
        Require(p.Followers == ordinary.ControlSlots && pet.ControlSlots == 0, "Inactive class still consumed follower slots");
        int limit = p.FollowersMax; p.FollowersMax = p.Followers;
        Require(!Swap(p, DiabloClass.Necromancer) && pet.Map == Map.Internal && Profile(p).Class == DiabloClass.Sorceress, "Follower limit accepted partial summon return");
        p.FollowersMax = limit; p.Hits = p.HitsMax; RatioSwap(p, DiabloClass.Necromancer);
        Require(pet.ControlSlots == 2 && p.Followers == ordinary.ControlSlots + 2, "Returning follower budget was not restored");
        // Absolute expiry applies even while class is parked.
        p.Hits = p.HitsMax; RatioSwap(p, DiabloClass.Sorceress);
        var expiring = new SkeletonGuardian(); expiring.Initialize(p, 1, 0); expiring.SetControlMaster(p);
        var tracked = PersistentMinions.Profiles.Track(expiring);
        PersistentMinions.Profiles.Store(expiring, tracked with { ExpiresUtc = DateTime.UtcNow.AddSeconds(-1) });
        PersistentMinions.Profiles.Sweep(DateTime.UtcNow); Require(expiring.Deleted, "Inactive summon lifetime was frozen");
        var timed = new SkeletonGuardian(); timed.Initialize(p, 1, 0); timed.SetControlMaster(p);
        var until = DateTime.UtcNow.AddSeconds(10);
        PersistentMinions.Profiles.Store(timed, PersistentMinions.Profiles.Track(timed) with { ExpiresUtc = until });
        PersistentMinions.Restore(timed);
        File.WriteAllText(Manifest, JsonSerializer.Serialize(new Saved(p.Serial.Value, dagger.Serial.Value, robe.Serial.Value,
            pet.Serial.Value, ordinary.Serial.Value, gear, pet.Hits, timed.Serial.Value, until)));
    }
    private static void StatDependencies()
    {
        var p = Player(); var ring = new GoldRing(); var sword = new Longsword();
        for (int seed = 0; seed < 10000 && ClassGear.Bonus(ring, "str") < 5; seed++)
        { ClassGear.Profiles.Convert(ring, 80, GearRarity.Rare, new Random(seed)); }
        Require(ClassGear.Bonus(ring, "str") >= 5, "Could not generate a strength ring");
        int requirement = ClassGear.Stat(sword, ClassGearStat.StrengthRequirement) ?? 0;
        while (p.Str < requirement - 3) { Require(ClassCreation.Profiles.TrySpendStat(p, CharacterStat.Strength), "Could not allocate gear dependency stats"); }
        while (p.Dex < sword.DexRequirement) { Require(ClassCreation.Profiles.TrySpendStat(p, CharacterStat.Dexterity), "Could not allocate weapon dexterity"); }
        Require(p.Str < requirement && p.EquipItem(ring), "Stat ring setup failed"); ClassGear.Refresh(p);
        Require(p.Str >= requirement && p.EquipItem(sword), $"Strength-granted weapon could not equip: str={p.Str} raw={p.RawStr} req={requirement}, ringbonus={ClassGear.Bonus(ring, "str")} active={ClassGear.Active(p, ring)} dex={p.Dex}/{sword.DexRequirement}, int={p.Int}/{sword.IntRequirement}, can={sword.CanEquip(p)}");
        p.Hits = p.HitsMax; p.Mana = p.ManaMax; p.Stam = p.StamMax;
        RatioSwap(p, DiabloClass.Sorceress);
        p.Hits = p.HitsMax; RatioSwap(p, DiabloClass.Necromancer);
        Require(ring.Parent == p && sword.Parent == p && ClassGear.CanEquip(sword, p) == true, "Loadout did not restore stat dependencies iteratively");
        p.Delete(); WardrobeEquipment.Instance.Prune();
    }
    private static void Guards()
    {
        var p = Player(); var robe = new Robe(); Require(p.EquipItem(robe), "Rollback robe failed");
        p.Hits = 1; int max = p.HitsMax;
        Require(!Swap(p, DiabloClass.Sorceress) && Profile(p).Class == DiabloClass.Necromancer
            && ClassCreation.Profiles.Histories(p.Serial).Length == 1 && p.Hits == 1 && p.HitsMax == max
            && robe.Parent == p && !WardrobeEquipment.Instance.HasReference(p.Serial, DiabloClass.Necromancer),
            "Zero-life rounding left a new history, storage or partial selection");
        p.Hits = p.HitsMax;
        var pet = new SkeletonGuardian(); pet.Initialize(p, 1, 0); pet.SetControlMaster(p); PersistentMinions.Profiles.Track(pet);
        Require(Swap(p, DiabloClass.Sorceress), "Death fixture could not park summon");
        ClassCreation.Profiles.AdvanceLevel(p, 2); Require(ClassCreation.Profiles.TrySpendStat(p, CharacterStat.Vitality), "Respec fixture stat failed");
        p.Skills.Cartography.Base = 100; string site = RecognitionChecks.Place(p);
        Require(TownRecognition.Claim(p, site, RecognitionHonor.Pillar), "Respec second ceremony failed");
        p.Backpack.DropItem(new Gold(5000));
        Require(ClassRespec.Commit(p, Wardrobe(p), Profile(p), ClassCreation.Profiles.Stats(p.Serial), ClassCreation.Profiles.AvailablePoints(p.Serial))
            && !pet.Deleted && pet.Map == Map.Internal, "Other-class respec erased parked summons");
        p.Kill(); Require(!p.Alive && pet.Deleted, "Character death retained inactive class summons");
        p.Corpse?.Delete(); p.Delete(); WardrobeEquipment.Instance.Prune();
        var heavy = Player(); heavy.Backpack.DropItem(new IronIngot(3000));
        Require(Swap(heavy, DiabloClass.Sorceress) && heavy.TotalWeight > heavy.MaxWeight, "Weaker-class encumbrance incorrectly cancelled a valid swap");
        heavy.Delete(); WardrobeEquipment.Instance.Prune();
    }
    private static Server.Network.LRReason _reason;
    private static void Reload()
    {
        var saved = JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));
        PersistentMinions.Profiles.Sweep(DateTime.UtcNow);
        var timed = World.FindMobile((Serial)saved.Expiring);
        Require(saved.Until <= DateTime.UtcNow ? timed == null || timed.Deleted : timed is { Deleted: false }, "Absolute summon expiry did not advance across downtime");
        var p = (PlayerMobile)World.FindMobile((Serial)saved.Owner); var dagger = World.FindItem((Serial)saved.Dagger);
        var robe = World.FindItem((Serial)saved.Robe); var pet = (SkeletonGuardian)World.FindMobile((Serial)saved.Pet);
        var ordinary = (BaseCreature)World.FindMobile((Serial)saved.Ordinary);
        var set = WardrobeEquipment.Instance.Find(p.Serial, DiabloClass.Necromancer);
        Require(Profile(p).Class == DiabloClass.Sorceress && ClassCreation.Profiles.Histories(p.Serial).Length == Enum.GetValues<DiabloClass>().Length
            && dagger.Parent == set && robe.Parent == set && pet is { Deleted: false, ControlSlots: 0 }
            && pet.Map == Map.Internal && pet.Hits == saved.PetLife && pet.ControlMaster == p
            && PersistentMinions.Profiles.Find(pet) is { Parked: true, Slots: 2 }
            && (ordinary.ControlMaster == p || ordinary.IsStabled && ordinary.StabledBy == p) && JsonSerializer.Serialize(ClassGear.State(dagger)) == saved.Gear,
            $"Restart lost state: class={Profile(p).Class}, histories={ClassCreation.Profiles.Histories(p.Serial).Length}, dagger={dagger?.Parent}, robe={robe?.Parent}, set={set}, pet={pet?.Deleted}/{pet?.ControlSlots}/{pet?.Map}/{pet?.Hits}/{saved.PetLife}/{pet?.ControlMaster}, ledger={PersistentMinions.Profiles.Find(pet)}, ordinary={ordinary?.ControlMaster}, gear={JsonSerializer.Serialize(ClassGear.State(dagger)) == saved.Gear}");
        PersistentMinions.Resume(p); Require(pet.Map == Map.Internal, "Inactive summons returned on movement/login");
        p.Hits = p.HitsMax; RatioSwap(p, DiabloClass.Necromancer);
        Require(dagger.Parent == p && robe.Parent == p && pet.ControlSlots == 2 && pet.Hits == saved.PetLife, "Reloaded class could not restore its real set");
        p.Hits = p.HitsMax; RatioSwap(p, DiabloClass.Sorceress);
    }
}
