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

public static class MouldUiChecks
{
    private static readonly ILogger _logger = LogFactory.GetLogger(typeof(MouldUiChecks));
    private static PlayerMobile _player;
    private static Skeleton _enemy;
    private static Corpse _corpse;
    private static RuneMould _mould;
    private static string _identity;
    private static Action<BaseCreature, Container> _original;
    public static void Configure()
    {
        Register("setup", Setup);
        Register("dead", () =>
        {
            ClassCombat.Stop(_player); ClassLootHooks.CreatureDied = _original;
            _mould = _corpse?.Items.OfType<RuneMould>().Single();
            Require(_mould is { StatId: "cast-rate", Difficulty: MonsterDifficulty.Nightmare, Grade: MouldGrade.Ordinary }
                && _mould.Tier == MouldRewards.TierForLevel(CreatureProfiles.Level(_enemy)), "Native kill lost declared mould/tier");
            _identity = MouldChecks.Identity(_mould);
        });
        Register("opened", () => Require(ClassLoot.IsOpen(_player), "Corpse click did not open mould loot grid"));
        Register("taken", () => Require(_mould.IsChildOf(_player.Backpack) && MouldChecks.Identity(_mould) == _identity,
            "Actual pickup lost mould identity"));
        Register("live-final", Final); Register("final", Final);
    }
    private static void Setup()
    {
        _player = (PlayerMobile)Accounts.GetAccount("combatui")[0]; ClassCombat.Stop(_player); _player.Target = null;
        _player.Frozen = _player.Paralyzed = false; ClassCreation.Profiles.AdvanceLevel(_player, 99);
        _player.MoveToWorld(CombatPractice.FindFloor(Map.Felucca, 1382, 1495), Map.Felucca); _player.RawStr = _player.RawDex = 1000;
        var dagger = _player.Backpack.FindItemByType<Dagger>(); Require(dagger != null && _player.EquipItem(dagger), "Starter weapon missing");
        _enemy = new Skeleton { Frozen = true, CantWalk = true, Name = "T21 mould skeleton", Body = 50 };
        MonsterVariants.Assign(_enemy, MonsterDifficulty.Nightmare);
        _enemy.MoveToWorld(new Point3D(_player.X + 1, _player.Y, _player.Z), _player.Map);
        _enemy.AIObject.AITimer.Stop(); _enemy.SetHits(1); _enemy.Hits = 1;
        int ticket = 0;
        foreach (var stat in MouldRewards.Settings.Stats) { if (stat.Id == "cast-rate") { break; } ticket += stat.Weight; }
        _original = ClassLootHooks.CreatureDied;
        ClassLootHooks.CreatureDied = (victim, body) =>
        {
            if (victim != _enemy) { _original(victim, body); return; }
            RewardLife.Process(victim, body, new MouldChecks.Drops(true, ticket)); _corpse = (Corpse)body;
        };
    }
    private static void Final()
    {
        Require(_mould is { Deleted: false, Amount: 1 } && _mould.IsChildOf(_player.Backpack)
            && MouldChecks.Identity(_mould) == _identity, "Mould changed or was consumed before rune crafting");
        var roll = new MouldChecks.Drops(true); RewardLife.Process(_enemy, _corpse, roll);
        Require(roll.ChanceCalls == 0 && !_corpse.Items.OfType<RuneMould>().Any(), "Looted life rewarded again");
    }
    private static object Position(IEntity item) => item == null ? null : new { x = item.X, y = item.Y, z = item.Z, serial = item.Serial.Value };
    private static void Require(bool value, string message) => MouldChecks.Require(value, message);
    private static void Register(string suffix, Action action)
    {
        string name = "mould-ui-" + suffix;
        ConsoleInputHandler.RegisterCommand(name, "Actual client kill and known mould pickup", _ => Core.LoopContext.Post(() =>
        {
            try
            {
                action(); File.WriteAllText(Environment.GetEnvironmentVariable("SANCTUARY_TEST_UI_STATE"), JsonSerializer.Serialize(new
                { player = Position(_player), enemy = Position(_enemy), corpse = Position(_corpse), mould = _mould?.Serial.Value, name = _mould?.Name }));
                _logger.Information("{Command}: passed", name);
            }
            catch (Exception error) { _logger.Error(error, "{Command}: failed", name); }
        }));
    }
}
