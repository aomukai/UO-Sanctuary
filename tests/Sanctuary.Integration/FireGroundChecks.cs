using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class FireGroundChecks
{
    private static readonly ILogger _log = LogFactory.GetLogger(typeof(FireGroundChecks));
    private sealed record Saved(uint Owner, uint FractionTarget, BlazeState Aura, FirePatch[] Patches);
    private static string Manifest => Path.Join(Core.BaseDirectory, "fire-ground-checks.json");
    public static void Configure() { Register("fire-ground-check", Check); Register("fire-ground-reload-check", Reload); }
    private static void Register(string name, Action action) => ConsoleInputHandler.RegisterCommand(name, "Ground fire, fractional damage and lifecycle",
        _ => Core.LoopContext.Post(() => { try { action(); _log.Information("{Name}: passed", name); } catch (Exception e) { _log.Error(e, "{Name}: failed", name); } }));
    private static void Require(bool value, string why) { if (!value) { throw new InvalidOperationException(why); } }
    private static GraveyardSkeleton Enemy(PlayerMobile owner, int x, int y = 0)
    {
        var enemy = new GraveyardSkeleton { Frozen = true, CantWalk = true, NoKillAwards = true };
        enemy.MoveToWorld(new Point3D(owner.X + x, owner.Y + y, owner.Z), owner.Map); enemy.AIObject.AITimer.Stop();
        enemy.SetHits(20000); enemy.Hits = enemy.HitsMax; enemy.SetResistance(ResistanceType.Fire, 0); return enemy;
    }
    private static FirePatch[] Patches(PlayerMobile owner) => FireGround.Snapshot().Patches.Where(x => x.Owner == owner.Serial.Value).ToArray();
    private static void Cast(PlayerMobile owner, CombatAbility ability, Point3D? target = null)
    {
        owner.Mana = owner.ManaMax;
        Require(SorceressCombat.Release(owner, ability, Serial.Zero, target ?? owner.Location), "Fire cast failed: " + ability);
    }
    private static void Check()
    {
        var owner = ClassSkillChecks.Player(DiabloClass.Sorceress);
        owner.MoveToWorld(ClassSkillChecks.Corridor(owner, 5880, 1470), owner.Map);
        var fraction = Enemy(owner, 12); int start = fraction.Hits;
        Require(SkillHealth.Damage(owner, fraction, 128) == 0 && SkillHealth.Remainder256(fraction) == 128, "Half-point damage discarded");
        Require(SkillHealth.Damage(owner, fraction, 128) == 1 && fraction.Hits == start - 1 && SkillHealth.Remainder256(fraction) == 0,
            "Half-point accumulation failed");
        for (int i = 0; i < 20; i++) { SkillCombat.FireFrame(owner, fraction, 64); }
        Require(fraction.Hits == start - 6 && CreatureHitRecovery.Remaining(fraction) == 0, "Per-frame fire damage or periodic recovery exclusion");
        fraction.Hits = fraction.HitsMax; SkillHealth.Clear(fraction); SkillHealth.Damage(owner, fraction, 17);
        Reduction(owner);
        var wall = SkillMechanics.Values(owner, CombatAbility.FireWall);
        Require(wall.Eval("EDmgSymPerCalc") == 5 && wall.Damage256(false) == 252
            && NecromancerSummons.Formula("firewallmaker", "Range", wall) == 9, "Pinned wall formula or maker lifetime");
        var inferno = SkillMechanics.Values(owner, CombatAbility.Inferno);
        Require(inferno.Eval("EDmgSymPerCalc") == 16, "Balanced Inferno source formula was misclassified");
        int walk = owner.ComputeMovementSpeed(Direction.East, false), run = owner.ComputeMovementSpeed(Direction.East | Direction.Running, false);
        int mana = owner.Mana;
        Require(SorceressCombat.Release(owner, CombatAbility.Blaze, Serial.Zero, owner.Location) && owner.Mana == mana - 11, "Blaze one-time source mana");
        var aura = FireGround.Blaze(owner);
        Require(aura is { Rank: 1, Speed: 2 } && Math.Abs((aura.UntilUtc - TimedEffects.Now).TotalMilliseconds - 4680) < 100
            && Patches(owner).Length == 0, "Blaze duration/speed or idle emission");
        Require(owner.ComputeMovementSpeed(Direction.East, false) < walk && owner.ComputeMovementSpeed(Direction.East | Direction.Running, false) < run,
            "Blaze did not accelerate walking/running");
        owner.Direction = Direction.East; var origin = owner.Location;
        Require(owner.Move(Direction.East) && owner.Move(Direction.East) && owner.X == origin.X + 2, "Accepted Blaze movement fixture");
        Require(Patches(owner).Length == 4 && Patches(owner).All(x => Math.Abs((x.UntilUtc - TimedEffects.Now).TotalMilliseconds - 4600) < 100),
            $"Movement did not lay source-lifetime half-tile fire patches: {Patches(owner).Length} at {owner.Location}, aura {FireGround.Blaze(owner)}");
        var trail = Enemy(owner, -1); var outside = Enemy(owner, 8); int trailLife = trail.Hits, outsideLife = outside.Hits;
        Timer.StartTimer(TimeSpan.FromMilliseconds(700), () =>
        {
            try
            {
                Require(trail.Hits < trailLife && outside.Hits == outsideLife && owner.Hits == owner.HitsMax, "Blaze trail targeting, tick damage or friendly exclusion");
                FireGround.Cancel(owner);
                var center = new Point3D(owner.X + 3, owner.Y, owner.Z); var wallEnemy = Enemy(owner, 3); int before = wallEnemy.Hits;
                Cast(owner, CombatAbility.FireWall, center); int paid = owner.Mana;
                Require(!SorceressCombat.Release(owner, CombatAbility.FireWall, Serial.Zero, center) && owner.Mana == paid
                    && !FireGround.Ready(owner, CombatAbility.FireWall), "Fire Wall local delay did not reject a free repeat");
                Cast(owner, CombatAbility.FrozenArmor);
                Timer.StartTimer(TimeSpan.FromMilliseconds(500), () =>
                {
                    try
                    {
                        var patches = Patches(owner);
                        Require(patches.Length > 3 && patches.All(x => x.Ability == CombatAbility.FireWall && x.DamageRate == 41 && Math.Abs(x.X - center.X) < 0.01)
                            && patches.Any(x => x.Y < center.Y) && patches.Any(x => x.Y > center.Y) && wallEnemy.Hits < before,
                            "Perpendicular growing Fire Wall or source damage-rate delivery");
                    }
                    catch (Exception e) { _log.Error(e, "fire-ground-result: failed"); }
                });
                Timer.StartTimer(TimeSpan.FromMilliseconds(4400), () =>
                {
                    try
                    {
                        Require(Patches(owner).Length == 0 && !FireGround.Snapshot().Makers.Any(x => x.Owner == owner.Serial.Value)
                            && FireGround.Ready(owner, CombatAbility.FireWall) && FireGround.Speed(owner) == 0,
                            "Natural field/aura/local-delay expiration failed");
                        var row = SkillMechanics.For(CombatAbility.Blaze);
                        var high = new SkillValues(row, k => k == row.SourceKey ? 99 : 0, _ => 0, 99);
                        FireGround.CastBlaze(owner, high);
                        Require(owner.Move(Direction.East), "Persistence fire movement fixture");
                        var patches = Patches(owner);
                        Require(patches.Length == 2 && (patches[0].UntilUtc - TimedEffects.Now).TotalSeconds > 100, "High source-rank field lifetime fixture");
                        // Ordinary source lifetime is checked above. Keep the reload payload independent of suite duration.
                        patches = patches.Select(x => x with { UntilUtc = TimedEffects.Now.AddMinutes(10) }).ToArray();
                        var all = FireGround.Snapshot();
                        FireGround.Restore(all with { Patches = all.Patches.Select(x => patches.FirstOrDefault(p => p.Id == x.Id) ?? x).ToArray() });
                        File.WriteAllText(Manifest, JsonSerializer.Serialize(new Saved(owner.Serial.Value, fraction.Serial.Value,
                            SorceressEffects.Profiles.Find(owner).Blaze, patches)));
                        trail.Delete(); outside.Delete(); wallEnemy.Delete();
                        _log.Information("fire-ground-result: passed");
                    }
                    catch (Exception e) { _log.Error(e, "fire-ground-result: failed"); }
                });
            }
            catch (Exception e) { _log.Error(e, "fire-ground-result: failed"); }
        });
    }
    private static void Reduction(PlayerMobile owner)
    {
        var target = ClassSkillChecks.Player(); target.RawStr = target.RawDex = 1000;
        if (target.Backpack == null) { target.AddItem(new Backpack()); }
        var definition = NamedGear.Catalog.Items.First(d => NamedGear.Available(d.Id) && d.Properties.Any(x => x.Code == "red-mag")
            && !d.Properties.Any(x => x.Code.StartsWith("abs-fire", StringComparison.Ordinal)));
        var item = NamedGear.Create(definition.Id, 99, new Random(1)); target.Backpack.DropItem(item);
        Require(target.EquipItem(item), "Damage-rate reduction gear fixture"); ClassGear.Refresh(target);
        int reduction = GemCombat.Reduction(target); double resist = Math.Clamp(100 - target.FireResistance, 0, 200) / 100.0;
        double ordinary = NamedElements.Damage(owner, target, 10, "fire", enhance: false);
        double rate = NamedElements.Damage(owner, target, 10, "fire", enhance: false, damageRate: 41);
        double expected = Math.Max(0, 10 - Math.Floor(reduction * 256 * 41 / 1024.0) / 256) * resist;
        Require(reduction > 0 && rate > ordinary && Math.Abs(rate - expected) < 0.00001, "Source per-frame fixed-point MDR scaling");
        item.Delete(); target.Delete();
    }
    private static void Reload()
    {
        var saved = JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));
        var owner = World.FindMobile((Serial)saved.Owner) as PlayerMobile;
        Require(owner != null && SorceressEffects.Profiles.Find(owner)?.Blaze == saved.Aura, "Saved Blaze cast state/lifetime changed");
        var current = Patches(owner);
        Require(current.Length == saved.Patches.Length && current.All(p => saved.Patches.Any(old => p with { NextUtc = old.NextUtc } == old)),
            "Ground fire identity, geometry, damage snapshot or expiration lost across reload");
        var fraction = World.FindMobile((Serial)saved.FractionTarget);
        Require(fraction != null && SkillHealth.Remainder256(fraction) == 17, "Fractional health remainder lost across reload");
        Require(SkillHealth.Damage(owner, fraction, 239) == 1 && SkillHealth.Remainder256(fraction) == 0, "Saved health fraction did not settle exactly");
        fraction.Hits = fraction.HitsMax; SkillHealth.Clear(fraction); SkillHealth.Damage(owner, fraction, 17);
        Require(FireGround.Ready(owner, CombatAbility.FireWall), "Expired cooldown remained latched after reload");
        owner.MoveToWorld(new Point3D(saved.Aura.X, saved.Aura.Y, saved.Aura.Z), Map.Trammel);
        var enemy = Enemy(owner, 0); int before = enemy.Hits;
        Timer.StartTimer(TimeSpan.FromMilliseconds(250), () =>
        {
            try
            {
                Require(enemy.Hits < before, "Restored ground fire did not resume damaging a target");
                _log.Information("fire-ground-reload-result: passed");
            }
            catch (Exception e) { _log.Error(e, "fire-ground-reload-result: failed"); }
            finally { enemy.Delete(); }
        });
    }
}
