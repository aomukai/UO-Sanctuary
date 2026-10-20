using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Gumps;
using Server.Accounting;
using Server.Items;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class AlchemyUiChecks
{
    private static readonly ILogger _logger = LogFactory.GetLogger(typeof(AlchemyUiChecks));
    private static PlayerMobile _p;
    private static MortarPestle _tool;
    private static RestorationPotion _mixture, _drop;
    private static AlchemyGump _confirmation;
    private static TimedBuff _buff;
    private static int _hp, _mana;
    private static long _action, _recovery;
    public static void Configure()
    {
        Register("setup", () =>
        {
            _p = (PlayerMobile)Accounts.GetAccount("combatui")[0]; ClassCombat.Stop(_p); _p.Target = null;
            foreach (var item in _p.Backpack.Items.ToArray()) { item.Delete(); }
            ClassCreation.Profiles.AdvanceLevel(_p, 99); _p.RawStr = 500;
            ClassProfessions.Profiles.Select(_p, SkillName.Alchemy); _p.Skills.Alchemy.Cap = 120; _p.Skills.Alchemy.Base = 120;
            _p.Skills.Alchemy.SetLockNoRelay(SkillLock.Locked);
            _p.MoveToWorld(CombatPractice.FindFloor(Map.Trammel, 5450, 1160), Map.Trammel);
            _tool = new MortarPestle(20); _p.Backpack.DropItem(_tool);
            AlchemyChecks.Inputs(_p, AlchemyRestoratives.Recipes[2]); _tool.OnDoubleClick(_p);
        });
        Register("quoted", () => { _confirmation = _p.FindGump<AlchemyGump>(); Require(_confirmation != null, "No mixture confirmation"); });
        Register("cancelled", () =>
        {
            Require(_tool.UsesRemaining == 20 && _p.Backpack.Items.OfType<RestorationPotion>().Sum(p => p.Amount) == 6
                && _p.Backpack.GetAmount(typeof(Bottle)) == 1, "Cancel consumed ingredients"); _tool.OnDoubleClick(_p);
        });
        Register("crafted", () =>
        {
            _mixture = _p.Backpack.Items.OfType<RestorationPotion>().Single();
            Require(_mixture.Kind == RestorationKind.Rejuvenation && _mixture.Tier == 1 && _mixture.Amount == 1
                && _mixture.Origin == ItemOrigin.Crafted && _tool.UsesRemaining == 19 && _p.Backpack.GetAmount(typeof(Bottle)) == 0,
                "Actual craft output/cost wrong");
            _confirmation.OnResponse(_p.NetState, new Server.Gumps.RelayInfo(1, [], [], [], []));
            Require(_tool.UsesRemaining == 19, "Confirmation replayed");
            TimedEffects.ApplyBuff(_p, TimedEffectCategory.Potion, "protection", 10); _buff = TimedEffects.Buff(_p, TimedEffectCategory.Potion);
            _p.Hits = _hp = 1; _p.Mana = _mana = 0;
            Require(PotionBelts.Load(_p, 0, _mixture), "Could not load crafted mixture"); PotionBeltNetwork.Send(_p);
            var session = ClassCombat.For(_p); session.NextAction = _action = Core.TickCount + 5000; session.RecoveryUntil = _recovery = Core.TickCount + 2000;
        });
        Register("drunk", () =>
        {
            Require(_mixture.Deleted && _p.Hits >= _hp + _p.HitsMax * 35 / 100 && _p.Mana >= _mana + _p.ManaMax * 35 / 100
                && TimedEffects.Buff(_p, TimedEffectCategory.Potion) == _buff && ClassCombat.For(_p).NextAction == _action
                && ClassCombat.For(_p).RecoveryUntil == _recovery, "Belt mixture changed instant amount/buff/action/recovery");
            _drop = AlchemyRestoratives.Drop("hp5"); _p.Backpack.DropItem(_drop); _p.Hits = 1;
            Require(PotionBelts.Load(_p, 0, _drop), "Could not load dropped Super potion"); PotionBeltNetwork.Send(_p);
        });
        Register("final", () => Require(_drop.Deleted && TimedEffects.Snapshot().Any(s => s.Owner == _p.Serial.Value && s.Life.Duration > 0)
            && _tool.UsesRemaining == 19, "Dropped Super did not start timed restoration"));
    }
    private static void Require(bool value, string why) => RuneChecks.Require(value, why);
    private static void Register(string suffix, Action action)
    {
        string name = "alchemy-ui-" + suffix;
        ConsoleInputHandler.RegisterCommand(name, "Actual mortar menus, cancel/brew and crafted/dropped belt potions", _ => Core.LoopContext.Post(() =>
        {
            try
            {
                action(); File.WriteAllText(Environment.GetEnvironmentVariable("SANCTUARY_TEST_UI_STATE"), JsonSerializer.Serialize(new
                { uses = _tool?.UsesRemaining, hits = _p?.Hits, mana = _p?.Mana, mixture = _mixture?.Serial.Value }));
                _logger.Information("{Command}: passed", name);
            }
            catch (Exception e) { _logger.Error(e, "{Command}: failed", name); }
        }));
    }
}
