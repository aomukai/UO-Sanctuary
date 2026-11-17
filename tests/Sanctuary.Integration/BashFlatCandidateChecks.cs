using System;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

// Candidate only: no learned Bash route or production default is changed.
public static class BashFlatCandidateChecks
{
    private static readonly ILogger Log = LogFactory.GetLogger(typeof(BashFlatCandidateChecks));
    public static void Configure() => ConsoleInputHandler.RegisterCommand("bash-flat-candidate-check",
        "Proposed post-critical/pre-mitigation flat Bash damage through existing contact services", _ => Core.LoopContext.Post(() =>
        {
            try { Check(); Log.Information("bash-flat-candidate-check: passed"); }
            catch (Exception error) { Log.Error(error, "bash-flat-candidate-check: failed"); }
        }));
    private static void Require(bool condition, string message)
    { if (!condition) { throw new InvalidOperationException(message); } }

    private static void Check()
    {
        var owner = ClassSkillChecks.Player(DiabloClass.Barbarian);
        owner.AddItem(new Backpack());
        FixtureSpace.Corridor(owner, 6200, 24, 1700);
        var dagger = new Dagger();
        ClassGear.Profiles.Convert(dagger, 1, GearRarity.Normal, new Random(73));
        Require(owner.EquipItem(dagger), "Candidate weapon setup");
        ClassGear.Refresh(owner);
        var target = new GraveyardZombie { NoKillAwards = true, Frozen = true, CantWalk = true };
        target.SetHits(50000);
        target.MoveToWorld(new Point3D(owner.X + 1, owner.Y, owner.Z), owner.Map);
        target.AIObject?.AITimer.Stop();
        var block = SourceBlocking.BlockRoll;
        try
        {
            SourceBlocking.BlockRoll = n => n - 1;
            int cases = 0;
            foreach (int rank in new[] { 1, 20 })
            foreach (int resistance in new[] { -50, 0, 50, 100 })
            foreach (bool critical in new[] { false, true })
            {
                target.SetResistance(ResistanceType.Physical, resistance);
                var row = SkillMechanics.For(CombatAbility.Bash);
                var values = new SkillValues(row, key => key == row.SourceKey ? rank : 0, _ => 0, 99);
                int flat = checked((int)values.Eval("calc2"));
                Require(flat == rank, "Pinned Bash flat formula");
                int callbacks = 0;
                PreparedWeaponImpact Candidate()
                {
                    return GearEffects.PrepareWeaponImpact(owner, target, 0, new Random(73), new GearDamage(100, 0, 0, 0, 0),
                        projectileCritical: critical, physicalAfterCritical: physical =>
                        {
                            callbacks++;
                            Require(physical == (critical ? 200 : 100) * 256, "Flat hook moved before critical");
                            return checked(physical + flat * 256);
                        });
                }
                var prepared = PlayerWeaponContact.PrepareOrdinary(owner, target, 0, 0, () => true,
                    () => 0, () => 0, _ => 0, _ => 0, new Random(73), true, captured: Candidate);
                Require((prepared.Flags & SourceMeleeResult.Successful) != 0 && callbacks == 1,
                    "Successful contact did not capture candidate exactly once");
                int expected = checked(((critical ? 200 : 100) + flat) * 256 * (100 - resistance) / 100);
                Require(NamedElements.To256(prepared.Impact.Parts.Physical) == expected,
                    "Candidate bonus bypassed resistance or was doubled by critical");
                int before = SkillHealth.Balance256(target);
                PlayerWeaponContact.Release(owner, target, prepared, TimedEffects.Now, true);
                Require(before - SkillHealth.Balance256(target) == expected, "Candidate event and actual health diverged");
                int missCallbacks = 0;
                var missed = PlayerWeaponContact.PrepareOrdinary(owner, target, 0, 0, () => true,
                    () => 1, () => 0, _ => 0, _ => 0, new Random(73), true, captured: () =>
                    { missCallbacks++; return Candidate(); });
                Require((missed.Flags & SourceMeleeResult.Successful) == 0 && missCallbacks == 0
                    && missed.Impact.Parts.Total == 0, "Miss captured flat candidate damage");
                cases++;
            }
            Require(cases == 16, "Incomplete candidate matrix");
            Log.Information("Bash flat candidate:16 rank/resistance/critical cases,actual contact health,physical immunity and miss exclusions pass;learned Bash is unchanged");
        }
        finally
        {
            SourceBlocking.BlockRoll = block;
            target.Delete();
            owner.Delete();
        }
    }
}
