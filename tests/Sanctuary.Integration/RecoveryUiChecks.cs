using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Server;
using Server.Accounting;
using Server.Logging;
using Server.Mobiles;
using Server.Network;

namespace Sanctuary.Integration;

// Deterministic eligible damage events isolate input/prediction from damage rolls.
// A real-timer zombie impact separately proves the production damage path enters this state.
public static class RecoveryUiChecks
{
    private static readonly ILogger _logger = LogFactory.GetLogger(typeof(RecoveryUiChecks));
    private static readonly List<object> _moves = [], _hits = [];
    private static PlayerMobile _player;
    private static GraveyardZombie _zombie;
    private static CombatRandom _random;
    private static int _life;
    private static Action<PlayerMobile, Direction> _originalMoved;
    private static Point3D _hitPosition;
    private static long _started, _deadline;
    private static bool _tracking, _throttle, _invalidMove;
    private static int _generation;
    public static void Configure()
    {
        Register("setup", () =>
        {
            _player = (PlayerMobile)Accounts.GetAccount("combatui")[0];
            ClassCombat.Stop(_player);
            ClassCombat.For(_player).RecoveryUntil = Core.TickCount;
            _player.MoveToWorld(CombatPractice.FindFloor(Map.Trammel, 5450, 1150), Map.Trammel);
            _started = Core.TickCount;
            _moves.Clear();
            _hits.Clear();
            _invalidMove = false;
            if (!_tracking)
            {
                _throttle = ServerFeatureFlags.SpeedhackDetection;
                ServerFeatureFlags.SpeedhackDetection = true;
                _originalMoved = ClassCombatHooks.Moved;
                ClassCombatHooks.Moved = (player, direction) =>
                {
                    _originalMoved?.Invoke(player, direction);
                    if (player == _player)
                    {
                        _invalidMove |= HitRecovery.Active(player, Core.TickCount);
                        _moves.Add(new { t = Core.TickCount - _started, x = player.X, y = player.Y, z = player.Z });
                    }
                };
                _tracking = true;
            }
        });
        Register("zombie", () =>
        {
            _zombie = new GraveyardZombie { CantWalk = true };
            _zombie.MoveToWorld(new Point3D(_player.X + 1, _player.Y, _player.Z), _player.Map);
            _zombie.AIObject.AITimer.Stop();
            _random = new CombatRandom(0, 0.5);
            _life = _player.Hits;
            Require(_zombie.BeginMelee(_player).TotalMilliseconds == 1240, "Zombie failed to begin real melee");
        });
        Register("zombie-hit", () =>
        {
            Require(_player.Hits == _life - 4 && HitRecovery.Active(_player, Core.TickCount),
                $"Actual zombie critical did not cause recovery: damage={_life - _player.Hits}, life={_player.HitsMax}");
            _random.Dispose();
            _random = null;
            _zombie.Delete();
            _zombie = null;
        });
        Register("weak", () => Require(!HitRecovery.Apply(_player, 1, Core.TickCount), "Weak event started recovery"));
        Register("strong", Strong);
        Register("repeat", () => Require(!HitRecovery.Apply(_player, _player.HitsMax, Core.TickCount)
            && ClassCombat.For(_player).RecoveryUntil == _deadline, "Repeated hit extended active recovery"));
        Register("locked", () => Require(HitRecovery.Active(_player, Core.TickCount)
            && _player.Location == _hitPosition, "Client moved during recovery"));
        Register("resumed", () => Require(!HitRecovery.Active(_player, Core.TickCount)
            && _player.Location != _hitPosition, "Held input did not resume after recovery"));
        Register("pack", () => { Strong(); Pack(_generation, 7); });
        Register("state", () => { });
        Register("released", () => Require(!HitRecovery.Active(_player, Core.TickCount)
            && _player.Location == _hitPosition, "Release during recovery resumed an abandoned route"));
        Register("final", () =>
        {
            _generation++;
            _random?.Dispose();
            _random = null;
            _zombie?.Delete();
            ClassCombat.Stop(_player);
            if (_tracking)
            {
                ClassCombatHooks.Moved = _originalMoved;
                ServerFeatureFlags.SpeedhackDetection = _throttle;
                _tracking = false;
            }
        });
    }
    private static void Strong()
    {
        Require(HitRecovery.Apply(_player, _player.HitsMax, Core.TickCount), "Eligible event did not start recovery");
        _deadline = ClassCombat.For(_player).RecoveryUntil;
        _hitPosition = _player.Location;
        _hits.Add(new { t = Core.TickCount - _started, until = _deadline - _started });
    }
    private static void Pack(int generation, int remaining)
    {
        Timer.StartTimer(TimeSpan.FromMilliseconds(160), () =>
        {
            if (generation != _generation)
            {
                return;
            }
            if (HitRecovery.Apply(_player, _player.HitsMax, Core.TickCount))
            {
                _hits.Add(new { t = Core.TickCount - _started, until = ClassCombat.For(_player).RecoveryUntil - _started });
            }
            if (remaining > 1)
            {
                Pack(generation, remaining - 1);
            }
        });
    }
    private static void Require(bool value, string message)
    {
        if (!value)
        {
            throw new InvalidOperationException(message);
        }
    }
    private static void Register(string suffix, Action action)
    {
        string command = "recovery-ui-" + suffix;
        ConsoleInputHandler.RegisterCommand(command, "Isolated hit recovery input checks", _ => Core.LoopContext.Post(() =>
        {
            try
            {
                action();
                Require(!_invalidMove, "An accepted translation bypassed server recovery");
                File.WriteAllText(Environment.GetEnvironmentVariable("SANCTUARY_TEST_UI_STATE"), JsonSerializer.Serialize(new
                {
                    player = new { x = _player.X, y = _player.Y, z = _player.Z }, elapsed = Core.TickCount - _started,
                    until = ClassCombat.For(_player).RecoveryUntil - _started, moves = _moves.ToArray(), hits = _hits.ToArray(),
                    network = MovementThrottle.GetMovementStats(_player.NetState)
                }));
                _logger.Information("{Command}: passed", command);
            }
            catch (Exception error)
            {
                _logger.Error(error, "{Command}: failed", command);
            }
        }));
    }
}
