using System;
using System.IO;
using System.Text.Json;
using Server;
using Server.Accounting;
using Server.Gumps;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class ResurrectionUiChecks
{
    private static readonly ILogger _logger = LogFactory.GetLogger(typeof(ResurrectionUiChecks));
    private static PlayerMobile _player;
    private static GraveyardHealer _healer;
    private static Point3D _death;
    public static void Configure()
    {
        Register("setup", () =>
        {
            _player = (PlayerMobile)Accounts.GetAccount("combatui")[0];
            GraveyardPopulation.Install();
            foreach (var healer in Map.Felucca.GetMobilesInRange<GraveyardHealer>(new Point3D(1380, 1504, 0), 4))
            {
                _healer = healer;
            }
            Require(_healer != null, "No healer in UI fixture");
            _death = CombatPractice.FindFloor(Map.Felucca, _healer.X, _healer.Y - 6);
            foreach (var spawner in Map.Felucca.GetItemsInRange<Server.Engines.Spawners.BaseSpawner>(_death, 30))
            {
                if (spawner.Name?.StartsWith("Sanctuary Graveyard", StringComparison.Ordinal) == true)
                {
                    spawner.Stop();
                    spawner.RemoveSpawns();
                }
            }
            using var testMonsters = Server.Collections.PooledRefList<GraveyardUndead>.Create();
            foreach (var monster in Map.Felucca.GetMobilesInRange<GraveyardUndead>(_death, 30))
            {
                testMonsters.Add(monster);
            }
            foreach (var undead in testMonsters)
            {
                undead.Delete();
            }
            _logger.Information("Resurrection UI removed {Count} remaining undead", testMonsters.Count);
            _player.MoveToWorld(_death, Map.Felucca);
        });
        Register("die", () =>
        {
            var dagger = _player.Backpack.FindItemByType<Server.Items.Dagger>();
            Require(dagger != null && _player.EquipItem(dagger), "Starting dagger was not available before death");
            _player.Kill();
            Require(dagger.Parent == _player.Backpack && dagger.LootType == LootType.Blessed,
                "Actual starting dagger was lost on death");
            Require(!_player.Alive && _player.Location == _death, "Death did not leave a ghost at the original location");
        });
        Register("offered", () => Require(!_player.Alive && _player.Location != _death
            && _player.InRange(_healer, 4) && _player.NetState.GetGumps().Has<ResurrectGump>(),
            $"Ghost could not move to healer and receive an offer: {_player.Location}/{_healer.Location}"));
        Register("recovery-setup", () => _player.MoveToWorld(
            CombatPractice.FindFloor(Map.Felucca, _death.X + 2, _death.Y), Map.Felucca));
        Register("recovered", () => Require(_player.FindItemOnLayer(Layer.OneHanded) is Server.Items.Dagger
            && _player.Backpack.GetAmount(typeof(Server.Items.Gold)) == 800
            && !ClassLoot.HasContents((Server.Items.Corpse)_player.Corpse),
            $"Actual corpse click did not re-equip protected starting gear and reclaim gold: equipped={_player.FindItemOnLayer(Layer.OneHanded)?.GetType().Name}, gold={_player.Backpack.GetAmount(typeof(Server.Items.Gold))}, frozen={_player.Frozen}"));
        Register("final", () => Require(_player.Alive && _player.Hits == _player.HitsMax
            && _player.Corpse is { Deleted: false } && _player.Location != _death,
            "Actual client resurrection did not give full life while retaining the corpse"));
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
        var command = "resurrection-ui-" + suffix;
        ConsoleInputHandler.RegisterCommand(command, "Actual-client resurrection fixture", _ => Core.LoopContext.Post(() =>
        {
            try
            {
                action();
                File.WriteAllText(Environment.GetEnvironmentVariable("SANCTUARY_TEST_UI_STATE"), JsonSerializer.Serialize(new
                {
                    player = new { x = _player.X, y = _player.Y, z = _player.Z },
                    healer = new { x = _healer.X, y = _healer.Y, z = _healer.Z },
                    corpse = new { x = _death.X, y = _death.Y, z = _death.Z }
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
