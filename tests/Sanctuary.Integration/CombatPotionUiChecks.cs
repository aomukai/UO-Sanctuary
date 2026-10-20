using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Accounting;
using Server.Items;
using Server.Gumps;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class CombatPotionUiChecks
{
    private static readonly ILogger _logger = LogFactory.GetLogger(typeof(CombatPotionUiChecks));
    private static PlayerMobile _p;
    private static MortarPestle _tool;
    private static RestorationPotion _potion;
    private static Point3D _origin, _goal, _last;
    private static readonly List<long> _steps = [];
    private static Action<PlayerMobile, Direction> _previous;
    public static void Configure()
    {
        Register("setup", () =>
        {
            _p = (PlayerMobile)Accounts.GetAccount("combatui")[0]; ClassCombat.Stop(_p); _p.Target = null;
            foreach (var i in _p.Backpack.Items.ToArray()) { i.Delete(); }
            foreach (var i in _p.Items.ToArray()) { if (i is BaseWeapon or BaseArmor or BaseJewel) { i.Delete(); } }
            TimedEffects.Clear(_p); ClassCreation.Profiles.AdvanceLevel(_p, 99); _p.RawStr = 500;
            ClassProfessions.Profiles.Select(_p, SkillName.Alchemy); _p.Skills.Alchemy.Cap = 120; _p.Skills.Alchemy.Base = 120;
            _p.Skills.Alchemy.SetLockNoRelay(SkillLock.Locked);
            _p.MoveToWorld(CombatPractice.FindFloor(Map.Trammel, 5450, 1160), Map.Trammel);
            _tool = new MortarPestle(20); _p.Backpack.DropItem(_tool); AlchemyChecks.Inputs(_p, AlchemyRestoratives.Recipes[4]); _tool.OnDoubleClick(_p);
        });
        Register("quoted", () => Require(_p.FindGump<AlchemyGump>() != null, "Missing combat brew confirmation"));
        Register("cancelled", () =>
        {
            Require(_tool.UsesRemaining == 20 && _p.Backpack.GetAmount(typeof(Bloodmoss)) == 5 && _p.Backpack.GetAmount(typeof(Bottle)) == 1, "Cancel spent ingredients");
            _tool.OnDoubleClick(_p);
        });
        Register("crafted", () =>
        {
            _potion = _p.Backpack.Items.OfType<RestorationPotion>().Single();
            Require(_potion.Kind == RestorationKind.CombatBuff && _potion.Tier == 1 && _potion.Origin == ItemOrigin.Crafted
                && _tool.UsesRemaining == 19 && !_p.Backpack.Items.OfType<BaseReagent>().Any() && _p.Backpack.GetAmount(typeof(Bottle)) == 0, "Combat brew costs/output wrong");
            Require(PotionBelts.Load(_p, 0, _potion), "Combat potion would not load"); PotionBeltNetwork.Send(_p);
            _origin = CombatPractice.FindFloor(Map.Trammel, 1440, 1695); _goal = new Point3D(_origin.X + 7, _origin.Y, _origin.Z);
            _previous = ClassCombatHooks.Moved;
            ClassCombatHooks.Moved = (p, direction) =>
            {
                _previous?.Invoke(p, direction);
                if (p == _p && p.Location != _last) { _steps.Add(Core.TickCount); _last = p.Location; }
            };
        });
        Register("baseline", Route);
        Register("boosted", () =>
        {
            Require(_potion.Deleted && CombatPotions.RunBonus(_p) == 30, "Client belt key did not consume Second Wind"); Route();
        });
        Register("checked", () => Require(_p.Location == _goal && _steps.Count >= 6, "Actual running route incomplete"));
        Register("expire", () =>
        {
            TimedEffects.Clear(_p); Require(CombatPotions.Apply(_p, 1, TimedEffects.Now.AddSeconds(-29)), "Expiry fixture failed");
        });
        Register("expired", () => { Require(CombatPotions.RunBonus(_p) == 0, "Expired Second Wind active"); Route(); });
        Register("final", () =>
        {
            if (_previous != null) { ClassCombatHooks.Moved = _previous; _previous = null; }
            Require(_tool.UsesRemaining == 19 && CombatPotions.RunBonus(_p) == 0, "Post-client state changed");
        });
    }
    private static void Route() { ClassCombat.Stop(_p); _p.MoveToWorld(_origin, Map.Trammel); _last = _origin; _steps.Clear(); StatNetwork.Send(_p); }
    private static void Require(bool v, string why) => RuneChecks.Require(v, why);
    private static void Register(string suffix, Action action)
    {
        string name = "combat-potion-ui-" + suffix;
        ConsoleInputHandler.RegisterCommand(name, "Actual combat brewing, belt and movement timing", _ => Core.LoopContext.Post(() =>
        {
            try
            {
                action(); File.WriteAllText(Environment.GetEnvironmentVariable("SANCTUARY_TEST_UI_STATE"), JsonSerializer.Serialize(new
                { player = new { x = _p.X, y = _p.Y, z = _p.Z }, goal = new { x = _goal.X, y = _goal.Y, z = _goal.Z }, steps = _steps.ToArray(), bonus = CombatPotions.RunBonus(_p) }));
                _logger.Information("{Command}: passed", name);
            }
            catch (Exception e) { _logger.Error(e, "{Command}: failed", name); }
        }));
    }
}
