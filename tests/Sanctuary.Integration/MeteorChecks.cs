using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class MeteorChecks
{
    private static readonly ILogger _log = LogFactory.GetLogger(typeof(MeteorChecks));
    private sealed record Saved(uint Owner, MeteorDrop Drop);
    private static string Manifest => Path.Join(Core.BaseDirectory, "meteor-checks.json");
    public static void Configure() { Register("meteor-check", Check); Register("meteor-reload-check", Reload); }
    private static void Register(string name, Action action) => ConsoleInputHandler.RegisterCommand(name, "Meteor impact, residual flames and persistence",
        _ => Core.LoopContext.Post(() => { try { action(); _log.Information("{Name}: passed", name); } catch (Exception e) { _log.Error(e, "{Name}: failed", name); } }));
    private static void Require(bool value, string why) { if (!value) { throw new InvalidOperationException(why); } }
    private static GraveyardSkeleton Enemy(PlayerMobile owner, Point3D point)
    {
        var enemy = new GraveyardSkeleton { Frozen = true, CantWalk = true, NoKillAwards = true };
        enemy.MoveToWorld(point, owner.Map); enemy.AIObject.AITimer.Stop(); enemy.SetHits(20000); enemy.Hits = enemy.HitsMax;
        enemy.SetResistance(ResistanceType.Fire, 0); return enemy;
    }
    private static void Later(int milliseconds, string result, Action action) => Timer.StartTimer(TimeSpan.FromMilliseconds(milliseconds), () =>
    { try { action(); } catch (Exception e) { _log.Error(e, "{Name}: failed", result); } });
    private static void Check()
    {
        var owner = ClassSkillChecks.Player(DiabloClass.Sorceress);
        owner.MoveToWorld(ClassSkillChecks.Corridor(owner, 5880, 1510), owner.Map);
        var center = new Point3D(owner.X + 6, owner.Y, owner.Z);
        var recovery = Enemy(owner, new(owner.X + 14, owner.Y, owner.Z)); recovery.SetHits(400); recovery.Hits = recovery.HitsMax;
        Require(SkillCombat.FireImpact(owner, recovery, 200 * 256) == 200 && CreatureHitRecovery.Remaining(recovery) > 0,
            "Direct Meteor damage did not use ordinary spell hit recovery"); recovery.Delete();
        var target = Enemy(owner, center); var twinA = Enemy(owner, new(center.X - 3, center.Y, center.Z));
        var twinB = Enemy(owner, twinA.Location); var outside = Enemy(owner, new(center.X + 3, center.Y + 1, center.Z));
        var immune = Enemy(owner, new(center.X, center.Y - 2, center.Z)); immune.SetResistance(ResistanceType.Fire, 100);
        int life = target.Hits, twinLife = twinA.Hits, outsideLife = outside.Hits, immuneLife = immune.Hits;
        var values = SkillMechanics.Values(owner, CombatAbility.Meteor);
        Require(values.Mana256 == 17 * 256 && values.Damage256(false) == 88 * 256 && values.Damage256(true) == 110 * 256
            && FireGround.MeteorFireDamage256(values, false) == 120 && FireGround.MeteorFireDamage256(values, true) == 200,
            "Source direct/residual damage or distinct hard-point synergies");
        var row = SkillMechanics.For(CombatAbility.Meteor);
        var high = new SkillValues(row, k => k == row.SourceKey ? 28 : 0, _ => 0, 99);
        Require(FireGround.MeteorFireDamage256(high, false) == 1240 && FireGround.MeteorFireDamage256(high, true) == 1320,
            "Missile-specific residual tier progression");
        int mana = owner.Mana;
        Require(SorceressCombat.Release(owner, CombatAbility.Meteor, Serial.Zero, center) && owner.Mana == mana - 17, "Meteor release or one-time mana");
        var drop = FireGround.Snapshot().Meteors.Single(x => x.Owner == owner.Serial.Value);
        Require(drop.Rank == 1 && drop.Radius == 3 && drop.Min256 == 29286 && drop.Max256 == 36608
            && Math.Abs((drop.ImpactUtc - TimedEffects.Now).TotalMilliseconds - 2400) < 100, "Source pending impact snapshot or fall delay");
        int paid = owner.Mana;
        Require(!SorceressCombat.Release(owner, CombatAbility.Meteor, Serial.Zero, center) && owner.Mana == paid
            && !FireGround.Ready(owner, CombatAbility.Meteor) && FireGround.Ready(owner, CombatAbility.FireWall), "Per-skill Meteor delay");
        Require(SorceressCombat.Release(owner, CombatAbility.FrozenArmor, Serial.Zero, owner.Location), "Meteor delay blocked a different skill");
        Require(ClassCreation.Profiles.TryTrain(owner.Serial, "sorceress.firemastery", out _), "Impact snapshot mastery-change fixture");
        Later(2100, "meteor-result", () =>
        {
            Require(target.Hits == life && FireGround.Snapshot().Meteors.Contains(drop)
                && FireGround.Ready(owner, CombatAbility.Meteor), "Impact was early or local delay used the fall duration");
        });
        Later(2520, "meteor-result", () =>
        {
            var patches = FireGround.Snapshot().Patches.Where(x => x.Owner == owner.Serial.Value).ToArray();
            Require(!FireGround.Snapshot().Meteors.Any(x => x.Owner == owner.Serial.Value) && target.Hits < life
                && twinA.Hits < twinLife && twinA.Hits == twinB.Hits && SkillHealth.Remainder256(twinA) == SkillHealth.Remainder256(twinB)
                && twinLife - twinA.Hits is >= 114 and <= 142, "Area damage, shared roll or cast-time impact mastery snapshot");
            Require(patches.Length == 18 && patches.All(x => x.Ability == CombatAbility.Meteor && x.Size == 2 && x.DamageRate == 41
                && x.Min256 == 164 && x.Max256 == 274 && (x.UntilUtc - TimedEffects.Now).TotalMilliseconds is > 950 and < 1250),
                "Residual offsets/count, fresh mastery, independent damage or rank1 lifetime");
            Require(patches.Any(x => x.X == center.X && x.Y == center.Y + 2.5)
                && patches.Any(x => x.X == center.X - 2 && x.Y == center.Y - 1), "Source residual footprint offsets");
            Require(outside.Hits == outsideLife && immune.Hits == immuneLife && owner.Hits == owner.HitsMax,
                "Meteor radius, immunity or friendly exclusion");
            int afterImpact = target.Hits;
            Later(450, "meteor-result", () => Require(target.Hits < afterImpact, "Residual ground fire did not continue damaging"));
        });
        Later(4020, "meteor-result", () =>
        {
            Require(!FireGround.Snapshot().Patches.Any(x => x.Owner == owner.Serial.Value), "Rank1 residual fire did not expire independently");
            target.Delete(); twinA.Delete(); twinB.Delete(); outside.Delete(); immune.Delete();
            owner.Mana = owner.ManaMax;
            Require(SorceressCombat.Release(owner, CombatAbility.Meteor, Serial.Zero, center), "Next Meteor after local delay failed");
            var saved = FireGround.Snapshot(); var pending = saved.Meteors.Single(x => x.Owner == owner.Serial.Value);
            // Extend only this fixture's deadline so two process reloads can inspect the pending payload.
            pending = pending with { ImpactUtc = TimedEffects.Now.AddMinutes(10) };
            FireGround.Restore(saved with { Meteors = saved.Meteors.Select(x => x.Id == pending.Id ? pending : x).ToArray() });
            File.WriteAllText(Manifest, JsonSerializer.Serialize(new Saved(owner.Serial.Value, pending)));
            _log.Information("meteor-result: passed");
        });
    }
    private static void Reload()
    {
        var saved = JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));
        var owner = World.FindMobile((Serial)saved.Owner) as PlayerMobile;
        Require(owner != null, "Saved Meteor caster missing");
        var snapshot = FireGround.Snapshot();
        Require(snapshot.Meteors.Contains(saved.Drop), "Pending Meteor identity, rank, damage, map or absolute deadline changed");
        owner.MoveToWorld(new(saved.Drop.X - 6, saved.Drop.Y, saved.Drop.Z), Map.Maps[saved.Drop.Map]);
        var enemy = Enemy(owner, new(saved.Drop.X, saved.Drop.Y, saved.Drop.Z)); int life = enemy.Hits;
        // Preserve the saved drop for the second reload and run a short-lived copy through the real impact loop.
        long id = snapshot.Meteors.Select(x => x.Id).Concat(snapshot.Patches.Select(x => x.Id)).Concat(snapshot.Makers.Select(x => x.Id)).DefaultIfEmpty().Max() + 1;
        var copy = saved.Drop with { Id = id, ImpactUtc = TimedEffects.Now.AddMilliseconds(80) };
        FireGround.Restore(snapshot with { Meteors = snapshot.Meteors.Append(copy).ToArray() });
        Later(250, "meteor-reload-result", () =>
        {
            Require(enemy.Hits < life && FireGround.Snapshot().Meteors.Contains(saved.Drop)
                && !FireGround.Snapshot().Meteors.Contains(copy)
                && FireGround.Snapshot().Patches.Count(x => x.Owner == owner.Serial.Value) == 18,
                "Restored Meteor did not land, damage and create residual fire");
            enemy.Delete();
            // Keep only the original pending drop; residual lifetimes were checked during the first boot.
            var current = FireGround.Snapshot();
            FireGround.Restore(current with { Patches = current.Patches.Where(x => x.Owner != owner.Serial.Value).ToArray() });
            _log.Information("meteor-reload-result: passed");
        });
    }
}
