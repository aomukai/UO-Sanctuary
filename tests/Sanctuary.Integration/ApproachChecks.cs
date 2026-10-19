using System;
using System.Collections.Generic;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class ApproachChecks
{
    private static readonly ILogger _logger = LogFactory.GetLogger(typeof(ApproachChecks));
    private static readonly List<Item> _walls = [];
    private static PlayerMobile _player;
    private static GraveyardUndead _enemy;
    private static LeashedMeleeAI _ai;
    private static CombatRandom _random;
    private static Point3D _origin, _afterWander;
    private static long _until, _moveDue, _routeStarted;
    private static int _rolls;

    public static void Configure() => ConsoleInputHandler.RegisterCommand("approach-check",
        "Species approach decisions, movement clocks and collision", _ => Core.LoopContext.Post(() => Run(Setup)));

    private static void Require(bool value, string message)
    {
        if (!value)
        {
            throw new InvalidOperationException(message);
        }
    }
    private static void Run(Action action)
    {
        try
        {
            action();
        }
        catch (Exception error)
        {
            _logger.Error(error, "approach-check: failed");
            Cleanup();
        }
    }
    private static void Later(int ms, Action action) => Timer.StartTimer(TimeSpan.FromMilliseconds(ms), () => Run(action));
    private static void Roll(double value)
    {
        _random?.Dispose();
        _random = new CombatRandom(value);
    }
    private static void Enemy(bool zombie, int distance = 6)
    {
        _enemy?.Delete();
        _enemy = zombie ? new GraveyardZombie() : new GraveyardSkeleton();
        _enemy.Home = _origin;
        _enemy.HomeMap = Map.Trammel;
        _enemy.RangeHome = 0;
        _enemy.MoveToWorld(_origin, Map.Trammel);
        _enemy.AIObject.AITimer.Stop();
        _player.MoveToWorld(new Point3D(_origin.X + distance, _origin.Y, _origin.Z), Map.Trammel);
        _enemy.Combatant = _player;
        _enemy.NextCombatTime = Core.TickCount + 60000;
        _ai = (LeashedMeleeAI)_enemy.AIObject;
        _ai.Action = ActionType.Combat;
    }
    private static void Setup()
    {
        _player = new PlayerMobile { Player = true, Body = 0x190, Name = "ApproachCheck" };
        ClassCreation.Profiles.Assign(_player, DiabloClass.Necromancer);
        _origin = CombatPractice.FindFloor(Map.Trammel, 5450, 1150);
        Require(EarlyNormalMonsters.Skeleton.Approach == new MonsterApproach(60, 0, 600, false)
            && EarlyNormalMonsters.Zombie.Approach == new MonsterApproach(30, 10, 600, true), "Species approach profiles changed");
        Enemy(false);
        Require(_enemy.AcquireOnApproachDelay == TimeSpan.Zero && _enemy.ReacquireDelay.TotalMilliseconds == 600,
            "Project undead inherited the native notice delay");
        var legacy = new Skeleton();
        var lich = new GraveyardLich();
        Require(legacy.AcquireOnApproachDelay.TotalSeconds == 2 && lich.AcquireOnApproachDelay.TotalSeconds == 2,
            "Species notice change leaked to stock mobs or caster prototype");
        legacy.Delete();
        lich.Delete();
        Roll(.99);
        _ai.DoActionCombat();
        Require(_ai.ApproachPhase == MonsterApproachPhase.Hesitating && _enemy.Location == _origin,
            "Skeleton failed its approach roll without hesitating");
        _until = _ai.ApproachUntil;
        _rolls = _ai.ApproachRolls;
        for (int i = 0; i < 64; i++)
        {
            _ai.DoActionCombat();
        }
        Require(_ai.ApproachRolls == _rolls && _ai.ApproachUntil == _until && !_ai.TryGetMoveWake(out _),
            "Think polling rerolled/extended hesitation or left a stale movement intent");
        _player.MoveToWorld(new Point3D(_origin.X + 1, _origin.Y, _origin.Z), Map.Trammel);
        Require(_enemy.BeginMelee(_player).TotalMilliseconds > 100 && _enemy.NextMeleeDecision - Core.TickCount <= 0,
            "Native swing loop bypassed a pending approach decision");
        Later(650, SkeletonReady);
    }
    private static void SkeletonReady()
    {
        Roll(0);
        _ai.DoActionCombat();
        Require(_ai.ApproachPhase == MonsterApproachPhase.Melee && _enemy.BeginMelee(_player).TotalMilliseconds == 1360,
            "Skeleton did not enter melee after its hesitation elapsed");
        Enemy(false);
        _ai.DoActionCombat();
        _rolls = _ai.ApproachRolls;
        _moveDue = _ai.NextMove;
        var first = _enemy.Location;
        Require(_ai.ApproachPhase == MonsterApproachPhase.Pursuing && first != _origin, "Successful approach did not commit");
        for (int i = 0; i < 32; i++)
        {
            _ai.DoActionCombat();
        }
        Require(_ai.ApproachRolls == _rolls && _enemy.Location == first && _ai.NextMove == _moveDue,
            "Committed pursuit rerolled per think or accelerated translation");
        Enemy(true);
        Roll(.99);
        _ai.DoActionCombat();
        _afterWander = _enemy.Location;
        _until = _ai.ApproachUntil;
        _moveDue = _ai.NextMove;
        Require(_ai.ApproachPhase == MonsterApproachPhase.Wandering && _afterWander != _origin
            && _moveDue - Core.TickCount is > 900 and <= 960, "Zombie lost its local slow wander");
        for (int i = 0; i < 32; i++)
        {
            _ai.DoActionCombat();
        }
        Require(_ai.ApproachRolls == 1 && _ai.ApproachUntil == _until && _enemy.Location == _afterWander
            && !_ai.TryGetMoveWake(out _), "Zombie wander rerolled or moved without a new budget");
        Later(650, ZombieCommit);
    }
    private static void ZombieCommit()
    {
        Roll(0);
        _ai.DoActionCombat();
        Require(_ai.ApproachPhase == MonsterApproachPhase.Pursuing && _ai.ApproachRolls == 2
            && _enemy.Location == _afterWander && _ai.NextMove == _moveDue,
            "Committing approach refunded the zombie's remaining movement period");
        Later(360, ZombieStep);
    }
    private static void ZombieStep()
    {
        _ai.DoActionCombat();
        Require(_enemy.Location != _afterWander && _ai.ApproachRolls == 2
            && _ai.NextMove - Core.TickCount is > 900 and <= 960, "Zombie failed to continue slow committed pursuit");
        Enemy(true, 10);
        _ai.DoActionCombat();
        Require(_ai.ApproachPhase == MonsterApproachPhase.Wandering, "Unaware zombie pursued outside its approach cutoff");
        Enemy(false);
        _enemy.Combatant = null;
        _enemy.ForceReacquire();
        Require(_ai.AcquireFocusMob(14, FightMode.Closest, false, false, true), "Visible hostile was not acquired");
        for (int i = 0; i < 64; i++)
        {
            _enemy.ForceReacquire();
            _ai.AcquireFocusMob(14, FightMode.Closest, false, false, true);
        }
        Require(_ai.FocusScans == 1, "Repeated notices flooded spatial acquisition scans");
        Enemy(false);
        _enemy.Combatant = null;
        _player.Hidden = true;
        _enemy.ForceReacquire();
        Require(!_ai.AcquireFocusMob(14, FightMode.Closest, false, false, true), "Hidden player was acquired");
        _player.Hidden = false;
        Enemy(false, 4);
        for (int dy = -2; dy <= 2; dy++)
        {
            var wall = new Static(0x80);
            wall.MoveToWorld(new Point3D(_origin.X + 2, _origin.Y + dy, _origin.Z), Map.Trammel);
            _walls.Add(wall);
        }
        Require(!_enemy.InLOS(_player), "Detour fixture lacks blocking LOS");
        _routeStarted = Core.TickCount;
        Detour();
    }
    private static void Detour()
    {
        _ai.Think();
        if (_enemy.InRange(_player, 1) && _enemy.InLOS(_player))
        {
            Require(_ai.ApproachRolls == 1 && !_enemy.ReturningHome, "Detour lost committed approach or exceeded leash");
            Require(_enemy.X > _origin.X + 2, "Enemy hit through the blocking wall");
            _logger.Information("approach-check: passed");
            Cleanup();
            return;
        }
        Require(Core.TickCount - _routeStarted < 7000, "Monster failed its collision-checked detour");
        Require(!_enemy.InRange(_player, 1) || !_enemy.InLOS(_player) || Math.Abs(_enemy.Z - _player.Z) < 16,
            "Detour accepted a false attack height");
        Later(40, Detour);
    }
    private static void Cleanup()
    {
        _random?.Dispose();
        _random = null;
        _enemy?.Delete();
        _player?.Delete();
        foreach (var wall in _walls)
        {
            wall.Delete();
        }
        _walls.Clear();
        ClassCreation.Profiles.PruneDeleted();
    }
}
