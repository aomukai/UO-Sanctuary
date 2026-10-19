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

public static class SmoothMovementUiChecks
{
    private static readonly ILogger _logger = LogFactory.GetLogger(typeof(SmoothMovementUiChecks));
    private static readonly List<object> _samples = [];
    private static readonly List<Item> _fixtures = [];
    private static PlayerMobile _player;
    private static Point3D _origin, _goal, _stopped;
    private static Action<PlayerMobile, Direction> _originalMoved;
    private static long _started;
    private static BaseDoor _door;
    private static bool _originalThrottle, _tracking;

    public static void Configure()
    {
        Register("angle-a", () => Setup(6, 2));
        Register("angle-b", () => Setup(2, 6));
        Register("angle-c", () => Setup(-6, -2));
        Register("angle-d", () => Setup(-2, -6));
        Register("straight", () => Setup(7, 0));
        Register("state", () => { });
        Register("arrived", () => Require(_player.Location == _goal, $"Route missed goal: {_player.Location}/{_goal}"));
        Register("mark", () => _stopped = _player.Location);
        Register("stopped", () => Require(_player.Location == _stopped, "Movement continued after release"));
        Register("wall", () =>
        {
            Setup(7, 0);
            for (int dy = -2; dy <= 2; dy++)
            {
                Add(0x21A4, 3, dy, 0);
            }
        });
        Register("fence", () =>
        {
            Setup(7, 0);
            Require(TileData.ItemTable[0x824].Impassable, "Fence fixture is not impassable");
            for (int dy = -2; dy <= 2; dy++)
            {
                Add(0x824, 3, dy, 0);
            }
        });
        Register("corner", () =>
        {
            Setup(6, 2);
            for (int dy = -2; dy <= 2; dy++)
            {
                Add(0x21A4, 3, dy, 0);
            }
            for (int dx = 3; dx <= 5; dx++)
            {
                Add(0x21A4, dx, 2, 0);
            }
        });
        Register("door", () =>
        {
            Setup(7, 0);
            for (int dy = -12; dy <= 12; dy++)
            {
                if (dy != 0)
                {
                    Add(0x21A4, 3, dy, 0);
                }
            }
            _door = new MetalDoor(DoorFacing.NorthCW);
            _door.MoveToWorld(new Point3D(_origin.X + 3, _origin.Y, _origin.Z), _player.Map);
            _fixtures.Add(_door);
        });
        Register("door-open", () =>
        {
            _door.Open = true;
            _player.MoveToWorld(_origin, _player.Map);
            _samples.Clear();
            _started = Core.TickCount;
        });
        Register("stairs", () =>
        {
            Setup(7, 0);
            Require(TileData.ItemTable[0x519].Surface, "Stair fixture floor is not a surface");
            for (int dx = 1; dx <= 8; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    Add(0x519, dx, dy, dx * 2);
                }
            }
            _goal = new Point3D(_origin.X + 9, _origin.Y, _origin.Z);
        });
        Register("descend", () => _goal = _origin);
        Register("elevated", () => Require(_player.Z > _origin.Z, "Client did not climb the stair fixture"));
        Register("final", () =>
        {
            ClearFixtures();
            if (_player != null)
            {
                ClassCombat.Stop(_player);
            }
            if (_tracking)
            {
                ClassCombatHooks.Moved = _originalMoved;
                _originalMoved = null;
                _tracking = false;
                ServerFeatureFlags.SpeedhackDetection = _originalThrottle;
            }
        });
    }

    private static void Setup(int dx, int dy)
    {
        ClearFixtures();
        _player = (PlayerMobile)Accounts.GetAccount("combatui")[0];
        ClassCombat.Stop(_player);
        _origin = CombatPractice.FindFloor(Map.Trammel, 5450, 1150);
        _player.Direction = Direction.East;
        _player.MoveToWorld(_origin, Map.Trammel);
        _goal = new Point3D(_origin.X + dx, _origin.Y + dy, _origin.Z);
        _samples.Clear();
        _started = Core.TickCount;
        if (!_tracking)
        {
            Require(_player.AccessLevel == AccessLevel.Player, "Movement fixture must use player validation");
            _originalThrottle = ServerFeatureFlags.SpeedhackDetection;
            ServerFeatureFlags.SpeedhackDetection = true;
            _originalMoved = ClassCombatHooks.Moved;
            _tracking = true;
            ClassCombatHooks.Moved = (player, direction) =>
            {
                _originalMoved?.Invoke(player, direction);
                if (player == _player)
                {
                    _samples.Add(new { t = Core.TickCount - _started, x = player.X, y = player.Y, z = player.Z, dir = (int)direction });
                }
            };
        }
    }

    private static void Add(int id, int dx, int dy, int dz)
    {
        var item = new Static(id);
        item.MoveToWorld(new Point3D(_origin.X + dx, _origin.Y + dy, _origin.Z + dz), _player.Map);
        _fixtures.Add(item);
    }

    private static void ClearFixtures()
    {
        foreach (var item in _fixtures)
        {
            item.Delete();
        }
        _fixtures.Clear();
        _door = null;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void Register(string suffix, Action action)
    {
        string command = "smooth-ui-" + suffix;
        ConsoleInputHandler.RegisterCommand(command, "Isolated movement smoothness checks", _ => Core.LoopContext.Post(() =>
        {
            try
            {
                action();
                File.WriteAllText(Environment.GetEnvironmentVariable("SANCTUARY_TEST_UI_STATE"), JsonSerializer.Serialize(new
                {
                    player = new { x = _player.X, y = _player.Y, z = _player.Z },
                    origin = new { x = _origin.X, y = _origin.Y, z = _origin.Z },
                    goal = new { x = _goal.X, y = _goal.Y, z = _goal.Z }, samples = _samples.ToArray(),
                    elapsed = Core.TickCount - _started,
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
