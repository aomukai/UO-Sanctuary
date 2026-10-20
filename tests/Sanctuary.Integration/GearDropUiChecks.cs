using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Accounting;
using Server.Gumps;
using Server.Items;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class GearDropUiChecks
{
    private static readonly ILogger _logger = LogFactory.GetLogger(typeof(GearDropUiChecks));
    private static PlayerMobile _p;
    private static Skeleton _enemy;
    private static Corpse _corpse;
    private static Item _gear;
    private static GearState _originalState;
    private static Action<BaseCreature, Container> _originalDeath;
    private static IdentifyScroll _scroll;
    private static SmithHammer _hammer;
    private static int _find, _gold;
    private static int? _variantSeed;
    public static void Configure()
    {
        Register("setup", Setup);
        Register("dead", () =>
        {
            Require(_corpse is { Deleted: false, SanctuaryNoRewards: true } && _corpse.Items.Count == 1, "Mouse kill did not generate a single drop");
            _gear = _corpse.Items.Single(); _originalState = ClassGear.State(_gear);
            Require(_originalState is { Identified: false } && _originalState.Roll.Rarity == GearRarity.Magic && _gear.Origin == ItemOrigin.Drop
                && _originalState.Roll.ItemLevel == CreatureProfiles.Level(_enemy), "Native death generated wrong gear");
            IdentificationChecks.Hidden(_gear); ClassLootHooks.CreatureDied = _originalDeath; ClassCombat.Stop(_p);
            Require(ClassLoot.CanOpen(_p, _corpse), "Generated corpse is not lootable");
        });
        Register("opened", () => Require(ClassLoot.IsOpen(_p), "Corpse click did not open loot grid"));
        Register("taken", () => Require(_gear.IsChildOf(_p.Backpack) && ReferenceEquals(ClassGear.State(_gear), _originalState), "Pickup changed/revealed the roll"));
        Register("identify", () =>
        {
            _scroll = new IdentifyScroll(); _p.Backpack.DropItem(_scroll); _scroll.OnDoubleClick(_p);
            Require(_p.Target != null, "Owned scroll did not request a target"); _p.Target.Invoke(_p, _gear);
            Require(_scroll.Deleted && ClassGear.State(_gear) == (_originalState with { Identified = true }), "Identify changed generated stats");
            foreach (var item in _p.Items.Where(i => ClassGear.Slot(i) is GearSlot.Weapon or GearSlot.Shield).ToArray())
            { _p.Backpack.DropItem(item); }
            Require(_p.EquipItem(_gear) && ClassGear.Active(_p, _gear), "Identified drop cannot equip"); ClassGear.Refresh(_p); StatNetwork.Send(_p);
        });
        Register("salvage", () =>
        {
            _p.Backpack.DropItem(_gear); _hammer = new SmithHammer(20); _p.Backpack.DropItem(_hammer);
            _gold = _p.Backpack.GetAmount(typeof(Gold)); _hammer.OnDoubleClick(_p);
        });
        Register("target", () =>
        { Require(_p.Target != null, "Salvage button did not target"); _p.Target.Invoke(_p, _gear); Require(_p.HasGump<EssenceGump>(), "No actual salvage confirmation"); });
        Register("live-final", Final); Register("final", Final);
    }
    private static void Setup()
    {
        _p = (PlayerMobile)Accounts.GetAccount("combatui")[0]; ClassCombat.Stop(_p); _p.Target = null;
        _p.Frozen = _p.Paralyzed = false; ClassCreation.Profiles.AdvanceLevel(_p, 99);
        _p.MoveToWorld(CombatPractice.FindFloor(Map.Felucca, 1382, 1495), Map.Felucca); _p.RawStr = _p.RawDex = 1000;
        var dagger = _p.Backpack.FindItemByType<Dagger>(); Require(dagger != null && _p.EquipItem(dagger), "Starter weapon missing");
        GearDropChecks.FindEquipment(_p); _find = GearDrops.MagicFind(_p); Require(_find > 0, "Real MF gear missing"); StatNetwork.Send(_p);
        _enemy = new Skeleton { Frozen = true, CantWalk = true, Name = "T19 reward skeleton", Body = 50 };
        if (Enum.TryParse<MonsterDifficulty>(Environment.GetEnvironmentVariable("SANCTUARY_TEST_VARIANT_UI"), out var difficulty)
            && difficulty != MonsterDifficulty.Normal)
        {
            MonsterVariants.Assign(_enemy, difficulty);
            _variantSeed = CreatureDifficultyChecks.GearSeed(difficulty, _find,
                difficulty == MonsterDifficulty.Hell ? GearTier.Elite : GearTier.Exceptional);
        }
        _enemy.MoveToWorld(new Point3D(_p.X + 1, _p.Y, _p.Z), _p.Map); _enemy.AIObject.AITimer.Stop(); _enemy.SetHits(1); _enemy.Hits = 1;
        _originalDeath = ClassLootHooks.CreatureDied;
        ClassLootHooks.CreatureDied = (victim, body) =>
        {
            if (victim != _enemy) { _originalDeath(victim, body); return; }
            RewardLife.Process(victim, body, _variantSeed is { } seed ? new Random(seed)
                : new GearDropChecks.PathRoll(121, 0, 0, 0, -1, -1, -1, 0));
            _corpse = (Corpse)body;
        };
    }
    private static void Final() => Require(_gear.Deleted && _scroll.Deleted && _hammer.UsesRemaining == 20
        && _p.Backpack.GetAmount(typeof(Gold)) == _gold && GearDrops.MagicFind(_p) == _find && _corpse.Items.Count == 0,
        "End-to-end kill/loot/identify/equip/salvage/MF state failed");
    private static object Position(IEntity item) => item == null ? null : new { x = item.X, y = item.Y, z = item.Z, serial = item.Serial.Value };
    private static void Require(bool value, string message) => GearDropChecks.Require(value, message);
    private static void Register(string suffix, Action action)
    {
        string name = "gear-drop-ui-" + suffix;
        ConsoleInputHandler.RegisterCommand(name, "Actual-client gear-reward and MF fixture", _ => Core.LoopContext.Post(() =>
        {
            try
            {
                action(); File.WriteAllText(Environment.GetEnvironmentVariable("SANCTUARY_TEST_UI_STATE"), JsonSerializer.Serialize(new
                { player = Position(_p), enemy = Position(_enemy), corpse = Position(_corpse), find = _find, gear = _gear?.Serial.Value }));
                _logger.Information("{Command}: passed", name);
            }
            catch (Exception error) { _logger.Error(error, "{Command}: failed", name); }
        }));
    }
}
