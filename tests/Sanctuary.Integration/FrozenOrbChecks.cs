using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class FrozenOrbChecks
{
    private static readonly ILogger _log = LogFactory.GetLogger(typeof(FrozenOrbChecks));
    private sealed record Saved(uint Owner, FrozenMissile Orb, FrozenMissile Curve);
    private static string Manifest => Path.Join(Core.BaseDirectory, "frozen-orb-checks.json");
    public static void Configure() { Register("frozen-orb-check", Check); Register("frozen-orb-reload-check", Reload); }
    private static void Register(string name, Action action) => ConsoleInputHandler.RegisterCommand(name, "Frozen Orb paths, collision and persistence",
        _ => Core.LoopContext.Post(() => { try { action(); _log.Information("{Name}: passed", name); } catch (Exception e) { _log.Error(e, "{Name}: failed", name); } }));
    private static void Require(bool value, string why) { if (!value) { throw new InvalidOperationException(why); } }
    private static void Later(int milliseconds, string result, Action action) => Timer.StartTimer(TimeSpan.FromMilliseconds(milliseconds), () =>
    { try { action(); } catch (Exception e) { _log.Error(e, "{Name}: failed", result); } });
    private static GraveyardSkeleton Enemy(PlayerMobile owner, int x)
    {
        var enemy = new GraveyardSkeleton { Frozen = true, CantWalk = true, NoKillAwards = true };
        enemy.MoveToWorld(new(owner.X + x, owner.Y, owner.Z), owner.Map); enemy.AIObject.AITimer.Stop();
        enemy.SetHits(20000); enemy.Hits = enemy.HitsMax; enemy.SetResistance(ResistanceType.Cold, 0); return enemy;
    }
    private static void Check()
    {
        Require(MissileVectors.Direction(30, 0) == new Point2D(4096, 0) && MissileVectors.Direction(15, 15) == new Point2D(2896, 2896)
            && MissileVectors.Velocity(30, 0, 10).X == .234375 && SkillGeometry.Velocity(10) == .234375
            && MissileVectors.Velocity(15, 15, 24).X == .397705078125, "Compiled-reference Q12/factory velocity conversion");
        Require(FrozenOrbs.Direction(19) == new Point2D(-8, 28) && FrozenOrbs.Direction(38) == new Point2D(-24, -16)
            && FrozenOrbs.Turn(30, 0) == new Point2D(15, 15) && FrozenOrbs.Turn(15, 15) == new Point2D(0, 15)
            && FrozenOrbs.Turn(0, 15) == new Point2D(-7, 7), "Source orb direction stride or signed nova turns");
        var owner = ClassSkillChecks.Player(DiabloClass.Sorceress);
        owner.MoveToWorld(ClassSkillChecks.Corridor(owner, 5880, 1670), owner.Map);
        var body = Enemy(owner, 1); var nearest = Enemy(owner, 5); var behind = Enemy(owner, 6); var repeated = Enemy(owner, 9);
        var immune = Enemy(owner, 13); immune.SetResistance(ResistanceType.Cold, 100); int life = body.Hits;
        var saved = FrozenOrbs.Snapshot(); long id = saved.Select(x => x.Id).DefaultIfEmpty().Max(); var now = TimedEffects.Now;
        FrozenMissile Missile(FrozenKind kind, double x, int frame = 0, int index = 0) => new(++id, owner.Serial.Value, 1, owner.Map.MapID, kind,
            x, owner.Y, owner.Z, kind == FrozenKind.Orb ? .234375 : .421875, 0, frame, index, 30, 0,
            kind == FrozenKind.Orb ? 0 : 2560, kind == FrozenKind.Orb ? 0 : 2560, 200, 0, now.AddMilliseconds(1000), now);
        var orb = Missile(FrozenKind.Orb, owner.X + .1, 20, 32); var curve = Missile(FrozenKind.Nova, owner.X + 18);
        var edge = Missile(FrozenKind.Orb, owner.Map.Width - .05, 10, 32);
        FrozenOrbs.Restore(saved.Concat(new[] { orb, curve, edge, Missile(FrozenKind.Bolt, owner.X + 4), Missile(FrozenKind.Bolt, owner.X + 8),
            Missile(FrozenKind.Bolt, owner.X + 8), Missile(FrozenKind.Bolt, owner.X + 12) }).ToArray());
        Later(60, "frozen-orb-result", () =>
        {
            Require(body.Hits == life && FrozenOrbs.Snapshot().Single(x => x.Id == orb.Id).LastCollision == body.Serial.Value,
                "Orb body dealt damage or did not pass through/remember collision");
            Require(nearest.Hits == life - 12 && behind.Hits == life && repeated.Hits == life - 24 && immune.Hits == life
                && GearEffects.ChillPercent(nearest) > 0 && GearEffects.ChillPercent(immune) == 0,
                "Nearest non-piercing collision, independent same-frame shards, cold/chill or immunity");
            Require(!FrozenOrbs.Snapshot().Any(x => x.Id == edge.Id) && FrozenOrbs.Snapshot().Count(x => x.Kind == FrozenKind.Nova) == 1,
                "Premature orb destruction created a terminal nova");
            FrozenOrbs.Restore(FrozenOrbs.Snapshot().Where(x => x.Owner != owner.Serial.Value || x.Id == curve.Id).ToArray());
            body.Delete(); nearest.Delete(); behind.Delete(); repeated.Delete(); immune.Delete();
        });
        Later(280, "frozen-orb-result", () =>
        {
            var current = FrozenOrbs.Snapshot().Single(x => x.Id == curve.Id);
            Require(current.Frame >= 6 && current.VectorX == -7 && current.VectorY == 7 && current.Vx < 0 && current.Vy > 0,
                "Terminal shard did not execute its three source turns");
            FrozenOrbs.Cancel(owner); CastCheck(owner);
        });
    }
    private static void CastCheck(PlayerMobile owner)
    {
        var point = new Point3D(owner.X + 12, owner.Y, owner.Z); var values = SkillMechanics.Values(owner, CombatAbility.FrozenOrb);
        Require(values.Mana256 == 25 * 256 && values.Damage256(false) == 10444 && values.Damage256(true) == 11750 && values.DurationFrames == 200,
            "Frozen Orb source mana/damage/hard synergy/cold length");
        int mana = owner.Mana;
        Require(SorceressCombat.Release(owner, CombatAbility.FrozenOrb, Serial.Zero, point) && owner.Mana == mana - 25, "Orb release/source mana");
        var orb = FrozenOrbs.Snapshot().Single(x => x.Owner == owner.Serial.Value);
        Require(orb.Kind == FrozenKind.Orb && orb.Rank == 1 && orb.Min256 == 0 && orb.Max256 == 0
            && Math.Abs((orb.UntilUtc - TimedEffects.Now).TotalMilliseconds - 1200) < 100, "Orb body or lifetime source metadata");
        int paid = owner.Mana;
        Require(!SorceressCombat.Release(owner, CombatAbility.FrozenOrb, Serial.Zero, point) && owner.Mana == paid
            && !FireGround.Ready(owner, CombatAbility.FrozenOrb) && FireGround.Ready(owner, CombatAbility.Blizzard), "Independent 25-frame Orb delay");
        Require(ClassCreation.Profiles.TryTrain(owner.Serial, "sorceress.frozenorb", out _)
            && ClassCreation.Profiles.TryTrain(owner.Serial, "sorceress.icebolt", out _), "Stored rank/fresh child synergy fixture");
        Later(220, "frozen-orb-result", () =>
        {
            var missiles = FrozenOrbs.Snapshot().Where(x => x.Owner == owner.Serial.Value).ToArray(); var parent = missiles.Single(x => x.Kind == FrozenKind.Orb);
            Require(parent.Rank == 1 && Math.Abs(parent.X - owner.X - parent.Frame * .234375) < .00001
                && missiles.Any(x => x.Kind == FrozenKind.Bolt) && missiles.Where(x => x.Kind == FrozenKind.Bolt)
                    .All(x => x.Rank == 1 && x.Min256 == 10649 && x.Max256 == 11980 && x.ColdFrames == 200),
                "Source travel/rotating emission, stored cast rank or fresh per-child hard synergy");
        });
        Later(1260, "frozen-orb-result", () =>
        {
            var missiles = FrozenOrbs.Snapshot().Where(x => x.Owner == owner.Serial.Value).ToArray();
            Require(!missiles.Any(x => x.Kind == FrozenKind.Orb) && missiles.Count(x => x.Kind == FrozenKind.Nova) == 16
                && missiles.Max(x => x.Id) == orb.Id + 46, $"Thirty travel shards or sixteen natural-expiry terminal shards: orbs={missiles.Count(x=>x.Kind==FrozenKind.Orb)}, nova={missiles.Count(x=>x.Kind==FrozenKind.Nova)}, maxId={missiles.Max(x=>x.Id)}, expected={orb.Id+46}");
            Require(FireGround.Ready(owner, CombatAbility.FrozenOrb), "Orb local delay used total effect lifetime");
        });
        Later(2500, "frozen-orb-result", () =>
        {
            Require(!FrozenOrbs.Snapshot().Any(x => x.Owner == owner.Serial.Value), "Orb/shards did not expire naturally");
            var at=TimedEffects.Now;var state=FrozenOrbs.Snapshot();
            long lateId=state.Select(x=>x.Id).DefaultIfEmpty().Max()+100;
            var late=orb with{Id=lateId,X=owner.X+28*.234375,Frame=28,DirectionIndex=(28*19)&63,
                NextUtc=at.AddMilliseconds(-80),UntilUtc=at.AddMilliseconds(10)};
            FrozenOrbs.Restore(state.Append(late).ToArray());
            Later(100,"frozen-orb-result",()=>
            {
                var completed=FrozenOrbs.Snapshot().Where(x=>x.Owner==owner.Serial.Value).ToArray();
                Require(!completed.Any(x=>x.Kind==FrozenKind.Orb) && completed.Count(x=>x.Kind==FrozenKind.Bolt)==2
                    && completed.Count(x=>x.Kind==FrozenKind.Nova)==16 && completed.Max(x=>x.Id)==lateId+18,
                    "Delayed callback lost the two remaining source travel emissions before natural expiry");
                FrozenOrbs.Cancel(owner);
            owner.Mana = owner.ManaMax; Require(SorceressCombat.Release(owner, CombatAbility.FrozenOrb, Serial.Zero, point), "Persistence Orb cast");
            var missiles = FrozenOrbs.Snapshot(); var parent = missiles.Single(x => x.Owner == owner.Serial.Value);
            var next = TimedEffects.Now.AddMinutes(10);
            // Hold test-only final-frame/curve payloads across two process reloads.
            parent = parent with { X = owner.X + 14, Frame = 29, DirectionIndex = 39, NextUtc = next, UntilUtc = next.AddMilliseconds(40) };
            var curve = parent with { Id = parent.Id + 1, Kind = FrozenKind.Nova, X = owner.X + 4, Frame = 4, VectorX = 0, VectorY = 15,
                Vx = 0, Vy = .5625, Min256 = 2560, Max256 = 2560, ColdFrames = 200, UntilUtc = next.AddMilliseconds(840) };
            FrozenOrbs.Restore(missiles.Select(x => x.Id == parent.Id ? parent : x).Append(curve).ToArray());
            File.WriteAllText(Manifest, JsonSerializer.Serialize(new Saved(owner.Serial.Value, parent, curve)));
            _log.Information("frozen-orb-result: passed");
            });
        });
    }
    private static void Reload()
    {
        var saved = JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest)); var owner = World.FindMobile((Serial)saved.Owner) as PlayerMobile;
        Require(owner != null, "Saved Orb caster missing"); var missiles = FrozenOrbs.Snapshot();
        Require(missiles.Contains(saved.Orb) && missiles.Contains(saved.Curve), "Saved Orb/curve phase, vectors, rank, damage or deadlines changed");
        owner.MoveToWorld(new((int)saved.Orb.X - 14, (int)saved.Orb.Y, saved.Orb.Z), Map.Maps[saved.Orb.Map]);
        var blast = Enemy(owner, 14); var curveTarget = Enemy(owner, 3); int blastLife = blast.Hits, curveLife = curveTarget.Hits;
        long id = missiles.Select(x => x.Id).Max(); var now = TimedEffects.Now;
        var orb = saved.Orb with { Id = ++id, NextUtc = now, UntilUtc = now.AddMilliseconds(200) };
        var curve = saved.Curve with { Id = ++id, NextUtc = now, UntilUtc = now.AddMilliseconds(840) };
        FrozenOrbs.Restore(missiles.Append(orb).Append(curve).ToArray());
        Later(250, "frozen-orb-reload-result", () =>
        {
            Require(blastLife - blast.Hits >= 16 * 60 && curveLife - curveTarget.Hits == 12,
                $"Restored final burst/turning shard did not damage: {blastLife-blast.Hits}/{curveLife-curveTarget.Hits}");
            blast.Delete(); curveTarget.Delete(); var current = FrozenOrbs.Snapshot();
            Require(current.Contains(saved.Orb) && current.Contains(saved.Curve), "Held persistence payload changed during resumed-damage checks");
            FrozenOrbs.Restore(current.Where(x => x.Owner != owner.Serial.Value || x.Id == saved.Orb.Id || x.Id == saved.Curve.Id).ToArray());
            _log.Information("frozen-orb-reload-result: passed");
        });
    }
}
