using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class InfernoPrimitiveChecks
{
    private static readonly ILogger _log = LogFactory.GetLogger(typeof(InfernoPrimitiveChecks));
    private sealed record Saved(uint Owner, uint Last, uint Next, InfernoFlame Flame);
    private static string Manifest => Path.Join(Core.BaseDirectory, "inferno-primitives.json");
    public static void Configure() { Register("inferno-primitives-check", Check); Register("inferno-primitives-reload-check", Reload); }
    private static void Register(string name, Action action) => ConsoleInputHandler.RegisterCommand(name, "Gated Inferno source timing/flame primitives",
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
    private static void Check()
    {
        var owner = ClassSkillChecks.Player(DiabloClass.Sorceress); owner.MoveToWorld(ClassSkillChecks.Corridor(owner, 5880, 1810), owner.Map);
        Require(!SorceressCombat.Implemented(CombatAbility.Inferno) && !ClassCombat.Available(owner, CombatAbility.Inferno), "Unverified channel accidentally enabled");
        var weapon = new WeaponTiming(AttackFamily.Unarmed, 0);
        Require(CombatTiming.Calculate(DiabloClass.Sorceress, CombatAbility.Inferno, weapon) == new ActionTiming(15, 10)
            && CombatTiming.Calculate(DiabloClass.Sorceress, CombatAbility.Inferno, weapon, fcr: 105) == new ActionTiming(10, 7),
            "Source SQ6 points/action/FCR ceiling calculation");
        Require(!InfernoFlames.StartAllowed(5 * 256, false) && InfernoFlames.StartAllowed(6 * 256, false)
            && InfernoFlames.StartAllowed(0, true), "Source threshold/state bypass became an upfront mana debit");
        var values = SkillMechanics.Values(owner, CombatAbility.Inferno);
        Require(values.Mana256 == 96 && values.Damage256(false) == 334 && values.Damage256(true) == 668
            && values.Eval("calc1") == 17 && NecromancerSummons.Source("infernoflame1", "DamageRate") == 82,
            "Pinned source invocation cost, damage/synergy, flame lifetime or MDR rate");
        int mana = owner.Mana; InfernoFlames.Launch(owner, new(owner.X + 10, owner.Y, owner.Z), values);
        var source = InfernoFlames.Snapshot().Single(x => x.Owner == owner.Serial.Value);
        Require(owner.Mana == mana && source.Min256 == 434 && source.Max256 == 868 && source.Frames == 17 && source.Vx == .28125
            && source.Vy == 0 && Math.Abs((source.UntilUtc - TimedEffects.Now).TotalMilliseconds - 680) < 100,
            "Launched flame snapshot/range/velocity or independent resource ownership");
        // Each phase stays inside the fixture's verified six-tile clear corridor.
        var overlapA = Enemy(owner, 2); var overlapB = Enemy(owner, 2); int life = overlapA.Hits;
        var main = source with { Min256 = 2560, Max256 = 2560, NextUtc = TimedEffects.Now, UntilUtc = TimedEffects.Now.AddMilliseconds(680) };
        InfernoFlames.Restore(InfernoFlames.Snapshot().Select(x => x.Id == source.Id ? main : x).ToArray());
        Later(750, "inferno-primitives-result", () =>
        {
            Require(overlapA.Hits == life - 30 && overlapB.Hits == life - 30,
                $"LastCollide alternation across six overlapping collision steps: {life-overlapA.Hits}/{life-overlapB.Hits}");
            overlapA.Delete(); overlapB.Delete();
            var first = Enemy(owner, 2); var next = Enemy(owner, 4);
            var piercing = main with { NextUtc = TimedEffects.Now, UntilUtc = TimedEffects.Now.AddMilliseconds(680) };
            InfernoFlames.Restore(InfernoFlames.Snapshot().Append(piercing).ToArray());
            Later(750, "inferno-primitives-result", () =>
            {
                Require(first.Hits == life - 10 && next.Hits == life - 10 && owner.Hits == owner.HitsMax,
                    $"Piercing/last-target exclusion: {life-first.Hits}/{life-next.Hits}");
                Require(!InfernoFlames.Snapshot().Any(x => x.Owner == owner.Serial.Value), "Source flame lifetime did not expire");
                first.SetResistance(ResistanceType.Fire, 100); int a = first.Hits, b = next.Hits;
                var immune = main with { NextUtc = TimedEffects.Now, UntilUtc = TimedEffects.Now.AddMilliseconds(680) };
                InfernoFlames.Restore(InfernoFlames.Snapshot().Append(immune).ToArray());
                Later(750, "inferno-primitives-result", () =>
                {
                    Require(first.Hits == a && next.Hits == b - 10, "Immune collision stopped the piercing flame or broke immunity");
                    first.SetResistance(ResistanceType.Fire, 0);
                    var held = main with { Frame = 5, X = owner.X + 1.40625, LastCollision = first.Serial.Value,
                        NextUtc = TimedEffects.Now.AddMinutes(5), UntilUtc = TimedEffects.Now.AddMinutes(5).AddMilliseconds(480) };
                    InfernoFlames.Restore(InfernoFlames.Snapshot().Append(held).ToArray());
                    File.WriteAllText(Manifest, JsonSerializer.Serialize(new Saved(owner.Serial.Value, first.Serial.Value, next.Serial.Value, held)));
                    _log.Information("inferno-primitives-result: passed");
                });
            });
        });
    }
    private static void Reload()
    {
        var saved = JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest)); var owner = World.FindMobile((Serial)saved.Owner) as PlayerMobile;
        var last = World.FindMobile((Serial)saved.Last) as BaseCreature; var next = World.FindMobile((Serial)saved.Next) as BaseCreature;
        Require(owner != null && last is { Alive: true } && next is { Alive: true }, "Saved primitive caster/targets missing");
        var snapshot = InfernoFlames.Snapshot(); Require(snapshot.Contains(saved.Flame), "Saved flame rank/damage/velocity/phase/LastCollide/deadline changed");
        owner.MoveToWorld(new((int)(saved.Flame.X - 1.40625), (int)saved.Flame.Y, saved.Flame.Z), Map.Maps[saved.Flame.Map]);
        last.MoveToWorld(new(owner.X + 2, owner.Y, owner.Z), owner.Map); next.MoveToWorld(new(owner.X + 4, owner.Y, owner.Z), owner.Map);
        last.Frozen = next.Frozen = true; last.CantWalk = next.CantWalk = true; last.AIObject.AITimer.Stop(); next.AIObject.AITimer.Stop();
        last.SetResistance(ResistanceType.Fire, 0); next.SetResistance(ResistanceType.Fire, 0);
        int a = last.Hits, b = next.Hits; var copy = saved.Flame with { Id = snapshot.Max(x => x.Id) + 1,
            NextUtc = TimedEffects.Now, UntilUtc = TimedEffects.Now.AddMilliseconds(480) };
        InfernoFlames.Restore(snapshot.Append(copy).ToArray());
        Later(520, "inferno-primitives-reload-result", () =>
        {
            Require(last.Hits == a && next.Hits == b - 10 && InfernoFlames.Snapshot().Contains(saved.Flame)
                && !InfernoFlames.Snapshot().Contains(copy), "Restored flame failed last-target exclusion, resumed damage or expiry");
            _log.Information("inferno-primitives-reload-result: passed");
        });
    }
}
