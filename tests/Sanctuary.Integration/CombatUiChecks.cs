using System;
using System.IO;
using System.Text.Json;
using Server;
using Server.Accounting;
using Server.Items;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class CombatUiChecks
{
    private static readonly ILogger _logger = LogFactory.GetLogger(typeof(CombatUiChecks));
    private static PlayerMobile _player;
    private static Skeleton _enemy, _second;
    private static Blacksmith _friendly;
    private static Dagger _dagger;
    private static CombatRandom _random;
    private static int _hp, _otherHp, _flags, _releaseAllowance;
    private static long _action;
    private static Point3D _position, _castStopped;
    private static int _transitionMana;
    public static void Configure()
    {
        Register("setup", () => { _random = new CombatRandom(); Setup(); });
        Register("state", () => { });
        Register("run", () => Require(ClassCombat.For(_player).Running && !_player.Warmode, "Run toggle failed or activated war mode"));
        Register("walk", () => Require(!ClassCombat.For(_player).Running && !_player.Warmode, "Walk toggle failed or activated war mode"));
        Register("walk-step", () =>
        {
            Require(!ClassCombat.For(_player).Running && (_player.Direction & Direction.Running) == 0
                && _player.Location != _position && !ClassMovement.IsRunning(_player, Core.TickCount),
                "Walk mode sent running steps or did not move");
            _position = _player.Location;
        });
        Register("run-step", () =>
        {
            Require(ClassCombat.For(_player).Running && (_player.Direction & Direction.Running) != 0
                && _player.Location != _position, "Run mode did not change steps during an active path");
        });
        Register("overlap-setup", () =>
        {
            ClassCombat.Stop(_player);
            var point = CombatPractice.FindFloor(Map.Felucca, 1382, 1495);
            _player.MoveToWorld(point, Map.Felucca);
            _enemy.MoveToWorld(new Point3D(point.X + 4, point.Y, point.Z), Map.Felucca);
            _second.MoveToWorld(new Point3D(point.X + 4, point.Y, point.Z + 1), Map.Felucca);
            _enemy.Combatant = _second.Combatant = null;
            _enemy.FocusMob = _second.FocusMob = null;
            _enemy.Direction = Direction.East;
            _second.Direction = Direction.West;
        });
        Register("overlap-none", () => { _enemy.Hidden = _second.Hidden = true; });
        Register("overlap-back", () => { _enemy.Hidden = false; _second.Hidden = true; });
        Register("overlap-front", () => { _enemy.Hidden = true; _second.Hidden = false; });
        Register("overlap-both", () => { _enemy.Hidden = _second.Hidden = false; });
        Register("overlap-pick-back", () =>
        {
            Require(ClassCombat.For(_player).HeldAttack && ClassCombat.For(_player).CastTarget == _enemy.Serial, $"Rear visible pixel was lost: selected={ClassCombat.For(_player).Target}, rear={_enemy.Serial}, front={_second.Serial}");
            ClassCombat.Stop(_player);
        });
        Register("overlap-pick-front", () =>
        {
            Require(ClassCombat.For(_player).HeldAttack && ClassCombat.For(_player).CastTarget == _second.Serial, $"Overlap selection did not prefer front: target={ClassCombat.For(_player).Target}, rear={_enemy.Serial}, front={_second.Serial}, player={_player.Location}");
            ClassCombat.Stop(_player);
        });
        Register("overlap-held-front", () => Require(ClassCombat.For(_player).CastTarget == _second.Serial,
            "Right-click on a shared gap did not lock the front fallback creature"));
        Register("overlap-reset", () =>
        {
            ClassCombat.Stop(_player);
            var point = _player.Location;
            _enemy.MoveToWorld(new Point3D(point.X + 4, point.Y, point.Z), Map.Felucca);
            _second.MoveToWorld(new Point3D(point.X + 8, point.Y, point.Z), Map.Felucca);
        });
        Register("stationary-setup", () =>
        {
            ClassCombat.Stop(_player);
            _position = _player.Location;
            _hp = _enemy.Hits;
            _otherHp = _second.Hits;
            _action = Core.TickCount;
        });
        Register("stationary-empty", () =>
        {
            var session = ClassCombat.For(_player);
            Require(session.HeldAttack && session.Target == Serial.Zero && session.CastTarget == Serial.Zero
                && session.NextAction >= _action + 3 * CombatTiming.For(_player, CombatAbility.BasicAttack).DurationMs
                && session.NextAction < _action + 6 * CombatTiming.For(_player, CombatAbility.BasicAttack).DurationMs,
                $"Empty held swing did not run at melee cadence: {_action}/{session.NextAction}");
            Require(_player.Location == _position && _enemy.Hits >= _hp && _second.Hits >= _otherHp,
                "Empty swing moved or hit an unselected creature");
        });
        Register("stationary-far", () =>
        {
            var session = ClassCombat.For(_player);
            Require(session.HeldAttack && session.CastTarget == _enemy.Serial && session.Target == Serial.Zero
                && _player.Location == _position && _enemy.Hits >= _hp, "Distant force attack chased or hit");
        });
        Register("stationary-stopped", () =>
        {
            var session = ClassCombat.For(_player);
            Require(session.HeldAbility == CombatAbility.None && session.Target == Serial.Zero
                && _player.Location == _position, "Shift release resumed a chase or a ground path");
        });
        Register("stationary-near", () =>
        {
            ClassCombat.Stop(_player);
            _enemy.MoveToWorld(new Point3D(_player.X + 1, _player.Y, _player.Z), Map.Felucca);
            _hp = _enemy.Hits;
        });
        Register("stationary-hit", () =>
        {
            Require(ClassCombat.For(_player).HeldAttack && _enemy.Hits <= _hp - (4 * CombatTestActions.HitDamage(_player, _enemy)) + 2 && _enemy.Hits >= _hp - (4 * CombatTestActions.HitDamage(_player, _enemy))
                && _player.Location == _position && _second.Hits >= _otherHp,
                $"Force melee did not hit selected creature four times in place: {_hp}/{_enemy.Hits}");
        });
        Register("stationary-miss-mark", () => { _hp = _enemy.Hits; _action = ClassCombat.For(_player).NextAction; });
        Register("stationary-miss", () =>
        {
            var session = ClassCombat.For(_player);
            Require(session.HeldAttack && session.CastTarget == Serial.Zero && session.NextAction > _action
                && _enemy.Hits >= _hp && _player.Location == _position,
                "Moving the cursor away retained a melee victim or stopped empty swings");
        });
        Register("stationary-reset", () =>
        {
            Require(ClassCombat.For(_player).HeldAbility == CombatAbility.None && _enemy.Hits >= _hp
                && _player.Location == _position, "Mouse release retained stationary attacks");
            _enemy.Hits = _second.Hits = 120;
            _enemy.MoveToWorld(new Point3D(_player.X + 4, _player.Y, _player.Z), Map.Felucca);
            _second.MoveToWorld(new Point3D(_player.X + 8, _player.Y, _player.Z), Map.Felucca);
        });
        Register("approach", () =>
        {
            Require(_player.InRange(_enemy, 1) && _enemy.Hits <= 120 - 6 * CombatTestActions.HitDamage(_player, _enemy), $"Approach/repeated melee failed: location={_player.Location}, target={ClassCombat.For(_player).Target}, enemy={_enemy.Location}, hits={_enemy.Hits}, LOS={_player.InLOS(_enemy)}");
            Require(!_player.Warmode && _player.Combatant == null, "War/legacy swing loop was enabled");
            _position = _player.Location;
            _flags |= 1;
        });
        Register("stop", () =>
        {
            Require(ClassCombat.For(_player).Target == Serial.Zero, "Ground did not stop combat");
            _hp = _enemy.Hits;
        });
        Register("stopped", () =>
        {
            Require(_enemy.Hits >= _hp && _player.Location != _position, $"Ground move kept attacking or did not move: HP={_hp}/{_enemy.Hits}, position={_position}/{_player.Location}");
            _flags |= 2;
        });
        Register("shift-mark", () => _position = _player.Location);
        Register("shift", () =>
        {
            Require(_player.Location == _position, "Shift allowed movement");
            _flags |= 4;
        });
        Register("movement-mark", () => _position = _player.Location);
        Register("movement-held", () =>
        {
            Require(Math.Max(Math.Abs(_player.X - _position.X), Math.Abs(_player.Y - _position.Y)) > 3,
                $"Held ground movement stopped at its first destination: {_position}/{_player.Location}");
            _position = _player.Location;
        });
        Register("movement-resumed", () =>
        {
            Require(_player.Location != _position, "Held ground movement did not resume after Shift");
        });
        Register("cast-transition-mark", () => _transitionMana = _player.Mana);
        Register("cast-transition-still", () => _castStopped = _player.Location);
        Register("cast-transition", () => Require(_player.Location == _castStopped
            && _player.Mana < _transitionMana && ClassCombat.For(_player).HeldAbility == CombatAbility.Teeth,
            "Right-held cast failed to stop a left-held route or did not cast"));
        Register("movement-reset", () =>
        {
            _player.MoveToWorld(CombatPractice.FindFloor(Map.Felucca, 1382, 1495), Map.Felucca);
        });
        Register("held-target", () => Require(ClassCombat.For(_player).CastTarget == _enemy.Serial,
            "Transparent body click did not lock the creature"));
        Register("hold-mark", () =>
        {
            _enemy.Hits = _enemy.HitsMax;
            _hp = _enemy.Hits;
        });
        Register("held", () =>
        {
            Require(_enemy.Hits >= _hp - 30 && _enemy.Hits <= _hp - 22,
                $"Held Teeth did not fire at its cadence: {_hp}/{_enemy.Hits}");
            Require(ClassCombat.For(_player).HeldAbility == CombatAbility.None, "Right release did not stop casting");
            _releaseAllowance = ClassCombat.For(_player).PendingAbility == CombatAbility.Teeth ? 6 : 0;
            _hp = _enemy.Hits;
        });
        Register("released", () => Require(_enemy.Hits >= _hp - _releaseAllowance
            && ClassCombat.For(_player).HeldAbility == CombatAbility.None
            && ClassCombat.For(_player).PendingAbility == CombatAbility.None,
            "Released right button started another cast after its committed windup"));
        Register("far-mark", () => _action = ClassCombat.For(_player).NextAction);
        Register("far", () => Require(ClassCombat.For(_player).NextAction - _action > 0,
            "Far screen aim did not fire"));
        Register("teeth-mark", () => _hp = _enemy.Hits);
        Register("teeth", () =>
        {
            Require(ClassCombat.TeethRank(_player) == 1 && _enemy.Hits < _hp && _enemy.Hits >= _hp - 6, $"Right-click Teeth failed: rank={ClassCombat.TeethRank(_player)}, hp={_hp}/{_enemy.Hits}, mana={_player.Mana}, seq={ClassCombat.For(_player).Sequence}");
            _flags |= 8;
        });
        Register("unequip", () =>
        {
            _player.AddToBackpack(_dagger);
            Require(ClassCombat.TeethRank(_player) == 0, "Weapon grant persisted unequipped");
            _hp = _enemy.Hits;
        });
        Register("unavailable", () =>
        {
            Require(_enemy.Hits >= _hp, "Unequipped Teeth still fired");
            _player.EquipItem(_dagger);
            _flags |= 16;
        });
        Register("death-setup", () =>
        {
            ClassCombat.Stop(_player);
            _enemy.Hits = 1;
            _player.MoveToWorld(CombatPractice.FindFloor(_enemy.Map, _enemy.X - 2, _enemy.Y), _enemy.Map);
            _otherHp = _second.Hits;
        });
        Register("death", () =>
        {
            Require(_enemy.Deleted && ClassCombat.For(_player).Target == Serial.Zero && _second.Hits == _otherHp,
                "Death did not stop, or another target was acquired");
            _flags |= 32;
        });
        Register("friendly", () =>
        {
            Require(ClassCombat.For(_player).Target == Serial.Zero && _friendly.Hits == _friendly.HitsMax,
                "Friendly interaction attacked the blacksmith");
            _flags |= 64;
        });
        Register("final", () =>
        {
            Require(_flags == 127, "Missing combat UI phases");
            Require(_player.Skills.Focus.BaseFixedPoint == 0 && _player.Skills.Meditation.BaseFixedPoint == 0,
                "Actual combat/movement leaked Focus or Meditation gains");
            _random?.Dispose();
            _random = null;
        });
    }
    private static void Register(string command, Action action)
    {
        var name = "combat-ui-" + command;
        ConsoleInputHandler.RegisterCommand(name, "Isolated combat UI assertion", _ => Core.LoopContext.Post(() =>
        {
            try
            {
                action();
                Save();
                _logger.Information("{Command}: passed", name);
            }
            catch (Exception error)
            {
                _logger.Error(error, "{Command}: failed", name);
            }
        }));
    }
    private static void Require(bool value, string message)
    {
        if (!value)
        {
            throw new InvalidOperationException(message);
        }
    }
    private static void Setup()
    {
        _player = Accounts.GetAccount("combatui")?[0] as PlayerMobile;
        Require(_player != null && ClassCombat.TeethRank(_player) == 0, "Missing fresh Necromancer");
        var point = CombatPractice.FindFloor(Map.Felucca, 1382, 1495);
        _player.MoveToWorld(point, Map.Felucca);
        _dagger = _player.Backpack.FindItemByType<Dagger>();
        Require(_dagger != null && _player.EquipItem(_dagger), "Starter dagger missing");
        _enemy = new Skeleton { Frozen = true, CantWalk = true, Name = "UI Practice One", Body = 50 };
        _second = new Skeleton { Frozen = true, CantWalk = true, Name = "UI Practice Two", Body = 50 };
        _friendly = new Blacksmith { Frozen = true, CantWalk = true, Name = "UI Friendly Smith" };
        _enemy.SetHits(120);
        _second.SetHits(120);
        _enemy.Hits = _second.Hits = 120;
        _enemy.MoveToWorld(CombatPractice.FindFloor(Map.Felucca, point.X + 4, point.Y), Map.Felucca);
        _second.MoveToWorld(CombatPractice.FindFloor(Map.Felucca, point.X + 8, point.Y), Map.Felucca);
        _friendly.MoveToWorld(CombatPractice.FindFloor(Map.Felucca, point.X - 2, point.Y + 2), Map.Felucca);
        _enemy.AIObject.AITimer.Stop();
        _second.AIObject.AITimer.Stop();
        _friendly.AIObject.AITimer.Stop();
        Require(ClassCombat.IsHostile(_player, _enemy) && !ClassCombat.IsHostile(_player, _friendly), "Target classification failed");
    }
    private static object Position(Mobile mobile) => new { x = mobile.X, y = mobile.Y, z = mobile.Z, serial = mobile.Serial.Value };
    private static void Save()
    {
        var path = Environment.GetEnvironmentVariable("SANCTUARY_TEST_UI_STATE");
        if (path != null && _player != null)
        {
            File.WriteAllText(path, JsonSerializer.Serialize(new
            {
                player = Position(_player), enemy = Position(_enemy), second = Position(_second), friendly = Position(_friendly)
            }));
        }
    }
}
