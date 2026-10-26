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
using Server.Spells;
namespace Sanctuary.Integration;

public static class ArmageddonChecks
{
    private static readonly ILogger Log = LogFactory.GetLogger(typeof(ArmageddonChecks));
    private sealed record Saved(uint Owner, uint Target, int Life256, ArmageddonSave State, uint Pending, int PendingMana, int Map, Point3D Origin);
    private static string Manifest => Path.Join(Core.BaseDirectory, "armageddon-fixtures.json");
    private static int _scene;
    private static bool _creating;
    private static void Rates()
    {
        var old = Mobile.ManaRegenRateHandler;
        Mobile.ManaRegenRateHandler = p => _creating || p.Name?.StartsWith("ArmageddonFixture", StringComparison.Ordinal) == true
            ? TimeSpan.FromHours(1) : old?.Invoke(p) ?? Mobile.DefaultManaRate;
    }
    [CallPriority(int.MaxValue)] public static void Initialize() => Rates();
    public static void Configure()
    {
        Rates(); Register("armageddon-check", Check); Register("armageddon-reload-check", Reload);
        Register("armageddon-live-check", () => FixtureNetwork.Connected("armageddon-live-result", n => Live(n, false)));
        Register("armageddon-input-check", () => FixtureNetwork.Connected("armageddon-input-result", n => Live(n, true)));
    }
    private static void Register(string name, Action action) => ConsoleInputHandler.RegisterCommand(name, "Armageddon weather, delayed impacts and ground fire", _ => Core.LoopContext.Post(() =>
    { try { action(); Log.Information("{Name}: passed", name); } catch (Exception e) { Log.Error(e, "{Name}: failed", name); } }));
    private static void Require(bool condition, string why) { if (!condition) { throw new InvalidOperationException(why); } }
    private static string Text(ArmageddonSave state = null) => JsonSerializer.Serialize(state ?? DruidArmageddon.Snapshot());
    private static PlayerMobile Fresh()
    {
        PlayerMobile p; _creating = true; try { p = new PlayerMobile { Body = 0x190, Player = true, Name = "ArmageddonFixture" }; } finally { _creating = false; }
        p.AddItem(new Backpack()); ClassCreation.Profiles.Assign(p, DiabloClass.Druid); ClassCreation.Profiles.AdvanceLevel(p, 99);
        FixtureSpace.Corridor(p, 7000, 24, 250 + 32 * _scene++); return p;
    }
    private static void Train(PlayerMobile p, CombatAbility ability, int rank = 1)
    {
        var row = SkillMechanics.For(ability); Require(TalentCatalog.TryGet(DiabloClass.Druid, row.TalentId, out var talent), "Druid talent");
        foreach (string parent in talent.Prerequisites) { Train(p, SkillMechanics.All.Single(x => x.TalentId == parent).Ability); }
        while (ClassCreation.Profiles.GetRank(p.Serial, row.TalentId) < rank) { Require(ClassCreation.Profiles.TryTrain(p.Serial, row.TalentId, out _), "Train " + row.TalentId); }
    }
    private static GraveyardSkeleton Enemy(PlayerMobile p, int dx = 1)
    {
        var t = new GraveyardSkeleton { Name = "ArmageddonFixtureTarget", CantWalk = true, NoKillAwards = true }; t.SetHits(10000);
        foreach (var kind in new[] { ResistanceType.Physical, ResistanceType.Fire, ResistanceType.Cold, ResistanceType.Energy, ResistanceType.Poison }) { t.SetResistance(kind, 0); }
        t.MoveToWorld(new(p.X + dx, p.Y, p.Z), p.Map); t.AIObject.AITimer.Stop(); return t;
    }
    private static void Ready(PlayerMobile p)
    { ClassCombat.Stop(p); ClassCombat.For(p).NextAction = ClassCombat.For(p).RecoveryUntil = Core.TickCount; p.Frozen = p.Paralyzed = false; }
    private static void Mana(PlayerMobile p) => NecromancerEffects.SetMana256(p, p.ManaMax * 256);
    private static void Drop(PlayerMobile p)
    {
        var state = DruidArmageddon.Snapshot(); DruidArmageddon.Restore(state with { Weather = state.Weather.Where(x => x.Owner != p.Serial.Value).ToArray(), Actors = state.Actors.Where(x => x.Owner != p.Serial.Value).ToArray() });
    }
    private static bool Cast(PlayerMobile p)
    {
        Ready(p); int mana = NecromancerEffects.ManaBalance256(p); Require(ClassCombat.Perform(p, CombatAbility.Armageddon, null, new(65530, 65530, 0), Core.TickCount), "Self SC start");
        var s = ClassCombat.For(p); Require(s.PendingTarget == Serial.Zero && s.PendingPoint == p.Location && s.Animation == 16 && NecromancerEffects.ManaBalance256(p) == mana, "Unpaid self windup");
        ClassCombat.ReleaseCast(p); bool result = ClassCombat.ResolveAction(p, s.EffectAt);
        Require(NecromancerEffects.ManaBalance256(p) == mana - (result ? 8960 : 0) && !ClassCombat.ResolveAction(p, s.EffectAt), "Once-only35mana payment"); return result;
    }
    private static ArmageddonActor Actor(PlayerMobile p, int missile, DateTime at, BoulderEndpoints? fixedEnds = null, int? x = null)
    {
        long id = DruidArmageddon.Add(p, missile, 1, x ?? p.X * 2, p.Y * 2, p.Z, at);
        var saved = DruidArmageddon.Snapshot(); var actor = saved.Actors.Single(a => a.Id == id);
        if (fixedEnds is { } ends)
        { actor = actor with { Endpoints = ends }; DruidArmageddon.Restore(saved with { Actors = saved.Actors.Select(a => a.Id == id ? actor : a).ToArray() }); }
        return actor;
    }
    private static void Endpoints()
    {
        Require(ClassAbilities.SelfTarget(CombatAbility.Armageddon) && DruidCombat.Implemented(CombatAbility.Armageddon), "Learned self identity");
        using var data = JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory, "armageddon-damage-source-probe.json"))); int count = 0; var skill = SkillMechanics.For(CombatAbility.Armageddon);
        foreach (var row in data.RootElement.EnumerateArray())
        {
            int kind = row[0].GetInt32(), rank = row[1].GetInt32(), b = row[2].GetInt32(), f = row[3].GetInt32(), v = row[4].GetInt32(), mastery = row[5].GetInt32();
            var values = new SkillValues(skill, key => key == "Armageddon" ? rank : 255, key => key == "Molten Boulder" ? b : key == "Firestorm" ? f : key == "Volcano" ? v : 0, 99);
            var actual = DruidArmageddon.Endpoints(values, kind == 0 ? 577 : 623, mastery, SlowMissilePayload.Normal);
            Require(actual == new BoulderEndpoints(row[7].GetInt32(), row[8].GetInt32(), row[9].GetInt32(), row[10].GetInt32()), "Complete source impact/own-ground endpoints"); count++;
        }
        Require(count == 55080, "Complete damage capture scope"); Log.Information("Armageddon:55080complete-source endpoint comparisons pass");
    }
    private static void Emitter()
    {
        var p = Fresh(); Train(p, CombatAbility.Armageddon); var held = DruidArmageddon.Snapshot(); var gate = DruidArmageddon.CanPlace; int count = 0;
        try
        {
            using var data = JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory, "armageddon-emitter-source-probe.json")));
            foreach (var row in data.RootElement.EnumerateArray())
            {
                int success = row[2].GetInt32(), rank = row[3].GetInt32(), memory = row[5].GetInt32(), attempts = 0;
                DruidArmageddon.CanPlace = (_, _, _) => ++attempts >= success;
                var at = TimedEffects.Now.AddHours(1); var before = new ArmageddonState(p.Serial.Value, rank, memory, 0, at, at.AddMilliseconds(240), at.AddSeconds(10));
                var after = DruidArmageddon.Emit(p, before); var actors = DruidArmageddon.Snapshot().Actors.Where(x => x.Owner == p.Serial.Value).ToArray();
                Require(after.Memory == row[7].GetInt32() && after.Seed == row[8].GetUInt64() && attempts == row[9].GetInt32() && actors.Length == row[10].GetInt32(), "Complete source emitter memory/final-seed/attempts");
                if (actors.Length != 0) { Require(actors[0].X == p.X * 2 + row[11].GetInt32() - 40 && actors[0].Y == p.Y * 2 + row[12].GetInt32() - 50 && actors[0].Rank == rank, "Source offset/retained rank"); }
                Drop(p); count++;
            }
            Require(count == 720, "Emitter source scope");
            DruidArmageddon.CanPlace = (_, _, _) => true; DruidArmageddon.Create(p, SkillMechanics.Values(p, CombatAbility.Armageddon), TimedEffects.Now.AddHours(1)); var state = DruidArmageddon.State(p);
            DruidArmageddon.Tick(state.NextUtc, true); Require(DruidArmageddon.Snapshot().Actors.Count(x => x.Owner == p.Serial.Value) == 1 && DruidArmageddon.State(p).NextUtc == state.NextUtc.AddMilliseconds(240), "Tick retained new actor and replaced weather atomically");
        }
        finally { DruidArmageddon.CanPlace = gate; Drop(p); DruidArmageddon.Restore(held); p.Delete(); }
        Log.Information("Armageddon:720source emitter memory/coordinate/seed comparisons pass");
    }
    private static void Casting()
    {
        var p = Fresh(); Require(!ClassCombat.Available(p, CombatAbility.Armageddon), "Unlearned available"); Train(p, CombatAbility.Armageddon);
        Require(!SkillBindings.Instance.Assign(p, true, CombatAbility.Armageddon) && SkillBindings.Instance.Assign(p, false, CombatAbility.Armageddon) && SkillBindings.Instance.AssignQuick(p, 7, CombatAbility.Armageddon), "Right/quick-only binding");
        Ready(p); NecromancerEffects.SetMana256(p, 8959); Require(!ClassCombat.Perform(p, CombatAbility.Armageddon, null, p.Location, Core.TickCount), "Low mana start");
        foreach (int cancel in new[] { 0, 1, 2, 3 })
        {
            Ready(p); Mana(p); int mana = NecromancerEffects.ManaBalance256(p); Require(ClassCombat.Perform(p, CombatAbility.Armageddon, null, p.Location, Core.TickCount), $"Cancelled start {cancel}: mana={NecromancerEffects.ManaBalance256(p)}/{p.ManaMax*256}, available={ClassCombat.Available(p,CombatAbility.Armageddon)}, outdoors={DruidWeather.Outdoors(p)}, ally={AlliedOverlap.Allowed(p)}, pending={ClassCombat.For(p).PendingAbility}, frozen={p.Frozen}/{p.Paralyzed}, spell={p.Spell}, ready={FireGround.Ready(p,CombatAbility.Armageddon)}, pos={p.Location}"); long at = ClassCombat.For(p).EffectAt;
            if (cancel == 0) { ClassCombat.Stop(p); }
            if (cancel == 1) { var origin = p.Location; p.Direction = Direction.East; Require(p.Move(Direction.East), "Outward step"); p.Direction = Direction.West; Require(p.Move(Direction.West) && p.Location == origin, "Return step"); }
            if (cancel == 2) { NecromancerEffects.SetMana256(p, 0); }
            if (cancel == 3) { Require(HitRecovery.Apply(p, p.HitsMax, Core.TickCount), "Recovery cancellation"); }
            Require(!ClassCombat.ResolveAction(p, at) && DruidArmageddon.State(p) == null && NecromancerEffects.ManaBalance256(p) == (cancel == 2 ? 0 : mana), "Cancelled cast paid/emitted");
        }
        Ready(p); Mana(p); Require(Cast(p) && FireGround.Ready(p, CombatAbility.Armageddon), "Cast/no local delay"); var state = DruidArmageddon.State(p);
        Require(state.Rank == 1 && state.NextUtc - state.StartedUtc == TimeSpan.FromMilliseconds(240) && state.UntilUtc - state.StartedUtc == TimeSpan.FromSeconds(12), "Retained rank/hard Fissure duration/first emit");
        var actor = Actor(p, 577, TimedEffects.Now.AddDays(1)); var helm = NamedGear.Create(NamedGear.Catalog.Items.Single(x => x.Name == "Tarnhelm").Id, 99, new Random(173), true); Require(p.EquipItem(helm), "Real Tarnhelm equip"); ClassGear.Refresh(p);
        Require(DruidArmageddon.State(p) == state && Cast(p) && DruidArmageddon.State(p).Rank == 2 && DruidArmageddon.Snapshot().Actors.Any(x => x.Id == actor.Id), "Gear/recast retained released control");
        p.AddToBackpack(helm); ClassGear.Refresh(p); Require(Cast(p) && DruidArmageddon.State(p).Rank == 1, "Lower-rank replacement");
        Train(p, CombatAbility.Hurricane); DruidHurricane.Create(p, SkillMechanics.Values(p, CombatAbility.Hurricane), TimedEffects.Now.AddDays(1)); Require(DruidHurricane.State(p) != null && DruidArmageddon.State(p) != null, "Independent weather coexistence");
        DruidHurricane.Clear(p); Drop(p); p.Delete();
    }
    private static void DelayedAndGround()
    {
        var p = Fresh(); Train(p, CombatAbility.Armageddon); var a = Enemy(p); a.SetHits(40); var b = Enemy(p, 2); var outside = Enemy(p, 3); var at = TimedEffects.Now.AddHours(1);
        var actor = Actor(p, 577, at, new(2560, 2560, 1280, 1280)); int life = SkillHealth.Balance256(a), second = SkillHealth.Balance256(b), far = SkillHealth.Balance256(outside);
        DruidArmageddon.Tick(at.AddMilliseconds(960), true); Require(SkillHealth.Balance256(a) == life && DruidArmageddon.Snapshot().Actors.Single().Remaining == 1, "Control dealt contact damage before25-frame expiry");
        Train(p, CombatAbility.Firestorm, 2); DruidArmageddon.Clear(p); DruidArmageddon.Tick(actor.UntilUtc, true);
        Require(life - SkillHealth.Balance256(a) == 3840 && second - SkillHealth.Balance256(b) == 3840 && SkillHealth.Balance256(outside) == far && CreatureHitRecovery.Remaining(a) > 0, "Copied physical/fire radius4 and source get-hit");
        var field = DruidArmageddon.Snapshot().Actors.Single(); var fresh = DruidArmageddon.Endpoints(SkillMechanics.Values(p, CombatAbility.Armageddon, 1), 623, 0, SlowMissilePayload.Normal);
        Require(field.Missile == 623 && field.Remaining == 37 && field.Endpoints == fresh && field.NextUtc == actor.UntilUtc.AddMilliseconds(40) && field.UntilUtc == actor.UntilUtc.AddMilliseconds(1480), "Fresh terminal child/current hard synergies/separate lifetime");
        Require(MissileFootprints.Snapshot().Any(x => x.Source == "armageddonfire" && x.Map == field.Map && x.X == field.X && x.Y == field.Y), "Registered real Collision1 field projection");
        Drop(p); a.Delete(); b.Delete(); outside.Delete(); p.Delete();
        p = Fresh(); Train(p, CombatAbility.Armageddon); a = Enemy(p, 0); b = Enemy(p, 0); at = TimedEffects.Now.AddHours(2); field = Actor(p, 623, at, new(0, 0, 256, 256)); life = SkillHealth.Balance256(a); second = SkillHealth.Balance256(b);
        DruidArmageddon.Tick(field.UntilUtc, true); Require(life + second - SkillHealth.Balance256(a) - SkillHealth.Balance256(b) == 36 * 256 && DruidArmageddon.Snapshot().Actors.Length == 0, "Exactly36single-collider ground hits/no invented delay/terminal hit");
        a.Delete(); b.Delete(); a = Enemy(p, 0); a.SetHits(100); field = Actor(p, 623, at, new(0, 0, 7680, 7680)); life = SkillHealth.Balance256(a); DruidArmageddon.Tick(field.NextUtc, true);
        Require(life - SkillHealth.Balance256(a) == 7680 && CreatureHitRecovery.Remaining(a) == 0, "Ground added get-hit/weapon callback"); Drop(p); a.Delete(); p.Delete();
    }
    private static void EligibilityAndLifecycle()
    {
        var p = Fresh(); Train(p, CombatAbility.Armageddon); var target = Enemy(p, 0); var at = TimedEffects.Now.AddHours(1);
        var town = new GuardedRegion("ArmageddonFixtureTown", p.Map, 1000, new Rectangle2D(p.X - 1, p.Y - 1, 3, 3)); town.Register();
        try { Ready(p); Mana(p); Require(SpellHelper.IsTown(p.Location, p) && !ClassCombat.Perform(p, CombatAbility.Armageddon, null, p.Location, Core.TickCount), "Town cast"); DruidArmageddon.Create(p, SkillMechanics.Values(p, CombatAbility.Armageddon), at); DruidArmageddon.Tick(DruidArmageddon.State(p).NextUtc, true); Require(DruidArmageddon.State(p) == null, "Town weather cleanup"); } finally { town.Unregister(); }
        var field = Actor(p, 623, at, new(0, 0, 256, 256)); var wall = new Static(0x80); wall.MoveToWorld(p.Location, p.Map); int life = SkillHealth.Balance256(target);
        try { Require(!DruidArmageddon.Placement(p, p.X * 2, p.Y * 2), "Placement wall gate"); DruidArmageddon.Tick(field.NextUtc, true); Require(DruidArmageddon.Snapshot().Actors.Length == 0 && SkillHealth.Balance256(target) == life, "Ground wall removal"); } finally { wall.Delete(); }
        target.SetResistance(ResistanceType.Physical, 100); target.SetResistance(ResistanceType.Fire, 100); var control = Actor(p, 577, at, new(2560, 2560, 1280, 1280)); DruidArmageddon.Tick(control.UntilUtc, true); Require(SkillHealth.Balance256(target) == life, "Dual immunity"); Drop(p);
        Require(CombatPotions.Apply(p, 3), "Actual tier3 potion"); control = Actor(p, 577, at, new(2560, 2560, 1280, 1280)); DruidArmageddon.Tick(control.UntilUtc, true); Require(life - SkillHealth.Balance256(target) == 1152, "Approved potion channels through dual immunity"); Drop(p);
        var slow = AmazonSlowMissiles.Snapshot();
        try { AmazonSlowMissiles.Restore(slow.Append(new SlowMissilesState(p.Serial.Value, p.Serial.Value, 1, 50, 50, TimedEffects.Now.AddDays(2))).ToArray()); control = Actor(p, 577, at); var expected = DruidArmageddon.Endpoints(SkillMechanics.Values(p, CombatAbility.Armageddon, 1), 577, 0, new(50, 50)); Require(control.Endpoints == expected && control.Remaining == 25, "Slow payload/captured physical and stationary lifetime"); } finally { AmazonSlowMissiles.Restore(slow); Drop(p); }
        DruidArmageddon.Create(p, SkillMechanics.Values(p, CombatAbility.Armageddon), at); var state = DruidArmageddon.State(p); control = Actor(p, 577, at); life = SkillHealth.Balance256(target);
        DruidArmageddon.Tick(state.UntilUtc, false); Require(DruidArmageddon.State(p) == null && DruidArmageddon.Snapshot().Actors.Length == 0 && SkillHealth.Balance256(target) == life, "Offline aging without damage/replay/children");
        var point = p.Location; var map = p.Map; field = Actor(p, 623, at); p.MoveToWorld(point, Map.Felucca); if (p.Map == map) { p.MoveToWorld(point, Map.Trammel); }
        DruidArmageddon.Tick(field.NextUtc, true); Require(DruidArmageddon.Snapshot().Actors.Single().Map == field.Map && SkillHealth.Balance256(target) == life, "Old-facet field moved/fought"); DruidArmageddon.Tick(field.UntilUtc, true); p.MoveToWorld(point, map); target.Delete(); Drop(p); p.Delete();
        p = Fresh(); Train(p, CombatAbility.Armageddon); DruidArmageddon.Create(p, SkillMechanics.Values(p, CombatAbility.Armageddon), at); control = Actor(p, 577, at); Require(ClassCreation.Profiles.TryActivateHistory(p, DiabloClass.Amazon), "Idle class transition"); DruidArmageddon.Tick(control.NextUtc, true); Require(DruidArmageddon.State(p) == null && DruidArmageddon.Snapshot().Actors.Length == 0, "Class weather/actor cleanup"); p.Delete();
    }
    private static void WorkerAndMalformed()
    {
        var saved = DruidArmageddon.Snapshot(); string before = Text(); var actor = saved.Actors.First(); var weather = saved.Weather.Single();
        var bytes = Task.Run(() => { var w = new BufferWriter(new byte[2 * 1024 * 1024], true); DruidArmageddon.Profiles.Serialize(w); return w.Buffer[..(int)w.Position]; }).GetAwaiter().GetResult(); var r = new BufferReader(bytes);
        Require(r.ReadInt() == 0 && r.ReadString() == before && Text() == before, "Read-only worker snapshot");
        var bad = new[] { saved with { Weather = null }, saved with { Actors = null }, saved with { NextId = 0 }, saved with { Weather = [weather, weather] }, saved with { Weather = [weather with { Memory = 1000000 }] }, saved with { Weather = [weather with { NextUtc = weather.NextUtc.AddTicks(1) }] }, saved with { Actors = [actor, actor] }, saved with { Actors = [actor with { Remaining = 0 }] }, saved with { Actors = [actor with { NextUtc = actor.NextUtc.AddTicks(1) }] }, saved with { Actors = [actor with { Missile = 1 }] }, saved with { Actors = [actor with { Modifiers = null }] } };
        foreach (var row in bad) { bool refused = false; try { DruidArmageddon.Restore(row); } catch (InvalidDataException) { refused = true; } Require(refused && Text() == before, "Atomic invalid restore"); }
        saved.Weather[0] = weather with { Rank = 255 }; saved.Actors[0] = actor with { Rank = 255 }; Require(Text() == before, "Snapshot array isolation"); DruidArmageddon.Restore(JsonSerializer.Deserialize<ArmageddonSave>(before)); Require(Text() == before, "Exact JSON resumption");
    }
    private static void Check()
    {
        Require(DruidArmageddon.Snapshot().Actors.Length == 0 && DruidArmageddon.Snapshot().Weather.Length == 0, "Initial pool"); Endpoints(); Emitter(); Casting(); DelayedAndGround(); EligibilityAndLifecycle();
        var p = Fresh(); Train(p, CombatAbility.Armageddon); var target = Enemy(p, 0); TimedEffects.PreventHealing(target, TimedEffects.Now); var at = TimedEffects.Now.AddDays(2);
        DruidArmageddon.Create(p, SkillMechanics.Values(p, CombatAbility.Armageddon), at); Actor(p, 577, at, new(2560, 2560, 1280, 1280)); Actor(p, 623, at, new(0, 0, 256, 256)); WorkerAndMalformed();
        Require(SkillBindings.Instance.Assign(p, false, CombatAbility.Armageddon) && SkillBindings.Instance.AssignQuick(p, 7, CombatAbility.Armageddon), "Saved bindings"); var pending = Fresh(); Train(pending, CombatAbility.Armageddon); Ready(pending); Mana(pending); int mana = NecromancerEffects.ManaBalance256(pending);
        Require(ClassCombat.Perform(pending, CombatAbility.Armageddon, null, pending.Location, Core.TickCount), "Saved unpaid windup"); ClassCombat.For(pending).EffectAt = Core.TickCount + 3600000; ClassCombat.For(pending).NextAction = Core.TickCount + 3601000;
        File.WriteAllText(Manifest, JsonSerializer.Serialize(new Saved(p.Serial.Value, target.Serial.Value, SkillHealth.Balance256(target), DruidArmageddon.Snapshot(), pending.Serial.Value, mana, p.Map.MapID, p.Location)));
    }
    private static void Reload()
    {
        Endpoints(); var saved = JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest)); var p = (PlayerMobile)World.FindMobile((Serial)saved.Owner); var target = World.FindMobile((Serial)saved.Target); var pending = (PlayerMobile)World.FindMobile((Serial)saved.Pending);
        Require(Text() == Text(saved.State) && SkillHealth.Balance256(target) == saved.Life256, "Saved weather/control/field/seed/clock/injury"); WorkerAndMalformed();
        if (p.Map == Map.Internal) { p.MoveToWorld(saved.Origin, Map.Maps[saved.Map]); }
        Require(DruidArmageddon.State(pending) == null && ClassCombat.For(pending).PendingAbility == CombatAbility.None && NecromancerEffects.ManaBalance256(pending) == saved.PendingMana, "Unpaid windup replayed");
        Require(SkillBindings.Instance.Get(p).Right == CombatAbility.Armageddon && SkillBindings.Instance.GetQuick(p.Serial, DiabloClass.Druid, 7) == CombatAbility.Armageddon, "Saved bindings");
        var field = DruidArmageddon.Snapshot().Actors.First(x => x.Missile == 623); DruidArmageddon.Tick(field.NextUtc, true); Require(SkillHealth.Balance256(target) < saved.Life256 && DruidArmageddon.Snapshot().Actors.Any(x => x.Missile == 577), "Reload actual ground damage/delayed control preserved");
        File.WriteAllText(Manifest, JsonSerializer.Serialize(saved with { State = DruidArmageddon.Snapshot(), Life256 = SkillHealth.Balance256(target) }));
    }
    private static void Live(NetState network, bool input)
    {
        string label = input ? "armageddon-input" : "armageddon-live"; var p = Fresh(); Train(p, CombatAbility.Armageddon); Ready(p); Mana(p); var target = Enemy(p); int life = SkillHealth.Balance256(target), mana = NecromancerEffects.ManaBalance256(p);
        network.Seeded = true; network.CompressionEnabled = false; network.Mobile = p; p.NetState = network;
        var protocol = typeof(NetState).GetField("_protocolState", BindingFlags.Instance | BindingFlags.NonPublic); Require(protocol != null, "Protocol bootstrap"); protocol.SetValue(network, Enum.Parse(protocol.FieldType, "GameServer_LoggedIn")); network.ResetMovementState(); network.SendMobileUpdate(p);
        Require(SkillBindings.Instance.Assign(p, false, CombatAbility.Armageddon) && SkillBindings.Instance.AssignQuick(p, 7, CombatAbility.Armageddon), "Connected bindings");
        if (!input) { DruidArmageddon.Create(p, SkillMechanics.Values(p, CombatAbility.Armageddon), TimedEffects.Now); }
        bool began = !input, released = !input, controls = false, fields = false, damaged = false; long deadline = Core.TickCount + 20000;
        void Inspect()
        {
            try
            {
                Require(Core.TickCount < deadline, "Natural Armageddon timeout"); var state = DruidArmageddon.State(p); var actors = DruidArmageddon.Snapshot().Actors.Where(x => x.Owner == p.Serial.Value).ToArray();
                if (!began && ClassCombat.For(p).PendingAbility == CombatAbility.Armageddon) { began = true; Require(state == null && NecromancerEffects.ManaBalance256(p) == mana, "Unpaid quick windup"); }
                if (!released && state != null) { released = true; Require(began && NecromancerEffects.ManaBalance256(p) == mana - 8960, "Quick release/payment"); }
                controls |= actors.Any(x => x.Missile == 577); fields |= actors.Any(x => x.Missile == 623); damaged |= SkillHealth.Balance256(target) < life;
                if (!released || state != null || actors.Length != 0) { Timer.StartTimer(TimeSpan.FromMilliseconds(30), Inspect); return; }
                Require(controls && fields && damaged && NecromancerEffects.ManaBalance256(p) == mana - (input ? 8960 : 0), "Natural emit/delay/terminal/ground/expiry/one payment"); Drop(p); target.Delete(); p.Delete(); Log.Information("{Label}-result: passed", label);
            }
            catch (Exception e) { Drop(p); target.Delete(); p.Delete(); Log.Error(e, "{Label}-result: failed", label); }
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30), () => { Log.Information("{Label}: ready ability={Ability} x=65530 y=65530 z=0", label, (int)CombatAbility.Armageddon); Inspect(); });
    }
}
