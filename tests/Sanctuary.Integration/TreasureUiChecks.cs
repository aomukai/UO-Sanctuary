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
using Server.Network;
using Server.Targeting;

namespace Sanctuary.Integration;

public static class TreasureUiChecks
{
    private static readonly ILogger _log = LogFactory.GetLogger(typeof(TreasureUiChecks));
    private static PlayerMobile _owner;
    private static TinkerTools _tool;
    private static ValoriteIngot _ingots;
    private static TreasureSkeletonKey _key;
    private static TreasureChart _chart;
    private static ExpeditionSession _hunt;
    private static Point3D _entry;
    private static ExpeditionChest _chest;
    public static void Configure()
    {
        Register("setup", () =>
        {
            _owner = (PlayerMobile)Accounts.GetAccount("combatui")[0]; ClassCombat.Stop(_owner); _owner.Target = null; _owner.Frozen = _owner.Paralyzed = false;
            foreach (var item in _owner.Backpack.Items.ToArray()) { item.Delete(); }
            ClassCreation.Profiles.AdvanceLevel(_owner, 99); _owner.Blessed = true;
            ClassProfessions.Profiles.Select(_owner, SkillName.Tinkering);
            foreach (var skill in new[] { SkillName.Tinkering, SkillName.Cartography })
            { _owner.Skills[skill].Cap = 120; _owner.Skills[skill].Base = 120; _owner.Skills[skill].SetLockNoRelay(SkillLock.Locked); }
            _owner.MoveToWorld(CombatPractice.FindFloor(Map.Trammel, 5450, 1160), Map.Trammel);
            var recipe = new RareRecipeScroll("key/splendid"); _owner.Backpack.DropItem(recipe); recipe.OnDoubleClick(_owner);
            Require(recipe.Deleted, "Actual recipe learning failed");
            _tool = new TinkerTools(50); _ingots = new ValoriteIngot(2); _owner.Backpack.DropItem(_tool); _owner.Backpack.DropItem(_ingots);
            _tool.OnDoubleClick(_owner);
        });
        Register("book", () => Require(_owner.FindGump<TreasureKeyGump>() != null, "Tool key button did not open book"));
        Register("quoted", () => Require(_owner.FindGump<TreasureKeyGump>() != null && _ingots.Amount == 2 && _tool.UsesRemaining == 50, "Key quote spent resources"));
        Register("crafted", () =>
        {
            _key = _owner.Backpack.Items.OfType<TreasureSkeletonKey>().Single();
            Require(_key.Tier == 5 && _ingots.Amount == 1 && _tool.UsesRemaining == 49, "Actual key craft failed costs/guarantee");
            ClassProfessions.Profiles.Select(_owner, SkillName.Cartography); _owner.Backpack.DropItem(new Shovel());
            _owner.MoveToWorld(CombatPractice.FindFloor(Map.Trammel, 5470, 1180), Map.Trammel);
            _chart = new TreasureChart(5, _owner.Map, new(_owner.X + 2, _owner.Y), 173); _owner.Backpack.DropItem(_chart);
            for (int i = 0; i < 100 && _chart.Decoder == null; i++) { _chart.OnDoubleClick(_owner); }
            Require(_chart.Decoder != null && _owner.FindGump<TreasureChartGump>() != null, "Decoded map did not reveal expedition details");
        });
        Register("search", () =>
        {
            Require(_owner.Target != null, "Search button did not open target");
            _owner.Target.Invoke(_owner, new LandTarget(new Point3D(_chart.Site, 0), _owner.Map)); _owner.NetState.SendCancelTarget(); _hunt = TreasureHunts.Profiles.Find(_chart.Serial);
            Require(_hunt != null && _chart.Spent, $"Actual search did not reserve expedition: ready={_chart.Ready(_owner)}, facet={_chart.Facet?.Name}, owner={_owner.Map?.Name}, skill={_owner.Skills.Cartography.Base}, tool={TreasureMap.HasDiggingTool(_owner)}, spent={_chart.Spent}, target={_owner.Target?.GetType().Name}");
        });
        Register("ready", () =>
        {
            Require(_hunt.Active, "Dungeon construction did not finish");
            foreach (var serial in _hunt.Creatures) { if (World.FindMobile(serial) is BaseCreature creature) { creature.Frozen = true; } }
            var portal = _hunt.Items.Select(s => World.FindItem(s)).OfType<ExpeditionPortal>().Single(p => !p.Exit);
            portal.OnDoubleClick(_owner); _entry = _owner.Location;
            Require(_hunt.Contains(_owner), "Actual entrance did not enter dungeon");
        });
        Register("walked", () => { bool physical = _owner.CheckMovement(Direction.East, out int z); Require(_owner.Location != _entry && _hunt.Contains(_owner), $"Client could not walk on generated floor; east={physical}, z={z}, floorHeight={TileData.ItemTable[0x519].CalcHeight}, frozen={_owner.Frozen}, paralyzed={_owner.Paralyzed}, stam={_owner.Stam}, loc={_owner.Location}, entry={_entry}"); });
        Register("chest", () =>
        {
            _chest = _hunt.Items.Select(s => World.FindItem(s)).OfType<ExpeditionChest>().Single();
            foreach (var serial in _hunt.Guardians) { World.FindMobile(serial)?.Kill(); }
            _owner.MoveToWorld(new(_chest.X + 1, _chest.Y, _chest.Z), _chest.Map);
            _key.OnDoubleClick(_owner); Require(_owner.Target != null, "Key did not target chest"); _owner.Target.Invoke(_owner, _chest);
            Require(_key.Deleted && _chest.Unlocked && _chest.Items.OfType<RareRecipeScroll>().Count() == 1, "Actual chest/key/guaranteed recipe failed");
        });
        Register("final", () =>
        {
            Require(_key.Deleted && _chest.Unlocked, "Chest final state changed");
            var exit = _hunt.Items.Select(s => World.FindItem(s)).OfType<ExpeditionPortal>().Single(p => p.Exit);
            _owner.MoveToWorld(new(exit.X + 1, exit.Y, exit.Z), exit.Map); exit.OnDoubleClick(_owner);
            Require(!_hunt.Contains(_owner), "Expedition exit did not return owner");
        });
    }
    private static void Require(bool condition, string why) => GearDropChecks.Require(condition, why);
    private static void Register(string suffix, Action action)
    {
        string name = "treasure-ui-" + suffix;
        ConsoleInputHandler.RegisterCommand(name, "Actual key book/craft, immunity disclosure, search, dungeon movement and chest", _ => Core.LoopContext.Post(() =>
        {
            try
            {
                action(); File.WriteAllText(Environment.GetEnvironmentVariable("SANCTUARY_TEST_UI_STATE"), JsonSerializer.Serialize(new
                { map = _chart?.Serial.Value, ready = _hunt?.Active, roomCount = _hunt?.Plan.Rooms.Length, location = _owner?.Location.ToString(), unlocked = _chest?.Unlocked }));
                _log.Information("{Command}: passed", name);
            }
            catch (Exception error) { _log.Error(error, "{Command}: failed", name); }
        }));
    }
}
