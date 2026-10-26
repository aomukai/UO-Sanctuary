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

public static class FissureChecks
{
    private static readonly ILogger Log = LogFactory.GetLogger(typeof(FissureChecks));
    private sealed record Saved(uint Owner, uint Target, int Life256, FissureSave State);
    private static string Manifest => Path.Join(Core.BaseDirectory, "fissure-fixtures.json");
    private static int _scene;
    public static void Configure()
    {
        Register("fissure-check", Check); Register("fissure-reload-check", Reload);
        Register("fissure-live-check", () => FixtureNetwork.Connected("fissure-live-result", Live));
    }
    private static void Register(string name, Action action) => ConsoleInputHandler.RegisterCommand(name, "Prepared Fissure actors and persistence", _ => Core.LoopContext.Post(() =>
    { try { action(); Log.Information("{Name}: passed", name); } catch (Exception e) { Log.Error(e, "{Name}: failed", name); } }));
    private static void Require(bool value, string why) { if (!value) { throw new InvalidOperationException(why); } }
    private static string Text(FissureSave saved = null) => JsonSerializer.Serialize(saved ?? DruidFissure.Snapshot());
    private static PlayerMobile Fresh()
    {
        var p = new PlayerMobile { Body = 0x190, Player = true, Name = "FissureFixture" }; p.AddItem(new Backpack());
        ClassCreation.Profiles.Assign(p, DiabloClass.Druid); ClassCreation.Profiles.AdvanceLevel(p, 99);
        FixtureSpace.Corridor(p, 6900, 60, 1300 + 32 * _scene++); return p;
    }
    private static void Train(PlayerMobile p, CombatAbility ability)
    {
        var row = SkillMechanics.For(ability); Require(TalentCatalog.TryGet(DiabloClass.Druid, row.TalentId, out var talent), "Druid talent");
        foreach (string parent in talent.Prerequisites) { Train(p, SkillMechanics.All.Single(x => x.TalentId == parent).Ability); }
        if (ClassCreation.Profiles.GetRank(p.Serial, row.TalentId) == 0) { Require(ClassCreation.Profiles.TryTrain(p.Serial, row.TalentId, out _), "Train " + row.TalentId); }
    }
    private static GraveyardSkeleton Enemy(PlayerMobile p, int dx = 4)
    {
        var target = new GraveyardSkeleton { Name = "FissureFixtureTarget", Frozen = true, CantWalk = true, NoKillAwards = true }; target.SetHits(10000);
        foreach (var kind in new[] { ResistanceType.Physical, ResistanceType.Fire, ResistanceType.Cold, ResistanceType.Energy, ResistanceType.Poison }) { target.SetResistance(kind, 0); }
        target.MoveToWorld(new(p.X + dx, p.Y, p.Z), p.Map); target.AIObject.AITimer.Stop(); return target;
    }
    private static FissureCrack Field(PlayerMobile p, DateTime at, int min = 10 * 256, int max = 20 * 256, long cast = 1, long id = 2)
        => new(id, cast, p.Serial.Value, p.Map.MapID, 1, (p.X + 4) * 2, p.Y * 2, p.Z, 0, min, max,
            new(0, 0, 0, 0, 0), SourceRandom.InitLow(1), p.Serial.Value, at.AddMilliseconds(40), at.AddMilliseconds(84 * 40));
    private static void Set(FissureCrack crack) => DruidFissure.Restore(new([], [crack], [], crack.Id));
    private static void SourceDamage()
    {
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory, "fissure-damage-source-probe.json"))); int count = 0;
        foreach (var row in json.RootElement.EnumerateArray())
        {
            int kind = row[0].GetInt32(); if (kind == 0) { continue; }
            int rank = row[1].GetInt32(), firestorm = row[2].GetInt32(), volcano = row[3].GetInt32(), mastery = row[4].GetInt32();
            var values = new SkillValues(SkillMechanics.For(CombatAbility.Fissure), key => key == "Eruption" ? rank : 255,
                key => key == "Firestorm" ? firestorm : key == "Volcano" ? volcano : 0, 99);
            Require(DruidFirestorm.Endpoints(values, mastery, SlowMissilePayload.Normal) == (row[8].GetInt32(), row[9].GetInt32()), "Complete source Fissure endpoints");
            Require(values.Mana256 == 3840 && values.Eval("calc1") == 7 && values.Eval("calc2") == 6, "Fissure current formulas"); count++;
        }
        Require(count == 18360, "Incomplete source crack coverage"); Log.Information("Fissure:18360complete-source native endpoint comparisons pass");
    }
    private static void Emitter()
    {
        var p = Fresh(); Train(p, CombatAbility.Fissure); var at = TimedEffects.Now.AddDays(1); int mana = NecromancerEffects.ManaBalance256(p);
        long id = DruidFissure.Create(p, new(p.X + 6, p.Y, p.Z), 1, at); var center = DruidFissure.Snapshot().Centers.Single();
        Require(center.Id == id && center.Frame == 0 && center.NextUtc == at.AddMilliseconds(40) && center.UntilUtc == at.AddMilliseconds(3200), "Initial center clock");
        DruidFissure.Tick(center.NextUtc, true); var first = DruidFissure.Snapshot().Cracks.Single(); var offset = GroundMissileScatter.Sample(center.X, 0, 7);
        Require(first.Cast == id && first.X == center.X + offset.X && first.Y == center.Y + offset.Y && first.Rank == 1
            && first.LastTarget == p.Serial.Value && first.UntilUtc == at.AddMilliseconds(3400), "First crack request/owner/clock");
        var firstEnds = DruidFirestorm.Endpoints(SkillMechanics.Values(p, CombatAbility.Fissure, 1), 0, SlowMissilePayload.Normal);
        Require((first.Min256, first.Max256) == firstEnds, "Fresh first endpoints");
        Require(ClassCreation.Profiles.TryTrain(p.Serial, "druid.fissure", out _) && ClassCreation.Profiles.TryTrain(p.Serial, "druid.firestorm", out _), "Fresh hard/effective fixture");
        DruidFissure.Tick(at.AddMilliseconds(3160), true); var beforeLast = DruidFissure.Snapshot();
        Require(beforeLast.Centers.Single().Frame == 79 && beforeLast.Centers.Single().Seed == GroundMissileScatter.Sample(center.X, 78, 7).Seed, "Elapsed sample/decrement order");
        Require(beforeLast.Cracks.Length is > 1 and <= 14 && beforeLast.Cracks.All(x => x.Cast == id && x.Rank == 1), "Retained rank/attempt bounds");
        var freshEnds = DruidFirestorm.Endpoints(SkillMechanics.Values(p, CombatAbility.Fissure, 1), 0, SlowMissilePayload.Normal);
        Require(freshEnds != firstEnds && beforeLast.Cracks.Where(x => x.Id != first.Id).All(x => (x.Min256, x.Max256) == freshEnds), "Fresh hard synergies at retained rank");
        foreach (var a in beforeLast.Cracks) foreach (var b in beforeLast.Cracks.Where(x => x.Id > a.Id))
        { Require(Math.Abs(a.X - b.X) > 1 || Math.Abs(a.Y - b.Y) > 1, "Crack spawned inside existing Size3 footprint"); }
        DruidFissure.Tick(at.AddMilliseconds(3200), true);
        Require(DruidFissure.Snapshot() is { Centers.Length: 0, Cracks.Length: > 0 }, "Independent cracks died with center");
        DruidFissure.Tick(at.AddMilliseconds(6520), true);
        Require(DruidFissure.Snapshot() is { Centers.Length: 0, Cracks.Length: 0, Leases.Length: 0 } && NecromancerEffects.ManaBalance256(p) == mana, "Final expiry/prepared creator payment");
        p.Delete();
    }
    private static void Landing()
    {
        var p = Fresh(); Train(p, CombatAbility.Fissure); var at = TimedEffects.Now.AddDays(1); var field = Field(p, at); Set(field);
        Require(DruidFissure.LandingBlocked(field.Map, field.X, field.Y, field.Z, at)
            && DruidFissure.LandingBlocked(field.Map, field.X + 1, field.Y + 1, field.Z, at)
            && !DruidFissure.LandingBlocked(field.Map, field.X + 2, field.Y, field.Z, at), "Size3 point mask footprint");
        Require(!DruidFissure.LandingBlocked(field.Map, field.X, field.Y, field.Z, field.UntilUtc), "Expired footprint remained");
        DruidFissure.Cancel(p); long id = DruidFissure.Create(p, new(p.X + 6, p.Y, p.Z), 1, at); var initial = DruidFissure.Snapshot().Centers.Single();
        var other = DruidFissure.OtherMissileFootprint;
        try
        {
            int queries = 0; DruidFissure.OtherMissileFootprint = (_, _, _, _, _) => { queries++; return true; };
            DruidFissure.Tick(initial.NextUtc, true); var current = DruidFissure.Snapshot();
            Require(queries == 1 && current.Cracks.Length == 0 && current.Centers.Single().Seed == GroundMissileScatter.Sample(initial.X, 0, 7).Seed, "External mask failed to consume sample/stop allocation");
        }
        finally { DruidFissure.OtherMissileFootprint = other; DruidFissure.Cancel(p); }
        var wall = new Static(0x80); wall.MoveToWorld(new(p.X + 6, p.Y, p.Z), p.Map);
        try
        {
            bool refused = false; try { DruidFissure.Create(p, wall.Location, 1, at); } catch (NotSupportedException) { refused = true; }
            Require(refused && DruidFissure.Snapshot().Centers.Length == 0, "Initial landing crossed wall");
        }
        finally { wall.Delete(); }
        bool distant = false; try { DruidFissure.Create(p, new(p.X + 51, p.Y, p.Z), 1, at); } catch (NotSupportedException) { distant = true; }
        Require(distant, "Coordinate creator exceeded source100distance"); p.Delete();
    }
    private static void Colliders()
    {
        var p = Fresh(); Train(p, CombatAbility.Fissure); var a = Enemy(p); var b = Enemy(p); var at = TimedEffects.Now.AddDays(1); var field = Field(p, at);
        Set(field); var chosen = DruidFissure.Recipient(p, field); Require(chosen == a || chosen == b, "First native collider"); var other = chosen == a ? b : a;
        int chosenLife = SkillHealth.Balance256(chosen), otherLife = SkillHealth.Balance256(other); DruidFissure.Tick(field.NextUtc, true);
        var after = DruidFissure.Snapshot(); var hit = after.Cracks.Single();
        Require(hit.LastTarget == chosen.Serial.Value && SkillHealth.Balance256(chosen) < chosenLife && SkillHealth.Balance256(other) == otherLife
            && hit.Seed != field.Seed && after.Leases.Single().Target == chosen.Serial.Value, "One collider/damage/seed/lease");
        var lease = after.Leases.Single(); Require(!DruidFissure.Ready(field.Cast, chosen.Serial.Value, lease.UntilUtc.AddTicks(-1))
            && DruidFissure.Ready(field.Cast, chosen.Serial.Value, lease.UntilUtc) && DruidFissure.Ready(field.Cast + 1, chosen.Serial.Value, field.NextUtc), "Declared cast-local lease boundary");
        DruidFissure.Tick(hit.NextUtc, true); Require(DruidFissure.Snapshot().Cracks.Single().LastTarget == other.Serial.Value && SkillHealth.Balance256(other) < otherLife, "Last victim/NHD did not permit next collider");
        DruidFissure.Tick(lease.UntilUtc, true); Require(SkillHealth.Balance256(chosen) < chosenLife - field.Min256, "Alternation did not return to earlier victim at delay boundary");
        DruidFissure.Cancel(p); b.Delete(); a.Delete();
        var lone = Enemy(p); field = Field(p, at); Set(field); int life = SkillHealth.Balance256(lone); DruidFissure.Tick(at.AddMilliseconds(600), true);
        int loss = life - SkillHealth.Balance256(lone); Require(loss >= field.Min256 && loss < field.Max256 && DruidFissure.Snapshot().Cracks.Single().LastTarget == lone.Serial.Value, "LastCollide treated as per-victim repeat cooldown");
        DruidFissure.Cancel(p); lone.Delete(); p.Delete();
    }
    private static void DamageAndRecovery()
    {
        var p = Fresh(); Train(p, CombatAbility.Fissure); var target = Enemy(p); var at = TimedEffects.Now.AddDays(1); var field = Field(p, at, 100 * 256, 100 * 256);
        target.SetResistance(ResistanceType.Fire, 100); int life = SkillHealth.Balance256(target); Set(field); DruidFissure.Tick(field.NextUtc, true);
        Require(SkillHealth.Balance256(target) == life && DruidFissure.Snapshot().Cracks.Single().LastTarget == target.Serial.Value, "Immunity lost collision history");
        Require(CombatPotions.Apply(p, 3), "Actual tier3 potion"); field = Field(p, at, 100 * 256, 100 * 256) with { Modifiers = DruidFirestorm.Modifiers(p) }; Set(field); DruidFissure.Tick(field.NextUtc, true);
        Require(life - SkillHealth.Balance256(target) == 30 * 256, "Approved potion nonfire channels on fire immune");
        DruidFissure.Cancel(p); target.Delete(); p.Delete();
        p = Fresh(); Train(p, CombatAbility.Fissure); target = Enemy(p); target.SetHits(1000); field = Field(p, at, 400 * 256, 400 * 256); Set(field); DruidFissure.Tick(field.NextUtc, true);
        Require(target.Hits == 600 && CreatureHitRecovery.Remaining(target) > 0, "GetHit1 native damage/recovery");
        DruidFissure.Cancel(p); target.Delete(); p.Delete();
    }
    private static void Lifecycle()
    {
        var p = Fresh(); Train(p, CombatAbility.Fissure); var target = Enemy(p); var at = TimedEffects.Now.AddDays(1); var field = Field(p, at); Set(field);
        int life = SkillHealth.Balance256(target); DruidFissure.Create(p, new(p.X + 6, p.Y, p.Z), 1, at); DruidFissure.Tick(at.AddMilliseconds(3200), false);
        Require(DruidFissure.Snapshot() is { Centers.Length: 0, Cracks.Length: 1 } && SkillHealth.Balance256(target) == life, "Offline fought or stopped aging");
        DruidFissure.Tick(at.AddMilliseconds(3360), false); Require(DruidFissure.Snapshot().Cracks.Length == 0, "Offline crack failed to expire");
        Set(field); int map = p.Map.MapID; var origin = p.Location; p.MoveToWorld(origin, Map.Felucca);
        DruidFissure.Tick(field.NextUtc, true); Require(DruidFissure.Snapshot().Cracks.Single().Map == map && SkillHealth.Balance256(target) == life, "Ground field travelled/fought across facets");
        DruidFissure.Tick(field.UntilUtc, true); Require(DruidFissure.Snapshot().Cracks.Length == 0, "Old-facet lifetime stalled"); p.MoveToWorld(origin, Map.Maps[map]);
        Set(field); Require(ClassCreation.Profiles.TryActivateHistory(p, DiabloClass.Amazon), "Class-history transition fixture"); DruidFissure.Tick(field.NextUtc, true);
        Require(DruidFissure.Snapshot() is { Centers.Length: 0, Cracks.Length: 0 }, "Class change retained incompatible field"); target.Delete(); p.Delete();
    }
    private static void WorkerAndMalformed()
    {
        var before = DruidFissure.Snapshot(); string text = Text(before);
        var bytes = Task.Run(() => { var writer = new BufferWriter(new byte[2 * 1024 * 1024], true); DruidFissure.Profiles.Serialize(writer); return writer.Buffer[..(int)writer.Position]; }).GetAwaiter().GetResult();
        var reader = new BufferReader(bytes); Require(reader.ReadInt() == 0 && reader.ReadString() == text && Text() == text, "Worker serialization mutated actor state");
        var center = before.Centers.First(); var crack = before.Cracks.First();
        var invalid = new[]
        {
            before with { Centers = null }, before with { Cracks = null }, before with { Leases = null }, before with { NextId = 0 }, before with { DelayScope = "global-legacy" },
            before with { Centers = [center, center] }, before with { Cracks = [crack, crack] },
            before with { Centers = [center with { Frame = 80 }] }, before with { Centers = [center with { NextUtc = center.NextUtc.AddTicks(1) }] },
            before with { Cracks = [crack with { Frame = 84 }] }, before with { Cracks = [crack with { Rank = 256 }] },
            before with { Cracks = [crack with { Cast = crack.Id }] }, before with { Cracks = [crack with { Min256 = -1 }] },
            before with { Cracks = [crack with { NextUtc = DateTime.SpecifyKind(crack.NextUtc, DateTimeKind.Local) }] },
            before with { Leases = [new WeaponFlightLease(long.MaxValue, crack.Owner, crack.NextUtc)] }
        };
        foreach (var bad in invalid)
        {
            bool refused = false; try { DruidFissure.Restore(bad); } catch (InvalidDataException) { refused = true; }
            Require(refused && Text() == text, "Invalid restore partially replaced live pool");
        }
        var copy = DruidFissure.Snapshot(); copy.Centers[0] = center with { Frame = 79 }; Require(Text() == text, "Snapshot array leaked mutable pool");
    }
    private static void Save(PlayerMobile p, Mobile target) => File.WriteAllText(Manifest,
        JsonSerializer.Serialize(new Saved(p.Serial.Value, target.Serial.Value, SkillHealth.Balance256(target), DruidFissure.Snapshot())));
    private static void Check()
    {
        Require(!DruidCombat.Implemented(CombatAbility.Fissure), "Prepared component must not advertise learned availability");
        Require(DruidFissure.Snapshot() is { Centers.Length: 0, Cracks.Length: 0 }, "Unexpected existing Fissure fixtures");
        var seed = DruidFissure.SeedLow;
        try { DruidFissure.SeedLow = () => 1; SourceDamage(); Emitter(); Landing(); Colliders(); DamageAndRecovery(); Lifecycle(); }
        finally { DruidFissure.SeedLow = seed; }
        var p = Fresh(); Train(p, CombatAbility.Fissure); var target = Enemy(p); var at = TimedEffects.Now.AddDays(2);
        // The held injury is inspected across long natural controls/reloads.
        // Use the existing real prevent-heal state only on this fixture target;
        // ordinary damage and natural lifetime cases above remain unchanged.
        TimedEffects.PreventHealing(target, TimedEffects.Now);
        DruidFissure.Create(p, new(p.X + 6, p.Y, p.Z), 1, at); DruidFissure.Tick(at.AddMilliseconds(40), true);
        var first = DruidFissure.Snapshot().Cracks.Single(); target.MoveToWorld(new((first.X + 1) / 2, (first.Y + 1) / 2, first.Z), p.Map);
        DruidFissure.Tick(at.AddMilliseconds(160), true);
        Require(DruidFissure.Snapshot() is { Centers.Length: 1, Cracks.Length: > 0, Leases.Length: 1 }
            && DruidFissure.Snapshot().Cracks.Single().LastTarget == target.Serial.Value, "Held center/crack/active lease/history setup"); WorkerAndMalformed(); Save(p, target);
    }
    private static void Reload()
    {
        var saved = JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest)); var p = (PlayerMobile)World.FindMobile((Serial)saved.Owner); var target = World.FindMobile((Serial)saved.Target);
        Require(p != null && target != null && Text() == Text(saved.State) && SkillHealth.Balance256(target) == saved.Life256, "Exact native center/crack/seed/lease/life reload"); WorkerAndMalformed();
        var before = DruidFissure.Snapshot(); DateTime next = before.Centers.Select(x => x.NextUtc).Concat(before.Cracks.Select(x => x.NextUtc)).Min(); DruidFissure.Tick(next, true);
        var after = DruidFissure.Snapshot(); Require(after.Centers.Single().Frame == before.Centers.Single().Frame + 1, "Saved center failed to resume"); Save(p, target);
    }
    private static void Live(NetState network)
    {
        var held = DruidFissure.Snapshot(); var p = Fresh(); Train(p, CombatAbility.Fissure); network.Seeded = true; network.CompressionEnabled = false; network.Mobile = p; p.NetState = network;
        var protocol = typeof(NetState).GetField("_protocolState", BindingFlags.Instance | BindingFlags.NonPublic); Require(protocol != null, "Protocol bootstrap"); protocol.SetValue(network, Enum.Parse(protocol.FieldType, "GameServer_LoggedIn")); network.ResetMovementState(); network.SendMobileUpdate(p);
        var aim = new Point3D(p.X + 6, p.Y, p.Z); var sample = GroundMissileScatter.Sample(aim.X * 2, 0, 7); var target = Enemy(p);
        target.MoveToWorld(new((aim.X * 2 + sample.X + 1) / 2, (aim.Y * 2 + sample.Y + 1) / 2, p.Z), p.Map);
        int life = SkillHealth.Balance256(target); DruidFissure.Create(p, aim, 1, TimedEffects.Now);
        bool emitted = false, orphan = false, damaged = false; long deadline = Core.TickCount + 9000;
        void Inspect()
        {
            try
            {
                Require(Core.TickCount < deadline, "Natural prepared Fissure timeout"); var state = DruidFissure.Snapshot(); var centers = state.Centers.Where(x => x.Owner == p.Serial.Value).ToArray(); var cracks = state.Cracks.Where(x => x.Owner == p.Serial.Value).ToArray();
                emitted |= cracks.Length > 0; orphan |= centers.Length == 0 && cracks.Length > 0; damaged |= SkillHealth.Balance256(target) < life;
                if (centers.Length > 0 || cracks.Length > 0) { Timer.StartTimer(TimeSpan.FromMilliseconds(20), Inspect); return; }
                Require(emitted && orphan && damaged, "Natural timer missed emission/independent cracks/damage"); DruidFissure.Cancel(p); target.Delete(); p.Delete();
                Require(Text(DruidFissure.Snapshot() with { NextId = held.NextId }) == Text(held), "Natural fixture changed held state");
                var saved = JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest)); var owner = (PlayerMobile)World.FindMobile((Serial)saved.Owner); var last = World.FindMobile((Serial)saved.Target);
                Require(SkillHealth.Balance256(last) == saved.Life256, $"Natural fixture changed held target: {saved.Life256} to {SkillHealth.Balance256(last)}"); Save(owner, last); Log.Information("fissure-live-result: passed");
            }
            catch (Exception e) { DruidFissure.Cancel(p); target.Delete(); p.Delete(); Log.Error(e, "fissure-live-result: failed"); }
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(20), Inspect);
    }
}
