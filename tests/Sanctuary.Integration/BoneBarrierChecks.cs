using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class BoneBarrierChecks
{
    private static readonly ILogger _log = LogFactory.GetLogger(typeof(BoneBarrierChecks));
    private sealed record Saved(uint Root, uint Owner, DateTime Until, int Life, int Hits);
    private static string Manifest => Path.Join(Core.BaseDirectory, "bone-barrier-checks.json");
    public static void Configure()
    {
        Register("bone-barriers-check", Check); Register("bone-barriers-reload-check", Reload);
    }
    private static void Register(string name, Action action) => ConsoleInputHandler.RegisterCommand(name, "Destructible class barrier checks", _ => Core.LoopContext.Post(() =>
    { try { action(); _log.Information("{Name}: passed", name); } catch (Exception e) { _log.Error(e, "{Name}: failed", name); } }));
    private static void Require(bool value, string why) { if (!value) { throw new InvalidOperationException(why); } }
    private static Point3D Open(PlayerMobile owner)
    {
        for (int x = 5400; x < 5570; x++)
        {
            for (int y = 1100; y < 1290; y++)
            {
                int z = owner.Map.GetAverageZ(x, y); bool clear = true;
                for (int dx = -5; dx <= 5 && clear; dx++)
                {
                    for (int dy = -5; dy <= 5 && clear; dy++) { clear = owner.Map.CanSpawnMobile(x + dx, y + dy, z); }
                }
                if (clear) { return new Point3D(x, y, z); }
            }
        }
        throw new InvalidOperationException("No open native barrier fixture floor");
    }
    private static BoneBarrierSegment[] Segments(PlayerMobile owner, Point3D center)
    {
        var result = new List<BoneBarrierSegment>();
        foreach (var segment in owner.Map.GetMobilesInRange<BoneBarrierSegment>(center, 12))
        { if (segment.SummonMaster == owner) { result.Add(segment); } }
        return result.ToArray();
    }
    private static bool HasTile(Map map, Point3D center, int range)
    {
        foreach (var tile in map.GetItemsInRange<BoneBarrierTile>(center, range)) { return true; }
        return false;
    }
    private static void Check()
    {
        var owner = ClassSkillChecks.Player(); var center = Open(owner);
        owner.MoveToWorld(new Point3D(center.X - 5, center.Y, center.Z), owner.Map);
        var enemy = new GraveyardSkeleton { Frozen = true, CantWalk = true, NoKillAwards = true };
        enemy.AIObject.AITimer.Stop(); enemy.MoveToWorld(new Point3D(center.X + 3, center.Y, center.Z), owner.Map);
        var caster = new GraveyardLich { CantWalk = true, NoKillAwards = true };
        caster.MoveToWorld(new Point3D(center.X + 4, center.Y, center.Z), owner.Map); caster.AIObject.AITimer.Stop();
        BoneBarrierSegment wall = null;
        try
        {
            int mana = owner.Mana;
            Require(!NecromancerCombat.Release(owner, CombatAbility.BonePrison, Serial.Zero, center) && owner.Mana == mana,
                "Untargeted Bone Prison spent resources");
            Require(NecromancerCombat.Release(owner, CombatAbility.BoneWall, Serial.Zero, center), "Bone Wall release");
            wall = Segments(owner, center).Single();
            Require(owner.Mana == mana - 17 && wall.Life == 23 && wall.HitsMax == 23 && wall.SourceDefense == 35
                && wall.ColdResistance == 70 && wall.PoisonResistance == 70, "Barrier source stats, hard synergies or mana");
            Require(ClassCombat.Friendly(owner, wall) && !AlliedOverlap.PassThrough(owner, wall)
                && enemy.IsEnemy(wall) && enemy.CanBeHarmful(wall, false), "Barrier allegiance, collision or hostile targeting");
            enemy.NextReacquireTime = Core.TickCount;
            Require(enemy.AIObject.AcquireFocusMob(14, FightMode.Closest, false, false, true) && enemy.FocusMob == wall,
                "Hostile AI did not acquire the closer barrier");
            int life = owner.Hits;
            AOS.Damage(owner, enemy, 5, false, 100, 0, 0, 0, 0, archer: true, weaponDamage: true);
            Require(owner.Hits == life && wall.Hits == 18, "Native arrow did not hit intervening destructible barrier");
            AOS.Damage(wall, owner, 100, false, 100, 0, 0, 0, 0, weaponDamage: true);
            Require(wall.Hits == 18, "Friendly barrier took allied damage");
            Require(owner.Map.LineOfSight(new Point3D(owner.X, owner.Y, owner.Z + 12), new Point3D(enemy.X, enemy.Y, enemy.Z + 12)),
                "Barrier collision blocked allied spell LOS");
            Require(caster.BeginFirebolt(owner), "Hostile projectile fixture did not launch"); caster.AIObject.AITimer.Stop();
            var root = wall;
            Timer.StartTimer(TimeSpan.FromMilliseconds(1600), () =>
            {
                try
                {
                    var parts = Segments(owner, center);
                    Require(owner.Hits == life && root.Hits is >= 8 and <= 13, "Travelling hostile firebolt bypassed intervening barrier");
                    caster.Delete();
                    Require(parts.Length == 9, "Bone Wall did not grow both source arms");
                    Require(!Server.Movement.Movement.CheckMovement(owner, owner.Map, new Point3D(center.X + 1, center.Y, center.Z), Direction.West, out _),
                        "Owner walked through a bone wall");
                    var child = parts.Single(s => s.Y == center.Y + 1);
                    AOS.Damage(child, enemy, 100, false, 100, 0, 0, 0, 0, weaponDamage: true);
                    Require(child.Deleted && !root.Deleted && child.Corpse == null
                        && !HasTile(owner.Map, child.Location, 0), "Destroying a child failed to open its own gap");
                    owner.Mana = owner.ManaMax;
                    Require(NecromancerCombat.Release(owner, CombatAbility.BonePrison, enemy.Serial, enemy.Location), "Bone Prison release");
                    var prison = Segments(owner, center).Where(s => s.Ability == CombatAbility.BonePrison).ToArray();
                    Require(prison.Length == 12 && prison.Sum(s => s.Tiles.Count) == 16, "Source prison anchors/raster connectors");
                    Require(!Server.Movement.Movement.CheckMovement(enemy, enemy.Map, new Point3D(enemy.X + 1, enemy.Y, enemy.Z), Direction.East, out _),
                        "Prison left a traversable gap at a raster edge");
                    var expiry = prison.Max(s => s.ExpiresUtc);
                    Timer.StartTimer(TimeSpan.FromMilliseconds(Math.Max(0, (expiry - TimedEffects.Now).TotalMilliseconds) + 1000), () =>
                    {
                        try
                        {
                            Require(root.Deleted && Segments(owner, center).Length == 0
                                && !HasTile(owner.Map, center, 12), "Source 24-second barrier expiry left actors/collision behind");
                            owner.Mana = owner.ManaMax;
                            Require(NecromancerCombat.Release(owner, CombatAbility.BoneWall, Serial.Zero, center), "Persistence wall release");
                            var saved = Segments(owner, center).Single(); saved.Hits = saved.HitsMax - 4;
                            File.WriteAllText(Manifest, JsonSerializer.Serialize(new Saved(saved.Serial.Value, owner.Serial.Value, saved.ExpiresUtc, saved.HitsMax, saved.Hits)));
                            _log.Information("bone-barriers-result: passed");
                        }
                        catch (Exception e) { Cleanup(); _log.Error(e, "bone-barriers-result: failed"); }
                    });
                }
                catch (Exception e) { Cleanup(); _log.Error(e, "bone-barriers-result: failed"); }
            });
        }
        catch { Cleanup(); throw; }
        void Cleanup()
        {
            foreach (var part in Segments(owner, center)) { part.Delete(); }
            caster.Delete(); enemy.Delete(); owner.Delete();
        }
    }
    private static void Reload()
    {
        var saved = JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));
        var segment = World.FindMobile((Serial)saved.Root) as BoneBarrierSegment;
        if (TimedEffects.Now >= saved.Until)
        {
            Require(segment == null || segment.Deleted, "Expired saved barrier survived restart");
            _log.Information("Bone barrier reload: expired segment removed"); return;
        }
        Require(segment is { Deleted: false } && segment.SummonMaster?.Serial.Value == saved.Owner
            && segment.ExpiresUtc == saved.Until && segment.HitsMax == saved.Life && segment.Hits >= saved.Hits
            && segment.Hits <= saved.Life && segment.Tiles.All(t => !t.Deleted && t.Segment == segment && t.Map == segment.Map),
            "Barrier owner, expiry, life or collision links lost across reload");
        _log.Information("Bone barrier reload: snapshot retained");
    }
}
