// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class ClassHistoryChecks
{
    private static readonly ILogger _log = LogFactory.GetLogger(typeof(ClassHistoryChecks));
    private static CharacterProfiles Profiles => ClassCreation.Profiles;
    private static string Manifest => Path.Join(Core.BaseDirectory, "class-history-checks.json");
    private sealed record Saved(uint Player, DiabloClass Active, string Histories, string Bindings, uint Pack, uint Bank, uint Belt,
        uint Potion, uint Gold, uint BankGold, string Skills, DateTime FoodUntil, DateTime PotionUntil);
    private static void Require(bool value, string why) => GearDropChecks.Require(value, why);
    public static void Configure()
    { Register("class-history-check", Check); Register("class-history-reload-check", Reload); }
    private static void Register(string name, Action check) => ConsoleInputHandler.RegisterCommand(name, "Independent histories and save migrations", _ => Core.LoopContext.Post(() =>
    { try { check(); _log.Information("{Command}: passed", name); } catch (Exception error) { _log.Error(error, "{Command}: failed", name); } }));
    private static byte[] Encode(GenericPersistence persistence)
    {
        var writer = new BufferWriter(new byte[4 * 1024 * 1024], true); persistence.Serialize(writer);
        return writer.Buffer[..(int)writer.Position];
    }
    private static CharacterProfile Profile(PlayerMobile p) { Profiles.TryGet(p.Serial, out var result); return result; }
    private static string Histories(PlayerMobile p) => JsonSerializer.Serialize(Profiles.Histories(p.Serial));
    private static string Bindings(PlayerMobile p) => JsonSerializer.Serialize(Enum.GetValues<DiabloClass>()
        .Select(c => new { Class = c, Left = SkillBindings.Instance.Get(p.Serial, c).Left, Right = SkillBindings.Instance.Get(p.Serial, c).Right, Quick = Enumerable.Range(0, SkillBindingProtocol.Slots).Select(slot => SkillBindings.Instance.GetQuick(p.Serial, c, slot)).ToArray() }));
    private static string Skills(PlayerMobile p) => JsonSerializer.Serialize(Enumerable.Range(0, p.Skills.Length)
        .Select(i => new { p.Skills[i].BaseFixedPoint, p.Skills[i].CapFixedPoint, p.Skills[i].Lock }));
    private static PlayerMobile Player()
    {
        var p = new PlayerMobile { Player = true, Body = 0x190, Name = "ClassHistorian", Blessed = true };
        p.AddItem(new Backpack()); Profiles.Assign(p, DiabloClass.Necromancer);
        FixtureSpace.Place(p, new(5530, 1190, 0), Map.Trammel);
        return p;
    }
    private static void Legacy()
    {
        byte[] original = Encode(Profiles), originalBindings = Encode(SkillBindings.Instance);
        var players = Enum.GetValues<DiabloClass>().Select(_ => Player()).ToArray();
        try
        {
            foreach (int version in new[] { 0, 1, 2 })
            {
                var writer = new BufferWriter(new byte[65536], true); writer.Write(version); writer.Write(players.Length);
                for (int i = 0; i < players.Length; i++)
                {
                    var c = Enum.GetValues<DiabloClass>()[i];
                    writer.Write(players[i].Serial.Value); writer.Write((byte)c); writer.Write(42); writer.Write(123456L + i);
                    if (version >= 2) { writer.Write(3); writer.Write(4); writer.Write(5); writer.Write(6); }
                    if (version >= 1)
                    {
                        var talent = TalentCatalog.ForClass(c).Trees.SelectMany(t => t.Skills).First(t => t.RequiredLevel == 1 && t.Prerequisites.Length == 0);
                        writer.Write(1); writer.Write(talent.Id); writer.Write((byte)2);
                    }
                }
                Profiles.Deserialize(new BufferReader(writer.Buffer[..(int)writer.Position]));
                for (int i = 0; i < players.Length; i++)
                {
                    var histories = Profiles.Histories(players[i].Serial);
                    Require(histories.Length == 1 && histories[0].Profile == new CharacterProfile(Enum.GetValues<DiabloClass>()[i], 42, 123456L + i)
                        && histories[0].Allocation == (version >= 2 ? new StatAllocation(3, 4, 5, 6) : default)
                        && histories[0].Ranks.Values.Sum(b => b) == (version >= 1 ? 2 : 0), "Legacy version lost class IDs/progress/allocations");
                }
                byte[] migrated = Encode(Profiles); Profiles.Deserialize(new BufferReader(migrated));
                Require(migrated.SequenceEqual(Encode(Profiles)), "Migrated history encoding is unstable");
                var oldBindings = new BufferWriter(new byte[1024], true); oldBindings.Write(0); oldBindings.Write(1);
                oldBindings.Write(players[2].Serial.Value); oldBindings.Write((byte)CombatAbility.RaiseSkeleton); oldBindings.Write((byte)CombatAbility.BasicAttack);
                SkillBindings.Instance.Deserialize(new BufferReader(oldBindings.Buffer[..(int)oldBindings.Position])); SkillBindings.Instance.PostDeserialize();
                Require(SkillBindings.Instance.Get(players[2]) == (CombatAbility.RaiseSkeleton, CombatAbility.BasicAttack), "Legacy binding attached to wrong class");
                byte[] bound = Encode(SkillBindings.Instance); SkillBindings.Instance.Deserialize(new BufferReader(bound));
                Require(bound.SequenceEqual(Encode(SkillBindings.Instance)), "Binding migration is unstable");
            }
        }
        finally
        {
            Profiles.Deserialize(new BufferReader(original)); SkillBindings.Instance.Deserialize(new BufferReader(originalBindings));
            foreach (var p in players) { p.Delete(); }
        }
    }
    private static void Corruption()
    {
        byte[] before = Encode(Profiles);
        for (int bad = 0; bad < 11; bad++)
        {
            var w = new BufferWriter(new byte[4096], true); w.Write(bad == 0 ? 4 : 3); w.Write(1); w.Write(123U);
            w.Write((byte)(bad == 1 ? 0 : bad == 2 ? 102 : 103)); w.Write(bad == 3 ? Enum.GetValues<DiabloClass>().Length + 1 : bad == 4 ? 2 : 1);
            void Row()
            {
                w.Write((byte)103); w.Write(bad == 5 ? 100 : 20); w.Write(bad == 6 ? -1L : 5000L);
                w.Write(bad == 7 ? 96 : 0); w.Write(0); w.Write(0); w.Write(0);
                w.Write(bad == 8 ? 2 : bad >= 9 ? 1 : 0);
                if (bad >= 8)
                {
                    w.Write(bad == 9 ? "sorceress.firebolt" : bad == 10 ? "necromancer.skeletonmastery" : "necromancer.raiseskeleton"); w.Write((byte)1);
                    if (bad == 8) { w.Write("necromancer.raiseskeleton"); w.Write((byte)1); }
                }
            }
            Row(); if (bad == 4) { Row(); }
            bool rejected = false;
            try { Profiles.Deserialize(new BufferReader(w.Buffer[..(int)w.Position])); }
            catch (InvalidDataException) { rejected = true; }
            Require(rejected && before.SequenceEqual(Encode(Profiles)), $"Corrupt history {bad} accepted or changed loaded authority");
        }
        byte[] bindings = Encode(SkillBindings.Instance);
        for (int bad = 0; bad < 3; bad++)
        {
            var w = new BufferWriter(new byte[1024], true); w.Write(1); w.Write(bad == 2 ? 2 : 1);
            void Row()
            {
                w.Write(123U); w.Write((byte)(bad == 0 ? 0 : 103)); w.Write((byte)(bad == 1 ? 255 : 1)); w.Write((byte)0);
                for (int slot = 0; slot < SkillBindingProtocol.Slots; slot++) { w.Write((byte)0); }
            }
            Row(); if (bad == 2) { Row(); }
            bool rejected = false;
            try { SkillBindings.Instance.Deserialize(new BufferReader(w.Buffer[..(int)w.Position])); }
            catch (InvalidDataException) { rejected = true; }
            Require(rejected && bindings.SequenceEqual(Encode(SkillBindings.Instance)), "Corrupt bindings replaced authority");
        }
    }
    private static void QuickGuards()
    {
        var p = Player(); var mob = new GraveyardSkeleton { Frozen = true, CantWalk = true, NoKillAwards = true };
        mob.MoveToWorld(CombatPractice.FindFloor(p.Map, p.X + 2, p.Y), p.Map); mob.AIObject?.AITimer.Stop();
        Profiles.AdvanceLevel(p, 3); Require(Profiles.TryTrain(p.Serial, "necromancer.raiseskeleton", out _) && Profiles.TryTrain(p.Serial, "necromancer.teeth", out _), "Quick guard talent missing");
        p.DoHarmful(mob); mob.Damage(1000, p); if (mob.Alive) { mob.Kill(); }
        var corpse = (Corpse)mob.Corpse;
        try
        {
            Require(SkillBindings.Instance.AssignQuick(p, 0, CombatAbility.RaiseSkeleton) && SkeletonPets.CanRaise(p, corpse), "Quick guard corpse unavailable");
            int mana = p.Mana; long next = ClassCombat.For(p).NextAction;
            bool Use(DiabloClass c, int slot, CombatAbility ability, Serial target) => SkillBindingNetwork.Use(p, c, slot, ability, target, corpse.Location);
            Require(!Use(DiabloClass.Sorceress, 0, CombatAbility.RaiseSkeleton, corpse.Serial)
                && !Use(DiabloClass.Necromancer, 12, CombatAbility.RaiseSkeleton, corpse.Serial)
                && !Use(DiabloClass.Necromancer, 0, CombatAbility.Teeth, corpse.Serial)
                && !Use(DiabloClass.Necromancer, 0, CombatAbility.RaiseSkeleton, Serial.Zero)
                && !corpse.SanctuaryConsumed && SkeletonPets.Count(p) == 0 && p.Mana == mana && ClassCombat.For(p).NextAction == next,
                "Stale/invalid quick cast edited inactive state, spent resources or fell back to a nearby corpse");
            Require(SkillBindings.Instance.AssignQuick(p, 1, CombatAbility.Teeth)
                && Use(DiabloClass.Necromancer, 1, CombatAbility.Teeth, p.Serial)
                && ClassCombat.For(p).PendingTarget == Serial.Zero && ClassCombat.For(p).PendingPoint == corpse.Location,
                "Quick Teeth followed an entity or refused the cursor point over an ally");
        }
        finally { ClassCombat.Stop(p); corpse.Delete(); mob.Delete(); p.Delete(); Profiles.PruneDeleted(); SkillBindings.Instance.PruneDeleted(); }
    }
    private static void Select(PlayerMobile p, DiabloClass next)
    {
        int hp = p.Hits, mana = p.Mana, stam = p.Stam, mh = p.HitsMax, mm = p.ManaMax, ms = p.StamMax;
        var session = ClassCombat.For(p); var deadlines = (session.NextAction, session.RecoveryUntil, p.NextSkillTime, p.NextSpellTime);
        Require(Profiles.TryActivateHistory(p, next), $"History activation failed for {next}");
        Require(p.Hits == (long)hp * p.HitsMax / mh && p.Mana == (long)mana * p.ManaMax / mm && p.Stam == (long)stam * p.StamMax / ms
            && deadlines == (session.NextAction, session.RecoveryUntil, p.NextSkillTime, p.NextSpellTime), "Activation refilled a pool or reset a deadline");
    }
    private static void Check()
    {
        Legacy(); Corruption(); QuickGuards(); var p = Player();
        Require(Profiles.AwardExperience(p, 1500) && Profile(p).Level > 1 && Profiles.TrySpendStat(p, CharacterStat.Vitality)
            && Profiles.TryTrain(p.Serial, "necromancer.raiseskeleton", out _), "Initial class did not progress");
        Require(SkillBindings.Instance.Assign(p, true, CombatAbility.RaiseSkeleton) && SkillBindings.Instance.Assign(p, false, CombatAbility.BasicAttack), "Initial bindings failed");
        Require(Profiles.TryTrain(p.Serial, "necromancer.teeth", out _), "Quick Teeth training failed");
        for (int slot = 0; slot < SkillBindingProtocol.Slots; slot++)
        { Require(SkillBindings.Instance.AssignQuick(p, slot, slot % 2 == 0 ? CombatAbility.Teeth : CombatAbility.RaiseSkeleton), "Quick binding failed"); }
        Require(!SkillBindings.Instance.AssignQuick(p, -1, CombatAbility.Teeth) && !SkillBindings.Instance.AssignQuick(p, 12, CombatAbility.Teeth)
            && !SkillBindings.Instance.AssignQuick(p, 0, CombatAbility.Teleport), "Invalid or unlearned quick skill accepted");
        var necro = Profiles.Histories(p.Serial).Single();
        var gold = new Gold(1234); p.Backpack.DropItem(gold); var bankGold = new Gold(4321); p.BankBox.DropItem(bankGold);
        var potion = new RestorationPotion(RestorationKind.Healing, 1); p.Backpack.DropItem(potion);
        Require(PotionBelts.Load(p, 0, potion), "Shared belt setup failed");
        p.Skills.Alchemy.Cap = 115; p.Skills.Alchemy.Base = 45; p.Skills.Alchemy.SetLockNoRelay(SkillLock.Down);
        var pack = p.Backpack; var bank = p.BankBox; var belt = PotionBelts.Storage(p); var location = p.Location; var map = p.Map; string skills = Skills(p);
        p.Hits = p.HitsMax / 2; p.Mana = p.ManaMax / 2; p.Stam = p.StamMax / 2;
        TimedEffects.ApplyBuff(p, TimedEffectCategory.Food, "light", 1); TimedEffects.ApplyBuff(p, TimedEffectCategory.Potion, "protection", 10);
        DateTime foodUntil = TimedEffects.Buff(p, TimedEffectCategory.Food).ExpiresUtc, potionUntil = TimedEffects.Buff(p, TimedEffectCategory.Potion).ExpiresUtc;
        TimedEffects.Chill(p, 50, 5000, TimedEffects.Now); TimedEffects.Poison(p, p, 1, 5000, TimedEffects.Now);
        TimedEffects.AddRestoration(p, true, 3, 5000);
        var effects = TimedEffects.Snapshot().Single(r => r.Owner == p.Serial.Value);
        Select(p, DiabloClass.Sorceress);
        Require(Profile(p) == new CharacterProfile(DiabloClass.Sorceress, 1, 0) && Profiles.Stats(p.Serial) == default && Profiles.AvailablePoints(p.Serial) == 0
            && !Profiles.TryTrain(p.Serial, "necromancer.raiseskeleton", out _) && SkillBindings.Instance.Get(p) == (CombatAbility.BasicAttack, CombatAbility.None), "Fresh class inherited progression/bindings");
        Require(Enumerable.Range(0, SkillBindingProtocol.Slots).All(slot => SkillBindings.Instance.GetQuick(p.Serial, DiabloClass.Sorceress, slot) == CombatAbility.None)
            && !SkillBindingNetwork.Apply(p, DiabloClass.Necromancer, SkillBindingRequest.Quick, 0, CombatAbility.BasicAttack), "Fresh quick bar or stale class binding failed");
        Require(Profiles.AwardExperience(p, 4000) && Profiles.TrySpendStat(p, CharacterStat.Energy)
            && SkillBindings.Instance.Assign(p, true, CombatAbility.None) && SkillBindings.Instance.Assign(p, false, CombatAbility.BasicAttack), "Second class cannot progress or bind skills");
        Require(SkillBindings.Instance.AssignQuick(p, 11, CombatAbility.BasicAttack), "Independent second-class quick bar failed");
        var savedNecro = Profiles.Histories(p.Serial).Single(h => h.Profile.Class == DiabloClass.Necromancer);
        Require(JsonSerializer.Serialize(savedNecro) == JsonSerializer.Serialize(necro), "Inactive class advanced");
        int count = p.Backpack.Items.Count;
        foreach (var c in Enum.GetValues<DiabloClass>().Where(c => c is not (DiabloClass.Necromancer or DiabloClass.Sorceress)))
        { p.Hits = p.HitsMax; p.Mana = p.ManaMax; p.Stam = p.StamMax; Select(p, c); Require(Profile(p).Level == 1, "Fresh history not level1"); }
        Require(p.Backpack.Items.Count == count, "Class activation minted starter gear");
        p.Hits = p.HitsMax; p.Mana = p.ManaMax; p.Stam = p.StamMax; Select(p, DiabloClass.Necromancer);
        Require(Profile(p) == necro.Profile && Profiles.Stats(p.Serial) == necro.Allocation && Profiles.GetRank(p.Serial, "necromancer.raiseskeleton") == 1
            && SkillBindings.Instance.Get(p) == (CombatAbility.RaiseSkeleton, CombatAbility.BasicAttack), "Returning class lost its history/bindings");
        Require(SkillBindings.Instance.GetQuick(p.Serial, DiabloClass.Necromancer, 0) == CombatAbility.Teeth
            && SkillBindings.Instance.GetQuick(p.Serial, DiabloClass.Necromancer, 11) == CombatAbility.RaiseSkeleton
            && SkillBindings.Instance.GetQuick(p.Serial, DiabloClass.Sorceress, 11) == CombatAbility.BasicAttack, "Quick bars leaked across classes");
        Require(p.Backpack == pack && p.BankBox == bank && PotionBelts.Storage(p) == belt && PotionBelts.Entry(p, 0) == potion
            && gold.Parent == pack && gold.Amount == 1234 && bankGold.Parent == bank && bankGold.Amount == 4321 && Skills(p) == skills
            && p.Map == map && p.Location == location && TimedEffects.Buff(p, TimedEffectCategory.Food).ExpiresUtc == foodUntil
            && TimedEffects.Buff(p, TimedEffectCategory.Potion).ExpiresUtc == potionUntil && TimedEffects.Chill(p, TimedEffects.Now) == 50
            && TimedEffects.Snapshot().Single(r => r.Owner == p.Serial.Value).PoisonMs > 0, "Shared belongings, skills or timed effects changed");
        var remaining = TimedEffects.Snapshot().Single(r => r.Owner == p.Serial.Value);
        Require(remaining.ChillUntilUtc == effects.ChillUntilUtc && remaining.PoisonMs > 0 && remaining.PoisonMs <= effects.PoisonMs
            && remaining.Life.Duration > 0 && remaining.Life.Duration <= effects.Life.Duration && remaining.Life.Units <= effects.Life.Units,
            "Activation cleansed or restarted chill/poison/restoration");
        var sorc = Profiles.Histories(p.Serial).Single(h => h.Profile.Class == DiabloClass.Sorceress);
        Profiles.ResetAllocatedPoints(p, Profile(p));
        Require(Profiles.Stats(p.Serial) == default && Profiles.GetRank(p.Serial, "necromancer.raiseskeleton") == 0
            && JsonSerializer.Serialize(sorc) == JsonSerializer.Serialize(Profiles.Histories(p.Serial).Single(h => h.Profile.Class == DiabloClass.Sorceress)), "Active respec changed inactive history");
        Guards(p);
        Select(p, DiabloClass.Sorceress);
        TimedEffects.Clear(p); TimedEffects.ApplyBuff(p, TimedEffectCategory.Food, "light", 1); TimedEffects.ApplyBuff(p, TimedEffectCategory.Potion, "protection", 10);
        File.WriteAllText(Manifest, JsonSerializer.Serialize(new Saved(p.Serial.Value, Profile(p).Class, Histories(p), Bindings(p), pack.Serial.Value, bank.Serial.Value,
            belt.Serial.Value, potion.Serial.Value, gold.Serial.Value, bankGold.Serial.Value, skills,
            TimedEffects.Buff(p, TimedEffectCategory.Food).ExpiresUtc, TimedEffects.Buff(p, TimedEffectCategory.Potion).ExpiresUtc)));
        _log.Information("Class histories: v0/v1/v2 migration across7 IDs, atomic corrupt-save rejection, independent progression, per-class bindings, percentage pools, shared effects/belongings and active-only respec passed");
    }
    private static void Guards(PlayerMobile p)
    {
        var session = ClassCombat.For(p); string before = Histories(p);
        session.NextAction = Core.TickCount + 10000;
        Require(!Profiles.TryActivateHistory(p, DiabloClass.Amazon), "Recovery bypassed"); session.NextAction = Core.TickCount;
        p.Frozen = true; Require(!Profiles.TryActivateHistory(p, DiabloClass.Amazon), "Frozen activation"); p.Frozen = false;
        Require(!Profiles.TryActivateHistory(p, (DiabloClass)0), "Invalid class accepted");
        var robe = new Robe(); Require(p.EquipItem(robe), "Guard robe failed");
        Require(!Profiles.TryActivateHistory(p, DiabloClass.Amazon), "Foundation bypassed future equipment transaction"); p.Backpack.DropItem(robe);
        var pet = new Cat { Controlled = true, ControlMaster = p }; pet.MoveToWorld(p.Location, p.Map);
        Require(!Profiles.TryActivateHistory(p, DiabloClass.Amazon), "Foundation bypassed future pet transaction"); pet.Delete();
        Require(before == Histories(p), "Rejected activation mutated history");
        p.Hits = 1; Require(!Profiles.TryActivateHistory(p, DiabloClass.Sorceress) && p.Hits == 1 && before == Histories(p), "Near-zero transition invented free healing or changed history");
        p.Hits = p.HitsMax / 2; p.Mana = 0; p.Stam = 0;
        Select(p, DiabloClass.Sorceress); Select(p, DiabloClass.Necromancer);
        Require(p.Mana == 0 && p.Stam == 0, "Empty pools refilled");
        for (int i = 0; i < 8; i++)
        {
            int hits = p.Hits; Select(p, DiabloClass.Sorceress); Select(p, DiabloClass.Necromancer);
            Require(p.Hits <= hits, "Repeated cycling healed");
        }
    }
    private static void Reload()
    {
        var s = JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest)); var p = (PlayerMobile)World.FindMobile((Serial)s.Player);
        Require(p != null && Profile(p).Class == s.Active && Histories(p) == s.Histories && Bindings(p) == s.Bindings && p.Backpack.Serial.Value == s.Pack && p.BankBox.Serial.Value == s.Bank
            && PotionBelts.Storage(p).Serial.Value == s.Belt && PotionBelts.Entry(p, 0)?.Serial.Value == s.Potion && Skills(p) == s.Skills
            && World.FindItem((Serial)s.Gold) is Gold { Amount: 1234 } && World.FindItem((Serial)s.BankGold) is Gold { Amount: 4321 }
            && TimedEffects.Buff(p, TimedEffectCategory.Food)?.ExpiresUtc == s.FoodUntil, "Reload lost history/bindings/shared state");
        Require(TimedEffects.Buff(p, TimedEffectCategory.Potion) is not { } potion || potion.ExpiresUtc == s.PotionUntil, "Reload restarted potion timer");
        int hits = p.Hits; Select(p, DiabloClass.Necromancer); Select(p, s.Active);
        Require(p.Hits <= hits && Histories(p) == s.Histories && Bindings(p) == s.Bindings, "Reloaded class cycle changed progress or healed");
    }
}
