using System;
using System.Linq;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class CreatureProfileChecks
{
    private static readonly ILogger _logger = LogFactory.GetLogger(typeof(CreatureProfileChecks));
    private static PlayerMobile _player;
    private static BaseCreature _enemy;
    private static CombatRandom _random;
    private static Point3D _origin;
    private static long _until, _attack;
    private static int _life;
    public static void Configure() => ConsoleInputHandler.RegisterCommand("creature-profile-check",
        "Common NPC coverage, variants and actual creature recovery", _ => Core.LoopContext.Post(() => Run(Setup)));
    private static void Require(bool value, string message)
    {
        if (!value)
        {
            throw new InvalidOperationException(message);
        }
    }
    private static void Run(Action action)
    {
        try { action(); }
        catch (Exception error)
        {
            _logger.Error(error, "creature-profile-check: failed");
            Cleanup();
        }
    }
    private static void Later(int ms, Action action) => Timer.StartTimer(TimeSpan.FromMilliseconds(ms), () => Run(action));
    private static void Setup()
    {
        var compiled = typeof(BaseCreature).Assembly.GetTypes().Where(t => !t.IsAbstract && typeof(BaseCreature).IsAssignableFrom(t)).ToArray();
        Require(CreatureProfiles.All.Count == compiled.Length + 4 && compiled.All(t => CreatureProfiles.All.Any(r => r.Type == t.FullName)),
            "Common profile catalog does not cover every built-in creature");
        var dragon = new Dragon();
        var proposal = CreatureProfiles.Snapshot(dragon);
        var hell = CreatureProfiles.Snapshot(dragon, MonsterDifficulty.Hell);
        Require(proposal.Reference == "megademon3" && proposal.Resistances.Fire == 70 && hell.Resistances.Fire == 130
            && !proposal.Profile.ApplyStats && !hell.Profile.Enabled && ClassStats.Level(dragon) == 30
            && CreatureProfiles.Experience(dragon) == 1001 && dragon.FireResistance < 100,
            "Creative dragon proposal leaked disabled stats into the live native creature");
        Require(proposal.Profile.SourceStats.Count == 255 && proposal.Profile.SourceModes.ContainsKey("mGH"),
            "Common D2 source extension omitted source fields");
        dragon.Delete();
        var vendor = new Provisioner();
        Require(!CreatureProfiles.For(vendor).HitRecovery.Enabled && CreatureProfiles.Definition(vendor).Role == "Vendor",
            "Vendor inherited a hostile behavior/recovery profile");
        vendor.Delete();
        Require(CreatureHitRecovery.Chance(7, 128) == 0 && CreatureHitRecovery.Chance(8, 128) == 3
            && CreatureHitRecovery.Chance(16, 128) == 6 && CreatureHitRecovery.Chance(32, 128) == 8,
            "Creature damage thresholds lost D2 probability bands");
        _player = new PlayerMobile { Player = true, Body = 0x190, Name = "CreatureProfileCheck" };
        ClassCreation.Profiles.Assign(_player, DiabloClass.Necromancer);
        _origin = CombatPractice.FindFloor(Map.Trammel, 5450, 1150);
        _player.MoveToWorld(new Point3D(_origin.X + 1, _origin.Y, _origin.Z), Map.Trammel);
        _random = new CombatRandom(0);
        _enemy = new Zombie();
        _enemy.SetHits(128);
        _enemy.SetResistance(ResistanceType.Physical, 0);
        _enemy.SetResistance(ResistanceType.Poison, 0);
        _enemy.MoveToWorld(_origin, Map.Trammel);
        _enemy.AIObject.AITimer.Stop();
        Require(CreatureHitRecovery.Frames(CreatureProfiles.For(_enemy).HitRecovery) == 17,
            "Zombie GH metadata did not produce 680ms recovery");
        AOS.Damage(_enemy, _player, 1, 100, 0, 0, 0, 0);
        Require(CreatureHitRecovery.Remaining(_enemy) == 0, "Weak damage triggered recovery");
        AOS.Damage(_enemy, _player, 32, 100, 0, 0, 0, 0);
        Require(CreatureHitRecovery.Remaining(_enemy) is > 620 and <= 680 && !_enemy.Frozen && !_enemy.Paralyzed,
            "Real damage failed to enter independent creature recovery");
        _until = CreatureProfiles.Session(_enemy).RecoveryUntil;
        _life = _player.Hits;
        Require(!_enemy.Move(Direction.North), "Server creature movement bypassed recovery");
        ((BaseWeapon)_enemy.Weapon).OnSwing(_enemy, _player);
        Require(_player.Hits == _life, "Native melee bypassed recovery");
        AOS.Damage(_enemy, _player, 32, 100, 0, 0, 0, 0);
        Require(_until == CreatureProfiles.Session(_enemy).RecoveryUntil, "Repeated damage refreshed recovery");
        Later(750, Recovered);
    }
    private static void Recovered()
    {
        Require(CreatureHitRecovery.Remaining(_enemy) == 0 && _enemy.Move(Direction.North),
            "Creature did not regain authoritative movement after recovery");
        _enemy.Hits = _enemy.HitsMax;
        AOS.Damage(_enemy, _player, 32, 0, 0, 0, 100, 0);
        AOS.Damage(_enemy, _player, 32, 0, 0, 0, 50, 0);
        Require(CreatureHitRecovery.Remaining(_enemy) == 0, "Pure poison triggered get-hit");
        AOS.Damage(_enemy, _player, 16, 100, 0, 0, 100, 0);
        Require(CreatureHitRecovery.Remaining(_enemy) > 0, "Mixed poison/physical damage was mistaken for pure poison");
        _enemy.Delete();
        _enemy = new GraveyardSkeleton();
        _enemy.SetHits(128);
        _enemy.MoveToWorld(_origin, Map.Trammel);
        _enemy.AIObject.AITimer.Stop();
        Require(CreatureHitRecovery.Frames(CreatureProfiles.For(_enemy).HitRecovery) == 8, "Skeleton GH timing changed");
        var undead = (GraveyardUndead)_enemy;
        Require(undead.BeginMelee(_player).TotalMilliseconds == 1360, "Pending melee fixture did not start");
        _attack = undead.NextMeleeDecision;
        _life = _player.Hits;
        AOS.Damage(_enemy, _player, 32, 100, 0, 0, 0, 0);
        Require(undead.NextMeleeDecision == _attack && CreatureHitRecovery.Remaining(_enemy) > 280,
            "Recovery refunded an attack cooldown or failed to start");
        _enemy.AIObject.AITimer.Stop();
        Later(550, Interrupted);
    }
    private static void Interrupted()
    {
        Require(_player.Hits == _life && ((GraveyardUndead)_enemy).NextMeleeDecision == _attack,
            "Interrupted impact landed after recovery or its cooldown was refunded");
        Require(CreatureHitRecovery.Frames(new CreatureRecovery(true, 4, 256, 60)) < 8,
            "Monster FHR did not reduce recovery");
        _logger.Information("creature-profile-check: passed ({Count} built-in creature types)", CreatureProfiles.All.Count - 4);
        Cleanup();
    }
    private static void Cleanup()
    {
        _random?.Dispose();
        _random = null;
        _enemy?.Delete();
        _enemy = null;
        if (_player != null)
        {
            
            _player.Delete();
            _player = null;
            ClassCreation.Profiles.PruneDeleted();
        }
    }
}
