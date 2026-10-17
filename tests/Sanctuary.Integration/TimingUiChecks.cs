using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Accounting;
using Server.Items;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class TimingUiChecks
{
    private static readonly ILogger _logger = LogFactory.GetLogger(typeof(TimingUiChecks));
    private static PlayerMobile _player;
    private static Skeleton _enemy;
    private static Dagger _dagger;
    private static Halberd _halberd;
    private static int _hp;
    private static Item[] _speedGear = [];
    private static CombatRandom _random;
    public static void Configure()
    {
        Register("setup", () =>
        {
            _random = new CombatRandom();
            _player = (PlayerMobile)Accounts.GetAccount("combatui")[0];
            ClassCombat.Stop(_player);
            ClassCreation.Profiles.AdvanceLevel(_player, 99);
            _player.Str = _player.Dex = _player.Int = 100;
            _player.Mana = 100;
            _player.MoveToWorld(CombatPractice.FindFloor(Map.Felucca, 1382, 1495), Map.Felucca);
            _enemy = new Skeleton { Frozen = true, CantWalk = true, Body = 50, Name = "Timing Target" };
            _enemy.SetHits(1000);
            _enemy.Hits = 1000;
            _enemy.MoveToWorld(new Point3D(_player.X + 1, _player.Y - 1, _player.Z), Map.Felucca);
            _dagger = _player.FindItemOnLayer(Layer.OneHanded) as Dagger ?? _player.Backpack.FindItemByType<Dagger>();
            _halberd = new Halberd();
            Equip(false, 0, 0);
        });
        Register("dagger", () => Equip(false, 0, 0));
        Register("halberd", () => Equip(true, 0, 0));
        Register("ias", () => Equip(false, 20, 0));
        Register("fcr", () => Equip(false, 0, 20));
        Register("mark", () => _hp = _enemy.Hits);
        Register("windup", () => Require(_enemy.Hits == _hp && ClassCombat.For(_player).PendingAbility != CombatAbility.None,
            "Actual client attack caused damage immediately or failed to begin windup"));
        Register("dagger-hits", () => Hits(CombatTestActions.HitDamage(_player, _enemy) * 4));
        Register("halberd-hits", () => Hits(CombatTestActions.HitDamage(_player, _enemy) * 2));
        Register("ias-hits", () => Hits(CombatTestActions.HitDamage(_player, _enemy) * 5));
        Register("fcr-hits", () => Hits(24));
        Register("stop", () => ClassCombat.Stop(_player));
        Register("finish", () =>
        {
            ClassCombat.Stop(_player);
            Equip(false, 0, 0);
            ClassCombat.For(_player).Right = CombatAbility.Teeth;
            _enemy.Delete();
            _halberd.Delete();
            _random.Dispose();
        });
    }
    private static void Equip(bool heavy, int ias, int fcr)
    {
        ClassCombat.Stop(_player);
        if (_player.FindItemOnLayer(Layer.OneHanded) is BaseWeapon one)
        {
            _player.Backpack.DropItem(one);
        }
        if (_player.FindItemOnLayer(Layer.TwoHanded) is BaseWeapon two)
        {
            _player.Backpack.DropItem(two);
        }
        // Lock the timing fixture's victim through the right binding; cursor-following has separate checks.
        ClassCombat.For(_player).Right = fcr > 0 ? CombatAbility.Teeth : CombatAbility.BasicAttack;
        foreach (var item in _speedGear)
        {
            item.Delete();
        }
        _speedGear = ias > 0 ? [new LeatherGloves()] : fcr > 0 ? [new GoldRing(), new GoldNecklace()] : [];
        foreach (var item in _speedGear)
        {
            string code = ias > 0 ? "swing2" : "cast1";
            bool found = false;
            for (int seed = 0; seed < 2000 && !found; seed++)
            {
                var state = ClassGear.Profiles.Convert(item, 99, GearRarity.Magic, new Random(seed));
                found = state.Roll.Affixes.Any(a => a.Modifiers.Any(m => m.Code == code));
            }
            Require(found && _player.EquipItem(item), "Legal timing gear could not be equipped");
        }
        ClassGear.Refresh(_player);
        Require(_player.EquipItem(heavy ? _halberd : _dagger), "Timing fixture weapon could not be equipped");
    }
    private static void Hits(int amount)
    {
        Require(_enemy.Hits >= _hp - amount && _enemy.Hits <= _hp - amount + 2,
            $"Actual timing hit count was wrong: expected damage={amount}, HP={_hp}/{_enemy.Hits}, cycle={ClassCombat.For(_player).Timing.DurationMs}, pending={ClassCombat.For(_player).PendingAbility}, cursor={ClassCombat.For(_player).CastTarget}, enemy={_enemy.Serial}, player={_player.Location}, target={_enemy.Location}");
    }
    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
    private static object Position(Mobile m) => new { x = m.X, y = m.Y, z = m.Z, serial = m.Serial.Value };
    private static void Register(string command, Action action)
    {
        var name = "timing-ui-" + command;
        ConsoleInputHandler.RegisterCommand(name, "Disposable action timing UI assertion", _ => Core.LoopContext.Post(() =>
        {
            try
            {
                action();
                var path = Environment.GetEnvironmentVariable("SANCTUARY_TEST_UI_STATE");
                if (path != null)
                {
                    File.WriteAllText(path, JsonSerializer.Serialize(new { player = Position(_player), enemy = Position(_enemy) }));
                }
                _logger.Information("{Command}: passed", name);
            }
            catch (Exception error)
            {
                _logger.Error(error, "{Command}: failed", name);
            }
        }));
    }
}
