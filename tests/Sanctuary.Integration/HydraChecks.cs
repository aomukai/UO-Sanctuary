using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class HydraChecks
{
    private static readonly ILogger _log = LogFactory.GetLogger(typeof(HydraChecks));
    private sealed record Saved(uint Owner, uint Target, uint PierceTarget, HydraHead[] Heads, HydraBolt Bolt);
    private static string Manifest => Path.Join(Core.BaseDirectory, "hydra-checks.json");
    public static void Configure() { Register("hydra-check", Check); Register("hydra-reload-check", Reload); }
    private static void Register(string name, Action action) => ConsoleInputHandler.RegisterCommand(name, "Hydra summon cap, AI, inherited damage and persistence",
        _ => Core.LoopContext.Post(() => { try { action(); _log.Information("{Name}: passed", name); } catch (Exception e) { _log.Error(e, "{Name}: failed", name); } }));
    private static void Require(bool value, string why) { if (!value) { throw new InvalidOperationException(why); } }
    private static void Later(int milliseconds, string result, Action action) => Timer.StartTimer(TimeSpan.FromMilliseconds(milliseconds), () =>
    { try { action(); } catch (Exception e) { _log.Error(e, "{Name}: failed", result); } });
    private static GraveyardSkeleton Enemy(PlayerMobile owner, int x)
    {
        var enemy = new GraveyardSkeleton { Frozen = true, CantWalk = true, NoKillAwards = true };
        enemy.MoveToWorld(new(owner.X + x, owner.Y, owner.Z), owner.Map); enemy.AIObject.AITimer.Stop(); enemy.SetHits(20000); enemy.Hits = enemy.HitsMax;
        enemy.SetResistance(ResistanceType.Fire, 0); return enemy;
    }
    private static HydraHead[] Heads(PlayerMobile owner) => Hydras.Snapshot().Heads.Where(x => x.Owner == owner.Serial.Value).ToArray();
    private static void Check()
    {
        Require(Hydras.Decide(true, true, 24, 0) == HydraDecision.Die && Hydras.Decide(false, true, 24, 59) == HydraDecision.Shoot
            && Hydras.Decide(false, true, 25, 59) == HydraDecision.Idle && Hydras.Decide(false, true, 24, 60) == HydraDecision.Idle
            && Hydras.Decide(false, false, 24, 0) == HydraDecision.Idle, "Compiled-source AI expiry/range/chance gates");
        var owner = ClassSkillChecks.Player(DiabloClass.Sorceress); owner.MoveToWorld(ClassSkillChecks.Corridor(owner, 5880, 1750), owner.Map);
        var pierce = Enemy(owner, 12); pierce.SetResistance(ResistanceType.Fire, 50); var behind = Enemy(owner, 13);
        var immune = Enemy(owner, 16); immune.SetResistance(ResistanceType.Fire, 100); int life = pierce.Hits;
        var snapshot = Hydras.Snapshot(); long id = snapshot.Heads.Select(x => x.Id).Concat(snapshot.Bolts.Select(x => x.Id)).DefaultIfEmpty().Max(); var now = TimedEffects.Now;
        HydraBolt Bolt(int x, int penetration) => new(++id, 0, owner.Serial.Value, owner.Map.MapID, owner.X + x, owner.Y, owner.Z,
            .375, 0, 0, 2560, 2560, penetration, now.AddMilliseconds(1200), now);
        Hydras.Restore(snapshot with { Bolts = snapshot.Bolts.Concat(new[] { Bolt(11, 20), Bolt(15, 99) }).ToArray() });
        Later(80, "hydra-result", () =>
        {
            Require(pierce.Hits == life - 7 && behind.Hits == life && immune.Hits == life
                && !Hydras.Snapshot().Bolts.Any(x => x.Owner == owner.Serial.Value),
                "Inherited pierce/fully-enhanced bolt, immunity or nearest non-piercing collision");
            pierce.Delete(); behind.Delete(); immune.Delete(); CastCheck(owner);
        });
    }
    private static void CastCheck(PlayerMobile owner)
    {
        var center = new Point3D(owner.X + 6, owner.Y, owner.Z); var values = SkillMechanics.Values(owner, CombatAbility.Hydra);
        Require(values.Mana256 == 20 * 256 && values.Damage256(false) == 3799 && values.Damage256(true) == 5291
            && values.Eval("calc1") == 250 && values.Eval("petmax") == 18, "Pinned Hydra mana/damage/synergy/lifetime/cap");
        var high = new SkillValues(SkillMechanics.For(CombatAbility.Hydra), k => k == "Hydra" ? 20 : 0, _ => 0, 99);
        Require(high.Damage256(false) == 301 * 128 && high.Damage256(true) == 350 * 128, "Hydra source damage tiers");
        int mana = owner.Mana; Require(SorceressCombat.Release(owner, CombatAbility.Hydra, Serial.Zero, center) && owner.Mana == mana - 20, "Hydra release/source mana");
        var first = Heads(owner); long oldest = first[0].Group;
        Require(first.Length == 3 && first.Select(x => x.Index).Distinct().Count() == 3
            && first.All(x => x.Rank == 1 && x.Min256 == 4938 && x.Max256 == 6878 && x.Pierce == 0
                && Math.Abs((x.UntilUtc - TimedEffects.Now).TotalMilliseconds - 10000) < 100
                && Math.Abs((x.DecisionUtc - TimedEffects.Now).TotalMilliseconds - 1000) < 100)
            && first.Any(x => x.X == center.X - .5 && x.Y == center.Y - .5) && first.Any(x => x.X == center.X && x.Y == center.Y)
            && first.Any(x => x.X == center.X + .5 && x.Y == center.Y - .5), "Three source heads/offsets/rank/mastery/initial timer");
        Require(FireGround.Ready(owner, CombatAbility.Hydra), "Removed Hydra local delay was reintroduced");
        for (int i = 0; i < 6; i++)
        { owner.Mana = owner.ManaMax; Require(SorceressCombat.Release(owner, CombatAbility.Hydra, Serial.Zero, center), "Repeated no-delay Hydra cast"); }
        Require(Heads(owner).Length == 18 && Heads(owner).Select(x => x.Group).Distinct().Count() == 6 && Heads(owner).All(x => x.Group != oldest),
            "Six-group cap did not remove the oldest group");
        mana = owner.Mana; int count = Heads(owner).Length;
        Require(!SorceressCombat.Release(owner, CombatAbility.Hydra, Serial.Zero, new(-1, -1, 0)) && owner.Mana == mana && Heads(owner).Length == count,
            "Failed Hydra target spent mana or replaced a group");
        Require(ClassCreation.Profiles.TryTrain(owner.Serial, "sorceress.firemastery", out _)
            && ClassCreation.Profiles.TryTrain(owner.Serial, "sorceress.hydra", out _), "Hydra cast snapshot fixture");
        // Deterministic decision seeds only for this fixture; the game stores independently generated head seeds.
        var current = Hydras.Snapshot();
        Hydras.Restore(current with { Heads = current.Heads.Select(x => x.Owner == owner.Serial.Value ? x with { Seed = ((ulong)666 << 32) | 1 } : x).ToArray() });
        var enemy = Enemy(owner, 9); int life = enemy.Hits;
        Later(1150, "hydra-result", () =>
        {
            Require(enemy.Hits == life && Heads(owner).Any(x => x.Target == enemy.Serial.Value && x.ShotUtc > TimedEffects.Now)
                && Heads(owner).All(x => x.Rank == 1 && x.Min256 == 4938 && x.Max256 == 6878),
                "Startup/action frame or retained cast damage/mastery snapshot");
        });
        Later(2200, "hydra-result", () => Require(enemy.Hits < life && owner.Hits == owner.HitsMax,
            "Hydra heads did not release travelling hostile bolts or hit their owner"));
        Later(11800, "hydra-result", () =>
        {
            Require(Heads(owner).Length == 0 && !Hydras.Snapshot().Bolts.Any(x => x.Owner == owner.Serial.Value),
                "Hydra source lifetime/AI expiry or final bolts failed to expire");
            enemy.Delete(); SeedPersistence(owner, center); _log.Information("hydra-result: passed");
        });
    }
    private static void SeedPersistence(PlayerMobile owner, Point3D center)
    {
        owner.Mana = owner.ManaMax; Require(SorceressCombat.Release(owner, CombatAbility.Hydra, Serial.Zero, center), "Persistence Hydra cast");
        var target = Enemy(owner, 9); var pierce = Enemy(owner, 12); pierce.SetResistance(ResistanceType.Fire, 50);
        var snapshot = Hydras.Snapshot(); var next = TimedEffects.Now.AddMinutes(5);
        // Hold cast/AI/windup/bolt payloads across process reloads; normal timings are checked separately.
        var heads = Heads(owner).Select(x => x with { UntilUtc = next.AddSeconds(10), DecisionUtc = next.AddMilliseconds(1080),
            ShotUtc = next.AddMilliseconds(320), Target = target.Serial.Value }).ToArray();
        long id = snapshot.Heads.Select(x => x.Id).Concat(snapshot.Bolts.Select(x => x.Id)).Max() + 1;
        var bolt = new HydraBolt(id, heads[0].Id, owner.Serial.Value, owner.Map.MapID, owner.X + 11, owner.Y, owner.Z,
            .375, 0, 0, 2560, 2560, 20, next.AddMilliseconds(1200), next);
        Hydras.Restore(snapshot with { Heads = snapshot.Heads.Select(x => heads.FirstOrDefault(h => h.Id == x.Id) ?? x).ToArray(), Bolts = snapshot.Bolts.Append(bolt).ToArray() });
        File.WriteAllText(Manifest, JsonSerializer.Serialize(new Saved(owner.Serial.Value, target.Serial.Value, pierce.Serial.Value, heads, bolt)));
    }
    private static void Reload()
    {
        var saved = JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest)); var owner = World.FindMobile((Serial)saved.Owner) as PlayerMobile;
        var target = World.FindMobile((Serial)saved.Target) as BaseCreature; var pierce = World.FindMobile((Serial)saved.PierceTarget) as BaseCreature;
        Require(owner != null && target is { Alive: true } && pierce is { Alive: true }, "Saved Hydra caster/targets missing");
        var snapshot = Hydras.Snapshot(); Require(saved.Heads.All(snapshot.Heads.Contains) && snapshot.Bolts.Contains(saved.Bolt),
            "Hydra rank/damage/pierce/random/target/phase/deadline payload changed");
        owner.MoveToWorld(new((int)saved.Heads[1].X - 6, (int)saved.Heads[1].Y, saved.Heads[1].Z), Map.Maps[saved.Heads[1].Map]);
        _log.Information("Hydra reload target conditioning: fire resistance {Resistance}, locations {Target}/{Pierce}", pierce.FireResistance, target.Location, pierce.Location);
        // Reapply controlled target conditions independently of the creature-template reload policy.
        target.MoveToWorld(new(owner.X + 9, owner.Y, owner.Z), owner.Map); pierce.MoveToWorld(new(owner.X + 12, owner.Y, owner.Z), owner.Map);
        target.Frozen = pierce.Frozen = true; target.CantWalk = pierce.CantWalk = true;
        target.AIObject.AITimer.Stop(); pierce.AIObject.AITimer.Stop(); pierce.SetResistance(ResistanceType.Fire, 50);
        int life = target.Hits, pierceLife = pierce.Hits; long id = snapshot.Heads.Select(x => x.Id).Concat(snapshot.Bolts.Select(x => x.Id)).Max(); var now = TimedEffects.Now;
        var copy = saved.Heads[1] with { Id = ++id, Group = id, ShotUtc = now, DecisionUtc = now.AddSeconds(5), UntilUtc = now.AddSeconds(10) };
        var bolt = saved.Bolt with { Id = ++id, Head = copy.Id, NextUtc = now, UntilUtc = now.AddMilliseconds(1200) };
        Hydras.Restore(snapshot with { Heads = snapshot.Heads.Append(copy).ToArray(), Bolts = snapshot.Bolts.Append(bolt).ToArray() });
        Later(500, "hydra-reload-result", () =>
        {
            Require(target.Hits < life && pierce.Hits == pierceLife - 7,
                $"Restored Hydra windup/bolt did not resume real inherited damage: {life-target.Hits}/{pierceLife-pierce.Hits}, resistance {pierce.FireResistance}");
            var current = Hydras.Snapshot(); Require(saved.Heads.All(current.Heads.Contains) && current.Bolts.Contains(saved.Bolt), "Held persistence payload was mutated");
            Hydras.Restore(current with { Heads = current.Heads.Where(x => x.Owner != owner.Serial.Value || saved.Heads.Any(h => h.Id == x.Id)).ToArray(),
                Bolts = current.Bolts.Where(x => x.Owner != owner.Serial.Value || x.Id == saved.Bolt.Id).ToArray() });
            _log.Information("hydra-reload-result: passed");
        });
    }
}
