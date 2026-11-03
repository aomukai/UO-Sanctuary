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

public static class TaintedRankChecks
{
    private static readonly ILogger Log = LogFactory.GetLogger(typeof(TaintedRankChecks));
    private sealed record Saved(uint Owner, uint Body, uint Helm, int Mana, int Life, int Map, Point3D Point, Point3D BodyPoint, string BodyState, int Generation);
    private static string Manifest => Path.Join(Core.BaseDirectory, "tainted-ranks-fixtures.json");
    public static void Configure()
    {
        Register("tainted-ranks-check", Check);
        Register("tainted-ranks-reload-check", () => FixtureClock.NextLoop("tainted-ranks-reload-result", () => FixtureNetwork.Connected("tainted-ranks-reload-result", Reload)));
    }
    private static void Register(string name, Action run) => ConsoleInputHandler.RegisterCommand(name, "Tainted registered innate skill rank and release-time refresh", _ => Core.LoopContext.Post(() =>
    { try { run(); Log.Information("{Name}: passed", name); } catch (Exception e) { Log.Error(e, "{Name}: failed", name); } }));
    private static void Require(bool ok, string why) { if (!ok) { throw new InvalidOperationException(why); } }
    private static PlayerMobile Supplier()
    {
        var p = new PlayerMobile { Body = 0x190, Player = true }; p.AddItem(new Backpack());
        ClassCreation.Profiles.Assign(p, DiabloClass.Barbarian); ClassCreation.Profiles.AdvanceLevel(p, 99); ClassSkillChecks.Train(p, "barbarian.battlecommand"); return p;
    }
    private static void Command(WarlockTaintedBody b, DateTime at)
    { var p = Supplier(); try { BattleCommand.Apply(p, b, 1, at); } finally { p.Delete(); } }
    private static TaintedFireBallFlight[] Flights(WarlockTaintedBody b) => TaintedFireBallMissiles.Snapshot().Flights.Where(s => s.Actor == b.Serial.Value).ToArray();
    private static void Source()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory, "innate-npc-rank-source-cases.json"))); int count = 0;
        foreach (var row in doc.RootElement.EnumerateArray())
        {
            int rank = row[0].GetInt32(), bonus = row[1].GetInt32(); bool item = row[2].GetInt32() != -1, enabled = row[3].GetInt32() != 0;
            Require(InnateNpcSkills.Level(rank, bonus, enabled, item) == row[4].GetInt32(), "Complete shared innate getter context");
            if (!item && enabled) { Require(DruidSpiritStats.EffectiveAuraRank(rank, bonus) == row[4].GetInt32(), "Druid spirit getter changed while sharing the rule"); }
            count++;
        }
        Require(count == 2040, "Missing complete innate getter cases");
    }
    private static void Check()
    {
        Source(); var p = TaintedFireBallChecks.Owner(); var b = WarlockTaintedBody.CreatePrepared(p, new(p.X + 1, p.Y, p.Z), TimedEffects.Now.AddDays(2));
        var t = FireBoltChecks.Target(p, 9); var supplier = Supplier();
        try
        {
            var born = b.Prepared; int mana = NecromancerEffects.ManaBalance256(p); var at = TimedEffects.Now.AddDays(1);
            Require(TaintedNpcRanks.Level(b, 422) == 2 && TaintedNpcRanks.Level(b, 420) == 2 && TaintedNpcRanks.Level(b, 380) == 0, "Captured registered skill roster");
            BattleCommand.Apply(supplier, p, 1, at);
            Require(TaintedNpcRanks.Level(b, 422) == 2 && b.Prepared == born, "Owner allskills leaked or recaptured the pet"); BattleCommand.Clear(p);
            var outer = new GenericPetActionServices((_, _, _) => { }, _ => null, (_, _) => 0, (_, _) => false, _ => false, () => { },
                (_, _, _, _, _) => false, () => { }, _ => null, (_, _) => false, (_, _, _, _, _, _) => false, _ => null, _ => false, (_, _) => 0, _ => true, _ => 0);
            var bound = TaintedFireBallCasting.BindRegistered(b, outer, (_, rank) => rank == 2, () => at);
            Require(bound.ExecuteSkill(5, 422, unchecked((int)t.Serial.Value), 0, 0, true) && TaintedFireBallCasting.State(b) is { Rank: 2, LiveRank: true }, "Registered action adapter retained a caller-supplied rank");
            BattleCommand.Apply(supplier, b, 1, at);
            Require(TaintedNpcRanks.Level(b, 422) == 3 && TaintedNpcRanks.Level(b, 422, false) == 2 && TaintedNpcRanks.Level(b, 420) == 3
                && TaintedNpcRanks.Level(b, 380) == 0 && b.Prepared == born, "Own bonus/base getter, absent skill or immutable birth");
            var action = TaintedFireBallCasting.State(b); TaintedFireBallCasting.Tick(action.ActionUtc, true); var flight = Flights(b).Single();
            Require(flight.Rank == 3 && TaintedFireBallCasting.State(b).ReleasedRank == 3 && flight.Packet == TaintedNpcSkills.FireBall(born.Snapshot.Birth.Suppliers with { FireBallRank = 3 }), "Command gained during windup did not refresh the release packet");
            BattleCommand.Clear(b); Require(Flights(b).Single() == flight && TaintedNpcRanks.Level(b, 422) == 2, "Issued flight changed after Command removal");
            TaintedFireBallCasting.Cancel(b); TaintedFireBallMissiles.Cancel(b);
            Command(b, at); Require(TaintedFireBallCasting.BeginRegistered(b, t, at), "Registered boosted start"); action = TaintedFireBallCasting.State(b);
            Require(action.Rank == 3, "Start did not read own effective rank"); BattleCommand.Clear(b);
            TaintedFireBallCasting.Tick(action.ActionUtc, true); Require(Flights(b).Single().Rank == 2 && TaintedFireBallCasting.State(b).ReleasedRank == 2, "Removed Command left a stale release rank");
            Require(b.Prepared == born && NecromancerEffects.ManaBalance256(p) == mana, "Rank refresh rewrote birth or charged owner mana");
        }
        finally { TaintedFireBallCasting.Cancel(b); TaintedFireBallMissiles.Cancel(b); BattleCommand.Clear(b); BattleCommand.Clear(p); supplier.Delete(); t.Delete(); b.Delete(); p.Delete(); }
        Save();
    }
    private static void Save()
    {
        var p = TaintedFireBallChecks.Owner(); var b = WarlockTaintedBody.CreatePrepared(p, new(p.X + 1, p.Y, p.Z), TimedEffects.Now.AddDays(2));
        SkillHealth.SetBalance256(b, b.MaximumLife256 - 257); Command(b, TimedEffects.Now.AddDays(2)); Require(TaintedNpcRanks.Level(b, 422) == 3, "Saved own Command rank");
        var saved = new Saved(p.Serial.Value, b.Serial.Value, p.FindItemOnLayer(Layer.Helm).Serial.Value, NecromancerEffects.ManaBalance256(p), SkillHealth.Balance256(b), p.Map.MapID, p.Location, b.Location, b.SavedText, 0);
        PersistentMinions.Suspend(p); p.Internalize(); File.WriteAllText(Manifest, JsonSerializer.Serialize(saved));
    }
    private static void Reload(NetState network)
    {
        var saved = JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest)); var p = (PlayerMobile)World.FindMobile((Serial)saved.Owner); var b = (WarlockTaintedBody)World.FindMobile((Serial)saved.Body);
        Require(saved.Generation < 2 && b.ControlMaster == p && b.SavedText == saved.BodyState && SkillHealth.Balance256(b) == saved.Life && NecromancerEffects.ManaBalance256(p) == saved.Mana
            && World.FindItem((Serial)saved.Helm)?.Parent == p && TaintedNpcRanks.Level(b, 422) == 3 && TaintedFireBallCasting.State(b) == null, "Original pet, gear, wounds, Command and effective rank lost on reload");
        var account = FixtureNetwork.Authenticate(network, p, "tainted-rank-reload-"); p.MoveToWorld(saved.Point, Map.Maps[saved.Map]); PersistentMinions.Resume(p); b.MoveToWorld(saved.BodyPoint, p.Map);
        var t = FireBoltChecks.Target(p, 9); t.SetHits(10000); SkillHealth.SetBalance256(t, 10000 * 256); int hp = SkillHealth.Balance256(t);
        var command = BattleCommand.State(b) with { UntilUtc = TimedEffects.Now.AddMilliseconds(80) }; var pool = BattleCommand.Snapshot();
        BattleCommand.Restore(pool with { States = pool.States.Where(s => s.Recipient != b.Serial.Value).Append(command).ToArray() });
        Require(TaintedFireBallCasting.BeginRegistered(b, t, TimedEffects.Now) && TaintedFireBallCasting.State(b).Rank == 3, "Boosted registered reload start");
        bool sawBaseRank = false; var deadline = TimedEffects.Now.AddSeconds(5);
        void Observe()
        {
            sawBaseRank |= Flights(b).Any(s => s.Rank == 2) || TaintedFireBallCasting.State(b) is { Released: true, ReleasedRank: 2 };
            if ((SkillHealth.Balance256(t) == hp || TaintedFireBallCasting.State(b) != null) && TimedEffects.Now < deadline)
            { Timer.StartTimer(TimeSpan.FromMilliseconds(20), () => Core.LoopContext.Post(Observe)); return; }
            try
            {
                Require(sawBaseRank && BattleCommand.State(b) == null && TaintedNpcRanks.Level(b, 422) == 2 && SkillHealth.Balance256(t) < hp && Flights(b).Length == 0
                    && MonsterUninterruptibles.State(t)?.LastAttacker == b.Serial.Value && SkillHealth.Balance256(b) == saved.Life && NecromancerEffects.ManaBalance256(p) == saved.Mana,
                    "Natural Command expiry did not refresh the real A2/flight rank or altered resources");
                t.Delete(); Command(b, TimedEffects.Now.AddDays(2)); PersistentMinions.Suspend(p); p.Internalize(); network.Mobile = null; p.NetState = null; account[0] = null; network.Account = null; account.Delete();
                File.WriteAllText(Manifest, JsonSerializer.Serialize(saved with { Generation = saved.Generation + 1 })); Log.Information("tainted-ranks-reload-result: passed");
            }
            catch (Exception e) { Log.Error(e, "tainted-ranks-reload-result: failed"); }
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(20), () => Core.LoopContext.Post(Observe));
    }
}
