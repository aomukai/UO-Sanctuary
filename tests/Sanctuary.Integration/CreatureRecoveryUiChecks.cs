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

public static class CreatureRecoveryUiChecks
{
    private static readonly ILogger _logger = LogFactory.GetLogger(typeof(CreatureRecoveryUiChecks));
    private static PlayerMobile _player;
    private static GraveyardZombie _enemy;
    private static CombatRandom _random;
    private static readonly List<object> _samples = [];
    private static Point3D _locked;
    private static bool _seen, _invalid, _resumed;
    private static long _started, _until;
    private static int _generation;
    public static void Configure()
    {
        Register("setup", () =>
        {
            _player = (PlayerMobile)Accounts.GetAccount("combatui")[0];
            ClassCombat.Stop(_player);
            _player.MoveToWorld(CombatPractice.FindFloor(Map.Trammel, 5450, 1150), Map.Trammel);
            _player.Mana = _player.ManaMax;
            var dagger = _player.Backpack.FindItemByType<Dagger>();
            Require(ClassCombat.TeethRank(_player) > 0 || dagger != null && _player.EquipItem(dagger),
                "Missing starter weapon for Teeth");
            ClassCombat.For(_player).Right = CombatAbility.Teeth;
            ClassCombat.For(_player).NextAction = Core.TickCount;
            ClassCombat.SendState(_player, ClassCombat.For(_player));
            _enemy = new GraveyardZombie();
            _enemy.SetHits(24);
            _enemy.Home = new Point3D(_player.X + 4, _player.Y, _player.Z);
            _enemy.HomeMap = _player.Map;
            _enemy.RangeHome = 0;
            _enemy.MoveToWorld(_enemy.Home, _player.Map);
            _enemy.AIObject.AITimer.Stop();
            _enemy.NextCombatTime = Core.TickCount + 60000;
            _random = new CombatRandom(0);
            _started = Core.TickCount;
            _until = _started;
            _seen = _invalid = _resumed = false;
            _samples.Clear();
            Sample(++_generation);
        });
        Register("state", () => { });
        Register("locked", () => Require(_seen && CreatureHitRecovery.Remaining(_enemy) > 0
            && _enemy.Hits < 24 && !_invalid, "Real Teeth impact did not visibly stop the zombie"));
        Register("resumed", () => Require(_seen && _resumed && !_invalid,
            "Zombie moved while recovering or failed to resume its approach"));
        Register("final", () =>
        {
            ++_generation;
            ClassCombat.Stop(_player);
            _enemy?.Delete();
            _random?.Dispose();
            _random = null;
        });
    }
    private static void Sample(int generation)
    {
        if (generation != _generation)
        {
            return;
        }
        int remaining = CreatureHitRecovery.Remaining(_enemy);
        if (remaining > 0)
        {
            if (!_seen)
            {
                _seen = true;
                _locked = _enemy.Location;
                _until = Core.TickCount + remaining;
            }
            _invalid |= _enemy.Location != _locked;
        }
        else if (_seen && _enemy.Location != _locked)
        {
            _resumed = true;
        }
        _samples.Add(new { t = Core.TickCount - _started, hp = _enemy.Hits, recovery = remaining,
            x = _enemy.X, y = _enemy.Y, z = _enemy.Z });
        Timer.StartTimer(TimeSpan.FromMilliseconds(20), () => Sample(generation));
    }
    private static void Require(bool value, string message)
    {
        if (!value) { throw new InvalidOperationException(message); }
    }
    private static void Register(string suffix, Action action)
    {
        string command = "creature-recovery-ui-" + suffix;
        ConsoleInputHandler.RegisterCommand(command, "Actual client Teeth/recovery/approach checks", _ => Core.LoopContext.Post(() =>
        {
            try
            {
                action();
                File.WriteAllText(Environment.GetEnvironmentVariable("SANCTUARY_TEST_UI_STATE"), JsonSerializer.Serialize(new
                {
                    samples = _samples.ToArray(), seen = _seen, resumed = _resumed, invalid = _invalid,
                    remaining = CreatureHitRecovery.Remaining(_enemy), until = _until - _started,
                    enemy = new { x = _enemy.X, y = _enemy.Y, z = _enemy.Z },
                    player = new { x = _player.X, y = _player.Y, z = _player.Z }
                }));
                _logger.Information("{Command}: passed", command);
            }
            catch (Exception error) { _logger.Error(error, "{Command}: failed", command); }
        }));
    }
}
