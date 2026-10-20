using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Server;
using Server.Accounting;
using Server.Engines.Harvest;
using Server.Gumps;
using Server.Items;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class GemSocketUiChecks
{
    private static readonly ILogger _logger = LogFactory.GetLogger(typeof(GemSocketUiChecks));
    private static PlayerMobile _player;
    private static TinkerTools _tool;
    private static SocketGem _gem, _upgraded;
    private static InscribedRune _rune;
    private static Item _host;
    private static GemUpgradeGump _gemGump;
    private static RuneInsertionGump _runeGump;
    private static string _state;
    private sealed class Find : Random
    { private int _calls; public override int Next(int max) => _calls++ switch { 0 => 0, 1 => 3, _ => 0 }; }
    public static void Configure()
    {
        Register("setup", Setup);
        Register("gem-target", () =>
        { Target(_gem); Require(_player.HasGump<GemUpgradeGump>(), "Missing gem confirmation"); _gemGump = _player.FindGump<GemUpgradeGump>(); });
        Register("gem-cancelled", () =>
        { Require(!_gem.Deleted && _gem.Amount == 3 && _tool.UsesRemaining == 20, "Cancel consumed gems/use"); _tool.OnDoubleClick(_player); });
        Register("gem-crafted", () =>
        {
            _upgraded = _player.Backpack.Items.OfType<SocketGem>().Single();
            Require(_gem.Deleted && _upgraded.Family == "Ruby" && _upgraded.Quality == GemQuality.Flawed && _upgraded.Amount == 1
                && _upgraded.Effects.Weapon.Single().Minimum == 5 && _tool.UsesRemaining == 19, "Actual native upgrade output/cost wrong");
            _gemGump.OnResponse(_player.NetState, new RelayInfo(1, [], [], [], [])); Require(_tool.UsesRemaining == 19, "Upgrade confirmation replayed");
        });
        Register("rune-setup", () =>
        {
            ClassProfessions.Profiles.Select(_player, SkillName.Alchemy);
            _player.MoveToWorld(CombatPractice.FindFloor(Map.Trammel, 5430, 1180), Map.Trammel);
            _host = RuneSocketChecks.Host(_player, sockets: 2); _rune = RuneSocketChecks.Rune(_player);
            _tool.Location = new Point3D(100, 50, 0); _upgraded.Location = new Point3D(100, 85, 0);
            _host.Location = new Point3D(100, 120, 0); _rune.Location = new Point3D(45, 65, 0); _player.Backpack.OnDoubleClick(_player);
        });
        Register("rune-ready", () =>
        {
            Require(RuneSockets.Ready(_player), $"Rune owner not ready: affected={ClassProfessions.Affected(_player)}, ready={ClassProfessions.Ready(_player)}, craftAction={_player.CanBeginAction<Server.Engines.Craft.CraftSystem>()}, holding={_player.Holding?.GetType().Name}");
            Require(RuneSockets.Accessible(_player, _rune), "Owned rune inaccessible");
            Require(_player.Target == null, "Previous upgrade retained target: " + _player.Target?.GetType().Name);
        });
        Register("rune-target", () =>
        { Target(_host); Require(_player.HasGump<RuneInsertionGump>(), "Rune did not request host confirmation"); _runeGump = _player.FindGump<RuneInsertionGump>(); });
        Register("rune-cancelled", () =>
        { Require(!_rune.Deleted && ClassGear.State(_host).Runes.Length == 0, "Rune cancel consumed/installed"); _rune.OnDoubleClick(_player); });
        Register("inserted", () =>
        {
            Require(_rune.Deleted && ClassGear.State(_host).Runes.Length == 1 && ClassGear.RequiredLevel(_host) == 30
                && ClassGear.Bonus(_host, "balance1") == 10, "Actual insertion missing content/requirement/value");
            _runeGump.OnResponse(_player.NetState, new RelayInfo(1, [], [], [], [])); Require(ClassGear.State(_host).Runes.Length == 1, "Insertion replayed");
            Require(ClassGear.CanEquip(_host, _player) == true, "Level99 player could not equip"); _player.AddItem(_host);
            Require(HitRecovery.Fhr(_player) == 10, "Equipped host did not contribute FHR");
            _player.Backpack.DropItem(_host); Require(HitRecovery.Fhr(_player) == 0, "Unequipped host still contributed");
            _host.Location = new Point3D(45, 65, 0); _player.Backpack.OnDoubleClick(_player);
            Require(_host.PropertyList.Buffer.AsSpan().IndexOf(Encoding.Unicode.GetBytes("filled: 1")) >= 0, "Host tooltip lost socket count");
            _state = JsonSerializer.Serialize(ClassGear.State(_host));
        });
        Register("final", () => Require(_rune.Deleted && JsonSerializer.Serialize(ClassGear.State(_host)) == _state
            && !_upgraded.Deleted && _tool.UsesRemaining == 19, "Final client state changed"));
    }
    private static void Target(Item item) { Require(_player.Target != null, "Actual client did not request a target"); _player.Target.Invoke(_player, item); }
    private static void Setup()
    {
        _player = Accounts.GetAccount("combatui")?[0] as PlayerMobile; Require(_player?.NetState != null, "Actual class player missing");
        ClassCombat.Stop(_player); foreach (var item in _player.Backpack.Items.ToArray()) { item.Delete(); }
        ClassCreation.Profiles.AdvanceLevel(_player, 99); _player.RawStr = _player.RawDex = 200;
        _player.MoveToWorld(CombatPractice.FindFloor(Map.Trammel, 5450, 1160), Map.Trammel);
        ClassProfessions.Profiles.Select(_player, SkillName.Tinkering); _player.Skills.Tinkering.Cap = 120; _player.Skills.Tinkering.Base = 120;
        _player.Skills.Tinkering.SetLockNoRelay(SkillLock.Locked); _player.Skills.Mining.Cap = 120; _player.Skills.Mining.Base = 120;
        var tool = new Pickaxe(); _player.Backpack.DropItem(tool);
        var def = Mining.System.OreAndStone; var rock = new Static(def.StaticTiles[0]); rock.MoveToWorld(new Point3D(_player.X + 1, _player.Y, _player.Z), _player.Map);
        var original = ClassHarvestHooks.Successful; var skills = Mobile.SkillCheckLocationHandler; int calls = 0;
        try
        {
            Mobile.SkillCheckLocationHandler = (from, skill, min, max) => from == _player && skill == SkillName.Mining || skills?.Invoke(from, skill, min, max) == true;
            ClassHarvestHooks.Successful = (from, definition, trained, resourceType) =>
            { Require(typeof(BaseOre).IsAssignableFrom(resourceType), "Native mining produced a non-ore item"); calls++; RunebarkHarvesting.Successful(from, definition, trained, resourceType, new Find()); };
            Mining.System.FinishHarvesting(_player, tool, def, rock, Mining.System);
        }
        finally { ClassHarvestHooks.Successful = original; Mobile.SkillCheckLocationHandler = skills; rock.Delete(); tool.Delete(); }
        Require(calls == 1 && _player.Backpack.Items.OfType<BaseOre>().Any(), "Actual native ore extraction failed");
        _gem = _player.Backpack.Items.OfType<SocketGem>().Single(); Require(_gem.Amount == 1, "Mining gave multiple gems");
        foreach (var item in _player.Backpack.Items.Where(i => i != _gem).ToArray()) { item.Delete(); }
        var extra = ItemOrigins.Create(ItemOrigin.Drop, () => new SocketGem("Ruby", GemQuality.Chipped, 2));
        Require(_gem.StackWith(_player, extra), "Could not add fixture's two matching upgrade ingredients");
        _tool = new TinkerTools { UsesRemaining = 20 }; _player.Backpack.DropItem(_tool); _tool.OnDoubleClick(_player);
    }
    private static void Require(bool value, string message) => RuneChecks.Require(value, message);
    private static void Register(string suffix, Action action)
    {
        string name = "gem-socket-ui-" + suffix;
        ConsoleInputHandler.RegisterCommand(name, "Native mining, actual gem upgrade and permanent rune installation", _ => Core.LoopContext.Post(() =>
        {
            try
            {
                action(); File.WriteAllText(Environment.GetEnvironmentVariable("SANCTUARY_TEST_UI_STATE"), JsonSerializer.Serialize(new
                { gem = _upgraded?.Serial.Value, rune = _rune?.Serial.Value, host = _host?.Serial.Value, uses = _tool.UsesRemaining }));
                _logger.Information("{Command}: passed", name);
            }
            catch (Exception e) { _logger.Error(e, "{Command}: failed", name); }
        }));
    }
}
