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

public static class ClassSkillChecks
{
    private static readonly ILogger _log = LogFactory.GetLogger(typeof(ClassSkillChecks));
    private sealed record Saved(uint Owner, uint Target, int Armor, uint Minion, int Life, int Damage, byte Ability, uint MotionTarget = 0);
    private static string Manifest => Path.Join(Core.BaseDirectory, "class-skills-checks.json");
    public static void Configure()
    {
        Register("class-skills-check", Check); Register("class-skills-reload-check", Reload);
        Register("class-skills-projectiles-check", Projectiles); Register("class-skills-ai-check", CurseAi);
    }
    private static void Register(string name, Action action) => ConsoleInputHandler.RegisterCommand(name,
        "Isolated class skills, mechanics and persistence verification", _ => Core.LoopContext.Post(() =>
        { try { action(); _log.Information("{Name}: passed", name); } catch (Exception e) { _log.Error(e, "{Name}: failed", name); } }));
    private static void Require(bool value, string why) { if (!value) { throw new InvalidOperationException(why); } }
    internal static PlayerMobile Player(DiabloClass characterClass = DiabloClass.Necromancer)
    {
        var player = new PlayerMobile { Body = 0x190, Name = "ClassSkillFixture", Player = true };
        ClassCreation.Profiles.Assign(player, characterClass); ClassCreation.Profiles.AdvanceLevel(player, 99);
        FixtureSpace.Place(player, new Point3D(5450, 1160, 0), Map.Trammel);
        foreach (var tree in TalentCatalog.ForClass(characterClass).Trees)
        {
            foreach (var talent in tree.Skills.OrderBy(t => t.RequiredLevel)) { Train(player, talent.Id); }
        }
        player.Hits = player.HitsMax; player.Mana = player.ManaMax; return player;
    }
    private static void Train(PlayerMobile player, string id)
    {
        if (ClassCreation.Profiles.GetRank(player.Serial, id) > 0) { return; }
        ClassCreation.Profiles.TryGet(player.Serial, out var profile);
        Require(TalentCatalog.TryGet(profile.Class, id, out var definition), "No talent " + id);
        foreach (string prerequisite in definition.Prerequisites) { Train(player, prerequisite); }
        Require(ClassCreation.Profiles.TryTrain(player.Serial, id, out _), "Cannot train " + id);
    }
    private static GraveyardSkeleton Enemy(PlayerMobile player, int offset = 3)
    {
        var enemy = new GraveyardSkeleton { Frozen = true, CantWalk = true, NoKillAwards = true }; enemy.AIObject?.AITimer.Stop();
        enemy.MoveToWorld(CombatPractice.FindFloor(player.Map, player.X + offset, player.Y), player.Map);
        enemy.SetHits(5000); enemy.Hits = enemy.HitsMax;
        Require(ClassCombat.IsHostile(player, enemy), "Fixture monster is not hostile"); return enemy;
    }
    private static SkillValues Values(CombatAbility ability, params (string Skill, int Rank)[] levels)
    {
        var ranks = levels.ToDictionary(x => x.Skill, x => x.Rank);
        return new(SkillMechanics.For(ability), k => ranks.GetValueOrDefault(k), k => ranks.GetValueOrDefault(k), 99);
    }
    private static void Check()
    {
        Require(SkillMechanics.All.Count == 240 && ClassAbilities.Count < 256 && TalentCatalog.ForClass(DiabloClass.Warlock).Trees.Sum(t => t.Skills.Length) == 30, "Incomplete source/Warlock roster");
        Require(DiabloClasses.DisplayName(DiabloClass.Warlock, true) == "Witch" && DiabloClasses.TryFromWire(108, out _) && !DiabloClasses.TryFromWire(109, out _), "Warlock aliases or wire bounds");
        var one = Values(CombatAbility.Teeth, ("Teeth", 1));
        Require(one.Mana256 == 768 && one.Damage(false) == 2 && one.Damage(true) == 4 && one.Eval("calc1") == 2, "Rank1 Teeth data");
        var two = Values(CombatAbility.Teeth, ("Teeth", 2)); Require(two.Mana256 == 896, "Half-mana precision");
        var twenty = Values(CombatAbility.BoneSpear, ("Bone Spear", 20));
        Require(twenty.Damage(false) == 192 && twenty.Damage(true) == 204, "Bone Spear tier boundaries");
        var synergies = Values(CombatAbility.BoneSpear, ("Bone Spear", 20), ("Teeth", 20), ("Bone Wall", 20), ("Bone Prison", 20), ("Bone Spirit", 20));
        Require(synergies.Damage256(false) == 363724 && synergies.Damage256(true) == 386457, "Fixed-point synergy rounding");
        var differentRanks = new SkillValues(SkillMechanics.For(CombatAbility.BoneSpear), k => k == "Bone Spear" ? 20 : 40, k => k == "Bone Spear" ? 20 : 1, 99);
        Require(differentRanks.Eval("EDmgSymPerCalc") == 32, "Equipment soft ranks leaked into hard-point synergies");
        Require(SkillFormula.Evaluate("1 ? 9 : 1/0", (_, _, _) => 0) == 9, "Ternary evaluated dead branch");
        bool unknown = false; try { SkillFormula.Evaluate("unsupported", (_, _, _) => throw new NotSupportedException()); } catch (NotSupportedException) { unknown = true; }
        Require(unknown, "Unknown formulas silently succeeded");
        var p = Player(); var target = Enemy(p);
        var released = SkillMechanics.Values(p, CombatAbility.BoneSpear);
        long synergyBefore = released.Eval("EDmgSymPerCalc");
        Require(ClassCreation.Profiles.TryTrain(p.Serial, "necromancer.teeth", out _), "Cannot exercise release rank snapshot");
        Require(released.Eval("EDmgSymPerCalc") == synergyBefore
            && SkillMechanics.Values(p, CombatAbility.BoneSpear).Eval("EDmgSymPerCalc") == synergyBefore + 8,
            "Launched skill values changed when later ranks changed");
        Require(ClassCombat.Available(p, CombatAbility.AmplifyDamage) && ClassCombat.Available(p, CombatAbility.RaiseSkeletalMage)
            && !ClassCombat.Available(p, CombatAbility.GolemMastery) && !ClassCombat.Available(p, CombatAbility.IronGolem), "Availability or blocked/passive gate");
        Require(SkillBindings.Instance.AssignQuick(p, 11, CombatAbility.LowerResist)
            && SkillBindings.Instance.GetQuick(p.Serial, DiabloClass.Necromancer, 11) == CombatAbility.LowerResist, "High ability ID/F12 failed");
        int mana = p.Mana; Require(NecromancerEffects.Pay(p, 896) && p.Mana == mana - 4
            && NecromancerEffects.Profiles.Find(p).ManaFraction == 128, "Fractional cost accounting");
        Require(NecromancerEffects.Pay(p, 896) && p.Mana == mana - 7
            && NecromancerEffects.Profiles.Find(p).ManaFraction == 0, "Fractional cost accumulation");
        NecromancerEffects.BoneArmor(p, Values(CombatAbility.BoneArmor, ("Bone Armor", 1)));
        Require(NecromancerEffects.Armor(p) == 20, "Bone Armor rank1");
        int life = p.Hits; SkillCombat.Hit(target, p, 10, "fire");
        Require(p.Hits < life && NecromancerEffects.Armor(p) == 20, "Elemental damage consumed Bone Armor");
        int afterFire = p.Hits; AOS.Damage(p, target, 30, 100, 0, 0, 0, 0);
        Require(p.Hits == afterFire - 10 && NecromancerEffects.Armor(p) == 0, "Native melee failed physical-only Bone Armor mitigation");
        p.RawStr = p.RawDex = 1000;
        if (p.Backpack == null) { p.AddItem(new Backpack()); }
        var conversion = NamedGear.Catalog.Items.First(d => NamedGear.Available(d.Id) && d.Properties.Any(v => v.Code == "dmg-to-mana"));
        var gear = NamedGear.Create(conversion.Id, 99, new Random(1)); p.Backpack.DropItem(gear);
        Require(p.EquipItem(gear), "Cannot equip damage-to-mana fixture"); ClassGear.Refresh(p);
        NecromancerEffects.BoneArmor(p, Values(CombatAbility.BoneArmor, ("Bone Armor", 1)));
        p.Hits = p.HitsMax; p.Mana = 0; int beforeConversion = p.Hits;
        AOS.Damage(p, target, 80, false, 100, 0, 0, 0, 0, weaponDamage: true);
        Require(p.Mana == (beforeConversion - p.Hits) * ClassGear.Bonus(p, "dmg-to-mana") / 100,
            "Bone Armor generated mana from absorbed damage instead of actual life loss");
        gear.Delete(); ClassGear.Refresh(p);
        Require(NecromancerEffects.Apply(p, target, CombatAbility.AmplifyDamage, 1), "Amplify cast");
        Require(NecromancerEffects.Resistance(target, "physical", 0) == -100 && NecromancerEffects.Resistance(target, "physical", 100) == 80, "Amplify immunity breaking");
        int before = target.Hits; SkillCombat.Hit(p, target, 10, "physical");
        Require(target.Hits == before - 20, "Amplify failed damage consumer");
        Require(NecromancerEffects.Apply(p, target, CombatAbility.Decrepify, 1) && NecromancerEffects.Slow(target) == 50
            && NecromancerEffects.OutgoingPhysicalPercent(target) == 50 && NecromancerEffects.Resistance(target, "physical", 0) == -50, "Decrepify replacement or consumers");
        Require(NecromancerEffects.Apply(p, target, CombatAbility.LowerResist, 1)
            && NecromancerEffects.Resistance(target, "fire", 100) == 94 && NecromancerEffects.Resistance(target, "mag", 0) == 0, "Lower Resist immunity penalty or magic channel");
        Require(NecromancerEffects.Apply(p, target, CombatAbility.LifeTap, 1), "Life Tap cast"); p.Hits = p.HitsMax - 20;
        NecromancerEffects.WeaponHit(p, target, 20, false); Require(p.Hits == p.HitsMax - 10, "Life Tap physical healing");
        Require(NecromancerEffects.Apply(p, target, CombatAbility.IronMaiden, 1), "Iron Maiden fixture");
        target.SetResistance(ResistanceType.Physical, 0); before = target.Hits;
        AOS.Damage(p, target, 20, false, 100, 0, 0, 0, 0, weaponDamage: true);
        Require(target.Hits == before - 40, "Native melee did not return Iron Maiden's physical damage");
        before = target.Hits;
        AOS.Damage(p, target, 20, false, 100, 0, 0, 0, 0, archer: true, weaponDamage: true);
        AOS.Damage(p, target, 20, false, 0, 100, 0, 0, 0, weaponDamage: true);
        Require(target.Hits == before, "Iron Maiden reflected ordinary ranged or elemental damage");
        Require(NecromancerEffects.Apply(p, target, CombatAbility.Attract, 1)
            && !NecromancerEffects.Apply(p, target, CombatAbility.AmplifyDamage, 1), "Attract overwritten by ordinary curse");
        NecromancerEffects.Profiles.Clear(target);
        var expired = new NecromancerCurse(CombatAbility.Weaken, p.Serial.Value, 1, TimedEffects.Now.AddMilliseconds(-1), p.X, p.Y);
        NecromancerEffects.Profiles.Store(target, new(expired)); Require(NecromancerEffects.Curse(target) == null, "Expired curse remained active");
        var corpseOwner = Enemy(p, 2); corpseOwner.Damage(10000, p); if (corpseOwner.Alive) { corpseOwner.Kill(); }
        var corpse = (Corpse)corpseOwner.Corpse;
        Require(SkeletonPets.Usable(p, corpse), "Corpse fixture unusable");
        corpse.SanctuaryRestInPeace = true; Require(!NecromancerCombat.ValidTarget(p, CombatAbility.CorpseExplosion, corpse.Serial, corpse.Location), "Rest in Peace bypass");
        corpse.SanctuaryRestInPeace = false; p.Mana = p.ManaMax;
        Require(NecromancerCombat.Release(p, CombatAbility.RaiseSkeletalMage, corpse.Serial, corpse.Location), "Mage release failed");
        Require(corpse.SanctuaryConsumed && p.AllFollowers.OfType<NecromancerMinion>().Count(x => x.IsMage) == 1
            && SkeletonPets.Count(p) == 0, "Mage corpse or separate cap");
        var summonPoint = CombatPractice.FindFloor(p.Map, p.X - 2, p.Y);
        Require(NecromancerCombat.Release(p, CombatAbility.ClayGolem, Serial.Zero, summonPoint), "Clay Golem release");
        var golem = p.AllFollowers.OfType<NecromancerMinion>().Single(x => !x.IsMage);
        Require(golem.CombatProfile.Life == 125 && golem.CombatProfile.MinDamage == 2, "Clay Golem mastery/synergy base calculation");
        int oldMana = p.Mana;
        Require(!NecromancerCombat.Begin(p, CombatAbility.CorpseExplosion, Serial.Zero, p.Location, Core.TickCount)
            && p.Mana == oldMana, "Invalid corpse spent resources");
        p.Frozen = true; Require(!NecromancerCombat.Begin(p, CombatAbility.BoneArmor, Serial.Zero, p.Location, Core.TickCount), "Frozen caster acted"); p.Frozen = false;
        p.Mana = p.ManaMax; var session = ClassCombat.For(p); session.NextAction = Core.TickCount;
        Require(NecromancerCombat.Begin(p, CombatAbility.BoneArmor, Serial.Zero, p.Location, Core.TickCount), "Armor begin");
        p.Location = new Point3D(p.X + 1, p.Y, p.Z); int beforeCancel = p.Mana;
        Require(!ClassCombat.ResolveAction(p, session.EffectAt) && p.Mana == beforeCancel && session.PendingAbility == CombatAbility.None, "Movement canceled cast incorrectly");
        NecromancerEffects.BoneArmor(p, Values(CombatAbility.BoneArmor, ("Bone Armor", 1), ("Bone Wall", 20), ("Bone Prison", 20)));
        Require(NecromancerEffects.Armor(p) == 620, "Bone Armor synergy scaling");
        target.Combatant = p; target.AIObject.Action = ActionType.Combat;
        Require(NecromancerEffects.Apply(p, target, CombatAbility.DimVision, 1), "Persistent blind fixture");
        var savedBlind = NecromancerEffects.Curse(target);
        NecromancerEffects.Profiles.Store(target, new(savedBlind with { UntilUtc = TimedEffects.Now.AddHours(1) }));
        File.WriteAllText(Manifest, JsonSerializer.Serialize(new Saved(p.Serial.Value, target.Serial.Value, 620,
            golem.Serial.Value, golem.CombatProfile.Life, golem.CombatProfile.MinDamage, (byte)CombatAbility.LowerResist, p.Serial.Value)));
        Sorceress();
        SummonMechanics();
        _log.Information("Necromancer fixture: numeric tiers, hard/soft ranks, mana fractions, native damage, immunity, curse replacement, Life Tap, corpse guards, summons, bindings and cancellation passed");
    }
    internal static Point3D Corridor(PlayerMobile player, int minX = 5480, int minY = 1170)
    {
        for (int x = minX; x < minX + 50; x++)
        {
            for (int y = minY; y < minY + 50; y++)
            {
                var test = new Point3D(x, y, player.Map.GetAverageZ(x, y));
                if (Enumerable.Range(0, 6).All(i => player.Map.CanSpawnMobile(x + i, y, test.Z))
                    && player.Map.LineOfSight(new Point3D(x, y, test.Z + 12), new Point3D(x + 5, y, test.Z + 12))) { return test; }
            }
        }
        throw new InvalidOperationException("No clear class skill corridor");
    }
    private static void CurseAi()
    {
        var owner = Player(); var origin = Corridor(owner);
        owner.MoveToWorld(new Point3D(origin.X + 5, origin.Y, origin.Z), owner.Map);
        var enemy = Enemy(owner, -5); enemy.Frozen = false; enemy.CantWalk = false;
        var blocker = new Blocker();
        try
        {
            enemy.Combatant = owner; enemy.AIObject.Action = ActionType.Combat; enemy.AIObject.NextMove = Core.TickCount;
            Require(NecromancerEffects.Apply(owner, enemy, CombatAbility.DimVision, 1)
                && NecromancerEffects.Curse(enemy).Motion?.Target == owner.Serial.Value, "Dim Vision discarded an existing chase");
            Require(NecromancerEffects.Think(enemy) && enemy.X == origin.X + 1 && enemy.Y == origin.Y,
                "Dim Vision failed to continue an existing chase");
            Require(!NecromancerEffects.TargetAllowed(enemy, owner) && !NamedControl.SpecialAllowed(enemy), "Blinded monster used a distant/special attack");
            blocker.MoveToWorld(new Point3D(origin.X + 2, origin.Y, origin.Z), owner.Map);
            enemy.AIObject.NextMove = Core.TickCount;
            Require(NecromancerEffects.Think(enemy) && enemy.X == origin.X + 1 && enemy.Y == origin.Y
                && NecromancerEffects.Curse(enemy).Motion == null && enemy.Combatant == null,
                "Dim Vision chose a new route around an obstruction");
            blocker.Delete(); enemy.AIObject.AITimer.Stop();
            Timer.StartTimer(TimeSpan.FromMilliseconds(350), () =>
            {
                try
                {
                    owner.MoveToWorld(new Point3D(origin.X + 2, origin.Y, origin.Z), owner.Map);
                    enemy.NextReacquireTime = Core.TickCount;
                    Require(NecromancerEffects.Think(enemy) && enemy.Combatant == owner
                        && NecromancerEffects.TargetAllowed(enemy, owner) && NamedControl.Blind(enemy),
                        $"Blinded adjacent acquisition: {enemy.Location}->{owner.Location}, enemy={enemy.IsEnemy(owner)}, visible={enemy.CanSee(owner)}, harmful={enemy.CanBeHarmful(owner, false)}, los={enemy.InLOS(owner)}, combatant={enemy.Combatant?.Serial}, focus={enemy.FocusMob?.Serial}, returning={enemy.ReturningHome}");
                    var curse = NecromancerEffects.Curse(enemy);
                    NecromancerEffects.Profiles.Store(enemy, new(curse with { UntilUtc = TimedEffects.Now.AddMilliseconds(-1) }));
                    NecromancerEffects.Think(enemy);
                    Require(NecromancerEffects.Profiles.Find(enemy).Curse == null && NamedControl.SpecialAllowed(enemy), "Expired AI curse did not release its control state");
                    foreach (var difficulty in Enum.GetValues<MonsterDifficulty>())
                    {
                        var variant = new GraveyardSkeleton { Frozen = true, CantWalk = true, NoKillAwards = true };
                        try
                        {
                            MonsterVariants.Assign(variant, difficulty);
                            variant.AIObject.AITimer.Stop(); variant.MoveToWorld(new Point3D(owner.X + 1, owner.Y, owner.Z), owner.Map);
                            var start = TimedEffects.Now;
                            Require(NecromancerEffects.Apply(owner, variant, CombatAbility.Confuse, 1), "Confuse difficulty fixture");
                            double milliseconds = (NecromancerEffects.Curse(variant).UntilUtc - start).TotalMilliseconds;
                            Require(Math.Abs(milliseconds - 10000 / (difficulty == MonsterDifficulty.Hell ? 4 : difficulty == MonsterDifficulty.Nightmare ? 2 : 1)) < 100,
                                "AI curse source difficulty duration");
                        }
                        finally { variant.Delete(); }
                    }
                    NecromancerEffects.Profiles.Clear(enemy);
                    Require(NecromancerEffects.Apply(owner, enemy, CombatAbility.Terror, 1)
                        && NecromancerEffects.Apply(owner, enemy, CombatAbility.DimVision, 1)
                        && NecromancerEffects.Curse(enemy).Motion is { Flee: true }, "Dim Vision discarded pre-existing flee motion");
                    _log.Information("Necromancer curses: Dim Vision chase, obstruction, adjacent acquisition, special suppression, expiration, difficulty durations and preserved flee motion passed");
                    _log.Information("class-skills-ai-result: passed");
                }
                catch (Exception e) { _log.Error(e, "class-skills-ai-result: failed"); }
                finally { blocker.Delete(); enemy.Delete(); owner.Delete(); }
            });
        }
        catch { blocker.Delete(); enemy.Delete(); owner.Delete(); throw; }
    }
    private static void SummonMechanics()
    {
        var owner = Player();
        var drained = new GraveyardZombie { Frozen = true, CantWalk = true, NoKillAwards = true };
        drained.AIObject?.AITimer.Stop(); drained.MoveToWorld(CombatPractice.FindFloor(owner.Map, owner.X + 2, owner.Y), owner.Map);
        var immune = Enemy(owner);
        NecromancerMinion blood = null, fire = null;
        try
        {
            Require(GemCombat.Drain(drained) == 100 && GemCombat.Drain(immune) == 0, "Blood drain fixtures");
            Require(NecromancerMinion.BloodHealingBudget(200, 30, 50, 86) == 25
                && NecromancerMinion.BloodHealingBudget(40, 500, 50, 86) == 17,
                "Blood Golem capped life before applying target drain effectiveness");
            var mage = new NecromancerMinion();
            try
            {
                mage.Prepare(owner, CombatAbility.RaiseSkeletalMage);
                string source = SkillMechanics.For(CombatAbility.RaiseSkeletalMage).Text("summon");
                Require(mage.CombatProfile.MoveMs == 720 * 8 / NecromancerSummons.Source(source, "Velocity"),
                    "Golem Mastery changed a skeletal mage's movement");
            }
            finally { mage.Delete(); }
            blood = new NecromancerMinion(); blood.Prepare(owner, CombatAbility.BloodGolem);
            Require(blood.SetControlMaster(owner), "Blood owner"); blood.MoveToWorld(owner.Location, owner.Map);
            owner.Hits = owner.HitsMax - 100; blood.Hits = blood.HitsMax - 100;
            int a = owner.Hits, b = blood.Hits;
            blood.HealFromBloodHit(drained, 100);
            Require(owner.Hits - a == 25 && blood.Hits - b == 61, "Blood Golem did not split its 86 healing budget 30/70");
            owner.Hits = owner.HitsMax; blood.Hits = blood.HitsMax - 100; b = blood.Hits;
            blood.HealFromBloodHit(drained, 100);
            Require(blood.Hits - b == 86, "Full owner's unused healing did not return to golem");
            owner.Hits = owner.HitsMax - 100; blood.Hits = blood.HitsMax; a = owner.Hits;
            blood.HealFromBloodHit(drained, 100);
            Require(owner.Hits - a == 86, "Full golem's unused healing did not reach owner");
            owner.Hits = owner.HitsMax - 10; blood.Hits = blood.HitsMax - 10;
            blood.HealFromBloodHit(drained, 100);
            Require(owner.Hits == owner.HitsMax && blood.Hits == blood.HitsMax, "Blood overflow exceeded recipient maxima");
            owner.Hits--; blood.Hits--; a = owner.Hits; b = blood.Hits;
            blood.HealFromBloodHit(immune, 100);
            Require(owner.Hits == a && blood.Hits == b, "Blood Golem healed from zero-drain undead");
            immune.SetHits(30); immune.Hits = 30; immune.SetResistance(ResistanceType.Physical, 0);
            var overkill = SkillCombat.ResolveHitParts(blood, immune, new(200, 0, 0, 0, 0), false);
            Require(overkill.LifeLost == 30 && overkill.Physical == 200 && overkill.LifeBefore == 30,
                "Shared damage result lost pre-life-cap physical damage required by Blood Golem");
            blood.Delete();
            owner.Mana = owner.ManaMax;
            var point = CombatPractice.FindFloor(owner.Map, owner.X + 1, owner.Y);
            Require(NecromancerCombat.Release(owner, CombatAbility.FireGolem, Serial.Zero, point), "Fire Golem failed source passive selection");
            fire = owner.AllFollowers.OfType<NecromancerMinion>().Single(x => !x.IsMage);
            Require(fire.CombatProfile.Defense == NecromancerSummons.Source("FireGolem", "AC") + 35
                + NecromancerSummons.Source("level-" + fire.CombatProfile.Level, "L-AC"), "Fire Golem read pierce formula as armor");
            drained.SetHits(5000); drained.Hits = drained.HitsMax; drained.SetResistance(ResistanceType.Physical, 0); drained.SetResistance(ResistanceType.Fire, 0);
            int before = drained.Hits;
            owner.Mana = owner.ManaMax;
            var replacementPoint = CombatPractice.FindFloor(owner.Map, owner.X - 1, owner.Y);
            Require(NecromancerCombat.Release(owner, CombatAbility.ClayGolem, Serial.Zero, replacementPoint), "Replacement golem");
            Require(fire.Deleted && drained.Hits == before - 200, "Replacing Fire Golem skipped source death explosion");
            _log.Information("Necromancer summons: Blood Golem drain, healing split, both overflow directions, caps, Fire Golem source passives and replacement explosion passed");
        }
        finally
        {
            foreach (var follower in owner.AllFollowers?.ToArray() ?? []) { follower.Delete(); }
            blood?.Delete(); fire?.Delete(); drained.Delete(); immune.Delete(); owner.Delete();
        }
    }
    private static void Sorceress()
    {
        var owner = Player(DiabloClass.Sorceress); var target = Enemy(owner, 2);
        try
        {
            Require(SorceressCombat.Passive(owner, CombatAbility.Warmth) == 30
                && SorceressCombat.Passive(owner, CombatAbility.FireMastery) == 30
                && SorceressCombat.Passive(owner, CombatAbility.LightningMastery) == 50, "Sorceress passive ranks");
            target.SetResistance(ResistanceType.Fire, 0); target.SetResistance(ResistanceType.Energy, 0);
            Require(NamedElements.Damage(owner, target, 10, "fire") == 13, "Fire Mastery damage consumer");
            Require(NamedElements.Resistance(owner, target, "cold", 20) == 0
                && NamedElements.Resistance(owner, target, "cold", 100) == 100, "Cold Mastery immunity rule");
            int before = target.Hits;
            Require(SorceressCombat.Release(owner, CombatAbility.StaticField, Serial.Zero, owner.Location), "Static Field release");
            Require(target.Hits == before * 3 / 4, "Static Field current-life fraction or mastery exclusion");
            int nativeBefore = target.Hits;
            AOS.Damage(target, owner, 10, false, 0, 100, 0, 0, 0, spellDamage: true);
            Require(target.Hits == nativeBefore - 13, "Native typed spell bypassed Fire Mastery");
            var beforeRate = GearEffects.RegenerationRate(owner, TimeSpan.FromSeconds(2), "mana-per-five");
            Require(beforeRate < TimeSpan.FromSeconds(2), "Warmth did not reach regeneration");
            Require(NamedControl.FreezeSkill(owner, target, 3000) && NamedControl.FrozenRemaining(target) > 2500, "Spell freeze did not reach control state");
            _log.Information("Sorceress fixture: learned passives, native/direct damage, immunity, current-life Static Field, regeneration and freeze passed");
        }
        finally { owner.Delete(); target.Delete(); }
    }
    private static void Reload()
    {
        var saved = JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));
        var p = (PlayerMobile)World.FindMobile((Serial)saved.Owner);
        Require(p != null && NecromancerEffects.Armor(p) == saved.Armor, "Saved Bone Armor lost");
        var target = World.FindMobile((Serial)saved.Target);
        Require(NecromancerEffects.Curse(target) is { Ability: CombatAbility.DimVision, Motion: { } motion }
            && motion.Target == saved.MotionTarget && !motion.Flee, "Saved Dim Vision chase decision lost");
        Require(SkillBindings.Instance.GetQuick(p.Serial, DiabloClass.Necromancer, 11) == (CombatAbility)saved.Ability, "High-ID F12 lost on reload");
        var minion = World.FindMobile((Serial)saved.Minion) as NecromancerMinion;
        Require(minion is { Deleted: false } && minion.ControlMaster == p && minion.CombatProfile.Life == saved.Life
            && minion.CombatProfile.MinDamage == saved.Damage && minion.HitsMax == saved.Life, "Saved minion identity/derived stats lost");
    }
    private static void Projectiles()
    {
        var player = Player();
        Point3D origin = default; bool found = false;
        for (int x = 5480; x < 5530 && !found; x++)
        {
            for (int y = 1170; y < 1220 && !found; y++)
            {
                var test = new Point3D(x, y, player.Map.GetAverageZ(x, y));
                if (Enumerable.Range(0, 6).All(i => player.Map.CanSpawnMobile(x + i, y, test.Z))
                    && player.Map.LineOfSight(new Point3D(x, y, test.Z + 12), new Point3D(x + 5, y, test.Z + 12)))
                { origin = test; found = true; }
            }
        }
        Require(found, "No clear projectile corridor"); player.MoveToWorld(origin, player.Map);
        var first = Enemy(player, 2); var second = Enemy(player, 4);
        Require(first.X == origin.X + 2 && first.Y == origin.Y && second.X == origin.X + 4 && second.Y == origin.Y, "Projectile fixture is not collinear");
        player.Mana = player.ManaMax;
        Require(NecromancerCombat.Release(player, CombatAbility.BoneSpear, first.Serial, first.Location), "Bone Spear release");
        int a = first.Hits, b = second.Hits;
        Timer.StartTimer(TimeSpan.FromSeconds(2), () =>
        {
            try
            {
                Require(first.Hits < a && second.Hits < b, $"Travelling Bone Spear failed: {a}->{first.Hits}, {b}->{second.Hits}; {player.Location}->{first.Location}->{second.Location}; LOS={player.InLOS(first)}/{player.InLOS(second)}");
                _log.Information("class-skills-projectiles-result: passed");
            }
            catch (Exception e) { _log.Error(e, "class-skills-projectiles-result: failed"); }
            finally { player.Delete(); first.Delete(); second.Delete(); }
        });
    }
}
