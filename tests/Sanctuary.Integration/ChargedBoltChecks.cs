using System;
using Server;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class ChargedBoltChecks
{
    private static readonly ILogger _log = LogFactory.GetLogger(typeof(ChargedBoltChecks));
    private static void Require(bool value, string why) { if (!value) { throw new InvalidOperationException(why); } }
    public static void Configure() => ConsoleInputHandler.RegisterCommand("charged-bolt-check", "Independent wandering bolt checks",
        _ => Core.LoopContext.Post(() =>
        { try { Check(); _log.Information("charged-bolt-check: passed"); } catch (Exception e) { _log.Error(e, "charged-bolt-check: failed"); } }));
    private static void Check()
    {
        int[][] expectedX = [[1, 2, 3, 4, 5, 6, 7, 8], [1, 2, 3, 4, 5, 6, 7, 8], [1, 2, 3, 4, 5, 6, 7, 8]];
        int[][] expectedY = [[1, 2, 3, 2, 1, 2, 1, 2], [1, 0, 0, 0, -1, -1, -2, -3], [0, 1, 2, 3, 2, 1, 2, 2]];
        for (uint seed = 0; seed < 3; seed++)
        {
            var points = ChargedBolts.Path(seed, 0, 77);
            Require(points.Length == 39 && points[0] == Point2D.Zero, "Source charged-bolt waypoint count/origin");
            for (int i = 0; i < 8; i++)
            { Require(points[i + 1].X == expectedX[seed][i] && points[i + 1].Y == expectedY[seed][i], "Source seed/direction-offset path diverged"); }
        }
        Require(ChargedBolts.Heading(0, 0) == 6 && ChargedBolts.Heading(2, 1) == 0 && ChargedBolts.Heading(1, 2) == 2
            && ChargedBolts.Heading(-1, -1) == 5 && ChargedBolts.Heading(1, -1) == 7 && ChargedBolts.Frames == 77,
            "Source octant thresholds or capped lifetime");
        var row = SkillMechanics.For(CombatAbility.ChargedBolt);
        foreach (var (rank, count) in new[] { (1, 3), (20, 22), (22, 24), (99, 24) })
        {
            var values = new SkillValues(row, k => k == row.SourceKey ? rank : 0, _ => 0, 99);
            Require(values.Eval("calc1") == count, "Source charged-bolt count/cap");
        }
        var high = new SkillValues(row, k => k == row.SourceKey ? 20 : 0, _ => 0, 99);
        Require(high.Mana256 == 3200 && high.Damage256(true) == 3968, "Charged Bolt source rank20 mana/damage");
        var owner = ClassSkillChecks.Player(DiabloClass.Sorceress);
        owner.MoveToWorld(ClassSkillChecks.Corridor(owner, 5800, 1410), owner.Map);
        var first = new GraveyardSkeleton { Frozen = true, CantWalk = true, NoKillAwards = true };
        first.MoveToWorld(owner.Location, owner.Map); first.AIObject.AITimer.Stop(); first.SetHits(5000); first.Hits = first.HitsMax; first.SetResistance(ResistanceType.Energy, 0);
        var behind = new GraveyardSkeleton { Frozen = true, CantWalk = true, NoKillAwards = true };
        behind.MoveToWorld(new Point3D(owner.X + 3, owner.Y, owner.Z), owner.Map); behind.AIObject.AITimer.Stop(); behind.SetHits(5000); behind.Hits = behind.HitsMax;
        int life = first.Hits, untouched = behind.Hits, mana = owner.Mana;
        Require(SorceressCombat.Release(owner, CombatAbility.ChargedBolt, Serial.Zero, behind.Location)
            && owner.Mana == mana - 3 && first.Hits == life, "Charged Bolt paid per bolt or hit instantly");
        Timer.StartTimer(TimeSpan.FromMilliseconds(1250), () =>
        {
            try
            {
                Require(life - first.Hits is >= 9 and <= 18, "Independent bolts did not each collide and damage the first target");
                Require(behind.Hits == untouched, "Charged bolts pierced after collision");
                _log.Information("charged-bolt-result: passed");
            }
            catch (Exception e) { _log.Error(e, "charged-bolt-result: failed"); }
            finally { first.Delete(); behind.Delete(); owner.Delete(); }
        });
    }
}
