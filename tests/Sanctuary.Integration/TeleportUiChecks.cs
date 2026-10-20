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

public static class TeleportUiChecks
{
    private static readonly ILogger _logger = LogFactory.GetLogger(typeof(TeleportUiChecks));
    private static PlayerMobile _p;
    private static TeleportScroll _pack, _belt;
    private static Point3D _origin;
    private static int _mana;
    public static void Configure()
    {
        Register("setup", () =>
        {
            _p = (PlayerMobile)Accounts.GetAccount("combatui")[0]; ClassCombat.Stop(_p); _p.Target = null;
            foreach (var i in _p.Backpack.Items.ToArray()) { i.Delete(); }
            ClassCreation.Profiles.AdvanceLevel(_p, 99); _p.RawStr = 1000; TimedEffects.Clear(_p);
            Require(ClassCreation.Profiles.TryTrain(_p.Serial, "sorceress.telekinesis", out _) && ClassCreation.Profiles.TryTrain(_p.Serial, "sorceress.teleport", out _), "Sorcerer talent training failed");
            Require(SkillBindings.Instance.Assign(_p, false, CombatAbility.Teleport), "Teleport binding failed");
            _p.MoveToWorld(CombatPractice.FindFloor(Map.Trammel, 1382, 1495), Map.Trammel); _p.Mana = _p.ManaMax;
            _pack = new TeleportScroll(); _belt = new TeleportScroll(); _p.Backpack.DropItem(_pack); _p.Backpack.DropItem(_belt);
            _pack.Location = new Point3D(45, 65, 0); _belt.Location = new Point3D(85, 65, 0);
            _origin = _p.Location; _mana = _p.Mana; _p.Backpack.OnDoubleClick(_p);
        });
        Register("target", () => Require(_p.Target != null && !_pack.Deleted && _p.Location == _origin, $"Inventory right-click did not open target target={_p.Target} deleted={_pack.Deleted} location={_p.Location} origin={_origin} next={_p.NextActionTime-Core.TickCount}"));
        Register("cancelled", () => { Require(_p.Target == null && !_pack.Deleted && _p.Location == _origin, "Target cancel consumed/moved"); _p.Backpack.OnDoubleClick(_p); });
        Register("cast", () => { Require(_pack.Deleted && _p.Target == null && _p.Location != _origin && _p.Mana == _mana, "Inventory teleport effect/cost wrong"); _origin = _p.Location; });
        Register("loaded", () => Require(PotionBelts.Entry(_p, 0) == _belt, "Belt target did not load scroll"));
        Register("invalid", () => Require(PotionBelts.Entry(_p, 0) == _belt && !_belt.Deleted && _p.Target == null && _p.Location == _origin, "UI cursor consumed belt scroll"));
        Register("belt", () => { Require(_belt.Deleted && PotionBelts.Entry(_p, 0) == null && _p.Target == null && _p.Location != _origin && _p.Mana == _mana, "Belt world-cursor teleport failed"); _origin = _p.Location; });
        Register("final", () => Require(_p.Location != _origin && _p.Mana <= _mana - 23 && _p.Mana >= _mana - 24 && _p.Target == null, "Learned right-click teleport cost/aim wrong"));
    }
    private static void Require(bool v, string why) => RuneChecks.Require(v, why);
    private static void Register(string suffix, Action action)
    {
        string name = "teleport-ui-" + suffix;
        ConsoleInputHandler.RegisterCommand(name, "Actual scroll right-click, cancel, belt cursor and learned Teleport", _ => Core.LoopContext.Post(() =>
        { try { action(); File.WriteAllText(Environment.GetEnvironmentVariable("SANCTUARY_TEST_UI_STATE"), JsonSerializer.Serialize(new { location = _p.Location.ToString(), mana = _p.Mana })); _logger.Information("{Command}: passed", name); } catch (Exception e) { _logger.Error(e, "{Command}: failed", name); } }));
    }
}
