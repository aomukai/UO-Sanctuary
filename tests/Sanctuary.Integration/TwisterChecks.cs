// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using Server.Network;
using Server.Regions;

namespace Sanctuary.Integration;

public static class TwisterChecks
{
    private static readonly ILogger Log = LogFactory.GetLogger(typeof(TwisterChecks));
    private sealed record Saved(uint Owner, uint Target, int Life256, TwisterSave State, CombatStunState Stun, uint Pending, int PendingMana);
    private static string Manifest => Path.Join(Core.BaseDirectory, "twister-fixtures.json");
    private static int _scene;
    private static bool _creating;
    private static uint _pending;
    private static int _pendingMana;
    [CallPriority(int.MaxValue)] public static void Initialize()
    {
        var old = Mobile.ManaRegenRateHandler;
        Mobile.ManaRegenRateHandler = p => _creating || p.Name?.StartsWith("TwisterFixture", StringComparison.Ordinal) == true
            ? TimeSpan.FromHours(1) : old?.Invoke(p) ?? Mobile.DefaultManaRate;
    }
    public static void Configure()
    {
        Register("twister-check", Check); Register("twister-reload-check", Reload);
        Register("twister-live-check", () => FixtureNetwork.Connected("twister-live-result", n => Live(n, false)));
        Register("twister-input-check", () => FixtureNetwork.Connected("twister-input-result", n => Live(n, true)));
    }
    private static void Register(string name, Action action) => ConsoleInputHandler.RegisterCommand(name, "Twister mechanics and persistence", _ => Core.LoopContext.Post(() =>
    { try { action(); Log.Information("{Name}: passed", name); } catch (Exception e) { Log.Error(e, "{Name}: failed", name); } }));
    private static void Require(bool value, string why) { if (!value) { throw new InvalidOperationException(why); } }
    private static string Text(TwisterSave saved = null) => JsonSerializer.Serialize(saved ?? DruidTwister.Snapshot());
    private static PlayerMobile Fresh()
    {
        PlayerMobile p; _creating = true;
        try { p = new PlayerMobile { Body = 0x190, Player = true, Name = "TwisterFixture" }; } finally { _creating = false; }
        p.AddItem(new Backpack()); ClassCreation.Profiles.Assign(p, DiabloClass.Druid); ClassCreation.Profiles.AdvanceLevel(p, 99);
        FixtureSpace.Corridor(p, 7100, 35, 1500 + 32 * _scene++); return p;
    }
    private static void Train(PlayerMobile p, CombatAbility ability, int rank = 1)
    {
        var row = SkillMechanics.For(ability); Require(TalentCatalog.TryGet(DiabloClass.Druid, row.TalentId, out var talent), "Druid talent");
        foreach (string parent in talent.Prerequisites) { Train(p, SkillMechanics.All.Single(x => x.TalentId == parent).Ability); }
        while (ClassCreation.Profiles.GetRank(p.Serial, row.TalentId) < rank) { Require(ClassCreation.Profiles.TryTrain(p.Serial, row.TalentId, out _), "Train " + row.TalentId); }
    }
    private static GraveyardSkeleton Enemy(PlayerMobile p, int dx = 1)
    {
        var target = new GraveyardSkeleton { Name = "TwisterFixtureTarget", CantWalk = true, NoKillAwards = true }; target.SetHits(10000);
        foreach (var kind in new[] { ResistanceType.Physical, ResistanceType.Fire, ResistanceType.Cold, ResistanceType.Energy, ResistanceType.Poison }) { target.SetResistance(kind, 0); }
        target.MoveToWorld(new(p.X + dx, p.Y, p.Z), p.Map); target.AIObject.AITimer.Stop();
        Shield(target, 0); return target;
    }
    private static void Shield(BaseCreature target, int choice) => SourceBlocking.Restore(SourceBlocking.Snapshot().Where(x => x.Actor != target.Serial.Value)
        .Append(new SourceShield(target.Serial.Value, CreatureProfiles.Definition(target).Reference, choice)).ToArray());
    private static void Ready(PlayerMobile p)
    { ClassCombat.Stop(p); ClassCombat.For(p).NextAction = ClassCombat.For(p).RecoveryUntil = Core.TickCount; p.Frozen = p.Paralyzed = false; }
    private static void Mana(PlayerMobile p) => NecromancerEffects.SetMana256(p, p.ManaMax * 256);
    private static bool Start(PlayerMobile p, Mobile target = null, Point3D? point = null)
    { bool result = ClassCombat.Perform(p, CombatAbility.Twister, target, point ?? target?.Location ?? new Point3D(p.X + 6, p.Y, p.Z), Core.TickCount); ClassCombat.ReleaseCast(p); return result; }
    private static TwisterMissile One(PlayerMobile p, DateTime at, int raw = 1536)
    {
        DruidTwister.Create(p, new(p.X + 6, p.Y, p.Z), SkillMechanics.Values(p, CombatAbility.Twister), at);
        var first = DruidTwister.Snapshot().Missiles.First(); var fixedDamage = first with { Min256 = raw, Max256 = raw, Seed = SourceRandom.InitLow(1), Modifiers = new(0, 0, 0, 0, 0) };
        DruidTwister.Restore(new([fixedDamage], [], fixedDamage.Id)); return fixedDamage;
    }
    private static void Source()
    {
        using var damage = JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory, "twister-damage-source-probe.json"))); int count = 0;
        foreach (var row in damage.RootElement.EnumerateArray())
        {
            int rank = row[1].GetInt32(), tornado = row[2].GetInt32(), hurricane = row[3].GetInt32();
            var values = new SkillValues(SkillMechanics.For(CombatAbility.Twister), key => key == "Twister" ? rank : 255,
                key => key == "Tornado" ? tornado : key == "Hurricane" ? hurricane : 0, 99);
            Require(values.PhysicalDamage256(false) == row[6].GetInt32() && values.PhysicalDamage256(true) == row[7].GetInt32()
                && values.Mana256 == 1792 && values.Eval("calc1") == 3, "Twister source endpoints/count/mana"); count++;
        }
        Require(count == 9180, "Incomplete source endpoints");
        using var paths = JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory, "twister-path-source-probe.json"))); int pathCount = 0;
        foreach (var row in paths.RootElement.EnumerateArray())
        {
            int origin = row[0].GetInt32(), dx = row[1].GetInt32(), dy = row[2].GetInt32(), ordinal = row[3].GetInt32();
            var init = DruidEmitterPatterns.InitializeCharged(50, origin + dx, ordinal); int heading = ChargedBolts.Heading(dx, dy);
            Require(init.Frames == row[4].GetInt32() && row[5].GetInt32() == 50 && heading == row[6].GetInt32(), "Complete source charged initializer/heading");
            var actual = ChargedBolts.Path(init.Seed, heading, init.Distance); var expected = row[8].EnumerateArray().ToArray();
            Require(actual.Length == expected.Length && actual.Length == 26, "Complete source path length");
            for (int i = 0; i < actual.Length; i++)
            {
                int x = unchecked((short)(ushort)(expected[i][0].GetInt32() - origin)), y = unchecked((short)(ushort)(expected[i][1].GetInt32() - origin));
                Require(actual[i].X * 2 == x && actual[i].Y * 2 == y, "Complete source path coordinates/wrap");
            }
            ulong seed = SourceRandom.InitLow(unchecked((int)init.Seed)); for (int i = 1; i < actual.Length; i++) { SourceRandom.Roll(ref seed); }
            Require(seed == row[7].GetUInt64(), "Complete source initializer/path/final RNG"); pathCount++;
        }
        Require(pathCount == 600, "Incomplete source charged path scope");
        using var impact = JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory, "twister-impact-source-probe.json"))); count = 0;
        foreach (var row in impact.RootElement.EnumerateArray())
        {
            int duration = row[0].GetInt32(), grade = row[1].GetInt32(), defense = row[4].GetInt32(), resistance = row[5].GetInt32();
            bool existing = row[2].GetInt32() != 0, guard = row[3].GetInt32() != 0, kb = row[6].GetInt32() != 0; uint low = row[7].GetUInt32();
            var flags = DruidTwister.ReactionFlags(guard, SourceRandom.InitLow(unchecked((int)low)));
            var plan = MoltenBoulderPackets.ResolveImpact(BoulderImpactKind.Rolling, flags, defense, kb, guard);
            Require(plan.Flags == row[12].GetInt32() && plan.Seed == row[10].GetUInt64(), "Complete source reaction flags/RNG");
            // The source hireling branch caps physical resistance at50 here.
            // There is no native hireling actor certificate in this fixture;
            // native ordinary-monster health is checked separately below.
            int tracedLoss = !plan.Damage ? 0 : resistance == 0 ? 1536 : grade == 4 ? 768 : 0;
            Require(25600 - tracedLoss == row[8].GetInt32(), "Complete source health guard");
            ulong ownerSeed = SourceRandom.InitLow(unchecked((int)low)); int roll = plan.Damage && duration > 0 && grade == 1 ? (int)SourceRandom.Limited(ref ownerSeed, 100) : 0;
            int frames = CombatStuns.Frames(duration, true, grade == 1, grade == 2, grade == 3 ? 0 : 6, grade == 4, roll);
            bool applied = plan.Damage && frames > 0;
            Require(ownerSeed == row[11].GetUInt64() && (applied || existing) == (row[13].GetInt32() != 0)
                && (applied ? 1000 + frames : existing ? 1999 : 0) == row[14].GetInt32()
                && (applied && !existing ? 1 : 0) == row[15].GetInt32() && (applied ? 1 : 0) == row[16].GetInt32(), "Complete source stun/refresh/cap/RNG");
            int mode = guard ? -1 : plan.Push ? 13 : (plan.Flags & 0x10) != 0 ? 6 : (plan.Flags & 4) != 0 && (existing || applied) ? 3 : -1;
            Require(mode == row[9].GetInt32(), "Complete source mode priority"); count++;
        }
        Require(count == 11200, "Incomplete source impact scope"); Log.Information("Twister:9180source endpoints/600complete paths/11200flag-health-stun-mode controls pass");
    }
    private static void Casting()
    {
        var p = Fresh(); Require(!ClassCombat.Available(p, CombatAbility.Twister), "Untrained Twister available"); Train(p, CombatAbility.Twister); Mana(p);
        Require(SkillBindings.Instance.Assign(p, true, CombatAbility.Twister) && SkillBindings.Instance.Assign(p, false, CombatAbility.Twister)
            && SkillBindings.Instance.AssignQuick(p, 7, CombatAbility.Twister), "Twister L/R/quick bindings");
        Ready(p); NecromancerEffects.SetMana256(p, 1791); Require(!Start(p) && NecromancerEffects.ManaBalance256(p) == 1791, "Low mana began Twister"); Mana(p);
        Ready(p); int mana = NecromancerEffects.ManaBalance256(p); Require(Start(p), "SC windup"); long at = ClassCombat.For(p).EffectAt;
        Require(ClassCombat.For(p).Animation == 16 && !ClassCombat.ResolveAction(p, at - 1) && NecromancerEffects.ManaBalance256(p) == mana, "Early SC/payment"); ClassCombat.Stop(p);
        Require(!ClassCombat.ResolveAction(p, at) && NecromancerEffects.ManaBalance256(p) == mana, "Stop cancellation");
        Ready(p); Require(Start(p), "Move windup"); at = ClassCombat.For(p).EffectAt; var origin = p.Location; p.Direction = Direction.East; Require(p.Move(Direction.East), "Outward fixture step"); p.Direction = Direction.West;
        Require(p.Move(Direction.West) && p.Location == origin && !ClassCombat.ResolveAction(p, at) && NecromancerEffects.ManaBalance256(p) == mana, "Out/back revived cast");
        Ready(p); Require(Start(p), "Recovery windup"); at = ClassCombat.For(p).EffectAt; Require(HitRecovery.Apply(p, p.HitsMax, Core.TickCount) && !ClassCombat.ResolveAction(p, at), "Recovery cancellation");
        Ready(p); Require(Start(p), "Lost-mana windup"); at = ClassCombat.For(p).EffectAt; NecromancerEffects.SetMana256(p, 0); string before = Text();
        Require(!ClassCombat.ResolveAction(p, at) && Text() == before, "Lost mana emitted missiles"); Mana(p); Ready(p); mana = NecromancerEffects.ManaBalance256(p);
        Require(Start(p), "Ground windup"); at = ClassCombat.For(p).EffectAt;
        Require(ClassCombat.ResolveAction(p, at) && !ClassCombat.ResolveAction(p, at) && NecromancerEffects.ManaBalance256(p) == mana - 1792
            && DruidTwister.Snapshot().Missiles.Length == 3 && FireGround.Ready(p, CombatAbility.Twister), "Once-only7mana/three missiles/no local delay"); DruidTwister.Cancel(p);
        Ready(p); Mana(p); var target = Enemy(p, 6); Require(Start(p, target), "Live target windup"); at = ClassCombat.For(p).EffectAt; target.MoveToWorld(new(p.X, p.Y + 2, p.Z), p.Map);
        Require(ClassCombat.ResolveAction(p, at), "Live target release"); var live = DruidTwister.Snapshot().Missiles.First(); var expected = ChargedBolts.Path(ChargedBolts.SeedSubtile(target.X * 2, 0), ChargedBolts.Heading(0, 4), 50);
        Require(live.Path.SequenceEqual(expected), "Live target aim/path"); DruidTwister.Cancel(p); target.Delete();
        Ready(p); Mana(p); target = Enemy(p, 6); Require(Start(p, target), "Deleted target windup"); at = ClassCombat.For(p).EffectAt; target.Delete(); Require(ClassCombat.ResolveAction(p, at), "Deleted-target cached ground fallback"); DruidTwister.Cancel(p);
        Ready(p); Mana(p); Require(Start(p, point: p.Location), "Self-ground windup"); Require(ClassCombat.ResolveAction(p, ClassCombat.For(p).EffectAt), "Self-ground fallback"); DruidTwister.Cancel(p);
        var town = new GuardedRegion("TwisterFixtureTown", p.Map, 1000, new Rectangle2D(p.X - 1, p.Y - 1, 3, 3)); town.Register();
        try { Ready(p); Require(!Start(p), "Twister began in town"); } finally { town.Unregister(); }
        p.Delete();
    }
    private static void Prepared()
    {
        var p = Fresh(); Train(p, CombatAbility.Twister); var at = TimedEffects.Now.AddDays(1); Mana(p); int mana = NecromancerEffects.ManaBalance256(p);
        DruidTwister.Create(p, new(p.X + 6, p.Y, p.Z), SkillMechanics.Values(p, CombatAbility.Twister), at); var saved = DruidTwister.Snapshot();
        Require(saved.Missiles.Length == 3 && saved.Missiles.Select(x => x.Cast).Distinct().Count() == 1, "Charged triplet cast identity");
        for (int i = 0; i < 3; i++)
        {
            var shot = saved.Missiles[i]; var path = ChargedBolts.Path(ChargedBolts.SeedSubtile((p.X + 6) * 2, i), 0, 50); ulong seed = SourceRandom.InitLow((p.X + 6) * 2 + i);
            for (int n = 1; n < path.Length; n++) { SourceRandom.Roll(ref seed); }
            Require(shot.Path.SequenceEqual(path) && shot.Seed == seed && shot.Frame == 0 && shot.Velocity256 == 1920
                && shot.NextUtc == at.AddMilliseconds(40) && shot.UntilUtc == at.AddMilliseconds(2000), "Path/RNG/velocity/clock capture");
        }
        var ends = saved.Missiles.Select(x => (x.Min256, x.Max256)).ToArray(); Train(p, CombatAbility.Tornado); Train(p, CombatAbility.ArcticBlast, 20);
        DruidTwister.Tick(at.AddMilliseconds(40), true);
        Require(DruidTwister.Snapshot().Missiles.Select(x => (x.Min256, x.Max256)).SequenceEqual(ends), "Live synergy rewrote released damage");
        Require(DruidTwister.Snapshot().Missiles.All(x => x.Frame == 1 && x.X != x.OriginX), "Actual movement failed"); DruidTwister.Tick(at.AddMilliseconds(2000), true);
        Require(DruidTwister.Snapshot().Missiles.Length == 0 && NecromancerEffects.ManaBalance256(p) == mana, "Prepared lifetime/payment");
        var target = Enemy(p); var one = One(p, at.AddSeconds(3)); int hp = SkillHealth.Balance256(target); DruidTwister.Tick(one.NextUtc, true);
        Require(hp - SkillHealth.Balance256(target) == 1536 && DruidTwister.Snapshot().Leases.Length == 1, "Direct physical source packet/lease");
        var stun = CombatStuns.Snapshot().Single(x => x.Target == target.Serial.Value); Require(stun.UntilUtc == one.NextUtc.AddMilliseconds(50 * 40), "Live hard Arctic Blast duration");
        var steps = new SourceStepTrace([new(target.X * 2, target.Y * 2)]); var shotNow = DruidTwister.Snapshot().Missiles.Single();
        Require(DruidTwister.Recipient(p, shotNow with { NextUtc = one.NextUtc.AddMilliseconds(999) }, steps) == null
            && DruidTwister.Recipient(p, shotNow with { NextUtc = one.NextUtc.AddMilliseconds(1000) }, steps) == target, "25frame boundary/no LastCollide suppression");
        CombatStuns.Clear(target); DruidTwister.Cancel(p); target.SetResistance(ResistanceType.Physical, 100); one = One(p, at.AddSeconds(7)); hp = SkillHealth.Balance256(target); DruidTwister.Tick(one.NextUtc, true);
        Require(SkillHealth.Balance256(target) == hp && CombatStuns.Snapshot().Any(x => x.Target == target.Serial.Value), "Physical immunity lost stun");
        CombatStuns.Clear(target); DruidTwister.Cancel(p); target.SetResistance(ResistanceType.Physical, 0); Shield(target, 1); var block = SourceBlocking.BlockRoll;
        try { SourceBlocking.BlockRoll = _ => 0; one = One(p, at.AddSeconds(11)); hp = SkillHealth.Balance256(target); DruidTwister.Tick(one.NextUtc, true); Require(SkillHealth.Balance256(target) == hp && !CombatStuns.Snapshot().Any(x => x.Target == target.Serial.Value), "Blocked hit damaged/stunned"); }
        finally { SourceBlocking.BlockRoll = block; }
        DruidTwister.Cancel(p); CombatStuns.Clear(target); target.Delete(); p.Delete();
    }
    private static void GearPotionAndObstacles()
    {
        var p = Fresh(); Train(p, CombatAbility.Twister); var at = TimedEffects.Now.AddDays(1);
        var helm = NamedGear.Create(NamedGear.Catalog.Items.Single(x => x.Name == "Tarnhelm").Id, 99, new Random(173), true);
        Require(p.EquipItem(helm), "Actual Tarnhelm"); ClassGear.Refresh(p);
        Require(SkillMechanics.Rank(p, CombatAbility.Twister) == 2, "Actual Twister soft rank");
        var values = SkillMechanics.Values(p, CombatAbility.Twister); DruidTwister.Create(p, new(p.X + 6, p.Y, p.Z), values, at);
        Require(DruidTwister.Snapshot().Missiles.All(x => x.Rank == 2 && x.Min256 == values.PhysicalDamage256(false) && x.Max256 == values.PhysicalDamage256(true)), "Actual gear capture"); DruidTwister.Cancel(p);
        var slow = AmazonSlowMissiles.Snapshot();
        try
        {
            AmazonSlowMissiles.Restore(slow.Append(new SlowMissilesState(p.Serial.Value, p.Serial.Value, 1, 50, 50, TimedEffects.Now.AddDays(2))).ToArray());
            DruidTwister.Create(p, new(p.X + 6, p.Y, p.Z), values, at);
            Require(DruidTwister.Snapshot().Missiles.All(x => x.Velocity256 == 960 && x.Min256 == values.PhysicalDamage256(false) && x.Max256 == values.PhysicalDamage256(true)), "Slow capture/physical endpoints");
        }
        finally { AmazonSlowMissiles.Restore(slow); DruidTwister.Cancel(p); }
        var target = Enemy(p); target.SetResistance(ResistanceType.Physical, 100); int hp = SkillHealth.Balance256(target);
        Require(CombatPotions.Apply(p, 3), "Actual spell potion"); var fixedHit = One(p, at.AddSeconds(3), 2560) with { Modifiers = DruidFirestorm.Modifiers(p) };
        Require(fixedHit.Modifiers.Potion == 10, "Tier3 potion magnitude"); DruidTwister.Restore(new([fixedHit], [], fixedHit.Id)); DruidTwister.Tick(fixedHit.NextUtc, true);
        int extra = new[] { fixedHit.Modifiers.FireMastery, fixedHit.Modifiers.ColdMastery, fixedHit.Modifiers.LightningMastery, fixedHit.Modifiers.PoisonMastery }.Sum(x => (int)(256L * Math.Max(0, 100 + x) / 100));
        Require(hp - SkillHealth.Balance256(target) == extra && CombatStuns.Remaining(target) > 0, "Approved potion channels/physical immunity/stun");
        DruidTwister.Cancel(p); CombatStuns.Clear(target); target.Delete(); p.Delete();
        p = Fresh(); Train(p, CombatAbility.Twister); target = Enemy(p); Shield(target, 1); target.CantWalk = target.Frozen = false;
        var block = SourceBlocking.BlockRoll;
        try
        {
            SourceBlocking.BlockRoll = _ => 0; ulong seed = 0;
            for (int low = 0; low < 10000; low++) { var candidate = SourceRandom.InitLow(low); var probe = candidate; if (SourceRandom.Roll(ref probe) % 100 == 0) { seed = candidate; break; } }
            Require(seed != 0, "Source one-percent seed"); fixedHit = One(p, at.AddSeconds(8)) with { Seed = seed };
            DruidTwister.Restore(new([fixedHit], [], fixedHit.Id)); hp = SkillHealth.Balance256(target); DruidTwister.Tick(fixedHit.NextUtc, true);
            Require(SkillHealth.Balance256(target) == hp && !CombatStuns.Snapshot().Any(x => x.Target == target.Serial.Value)
                && SourceKnockbacks.Snapshot().Any(x => x.Target == target.Serial.Value), "One-percent blocked-hit knockback mode");
        }
        finally { SourceBlocking.BlockRoll = block; SourceKnockbacks.Clear(target); DruidTwister.Cancel(p); target.Delete(); p.Delete(); }
        p = Fresh(); Train(p, CombatAbility.Twister); target = Enemy(p, 6); hp = SkillHealth.Balance256(target);
        DruidTwister.Create(p, new(p.X + 8, p.Y, p.Z), SkillMechanics.Values(p, CombatAbility.Twister), at.AddSeconds(12));
        var walls = Enumerable.Range(-16, 33).Select(y => { var wall = new Static(0x80); wall.MoveToWorld(new(p.X + 2, p.Y + y, p.Z), p.Map); return wall; }).ToArray();
        try { DruidTwister.Tick(at.AddSeconds(14), true); Require(DruidTwister.Snapshot().Missiles.Length == 0 && SkillHealth.Balance256(target) == hp, "Flight crossed actual wall barrier"); }
        finally { foreach (var wall in walls) { wall.Delete(); } DruidTwister.Cancel(p); target.Delete(); p.Delete(); }
        Log.Information("Twister:actualTarnhelm/SlowCapture/potion/blocked-knockback/wall controls pass");
    }
    private static void LifecycleAndWorker()
    {
        var p = Fresh(); Train(p, CombatAbility.Twister); var at = TimedEffects.Now.AddDays(1); One(p, at); var saved = DruidTwister.Snapshot(); string before = Text();
        var bytes = Task.Run(() => { var writer = new BufferWriter(new byte[1024 * 1024], true); DruidTwister.Profiles.Serialize(writer); return writer.Buffer[..(int)writer.Position]; }).GetAwaiter().GetResult();
        Require(bytes.Length > 0 && Text() == before, "Worker serialization changed live pool");
        var copy = DruidTwister.Snapshot(); copy.Missiles[0].Path[1] = new Point2D(999, 999); Require(Text() == before, "Snapshot leaked mutable path");
        foreach (var bad in new[] { saved with { Missiles = saved.Missiles.Concat(saved.Missiles).ToArray() }, saved with { DelayScope = "legacy-global" },
            saved with { Missiles = [saved.Missiles[0] with { Frame = 50 }] }, saved with { Leases = [new(saved.Missiles[0].Cast + 100, p.Serial.Value, at)] } })
        { bool refused = false; try { DruidTwister.Restore(bad); } catch (InvalidDataException) { refused = true; } Require(refused && Text() == before, "Malformed restore replaced live pool"); }
        DruidTwister.Tick(at.AddMilliseconds(1960), false); Require(DruidTwister.Snapshot().Missiles.Single() is { Frame: 49 } paused && paused.X == paused.OriginX, "Offline movement/aging");
        DruidTwister.Tick(at.AddMilliseconds(2000), false); Require(DruidTwister.Snapshot().Missiles.Length == 0, "Offline expiry");
        One(p, at.AddSeconds(3)); p.MoveToWorld(p.Location, Map.Felucca); DruidTwister.Tick(at.AddSeconds(5)); Require(DruidTwister.Snapshot().Missiles.Length == 0, "Old-facet expiry");
        One(p, at.AddSeconds(6)); Require(ClassCreation.Profiles.TryActivateHistory(p, DiabloClass.Sorceress), "Class-history transition fixture"); DruidTwister.Tick(at.AddSeconds(6).AddMilliseconds(40)); Require(DruidTwister.Snapshot().Missiles.Length == 0, "Class transition cleanup"); p.Delete();
    }
    private static void Save(PlayerMobile p, Mobile target) => File.WriteAllText(Manifest, JsonSerializer.Serialize(new Saved(p.Serial.Value, target.Serial.Value,
        SkillHealth.Balance256(target), DruidTwister.Snapshot(), CombatStuns.Snapshot().Single(x => x.Target == target.Serial.Value), _pending, _pendingMana)));
    private static void Check()
    {
        Require(DruidCombat.Implemented(CombatAbility.Twister) && DruidTwister.Snapshot().Missiles.Length == 0, "Twister handler/initial pool");
        Source(); Casting(); Prepared(); GearPotionAndObstacles(); LifecycleAndWorker();
        var p = Fresh(); Train(p, CombatAbility.Twister); var target = Enemy(p); target.Frozen = true; TimedEffects.PreventHealing(target, TimedEffects.Now); var at = TimedEffects.Now.AddDays(2);
        DruidTwister.Create(p, new(p.X + 6, p.Y, p.Z), SkillMechanics.Values(p, CombatAbility.Twister), at); DruidTwister.Tick(at.AddMilliseconds(40), true);
        Require(DruidTwister.Snapshot().Missiles.Length == 3 && DruidTwister.Snapshot().Leases.Length == 1 && !SkillHealth.Full(target), "Held damage/active cast lease");
        Require(SkillBindings.Instance.Assign(p, true, CombatAbility.Twister) && SkillBindings.Instance.Assign(p, false, CombatAbility.Twister) && SkillBindings.Instance.AssignQuick(p, 7, CombatAbility.Twister), "Saved bindings");
        var pending = Fresh(); Train(pending, CombatAbility.Twister); Mana(pending); Ready(pending); _pending = pending.Serial.Value; _pendingMana = NecromancerEffects.ManaBalance256(pending);
        Require(Start(pending), "Saved unpaid windup"); ClassCombat.For(pending).EffectAt = Core.TickCount + 3600000; ClassCombat.For(pending).NextAction = Core.TickCount + 3601000; Save(p, target);
    }
    private static void Reload()
    {
        var saved = JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest)); var p = (PlayerMobile)World.FindMobile((Serial)saved.Owner); var target = World.FindMobile((Serial)saved.Target);
        Require(p != null && target != null && Text() == Text(saved.State) && SkillHealth.Balance256(target) == saved.Life256
            && CombatStuns.Snapshot().Contains(saved.Stun), "Exact flight/path/seed/lease/stun/life reload");
        _pending = saved.Pending; _pendingMana = saved.PendingMana; var pending = (PlayerMobile)World.FindMobile((Serial)_pending);
        Require(pending != null && NecromancerEffects.ManaBalance256(pending) == _pendingMana && ClassCombat.For(pending).PendingAbility == CombatAbility.None, "Unpaid windup replayed");
        Require(SkillBindings.Instance.Get(p).Left == CombatAbility.Twister && SkillBindings.Instance.Get(p).Right == CombatAbility.Twister && SkillBindings.Instance.GetQuick(p.Serial, DiabloClass.Druid, 7) == CombatAbility.Twister, "Bindings changed");
        int frame = saved.State.Missiles.First().Frame; DruidTwister.Tick(saved.State.Missiles.Min(x => x.NextUtc), true);
        Require(DruidTwister.Snapshot().Missiles.All(x => x.Frame == frame + 1) && SkillHealth.Balance256(target) == saved.Life256, "Flight resume/lease boundary"); Save(p, target);
    }
    private static void Live(NetState network, bool input)
    {
        string prefix = input ? "twister-input" : "twister-live"; var held = DruidTwister.Snapshot(); var p = Fresh(); Train(p, CombatAbility.Twister); Mana(p); Ready(p); var target = Enemy(p);
        Require(SkillBindings.Instance.Assign(p, false, CombatAbility.Twister) && SkillBindings.Instance.AssignQuick(p, 7, CombatAbility.Twister), "Connected bindings");
        network.Seeded = true; network.CompressionEnabled = false; network.Mobile = p; p.NetState = network;
        var protocol = typeof(NetState).GetField("_protocolState", BindingFlags.Instance | BindingFlags.NonPublic); Require(protocol != null, "Protocol bootstrap"); protocol.SetValue(network, Enum.Parse(protocol.FieldType, "GameServer_LoggedIn")); network.ResetMovementState(); network.SendMobileUpdate(p);
        var aim = new Point3D(p.X + 6, p.Y, p.Z); int mana = NecromancerEffects.ManaBalance256(p), hp = SkillHealth.Balance256(target); bool began = !input, released = !input, moved = false, hurt = false, stunned = false; long deadline = Core.TickCount + 6000;
        if (!input) { DruidTwister.Create(p, aim, SkillMechanics.Values(p, CombatAbility.Twister), TimedEffects.Now); }
        void Inspect()
        {
            try
            {
                Require(Core.TickCount < deadline, "Natural Twister timeout"); var shots = DruidTwister.Snapshot().Missiles.Where(x => x.Owner == p.Serial.Value).ToArray();
                if (input && !began && ClassCombat.For(p).PendingAbility == CombatAbility.Twister) { began = true; Require(shots.Length == 0 && NecromancerEffects.ManaBalance256(p) == mana, "Early quick emission/payment"); }
                if (input && !released && shots.Length > 0) { released = true; Require(began && shots.Length == 3 && NecromancerEffects.ManaBalance256(p) == mana - 1792, "Quick triplet/payment"); }
                moved |= shots.Any(x => x.X != x.OriginX || x.Y != x.OriginY); hurt |= SkillHealth.Balance256(target) < hp; stunned |= CombatStuns.Remaining(target) > 0;
                if (!released || shots.Length > 0) { Timer.StartTimer(TimeSpan.FromMilliseconds(20), Inspect); return; }
                Require(moved && hurt && stunned && NecromancerEffects.ManaBalance256(p) == mana - (input ? 1792 : 0), $"Natural movement/damage/stun/payment: {moved}/{hurt}/{stunned}");
                target.Delete(); p.Delete(); Require(Text(DruidTwister.Snapshot() with { NextId = held.NextId }) == Text(held), "Natural fixture changed held pool");
                var saved = JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest)); var owner = (PlayerMobile)World.FindMobile((Serial)saved.Owner); var last = World.FindMobile((Serial)saved.Target);
                Require(SkillHealth.Balance256(last) == saved.Life256, "Natural fixture changed held injury"); Save(owner, last); Log.Information("{Prefix}-result: passed", prefix);
            }
            catch (Exception e) { DruidTwister.Cancel(p); target.Delete(); p.Delete(); Log.Error(e, "{Prefix}-result: failed", prefix); }
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30), () => { Log.Information("{Prefix}: ready ability={Ability} x={X} y={Y} z={Z}", prefix, (int)CombatAbility.Twister, aim.X, aim.Y, aim.Z); Inspect(); });
    }
}
