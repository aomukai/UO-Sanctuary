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

public static class FortificationUiChecks
{
    private static readonly ILogger _logger = LogFactory.GetLogger(typeof(FortificationUiChecks));
    private static PlayerMobile _p;
    private static MortarPestle _tool;
    private static Chitin _chitin;
    private static FortifyingPowder _powder;
    private static Dagger _dagger;
    private static int _before, _attempts, _maximum;
    public static void Configure()
    {
        Register("setup", () =>
        {
            _p = (PlayerMobile)Accounts.GetAccount("combatui")[0]; ClassCombat.Stop(_p); _p.Target = null;
            foreach (var item in _p.Backpack.Items.ToArray()) { item.Delete(); }
            ClassProfessions.Profiles.Select(_p, SkillName.Alchemy); _p.Skills.Alchemy.Cap = 120; _p.Skills.Alchemy.Base = 120;
            _p.Skills.Alchemy.SetLockNoRelay(SkillLock.Locked);
            _p.MoveToWorld(CombatPractice.FindFloor(Map.Trammel, 5450, 1160), Map.Trammel);
            _tool = new MortarPestle(50); _p.Backpack.DropItem(_tool);
            _chitin = new Chitin(200); _p.Backpack.DropItem(_chitin); _before = 200; _attempts = 0;
            _tool.OnDoubleClick(_p);
        });
        Register("quoted", () => Require(_p.FindGump<FortificationCraftGump>() != null, "New powder entry did not open confirmation"));
        Register("cancelled", () =>
        {
            Require(_tool.UsesRemaining == 50 && _chitin.Amount == 200, "Cancelled recipe spent ingredients"); _tool.OnDoubleClick(_p);
        });
        Register("attempt", () =>
        {
            _attempts++; _powder = _p.Backpack.Items.OfType<FortifyingPowder>().SingleOrDefault();
            Require(_tool.UsesRemaining == 50 - _attempts && _chitin.Amount == _before - (_powder == null ? 5 : 10), "Actual recipe did not charge exact success/failure costs");
            _before = _chitin.Amount;
            if (_powder == null) { _tool.OnDoubleClick(_p); return; }
            Require(_powder.Origin == ItemOrigin.Crafted && _powder.Amount == 1 && _powder.PlayerConstructed, "Actual output lost crafted identity");
            _dagger = new Dagger(); _maximum = _dagger.InitMaxHits - 3; _dagger.MaxHitPoints = _maximum; _dagger.HitPoints = 0;
            _p.Backpack.DropItem(_dagger); _powder.OnDoubleClick(_p);
        });
        Register("target", () =>
        { Require(_p.Target != null, "Powder did not request equipment"); _p.Target.Invoke(_p, _dagger); Require(_p.FindGump<FortificationGump>() != null, "Missing use confirmation"); });
        Register("cancel-use", () =>
        {
            Require(!_powder.Deleted && _dagger.MaxHitPoints == _maximum && _dagger.HitPoints == 0, "Cancelled fortification changed gear");
            _powder.OnDoubleClick(_p); _p.Target.Invoke(_p, _dagger);
        });
        Register("final", () => Require(_powder.Deleted && _dagger.MaxHitPoints == _dagger.InitMaxHits && _dagger.HitPoints == 0,
            "Actual fortification failed final partial dose or repaired current life"));
    }
    private static void Require(bool value, string message) => GearDropChecks.Require(value, message);
    private static void Register(string suffix, Action action)
    {
        string name = "fortification-ui-" + suffix;
        ConsoleInputHandler.RegisterCommand(name, "Actual Alchemy powder/cancel/native craft and fortification confirmation", _ => Core.LoopContext.Post(() =>
        {
            try
            {
                action(); File.WriteAllText(Environment.GetEnvironmentVariable("SANCTUARY_TEST_UI_STATE"), JsonSerializer.Serialize(new
                { powder = _powder?.Serial.Value, current = _dagger?.HitPoints, maximum = _dagger?.MaxHitPoints, attempts = _attempts, uses = _tool?.UsesRemaining }));
                _logger.Information("{Command}: passed", name);
            }
            catch (Exception e) { _logger.Error(e, "{Command}: failed", name); }
        }));
    }
}
