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

public static class LootUiChecks
{
    private static readonly ILogger _logger = LogFactory.GetLogger(typeof(LootUiChecks));
    private static PlayerMobile _player;
    private static Chicken _bird;
    private static Skeleton _owner;
    private static Corpse _first, _second;
    private static Item _one, _two;
    public static void Configure()
    {
        Register("setup", () =>
        {
            _player = (PlayerMobile)Accounts.GetAccount("combatui")[0];
            ClassCombat.Stop(_player);
            _player.MoveToWorld(CombatPractice.FindFloor(Map.Felucca, 1382, 1495), Map.Felucca);
            _player.Str = 100;
            var oldBodies = new List<Corpse>();
            foreach (var corpse in _player.Map.GetItemsInRange<Corpse>(_player.Location, 8))
            {
                if (corpse.OwnerWasBaseCreature)
                {
                    oldBodies.Add(corpse);
                }
            }
            foreach (var corpse in oldBodies)
            {
                corpse.Delete();
            }
            if (_player.FindItemOnLayer(Layer.OneHanded) is not Dagger && _player.Backpack.FindItemByType<Dagger>() is Dagger dagger)
            {
                _player.EquipItem(dagger);
            }
            _bird = new Chicken { Frozen = true };
            _bird.MoveToWorld(new Point3D(_player.X + 1, _player.Y + 2, _player.Z), Map.Felucca);
            _bird.Hits = 1;
            _bird.Damage(10, _player);
            _first = (Corpse)_bird.Corpse;
            foreach (var item in _first.Items.ToArray())
            {
                item.Delete();
            }
            _owner = new Skeleton { Frozen = true };
            _owner.MoveToWorld(_player.Location, Map.Felucca);
            _owner.RegisterDamage(10, _player);
            _second = new Corpse(_owner, new List<Item>());
            _second.MoveToWorld(new Point3D(_player.X + 5, _player.Y, _player.Z), Map.Felucca);
            _one = new Dagger { Name = "Nearby Loot Dagger" };
            _two = new Bow { Name = "Five Tile Loot Bow" };
            _first.DropItem(_one);
            _second.DropItem(_two);
            Require(ClassLoot.CanOpen(_player, _first) && ClassLoot.CanOpen(_player, _second), "Fixture corpses are not lootable");
        });
        Register("opened", () => Require(ClassLoot.IsOpen(_player), "Single corpse click did not open AoE loot"));
        Register("first-taken", () => Require((_one.IsChildOf(_player.Backpack) ? 1 : 0) + (_two.IsChildOf(_player.Backpack) ? 1 : 0) == 1,
            "First grid click did not take exactly one item"));
        Register("both-taken", () => Require(_one.IsChildOf(_player.Backpack) && _two.IsChildOf(_player.Backpack)
            && !_first.Deleted && !_second.Deleted && !ClassLoot.HasContents(_first) && !ClassLoot.HasContents(_second),
            "Combined grid did not loot both corpses within five tiles or deleted an unharvested body"));
        Register("blade", () =>
        {
            var blade = _player.FindItemOnLayer(Layer.OneHanded) as Dagger;
            Require(blade != null, "Equipped harvesting blade missing");
            blade.OnDoubleClick(_player);
            Require(_player.Target != null, "Bladed tool did not create its ordinary target cursor");
        });
        Register("harvested", () => Require(_first.SanctuaryHarvested && ClassLoot.HasContents(_first) && !_first.Deleted,
            "Bladed target did not leave harvested materials inside the body"));
        Register("harvest-looted", () => Require(_first.Deleted && !_second.Deleted,
            "Harvested corpse survived final pickup, or an unharvested body disappeared"));
        Register("closed", () => Require(!ClassLoot.IsOpen(_player), "Closing the nearby grid left server loot session open"));
        Register("final", () =>
        {
            Require(_first.Deleted && !_second.Deleted && _one.IsChildOf(_player.Backpack) && _two.IsChildOf(_player.Backpack),
                "Loot state changed after closing");
            _second.SanctuaryHarvested = true;
            _second.DropItem(new Dagger { Name = "HarvestPersistenceItem" });
            _second.Name = "HarvestPersistenceCorpse";
            _owner.Delete();
        });
        Register("reload", () =>
        {
            Corpse saved = null;
            foreach (var corpse in Map.Felucca.GetItemsInRange<Corpse>(new Point3D(1382, 1495, 0), 8))
            {
                if (corpse.Name == "HarvestPersistenceCorpse")
                {
                    saved = corpse;
                }
            }
            Require(saved != null && saved.SanctuaryHarvested && saved.OwnerWasBaseCreature && ClassLoot.HasContents(saved),
                "Reload lost the harvested-corpse flag or remaining loot");
        });
    }
    private static void Register(string command, Action action)
    {
        var name = "loot-ui-" + command;
        ConsoleInputHandler.RegisterCommand(name, "Disposable loot UI assertion", _ => Core.LoopContext.Post(() =>
        {
            try
            {
                action();
                var path = Environment.GetEnvironmentVariable("SANCTUARY_TEST_UI_STATE");
                if (path != null && _player != null)
                {
                    File.WriteAllText(path, JsonSerializer.Serialize(new { player = Position(_player), first = Position(_first), second = Position(_second), firstDeleted = _first.Deleted }));
                }
                _logger.Information("{Command}: passed", name);
            }
            catch (Exception error)
            {
                _logger.Error(error, "{Command}: failed", name);
            }
        }));
    }
    private static object Position(IEntity entity) => new { x = entity.X, y = entity.Y, z = entity.Z, serial = entity.Serial.Value };
    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
