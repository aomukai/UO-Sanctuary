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

public static class TaintedCastingChecks
{
    private static readonly ILogger Log = LogFactory.GetLogger(typeof(TaintedCastingChecks));
    private sealed record Saved(uint Owner, uint Body, uint Helm, uint PendingTarget, int Mana, int Life, int Map, Point3D Point, Point3D BodyPoint, string BodyState, int Generation);
    private static string Manifest => Path.Join(Core.BaseDirectory, "tainted-casting-fixtures.json");
    public static void Configure()
    {
        Register("tainted-casting-check", Check);
        Register("tainted-casting-reload-check", () => FixtureClock.NextLoop("tainted-casting-reload-result", () => FixtureNetwork.Connected("tainted-casting-reload-result", Reload)));
    }
    private static void Register(string name, Action run) => ConsoleInputHandler.RegisterCommand(name, "Tainted A2 event-driven NPC spell release", _ => Core.LoopContext.Post(() =>
    { try { run(); Log.Information("{Name}: passed", name); } catch (Exception e) { Log.Error(e, "{Name}: failed", name); } }));
    private static void Require(bool ok, string why) { if (!ok) { throw new InvalidOperationException(why); } }
    private static TaintedFireBallFlight[] Flights(WarlockTaintedBody b) => TaintedFireBallMissiles.Snapshot().Flights.Where(s => s.Actor == b.Serial.Value).ToArray();
    private static void Source()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory, "tainted-cast-source-cases.json")));
        int count = 0;
        foreach (var row in doc.RootElement.EnumerateArray())
        {
            int rate = row[0].GetInt32(), speed = SourceAttackRate.Speed(256, 0, rate); var flags = new int[144]; flags[6] = 2;
            var plan = SourceAnimationEvents.Initial(new(0, speed, 10 * 256, flags, []), 0);
            Require(speed == row[1].GetInt32() && row[2].GetInt32() == 1 && plan.Events.Length == row[3].GetArrayLength(), "Complete NPC A2 writer/scheduler case");
            for (int i = 0; i < plan.Events.Length; i++)
            { var e = plan.Events[i]; Require(JsonSerializer.Serialize(new[] { e.Type, e.Frame, e.Kind, e.Param }) == row[3][i].GetRawText(), "A2 ordered action/end event"); }
            count++;
        }
        Require(count == 401, "Missing A2 source cases");
    }
    private static GenericPetActionServices Services(WarlockTaintedBody b, Mobile target, Action delegated) => new(
        (_, _, _) => throw new InvalidOperationException("Unexpected aura skill selection"),
        id => id == unchecked((int)target.Serial.Value) ? new(1, true, target.X * 2, target.Y * 2) : null,
        (x, y) => checked((x - b.X * 2) * (x - b.X * 2) + (y - b.Y * 2) * (y - b.Y * 2)),
        (_, _) => false, _ => false, () => throw new InvalidOperationException("Unexpected move reset"),
        (_, _, _, _, _) => throw new InvalidOperationException("Unexpected move"), () => throw new InvalidOperationException("Unexpected move event"),
        _ => null, (_, _) => false, (_, _, _, _, _, _) => { delegated(); return false; },
        _ => throw new InvalidOperationException("Unexpected base mode"), _ => throw new InvalidOperationException("Unexpected base mode dispatch"),
        (_, _) => 0, _ => true, _ => 0);
    private static void Check()
    {
        Source(); var p = TaintedFireBallChecks.Owner(); var b = WarlockTaintedBody.CreatePrepared(p, new(p.X + 1, p.Y, p.Z), TimedEffects.Now.AddDays(2));
        var target = FireBoltChecks.Target(p, 9); target.SetHits(10000); SkillHealth.SetBalance256(target, 10000 * 256);
        try
        {
            DateTime at = TimedEffects.Now.AddDays(1); int mana = NecromancerEffects.ManaBalance256(p), hp = SkillHealth.Balance256(target), delegated = 0, admits = 0;
            bool accepted = false; var outer = Services(b, target, () => delegated++); int rank = b.Prepared.Snapshot.Birth.Suppliers.FireBallRank;
            var bound = TaintedFireBallCasting.Bind(b, outer, () => rank, (_, _) => { admits++; return accepted; }, () => at);
            Require(!bound.ExecuteSkill(5, 422, unchecked((int)target.Serial.Value), 0, 0, true) && admits == 1 && TaintedFireBallCasting.State(b) == null, "Mode admission refusal started a cast");
            Require(!bound.ExecuteSkill(5, 999, 0, 0, 0, true) && delegated == 1, "Other NPC skill service was overwritten");
            try { bound.ExecuteSkill(4, 422, unchecked((int)target.Serial.Value), 0, 0, true); throw new InvalidOperationException("A1 silently substituted for A2"); } catch (NotSupportedException) { }
            accepted = true;
            var frame = new GenericPetDecisionFrame(GenericPetProfiles.Raw(CombatAbility.SummonTainted), new() { Value0 = -1 });
            var context = new GenericPetCombatContext(1, new(1, true, b.X * 2, b.Y * 2));
            var rows = new[] { new GenericPetCombatCandidate(unchecked((int)target.Serial.Value), 0, 10, 422, rank, 5, 0, 11, 0, 0, 0) };
            Require(GenericPetActions.Try(context, frame, rows, null, 0, bound), "Existing GenericPet action wrapper did not reach NPC A2");
            var action = TaintedFireBallCasting.State(b); var clock = TaintedFireBallCasting.Timing(b);
            Require(action != null && !action.Released && action.Speed == clock.Speed && action.ActionUtc == at.AddMilliseconds(clock.Action * 40)
                && action.EndUtc == at.AddMilliseconds(clock.End * 40) && Flights(b).Length == 0 && NecromancerEffects.ManaBalance256(p) == mana
                && !TaintedFireBallCasting.BeginPrepared(b, target, rank, at), "A2 windup/busy/payment timing");
            TaintedFireBallCasting.Tick(action.ActionUtc.AddTicks(-1), true); Require(Flights(b).Length == 0 && SkillHealth.Balance256(target) == hp, "Early A2 skill event");
            target.MoveToWorld(new(target.X, target.Y + 1, target.Z), target.Map);
            TaintedFireBallCasting.Tick(action.ActionUtc, true); var flight = Flights(b).Single();
            Require(flight.Vy > 0 && flight.Rank == rank && TaintedFireBallCasting.State(b) is { Released: true, Fault: null } issued
                && issued.Flight == flight.Id && flight.NextUtc == action.ActionUtc.AddMilliseconds(40) && SkillHealth.Balance256(target) == hp, "A2 live-target release/factory ownership");
            TaintedFireBallCasting.Tick(action.ActionUtc, true); Require(Flights(b).Length == 1 && NecromancerEffects.ManaBalance256(p) == mana, "Duplicate action event emitted or repaid");
            TaintedFireBallCasting.Tick(action.EndUtc, true); Require(TaintedFireBallCasting.State(b) == null && Flights(b).Single().Id == flight.Id, "End animation deleted the issued missile or repeated release");
            for (int i = 0; i < 50 && Flights(b).Length > 0; i++) { TaintedFireBallMissiles.Tick(Flights(b).Single().NextUtc, true, flight.Id); }
            Require(Flights(b).Length == 0 && SkillHealth.Balance256(target) < hp && MonsterUninterruptibles.State(target)?.LastAttacker == b.Serial.Value, "Scheduled NPC A2 did not cause real projectile damage");
            Require(TaintedFireBallCasting.BeginPrepared(b, target, rank, at), "Second prepared cast"); TaintedFireBallCasting.Cancel(b);
            TaintedFireBallCasting.Tick(at.AddSeconds(4), true); Require(TaintedFireBallCasting.State(b) == null && Flights(b).Length == 0, "Cancelled action replayed");
            Require(TaintedFireBallCasting.BeginPrepared(b, target, rank, at), "Offline action control"); TaintedFireBallCasting.Tick(at.AddSeconds(4), false);
            Require(TaintedFireBallCasting.State(b) == null && Flights(b).Length == 0 && NecromancerEffects.ManaBalance256(p) == mana, "Offline action emitted or repaid");
        }
        finally { TaintedFireBallCasting.Cancel(b); TaintedFireBallMissiles.Cancel(b); target.Delete(); b.Delete(); p.Delete(); }
        Save();
    }
    private static void Save()
    {
        var p = TaintedFireBallChecks.Owner(); var b = WarlockTaintedBody.CreatePrepared(p, new(p.X + 1, p.Y, p.Z), TimedEffects.Now.AddDays(2));
        var t = FireBoltChecks.Target(p, 9); SkillHealth.SetBalance256(b, b.MaximumLife256 - 257);
        Require(TaintedFireBallCasting.BeginPrepared(b, t, b.Prepared.Snapshot.Birth.Suppliers.FireBallRank, TimedEffects.Now.AddDays(2)), "Unissued saved action fixture");
        File.WriteAllText(Manifest, JsonSerializer.Serialize(new Saved(p.Serial.Value, b.Serial.Value, p.FindItemOnLayer(Layer.Helm).Serial.Value, t.Serial.Value,
            NecromancerEffects.ManaBalance256(p), SkillHealth.Balance256(b), p.Map.MapID, p.Location, b.Location, b.SavedText, 0)));
    }
    private static void Reload(NetState network)
    {
        var saved = JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest)); var p = (PlayerMobile)World.FindMobile((Serial)saved.Owner); var b = (WarlockTaintedBody)World.FindMobile((Serial)saved.Body);
        Require(saved.Generation < 2 && b.ControlMaster == p && b.SavedText == saved.BodyState && SkillHealth.Balance256(b) == saved.Life && TaintedFireBallCasting.State(b) == null
            && Flights(b).Length == 0 && World.FindItem((Serial)saved.Helm)?.Parent == p && NecromancerEffects.ManaBalance256(p) == saved.Mana, "Unissued A2 replayed or original body/gear/wounds/payment changed");
        World.FindMobile((Serial)saved.PendingTarget)?.Delete(); var account = FixtureNetwork.Authenticate(network, p, "tainted-cast-reload-");
        p.MoveToWorld(saved.Point, Map.Maps[saved.Map]); PersistentMinions.Resume(p); b.MoveToWorld(saved.BodyPoint, p.Map);
        var t = FireBoltChecks.Target(p, 9); t.SetHits(10000); SkillHealth.SetBalance256(t, 10000 * 256); int hp = SkillHealth.Balance256(t);
        Require(TaintedFireBallCasting.BeginPrepared(b, t, b.Prepared.Snapshot.Birth.Suppliers.FireBallRank, TimedEffects.Now), "Original NPC cannot start after reload");
        var deadline = TimedEffects.Now.AddSeconds(5);
        void Observe()
        {
            if ((SkillHealth.Balance256(t) == hp || TaintedFireBallCasting.State(b) != null) && TimedEffects.Now < deadline)
            { Timer.StartTimer(TimeSpan.FromMilliseconds(20), () => Core.LoopContext.Post(Observe)); return; }
            try
            {
                Require(SkillHealth.Balance256(t) < hp && TaintedFireBallCasting.State(b) == null && Flights(b).Length == 0
                    && MonsterUninterruptibles.State(t)?.LastAttacker == b.Serial.Value && SkillHealth.Balance256(b) == saved.Life
                    && NecromancerEffects.ManaBalance256(p) == saved.Mana, "Automatic original-NPC A2/flight reload sequence failed");
                t.Delete(); PersistentMinions.Suspend(p); p.Internalize(); network.Mobile = null; p.NetState = null; account[0] = null; network.Account = null; account.Delete();
                File.WriteAllText(Manifest, JsonSerializer.Serialize(saved with { PendingTarget = 0, Generation = saved.Generation + 1 })); Log.Information("tainted-casting-reload-result: passed");
            }
            catch (Exception e) { Log.Error(e, "tainted-casting-reload-result: failed"); }
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(20), () => Core.LoopContext.Post(Observe));
    }
}
