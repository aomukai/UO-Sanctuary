using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Server;
using Server.Accounting;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using Server.Network;

namespace Sanctuary.Integration;

// Ordinary movement packets from a scripted hostile, observed by the real client.
public static class PursuitUiChecks
{
    private static readonly ILogger _logger = LogFactory.GetLogger(typeof(PursuitUiChecks));
    private static readonly List<object> _samples = [];
    private static PlayerMobile _player;
    private static Skeleton _enemy;
    private static Halberd _halberd;
    private static CombatRandom _random;
    private static int _generation, _step;
    private static long _started, _nextMove;
    private static bool _moving, _throttle, _tracking;

    public static void Configure()
    {
        Register("dagger", () => Setup(false));
        Register("halberd", () => Setup(true));
        Register("go", () =>
        {
            Require(ClassCombat.For(_player).Target == _enemy.Serial, "Mouse did not select pursuit enemy");
            _samples.Clear();
            _started = Core.TickCount;
            _nextMove = _started;
            _step = 0;
            _moving = true;
        });
        Register("state", () => { });
        Register("arrived", () =>
        {
            Require(_step == 32 && !_moving, "Scripted target did not finish movement attempts");
            Require(_player.InRange(_enemy, CombatTiming.Range(_player, CombatAbility.BasicAttack)), "Pursuit did not reacquire target");
            Require(_enemy.Hits < _enemy.HitsMax, "Pursuit never connected a melee hit");
        });
        Register("final", () =>
        {
            _generation++;
            ClassCombat.Stop(_player);
            _enemy.Delete();
            _halberd?.Delete();
            _random?.Dispose();
            _random = null;
            if (_tracking)
            {
                ServerFeatureFlags.SpeedhackDetection = _throttle;
                _tracking = false;
            }
        });
    }

    private static void Setup(bool halberd)
    {
        _generation++;
        _moving = false;
        _player = (PlayerMobile)Accounts.GetAccount("combatui")[0];
        ClassCombat.Stop(_player);
        _enemy?.Delete();
        _halberd?.Delete();
        _random ??= new CombatRandom();
        if (!_tracking)
        {
            _throttle = ServerFeatureFlags.SpeedhackDetection;
            ServerFeatureFlags.SpeedhackDetection = true;
            _tracking = true;
        }
        var oldWeapon = CombatTiming.Equipped(_player);
        if (oldWeapon != null)
        {
            _player.AddToBackpack(oldWeapon);
        }
        _player.Str = _player.Dex = _player.Int = 100;
        BaseWeapon weapon = _player.Backpack.FindItemByType<Dagger>();
        if (halberd)
        {
            weapon = _halberd = new Halberd();
        }
        Require(weapon != null && _player.EquipItem(weapon), "Cannot equip pursuit weapon");
        var point = CombatPractice.FindFloor(Map.Trammel, 5450, 1150);
        _player.MoveToWorld(point, Map.Trammel);
        _enemy = new Skeleton { Name = "Pursuit practice", Body = 50, Frozen = true };
        _enemy.SetHits(10000);
        _enemy.Hits = 10000;
        _enemy.MoveToWorld(new Point3D(point.X + 6, point.Y, point.Z), _player.Map);
        _enemy.AIObject.AITimer.Stop();
        _started = Core.TickCount;
        _samples.Clear();
        Sample(_generation);
    }

    private static void Sample(int generation)
    {
        if (generation != _generation)
        {
            return;
        }
        long now = Core.TickCount;
        bool moved = false;
        if (_moving && now >= _nextMove)
        {
            // Retreat, reverse through the pursuer, then turn laterally twice.
            var direction = _step switch
            {
                < 8 => Direction.East,
                < 20 => Direction.West,
                < 24 => Direction.South,
                < 32 => Direction.North,
                _ => Direction.East
            };
            if (_step < 32)
            {
                _enemy.Frozen = false;
                _enemy.Direction = direction;
                var before = _enemy.Location;
                moved = _enemy.Move(direction | Direction.Running) && _enemy.Location != before;
                _enemy.Frozen = true;
                _enemy.Combatant = null;
                _enemy.FocusMob = null;
                _enemy.AIObject.AITimer.Stop();
                _step++;
                _nextMove = now + 320;
            }
            else
            {
                _moving = false;
            }
        }
        var session = ClassCombat.For(_player);
        _samples.Add(new
        {
            t = now - _started, player = Position(_player), enemy = Position(_enemy), moved,
            hp = _enemy.Hits, next = session.NextAction, effect = session.EffectAt,
            pending = (int)session.PendingAbility, selected = session.Target.Value
        });
        Timer.StartTimer(TimeSpan.FromMilliseconds(40), () => Sample(generation));
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
        string command = "pursuit-ui-" + suffix;
        ConsoleInputHandler.RegisterCommand(command, "Isolated moving-target melee checks", _ => Core.LoopContext.Post(() =>
        {
            try
            {
                action();
                File.WriteAllText(Environment.GetEnvironmentVariable("SANCTUARY_TEST_UI_STATE"), JsonSerializer.Serialize(new
                {
                    player = Position(_player), enemy = Position(_enemy), samples = _samples.ToArray(),
                    range = CombatTiming.Range(_player, CombatAbility.BasicAttack),
                    cycle = CombatTiming.For(_player, CombatAbility.BasicAttack).DurationMs,
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
