using System;
using System.IO;
using System.Text.Json;
using Server;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class ThunderStormChecks
{
    private static readonly ILogger _log = LogFactory.GetLogger(typeof(ThunderStormChecks));
    private sealed record Saved(uint Owner, DateTime UntilUtc, int IntervalFrames, int Rank, int X, int Y, int Z);
    private static string Manifest => Path.Join(Core.BaseDirectory, "thunderstorm-checks.json");
    public static void Configure()
    { Register("thunderstorm-check", Check); Register("thunderstorm-reload-check", Reload); }
    private static void Register(string name, Action action) => ConsoleInputHandler.RegisterCommand(name,
        "Isolated native Thunder Storm scheduling and persistence", _ => Core.LoopContext.Post(() =>
        { try { action(); _log.Information("{Name}: passed", name); } catch (Exception e) { _log.Error(e, "{Name}: failed", name); } }));
    private static void Require(bool value, string why) { if (!value) { throw new InvalidOperationException(why); } }
    private static GraveyardSkeleton Enemy(PlayerMobile owner, int offset)
    {
        var enemy = new GraveyardSkeleton { Frozen = true, CantWalk = true, NoKillAwards = true };
        enemy.MoveToWorld(new Point3D(owner.X + offset, owner.Y, owner.Z), owner.Map); enemy.AIObject.AITimer.Stop();
        enemy.SetHits(5000); enemy.Hits = enemy.HitsMax; enemy.SetResistance(ResistanceType.Energy, 0);
        Require(ClassCombat.IsHostile(owner, enemy), "Storm enemy not hostile"); return enemy;
    }
    private static void Check()
    {
        var owner = ClassSkillChecks.Player(DiabloClass.Sorceress);
        owner.MoveToWorld(ClassSkillChecks.Corridor(owner, 5560, 1230), owner.Map);
        var first = Enemy(owner, 1); var second = Enemy(owner, 3); var outside = Enemy(owner, 10);
        int a = first.Hits, b = second.Hits, c = outside.Hits, mana = owner.Mana;
        var source = SkillMechanics.For(CombatAbility.ThunderStorm);
        var high = new SkillValues(source, k => k == source.SourceKey ? 20 : 0, _ => 0, 99);
        Require(high.Eval("perdelay") == 41 && high.Eval("auralencalc") == 15000, "Storm rank20 source timing");
        Require(SorceressCombat.Release(owner, CombatAbility.ThunderStorm, Serial.Zero, owner.Location), "Storm release failed");
        var cast = ThunderStorm.Active(owner);
        Require(cast is { Rank: 1, IntervalFrames: 110, RadiusSubtiles: 17 } && mana - owner.Mana == 19
            && Math.Abs((cast.UntilUtc - TimedEffects.Now).TotalSeconds - 144) < 0.1
            && first.Hits == a && second.Hits == b, "Storm source cost/duration/interval or immediate hit");
        int paid = owner.Mana;
        for (int i = 1; i < 20; i++) { Require(ClassCreation.Profiles.TryTrain(owner.Serial, "sorceress.thunderstorm", out _), "Storm rank fixture"); }
        var snapshot = SkillMechanics.Values(owner, CombatAbility.ThunderStorm, cast.Rank);
        Require(snapshot.Rank == 1 && snapshot.Damage256(true) == 27392 && ThunderStorm.Active(owner).IntervalFrames == 110,
            "Storm lost its cast rank to later training");
        Timer.StartTimer(TimeSpan.FromMilliseconds(4600), () =>
        {
            try
            {
                Require(first.Hits < a && second.Hits == b && ThunderStorm.Active(owner).LastTarget == first.Serial.Value,
                    "Storm did not begin with the smallest eligible ID");
                Require(owner.Mana >= paid && outside.Hits == c, "Storm spent periodic mana or hit outside source radius");
                a = first.Hits;
                Timer.StartTimer(TimeSpan.FromMilliseconds(4400), () =>
                {
                    try
                    {
                        Require(second.Hits < b && first.Hits == a && ThunderStorm.Active(owner).LastTarget == second.Serial.Value,
                            "Storm did not advance to the next eligible ID");
                        b = second.Hits;
                        Timer.StartTimer(TimeSpan.FromMilliseconds(4400), () =>
                        {
                            try
                            {
                                Require(first.Hits < a && second.Hits == b && outside.Hits == c,
                                    "Storm failed to wrap or respected no radius");
                                var state = ThunderStorm.Active(owner);
                                File.WriteAllText(Manifest, JsonSerializer.Serialize(new Saved(owner.Serial.Value, state.UntilUtc, state.IntervalFrames, state.Rank, owner.X, owner.Y, owner.Z)));
                                first.Delete(); second.Delete(); outside.Delete();
                                _log.Information("thunderstorm-result: passed");
                            }
                            catch (Exception e) { Fail(owner, first, second, outside, e); }
                        });
                    }
                    catch (Exception e) { Fail(owner, first, second, outside, e); }
                });
            }
            catch (Exception e) { Fail(owner, first, second, outside, e); }
        });
    }
    private static void Fail(PlayerMobile owner, Mobile first, Mobile second, Mobile outside, Exception e)
    { SorceressEffects.Profiles.Clear(owner); owner.Delete(); first.Delete(); second.Delete(); outside.Delete(); _log.Error(e, "thunderstorm-result: failed"); }
    private static void Reload()
    {
        var saved = JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));
        var owner = World.FindMobile((Serial)saved.Owner) as PlayerMobile;
        Require(owner != null, "Saved storm owner missing");
        owner.MoveToWorld(new Point3D(saved.X, saved.Y, saved.Z), Map.Trammel);
        var state = ThunderStorm.Active(owner);
        Require(owner != null && state is not null && state.UntilUtc == saved.UntilUtc && state.IntervalFrames == saved.IntervalFrames
            && state.Rank == saved.Rank && state.NextStrikeUtc <= TimedEffects.Now.AddMilliseconds(saved.IntervalFrames * 40 + 100),
            "Storm source snapshot, absolute lifetime or scheduling phase lost");
        var enemy = Enemy(owner, 2); int before = enemy.Hits;
        Timer.StartTimer(TimeSpan.FromMilliseconds(saved.IntervalFrames * 40 + 250), () =>
        {
            try { Require(enemy.Hits < before, "Saved storm did not resume periodic strikes"); _log.Information("thunderstorm-reload-result: passed"); }
            catch (Exception e) { _log.Error(e, "thunderstorm-reload-result: failed"); }
            finally { enemy.Delete(); }
        });
    }
}
