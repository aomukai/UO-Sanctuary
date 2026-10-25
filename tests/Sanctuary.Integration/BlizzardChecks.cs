using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class BlizzardChecks
{
    private static readonly ILogger _log = LogFactory.GetLogger(typeof(BlizzardChecks));
    private sealed record Saved(uint Owner, uint LastTarget, BlizzardStorm Storm, BlizzardShard Shard);
    private static string Manifest => Path.Join(Core.BaseDirectory, "blizzard-checks.json");
    public static void Configure() { Register("blizzard-check", Check); Register("blizzard-reload-check", Reload); }
    private static void Register(string name, Action action) => ConsoleInputHandler.RegisterCommand(name, "Blizzard sampling, collision and persistence",
        _ => Core.LoopContext.Post(() => { try { action(); _log.Information("{Name}: passed", name); } catch (Exception e) { _log.Error(e, "{Name}: failed", name); } }));
    private static void Require(bool value, string why) { if (!value) { throw new InvalidOperationException(why); } }
    private static void Later(int milliseconds, string result, Action action) => Timer.StartTimer(TimeSpan.FromMilliseconds(milliseconds), () =>
    { try { action(); } catch (Exception e) { _log.Error(e, "{Name}: failed", result); } });
    private static GraveyardSkeleton Enemy(PlayerMobile owner, Point3D point)
    {
        var enemy = new GraveyardSkeleton { Frozen = true, CantWalk = true, NoKillAwards = true };
        enemy.MoveToWorld(point, owner.Map); enemy.AIObject.AITimer.Stop(); enemy.SetHits(20000); enemy.Hits = enemy.HitsMax;
        enemy.SetResistance(ResistanceType.Cold, 0); return enemy;
    }
    private static void Check()
    {
        Require(Blizzards.Sample(1000, 0, 7) == new Point2D(0, 5) && Blizzards.Sample(1000, 4, 7) == new Point2D(-4, -6)
            && Blizzards.Sample(1000, 8, 7) == new Point2D(0, 0) && Blizzards.Sample(1000, 96, 7) == new Point2D(-4, -3),
            "Pinned elapsed-frame/seed-reset sampling vectors");
        var owner = ClassSkillChecks.Player(DiabloClass.Sorceress);
        owner.MoveToWorld(ClassSkillChecks.Corridor(owner, 5880, 1590), owner.Map);
        var single = Enemy(owner, new(owner.X + 4, owner.Y, owner.Z));
        var first = Enemy(owner, new(owner.X + 8, owner.Y, owner.Z)); var second = Enemy(owner, first.Location);
        var immune = Enemy(owner, new(owner.X + 12, owner.Y, owner.Z)); immune.SetResistance(ResistanceType.Cold, 100);
        int life = single.Hits; var now = TimedEffects.Now; var snapshot = Blizzards.Snapshot();
        long id = snapshot.Storms.Select(x => x.Id).Concat(snapshot.Shards.Select(x => x.Id)).DefaultIfEmpty().Max();
        BlizzardShard Shard(Mobile target) => new(++id, owner.Serial.Value, owner.Map.MapID, target.X, target.Y, target.Z,
            10 * 256, 10 * 256, 100, 9, 0, now.AddMilliseconds(360), now);
        Blizzards.Restore(snapshot with { Shards = snapshot.Shards.Concat(new[] { Shard(single), Shard(first), Shard(immune) }).ToArray() });
        Later(390, "blizzard-result", () =>
        {
            Require(single.Hits == life - 12 && first.Hits == life - 48 && second.Hits == life - 48 && immune.Hits == life,
                $"LastCollide single/overlap/immune delivery: {life-single.Hits}/{life-first.Hits}/{life-second.Hits}/{life-immune.Hits}");
            Require(GearEffects.ChillPercent(single) > 0 && GearEffects.ChillPercent(immune) == 0
                && !Blizzards.Snapshot().Shards.Any(x => x.Owner == owner.Serial.Value), "Chill, immunity or independent shard expiry");
            single.Delete(); first.Delete(); second.Delete(); immune.Delete(); CastCheck(owner);
        });
    }
    private static void CastCheck(PlayerMobile owner)
    {
        var center = new Point3D(owner.X + 6, owner.Y, owner.Z);
        var offset = Blizzards.Sample(center.X * 2, 0, 7);
        var target = Enemy(owner, new((int)Math.Round(center.X + offset.X / 2.0), (int)Math.Round(center.Y + offset.Y / 2.0), center.Z));
        var outside = Enemy(owner, new(center.X + 7, center.Y, center.Z)); int life = target.Hits, outsideLife = outside.Hits;
        var values = SkillMechanics.Values(owner, CombatAbility.Blizzard);
        Require(values.Damage256(false) == 13248 && values.Damage256(true) == 22080 && values.DurationFrames == 100 && values.Mana256 == 23 * 256,
            "Blizzard source synergy/damage, cold length or mana");
        var definition = SkillMechanics.For(CombatAbility.Blizzard);
        var high = new SkillValues(definition, k => k == definition.SourceKey ? 20 : 0, _ => 0, 99);
        Require(high.Damage256(false) == 570 * 256 && high.Damage256(true) == 619 * 256, "Blizzard source tier progression");
        int mana = owner.Mana;
        Require(SorceressCombat.Release(owner, CombatAbility.Blizzard, Serial.Zero, center) && owner.Mana == mana - 23, "Blizzard release/source mana");
        var storm = Blizzards.Snapshot().Storms.Single(x => x.Owner == owner.Serial.Value);
        Require(storm.Rank == 1 && Math.Abs((storm.UntilUtc - TimedEffects.Now).TotalMilliseconds - 4000) < 100,
            "Source storm lifetime/rank snapshot");
        int paid = owner.Mana;
        Require(!SorceressCombat.Release(owner, CombatAbility.Blizzard, Serial.Zero, center) && owner.Mana == paid
            && !FireGround.Ready(owner, CombatAbility.Blizzard) && FireGround.Ready(owner, CombatAbility.Meteor), "Blizzard independent local delay");
        Require(SorceressCombat.Release(owner, CombatAbility.FrozenArmor, Serial.Zero, owner.Location), "Blizzard delay blocked another skill");
        Require(ClassCreation.Profiles.TryTrain(owner.Serial, "sorceress.blizzard", out _)
            && ClassCreation.Profiles.TryTrain(owner.Serial, "sorceress.icebolt", out _), "Blizzard rank/synergy-change fixture");
        Later(220, "blizzard-result", () =>
        {
            var current = Blizzards.Snapshot();
            Require(current.Storms.Single(x => x.Id == storm.Id).Rank == 1 && current.Shards.Any(x => x.Owner == owner.Serial.Value)
                && current.Shards.Where(x => x.Owner == owner.Serial.Value).All(x => x.Min256 == 54 * 256 && x.Max256 == 90 * 256 && x.ColdFrames == 100)
                && target.Hits < life && outside.Hits == outsideLife,
                "Stored cast rank/fresh fall synergies, actual targeted falls or spatial exclusion");
        });
        Later(1900, "blizzard-result", () =>
        {
            owner.Mana = owner.ManaMax;
            Require(FireGround.Ready(owner, CombatAbility.Blizzard) && SorceressCombat.Release(owner, CombatAbility.Blizzard, Serial.Zero, center)
                && Blizzards.Snapshot().Storms.Count(x => x.Owner == owner.Serial.Value) == 2, "Independent overlapping storms after 45-frame delay");
        });
        Later(6350, "blizzard-result", () =>
        {
            Require(!Blizzards.Snapshot().Storms.Any(x => x.Owner == owner.Serial.Value)
                && !Blizzards.Snapshot().Shards.Any(x => x.Owner == owner.Serial.Value) && outside.Hits == outsideLife,
                "Natural storm/shard expiry or outside-area damage"); target.Delete(); outside.Delete();
            SeedPersistence(owner, center);
            _log.Information("blizzard-result: passed");
        });
    }
    private static void SeedPersistence(PlayerMobile owner, Point3D center)
    {
        owner.Mana = owner.ManaMax;
        Require(SorceressCombat.Release(owner, CombatAbility.Blizzard, Serial.Zero, center), "Persistence storm cast");
        var snapshot = Blizzards.Snapshot(); var storm = snapshot.Storms.Single(x => x.Owner == owner.Serial.Value);
        var last = Enemy(owner, new(owner.X + 12, owner.Y, owner.Z));
        // Hold the test-only payload until both process reloads; ordinary lifetime is checked above.
        var next = TimedEffects.Now.Add(FixtureSpace.Retention); storm = storm with { NextUtc = next, UntilUtc = next.AddSeconds(4) };
        long id = snapshot.Storms.Select(x => x.Id).Concat(snapshot.Shards.Select(x => x.Id)).DefaultIfEmpty().Max() + 1;
        var shard = new BlizzardShard(id, owner.Serial.Value, owner.Map.MapID, last.X, last.Y, last.Z,
            10 * 256, 10 * 256, 100, 9, last.Serial.Value, next.AddMilliseconds(360), next);
        Blizzards.Restore(snapshot with { Storms = snapshot.Storms.Select(x => x.Id == storm.Id ? storm : x).ToArray(), Shards = snapshot.Shards.Append(shard).ToArray() });
        File.WriteAllText(Manifest, JsonSerializer.Serialize(new Saved(owner.Serial.Value, last.Serial.Value, storm, shard)));
    }
    private static void Reload()
    {
        var saved = JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));
        var owner = World.FindMobile((Serial)saved.Owner) as PlayerMobile; var last = World.FindMobile((Serial)saved.LastTarget);
        Require(owner != null && last is { Alive: true }, "Saved Blizzard caster/collision target missing");
        var snapshot = Blizzards.Snapshot();
        Require(snapshot.Storms.Contains(saved.Storm) && snapshot.Shards.Contains(saved.Shard), "Saved Blizzard rank, timing, damage or LastCollide changed");
        owner.MoveToWorld(new(saved.Storm.X - 6, saved.Storm.Y, saved.Storm.Z), Map.Maps[saved.Storm.Map]);
        last.MoveToWorld(new((int)saved.Shard.X, (int)saved.Shard.Y, saved.Shard.Z), owner.Map);
        var next = Enemy(owner, last.Location); int lastLife = last.Hits, nextLife = next.Hits;
        var offset = Blizzards.Sample(saved.Storm.X * 2, saved.Storm.Frame, 7);
        var fallingTarget = Enemy(owner, new((int)Math.Round(saved.Storm.X + offset.X / 2.0), (int)Math.Round(saved.Storm.Y + offset.Y / 2.0), saved.Storm.Z));
        int fallLife = fallingTarget.Hits;
        long id = snapshot.Storms.Select(x => x.Id).Concat(snapshot.Shards.Select(x => x.Id)).DefaultIfEmpty().Max(); var now = TimedEffects.Now;
        var copyStorm = saved.Storm with { Id = ++id, NextUtc = now, UntilUtc = now.AddMilliseconds(100) };
        var copyShard = saved.Shard with { Id = ++id, Remaining = 2, NextUtc = now, UntilUtc = now.AddMilliseconds(80) };
        Blizzards.Restore(snapshot with { Storms = snapshot.Storms.Append(copyStorm).ToArray(), Shards = snapshot.Shards.Append(copyShard).ToArray() });
        Later(250, "blizzard-reload-result", () =>
        {
            Require(last.Hits == lastLife && next.Hits == nextLife - 12 && fallingTarget.Hits < fallLife,
                "Restored LastCollide or source fall loop failed to resume actual damage");
            next.Delete(); fallingTarget.Delete();
            var current = Blizzards.Snapshot();
            Require(current.Storms.Contains(saved.Storm) && current.Shards.Contains(saved.Shard), "Persistence payload changed during copy verification");
            Blizzards.Restore(current with { Storms = current.Storms.Where(x => x.Owner != owner.Serial.Value || x.Id == saved.Storm.Id).ToArray(),
                Shards = current.Shards.Where(x => x.Owner != owner.Serial.Value || x.Id == saved.Shard.Id).ToArray() });
            _log.Information("blizzard-reload-result: passed");
        });
    }
}
