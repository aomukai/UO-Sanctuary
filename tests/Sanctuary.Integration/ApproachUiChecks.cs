using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Server;
using Server.Accounting;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class ApproachUiChecks
{
    private static readonly ILogger _logger = LogFactory.GetLogger(typeof(ApproachUiChecks));
    private static readonly List<object> _samples = [];
    private static PlayerMobile _player;
    private static GraveyardUndead _enemy;
    private static CombatRandom _random;
    private static Action<PlayerMobile, Direction> _originalMoved;
    private static long _started, _notice, _acquired, _firstMove, _attack;
    private static Point3D _spawn;
    private static int _generation, _life;
    private static bool _tracking, _go;

    public static void Configure()
    {
        Register("skeleton", () => Setup(false));
        Register("zombie", () => Setup(true));
        Register("go", () =>
        {
            _started = Core.TickCount;
            _samples.Clear();
            _notice = _acquired = _firstMove = _attack = -1;
            _enemy.NextReacquireTime = Core.TickCount + (long)_enemy.ReacquireDelay.TotalMilliseconds;
            _go = true;
            _enemy.AIObject.AITimer.Prod();
        });
        Register("state", () => { });
        Register("arrived", () =>
        {
            Require(_notice >= 0 && _acquired >= 0 && _firstMove >= 0 && _attack >= 0, "Missing reaction/approach/attack event");
            Require(_enemy.InRange(_player, 1) && _enemy.InLOS(_player) && _player.Hits < _life,
                "Approach did not finish in a valid connected melee impact");
        });
        Register("final", () =>
        {
            _generation++;
            _go = false;
            _enemy?.Delete();
            _random?.Dispose();
            _random = null;
            if (_tracking)
            {
                ClassCombatHooks.Moved = _originalMoved;
                _tracking = false;
            }
            ClassCombat.Stop(_player);
        });
    }

    private static void Setup(bool zombie)
    {
        _generation++;
        _go = false;
        _enemy?.Delete();
        _random?.Dispose();
        _random = new CombatRandom();
        _player = (PlayerMobile)Accounts.GetAccount("combatui")[0];
        ClassCombat.Stop(_player);
        _player.Hits = _player.HitsMax;
        _life = _player.Hits;
        _player.MoveToWorld(CombatPractice.FindFloor(Map.Trammel, 5450, 1150), Map.Trammel);
        _enemy = zombie ? new GraveyardZombie() : new GraveyardSkeleton();
        _spawn = new Point3D(_player.X + 10, _player.Y, _player.Z);
        _enemy.Home = _spawn;
        _enemy.HomeMap = Map.Trammel;
        _enemy.RangeHome = 0;
        _enemy.MoveToWorld(_spawn, Map.Trammel);
        _enemy.AIObject.AITimer.Stop();
        _started = Core.TickCount;
        _notice = _acquired = _firstMove = _attack = -1;
        _samples.Clear();
        if (!_tracking)
        {
            _originalMoved = ClassCombatHooks.Moved;
            ClassCombatHooks.Moved = (player, direction) =>
            {
                _originalMoved?.Invoke(player, direction);
                if (_go && player == _player && _notice < 0)
                {
                    _notice = Core.TickCount - _started;
                }
            };
            _tracking = true;
        }
        Sample(_generation);
    }

    private static void Sample(int generation)
    {
        if (generation != _generation)
        {
            return;
        }
        if (_go)
        {
            long now = Core.TickCount;
            if (_enemy.Combatant == _player && _notice >= 0 && _acquired < 0)
            {
                _acquired = now - _started;
            }
            if (_acquired >= 0 && _enemy.Location != _spawn && _firstMove < 0)
            {
                _firstMove = now - _started;
            }
            if (_acquired >= 0 && _enemy.InRange(_player, 1) && _enemy.NextMeleeDecision - now > 0 && _attack < 0)
            {
                _attack = now - _started;
            }
            _samples.Add(new
            {
                t = now - _started, player = Position(_player), enemy = Position(_enemy), hp = _player.Hits,
                target = _enemy.Combatant?.Serial.Value ?? 0, next = _enemy.NextMeleeDecision - _started,
                scan = _enemy.NextReacquireTime - _started,
                phase = _enemy.AIObject.GetType().GetProperty("ApproachPhase")?.GetValue(_enemy.AIObject)?.ToString() ?? "Legacy"
            });
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(20), () => Sample(generation));
    }

    private static object Position(Mobile mobile) => new { x = mobile.X, y = mobile.Y, z = mobile.Z };
    private static void Require(bool value, string message)
    {
        if (!value)
        {
            throw new InvalidOperationException(message);
        }
    }
    private static void Register(string suffix, Action action)
    {
        string command = "approach-ui-" + suffix;
        ConsoleInputHandler.RegisterCommand(command, "Real-client monster reaction checks", _ => Core.LoopContext.Post(() =>
        {
            try
            {
                action();
                File.WriteAllText(Environment.GetEnvironmentVariable("SANCTUARY_TEST_UI_STATE"), JsonSerializer.Serialize(new
                {
                    player = Position(_player), enemy = Position(_enemy), samples = _samples.ToArray(),
                    elapsed = Core.TickCount - _started, notice = _notice, acquired = _acquired,
                    firstMove = _firstMove, attack = _attack
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
