using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Server;
using Server.Accounting;
using Server.Items;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class MovementUiChecks
{
    private static readonly ILogger _logger = LogFactory.GetLogger(typeof(MovementUiChecks));
    private static readonly List<Item> _walls = [];
    private static PlayerMobile _player;
    private static Point3D _origin, _goal;
    private static Point3D _stopped;
    private static readonly List<long> _steps = [];
    private static readonly List<Item> _gear = [];
    private static Action<PlayerMobile, Direction> _originalMoved;
    private static Point3D _lastStep;

    public static void Configure()
    {
        Register("setup", () =>
        {
            _player = (PlayerMobile)Accounts.GetAccount("combatui")[0];
            ClassCombat.Stop(_player);
            _origin = CombatPractice.FindFloor(Map.Trammel, 1440, 1695);
            _player.MoveToWorld(_origin, Map.Trammel);
            _goal = new Point3D(_origin.X + 7, _origin.Y, _origin.Z);
            Require((TileData.ItemTable[0x21A4].Flags & TileFlag.Impassable) != 0, "Test blocker is not impassable");
            for (int dx = -2; dx <= 2; dx++)
            {
                for (int dy = -2; dy <= 2; dy++)
                {
                    if (Math.Abs(dx) != 2 && Math.Abs(dy) != 2)
                    {
                        continue;
                    }
                    var wall = new Static(0x21A4);
                    wall.MoveToWorld(new Point3D(_goal.X + dx, _goal.Y + dy, _goal.Z), _player.Map);
                    _walls.Add(wall);
                }
            }
        });
        Register("state", () => { });
        Register("moved", () => Require(_player.Location != _origin && _player.Location != _goal,
            "Bounded movement did not approach the enclosed destination"));
        Register("open-setup", () =>
        {
            foreach (var wall in _walls)
            {
                wall.Delete();
            }
            _walls.Clear();
            ClassCombat.Stop(_player);
            _player.MoveToWorld(_origin, _player.Map);
            _goal = new Point3D(_origin.X + 7, _origin.Y, _origin.Z);
        });
        Register("clicked", () => Require(_player.Location == _goal,
            $"Short click did not finish its route: {_player.Location}/{_goal}"));
        Register("hold-mark", () => _stopped = _player.Location);
        Register("held-stop", () => Require(_player.Location == _stopped && _player.Location != _goal,
            $"Held movement continued after release: {_stopped}/{_player.Location}/{_goal}"));
        Register("gear-setup", () =>
        {
            _player.Str = 200;
            _player.MoveToWorld(_origin, _player.Map);
            _lastStep = _origin;
            _steps.Clear();
            var armor = new PlateChest();
            var shield = new HeaterShield();
            _gear.Add(armor);
            _gear.Add(shield);
            Require(_player.EquipItem(armor) && _player.EquipItem(shield)
                && EquipmentBalance.RunPenalty(_player) == 20, "Could not equip the heavy movement fixture");
            _originalMoved = ClassCombatHooks.Moved;
            ClassCombatHooks.Moved = (player, direction) =>
            {
                _originalMoved?.Invoke(player, direction);
                if (player == _player && player.Location != _lastStep)
                {
                    Require((direction & Direction.Running) != 0, "Equipment movement fixture is not running");
                    _lastStep = player.Location;
                    _steps.Add(Core.TickCount);
                }
            };
        });
        Register("gear-check", () =>
        {
            Require(_player.Location == _goal && _steps.Count >= 6, "Heavy equipment prevented a complete client route");
            var intervals = new List<long>();
            for (int i = 1; i < _steps.Count; i++)
            {
                intervals.Add(_steps[i] - _steps[i - 1]);
            }
            intervals.Sort();
            long median = intervals[intervals.Count / 2];
            Require(median is >= 235 and <= 320, $"Client did not adopt the 250 ms heavy running step: {median}");
        });
        Register("final", () =>
        {
            if (_originalMoved != null)
            {
                ClassCombatHooks.Moved = _originalMoved;
                _originalMoved = null;
            }
            foreach (var item in _gear)
            {
                item.Delete();
            }
            _gear.Clear();
            ClassCombat.Stop(_player);
            foreach (var wall in _walls)
            {
                wall.Delete();
            }
            _walls.Clear();
        });
    }

    private static void Require(bool value, string reason)
    {
        if (!value)
        {
            throw new InvalidOperationException(reason);
        }
    }

    private static void Register(string suffix, Action action)
    {
        string command = "movement-ui-" + suffix;
        ConsoleInputHandler.RegisterCommand(command, "Bounded held-movement checks", _ => Core.LoopContext.Post(() =>
        {
            try
            {
                action();
                File.WriteAllText(Environment.GetEnvironmentVariable("SANCTUARY_TEST_UI_STATE"), JsonSerializer.Serialize(new
                {
                    player = new { x = _player.X, y = _player.Y, z = _player.Z },
                    goal = new { x = _goal.X, y = _goal.Y, z = _goal.Z },
                    penalty = EquipmentBalance.RunPenalty(_player), stepTimes = _steps.ToArray()
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
