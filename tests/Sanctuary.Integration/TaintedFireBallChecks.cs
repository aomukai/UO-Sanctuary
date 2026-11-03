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

public static class TaintedFireBallChecks
{
    private static readonly ILogger Log = LogFactory.GetLogger(typeof(TaintedFireBallChecks));
    private sealed record Saved(uint Owner, uint Body, uint Helm, int Mana, int Life, int Map, Point3D Point, Point3D BodyPoint,
        string BodyState, TaintedFireBallFlight[] Flights, ulong ActorSeed, int Generation);
    private static string Manifest => Path.Join(Core.BaseDirectory, "tainted-fireball-fixtures.json");
    public static void Configure()
    {
        Register("tainted-fireball-check", Check);
        Register("tainted-fireball-reload-check", () => FixtureClock.NextLoop("tainted-fireball-reload-result", () => FixtureNetwork.Connected("tainted-fireball-reload-result", Reload)));
        var prior = ClassGearHooks.CanRegenerateMana;
        ClassGearHooks.CanRegenerateMana = p => p.Name != "TaintedFlightFixture" && prior?.Invoke(p) != false;
    }
    [CallPriority(int.MaxValue)] public static void Initialize()
    { var prior = ClassGearHooks.CanRegenerateHits; ClassGearHooks.CanRegenerateHits = p => p.Name != "TaintedFlightFixture" && prior?.Invoke(p) != false; }
    private static void Register(string name, Action run) => ConsoleInputHandler.RegisterCommand(name, "Tainted real NPC Fire Ball and original saved flights", _ => Core.LoopContext.Post(() =>
    { try { run(); Log.Information("{Name}: passed", name); } catch (Exception e) { Log.Error(e, "{Name}: failed", name); } }));
    private static void Require(bool value, string why) { if (!value) { throw new InvalidOperationException(why); } }
    internal static PlayerMobile Owner()
    {
        var p = new PlayerMobile { Name = "TaintedFlightFixture", Player = true, Body = 0x190 }; p.AddItem(new Backpack());
        ClassCreation.Profiles.Assign(p, DiabloClass.Warlock); ClassCreation.Profiles.AdvanceLevel(p, 47);
        ClassSkillChecks.Train(p, "warlock.summongoatman"); ClassSkillChecks.Train(p, "warlock.summontainted");
        ClassSkillChecks.Train(p, "warlock.demonicmastery", 16); ClassSkillChecks.Train(p, "warlock.bloodoath", 5);
        FixtureSpace.Corridor(p, 6300, 30, 3600); PoisonDaggerChecks.Named(p, "Tarnhelm");
        NecromancerEffects.SetMana256(p, p.ManaMax * 256); return p;
    }
    private static TaintedFireBallFlight State(long id) => TaintedFireBallMissiles.Snapshot().Flights.FirstOrDefault(s => s.Id == id);
    private static long Launch(WarlockTaintedBody b, DateTime at) => TaintedFireBallMissiles.LaunchPrepared(b, new(b.X + 20, b.Y, b.Z),
        b.Prepared.Snapshot.Birth.Suppliers.FireBallRank, TaintedNpcSkills.FireBall(b.Prepared.Snapshot.Birth.Suppliers), at);
    private static void Through(long id, int frames = 50, bool actions = true)
    { for (int i = 0; i < frames && State(id) is { } state; i++) { TaintedFireBallMissiles.Tick(state.NextUtc, actions, id); Require(State(id)?.Fault == null, "Flight fault: " + State(id)?.Fault); } }
    private static void Hold(long id, DateTime at)
    { var pool = TaintedFireBallMissiles.Snapshot(); TaintedFireBallMissiles.Restore(pool with { Flights = pool.Flights.Select(s => s.Id == id ? s with { NextUtc = at } : s).ToArray() }); }
    private static void Check()
    {
        var p = Owner(); var b = WarlockTaintedBody.CreatePrepared(p, new(p.X + 1, p.Y, p.Z), TimedEffects.Now.AddDays(2));
        var first = FireBoltChecks.Target(p, 9); var other = FireBoltChecks.Target(p, 9); other.MoveToWorld(new(other.X, other.Y + 1, other.Z), other.Map);
        var outside = FireBoltChecks.Target(p, 9); outside.MoveToWorld(new(outside.X, outside.Y + 4, outside.Z), outside.Map);
        try
        {
            foreach (var t in new[] { first, other, outside }) { t.SetHits(10000); SkillHealth.SetBalance256(t, 10000 * 256); }
            DateTime at = TimedEffects.Now.AddDays(1); int mana = NecromancerEffects.ManaBalance256(p), life = SkillHealth.Balance256(first);
            NpcEquipmentRandom.Set(b, SourceRandom.InitLow(42)); long id = Launch(b, at); var captured = State(id);
            Require(captured is { Frame: 0, VelocityFp8: 3840, Vx: .46875, Vy: 0 } && NecromancerEffects.ManaBalance256(p) == mana
                && SkillHealth.Balance256(first) == life && b is not ISourceUnarmedNpc, "NPC factory, original payment or unsupported melee binding");
            TaintedFireBallMissiles.Tick(at.AddMilliseconds(39), true, id); Require(State(id) == captured, "Early missile update");
            Through(id, 5); Require(State(id)?.Frame == 5 && SkillHealth.Balance256(first) == life, "Fire Ball hit without travel");
            Require(CombatPotions.Apply(p, 3), "Owner potion control");
            var helm = p.FindItemOnLayer(Layer.Helm); p.Backpack.DropItem(helm); ClassGear.Refresh(p); ClassSkillChecks.Train(p, "warlock.summontainted", 2);
            var before = State(id); Require(before.Packet == captured.Packet && before.Rank == captured.Rank, "In-flight gear/rank recapture");
            ulong seed = NpcEquipmentRandom.State(b).Seed; uint a = SourceRandom.Roll(ref seed), c = SourceRandom.Roll(ref seed);
            Require(a % 100 < 5 && c % 100 >= 5, "Mixed positive/negative intrinsic fixture");
            ulong stream = captured.Seed; int raw = WeaponMissileDamage.RollValue(captured.Packet.Minimum256, captured.Packet.Maximum256, 0, n => (int)SourceRandom.Limited(ref stream, n));
            Through(id);
            int one = life - SkillHealth.Balance256(first), two = life - SkillHealth.Balance256(other);
            Require(State(id) == null && new[] { one, two }.Order().SequenceEqual(new[] { raw * (a % 100 < 5 ? 2 : 1), raw * (c % 100 < 5 ? 2 : 1) }.Order())
                && SkillHealth.Balance256(outside) == life && NpcEquipmentRandom.State(b).Seed == seed
                && MonsterUninterruptibles.State(first)?.LastAttacker == b.Serial.Value && NecromancerEffects.ManaBalance256(p) == mana,
                $"Shared raw roll, per-recipient intrinsic, radius, owner potion, NPC identity or payment:raw={raw},one={one},two={two}");
            Require(p.EquipItem(helm), "Original helm"); ClassGear.Refresh(p); TimedEffects.Clear(p);
            TaintedFireBallMissiles.Cancel(b); first.Delete(); other.Delete(); outside.Delete();
            long empty = Launch(b, at); Through(empty, 49); Require(State(empty)?.Frame == 49, "Empty Fire Ball expired early"); Through(empty, 1); Require(State(empty) == null, "50-frame expiry missing");
            long offline = Launch(b, at); var held = State(offline); Through(offline, 10, false); Require(State(offline) is { Frame: 10 } aged && aged.X == held.X && aged.Seed == held.Seed, "Offline replay/movement/RNG");
            Through(offline, 40, false); Require(State(offline) == null, "Offline lifetime stopped");
            var amazon = new PlayerMobile { Body = 0x191, Player = true };
            try
            {
                ClassCreation.Profiles.Assign(amazon, DiabloClass.Amazon);
                Require(AmazonSlowMissiles.Apply(amazon, b, 20, TimedEffects.Now), "Slow Missiles NPC state");
            }
            finally { amazon.Delete(); }
            long slow = Launch(b, at); var slowed = State(slow);
            Require(slowed.Slow != SlowMissilePayload.Normal && slowed.VelocityFp8 < 3840 && slowed.Packet.Minimum256 < captured.Packet.Minimum256, "Slow Missiles not captured on NPC");
            AmazonSlowMissiles.Clear(b); Require(State(slow) == slowed, "Flight reread removed Slow Missiles"); TaintedFireBallMissiles.Cancel(b);
            long invalid = Launch(b, at); var pool = TaintedFireBallMissiles.Snapshot(); string original = JsonSerializer.Serialize(pool);
            foreach (var bad in new[] { State(invalid) with { Frame = 50 }, State(invalid) with { Vx = double.NaN }, State(invalid) with { Packet = captured.Packet with { Radius = 5 } } })
            {
                try { TaintedFireBallMissiles.Restore(pool with { Flights = pool.Flights.Append(bad).ToArray() }); throw new InvalidOperationException("Malformed pool admitted"); } catch (InvalidDataException) { }
                Require(JsonSerializer.Serialize(TaintedFireBallMissiles.Snapshot()) == original, "Rejected restore changed live pool");
            }
            TaintedFireBallMissiles.Cancel(b);
        }
        finally { TaintedFireBallMissiles.Cancel(b); first.Delete(); other.Delete(); outside.Delete(); b.Delete(); Require(NpcEquipmentRandom.State(b) == null, "Deleted actor retained random binding"); p.Delete(); }
        Save();
    }
    private static void Save()
    {
        var p = Owner(); var b = WarlockTaintedBody.CreatePrepared(p, new(p.X + 1, p.Y, p.Z), TimedEffects.Now.AddDays(2));
        SkillHealth.SetBalance256(b, b.MaximumLife256 - 257); NpcEquipmentRandom.Set(b, SourceRandom.InitLow(173));
        var at = TimedEffects.Now.AddDays(2); long first = Launch(b, at), second = Launch(b, at);
        var saved = new Saved(p.Serial.Value, b.Serial.Value, p.FindItemOnLayer(Layer.Helm).Serial.Value, NecromancerEffects.ManaBalance256(p), SkillHealth.Balance256(b),
            p.Map.MapID, p.Location, b.Location, b.SavedText, new[] { State(first), State(second) }, NpcEquipmentRandom.State(b).Seed, 0);
        PersistentMinions.Suspend(p); p.Internalize(); File.WriteAllText(Manifest, JsonSerializer.Serialize(saved));
    }
    private static void Reload(NetState network)
    {
        var saved = JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));
        var p = (PlayerMobile)World.FindMobile((Serial)saved.Owner); var b = (WarlockTaintedBody)World.FindMobile((Serial)saved.Body);
        Require(saved.Generation < 2 && b.ControlMaster == p && b.SavedText == saved.BodyState && SkillHealth.Balance256(b) == saved.Life
            && NpcEquipmentRandom.State(b)?.Seed == saved.ActorSeed && World.FindItem((Serial)saved.Helm)?.Parent == p
            && NecromancerEffects.ManaBalance256(p) == saved.Mana && saved.Flights.All(s => State(s.Id) == s), "Original body, streams, wounds, gear, payment or held flights lost on reload");
        var account = FixtureNetwork.Authenticate(network, p, "tainted-flight-reload-"); p.MoveToWorld(saved.Point, Map.Maps[saved.Map]); PersistentMinions.Resume(p);
        b.MoveToWorld(saved.BodyPoint, p.Map); var t = FireBoltChecks.Target(p, 9); t.SetHits(10000); SkillHealth.SetBalance256(t, 10000 * 256);
        var flight = saved.Flights[0]; Hold(flight.Id, TimedEffects.Now.AddMilliseconds(40)); int life = SkillHealth.Balance256(t); var deadline = TimedEffects.Now.AddSeconds(5);
        void Observe()
        {
            if (State(flight.Id) != null && TimedEffects.Now < deadline) { Timer.StartTimer(TimeSpan.FromMilliseconds(20), () => Core.LoopContext.Post(Observe)); return; }
            try
            {
                Require(State(flight.Id) == null && SkillHealth.Balance256(t) < life && MonsterUninterruptibles.State(t)?.LastAttacker == b.Serial.Value
                    && NecromancerEffects.ManaBalance256(p) == saved.Mana && SkillHealth.Balance256(b) == saved.Life, "Original NPC flight failed automatic reload travel/damage or recharged/healed");
                var remaining = saved.Flights.Skip(1).ToArray(); Require(remaining.All(s => State(s.Id) == s), "Unreleased saved cohort changed");
                var next = saved with { Flights = remaining, ActorSeed = NpcEquipmentRandom.State(b).Seed, Generation = saved.Generation + 1 };
                t.Delete(); PersistentMinions.Suspend(p); p.Internalize(); network.Mobile = null; p.NetState = null; account[0] = null; network.Account = null; account.Delete();
                File.WriteAllText(Manifest, JsonSerializer.Serialize(next)); Log.Information("tainted-fireball-reload-result: passed");
            }
            catch (Exception e) { Log.Error(e, "tainted-fireball-reload-result: failed"); }
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(20), () => Core.LoopContext.Post(Observe));
    }
}
