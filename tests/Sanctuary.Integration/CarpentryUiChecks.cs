using System;
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

public static class CarpentryUiChecks
{
    private static readonly ILogger _logger = LogFactory.GetLogger(typeof(CarpentryUiChecks));
    private static PlayerMobile _p;
    private static Saw _tool;
    private static SanctuaryPotionKeg _keg;
    private static RestorationPotion _potion;
    private static int _boards;
    public static void Configure()
    {
        Register("setup", () =>
        {
            _p = (PlayerMobile)Accounts.GetAccount("combatui")[0]; ClassCombat.Stop(_p); _p.Target = null;
            foreach (var i in _p.Backpack.Items.ToArray()) { i.Delete(); }
            ClassCreation.Profiles.AdvanceLevel(_p, 99); _p.RawStr = 1000; TimedEffects.Clear(_p);
            ClassProfessions.Profiles.Select(_p, SkillName.Carpentry); _p.Skills.Carpentry.Cap = 120; _p.Skills.Carpentry.Base = 120; _p.Skills.Carpentry.SetLockNoRelay(SkillLock.Locked);
            _p.MoveToWorld(CombatPractice.FindFloor(Map.Trammel, 5650, 1160), Map.Trammel);
            CraftFacilities.Register(new(SkillName.Carpentry, "Trammel", _p.X + 1, _p.Y, _p.Z, 0xB4A));
            _tool = new Saw(20); _p.Backpack.DropItem(_tool);
            CarpentryChecks.Inputs(_p, CarpentryCrafting.Recipes.First(r => r.Type == "WoodenChair"), 2); _boards = _p.Backpack.GetAmount(typeof(Board)); _tool.OnDoubleClick(_p);
        });
        Register("quoted", () => Require(_p.FindGump<CarpentryGump>() != null, "Carpentry confirmation missing"));
        Register("cancelled", () => { Require(_p.FindGump<CarpentryGump>() == null && _tool.UsesRemaining == 20 && _p.Backpack.GetAmount(typeof(Board)) == _boards, "Cancel spent resources"); _tool.OnDoubleClick(_p); });
        Register("crafted", () =>
        {
            var chair = _p.Backpack.Items.OfType<WoodenChair>().Single(); Require(chair.Origin == ItemOrigin.Crafted && chair.PlayerConstructed && _tool.UsesRemaining == 19 && _p.Backpack.GetAmount(typeof(Board)) == _boards - 13, "Native Carpentry timer/cost/output wrong");
            _keg = new SanctuaryPotionKeg(); _p.Backpack.DropItem(_keg);
            _potion = ItemOrigins.Create(ItemOrigin.Crafted, () => new RestorationPotion(RestorationKind.CombatBuff, 5) { Amount = 3 }); _p.Backpack.DropItem(_potion);
            foreach (var i in _p.Backpack.Items) { i.Location = new Point3D(100, 120, 0); }
            _potion.Location = new Point3D(45, 65, 0); _p.Backpack.OnDoubleClick(_p); _keg.OnDoubleClick(_p);
        });
        Register("poured", () => Require(_potion.Deleted && _keg.Held == 3 && _keg.Kind == RestorationKind.CombatBuff && _keg.Tier == 5 && _p.Backpack.GetAmount(typeof(Bottle)) == 3, "Actual target deposit identity/bottles wrong"));
        Register("withdrawn", () =>
        {
            var potion = _p.Backpack.Items.OfType<RestorationPotion>().Single();
            Require(_keg.Held == 2 && potion.Kind == RestorationKind.CombatBuff && potion.Tier == 5 && potion.Origin == ItemOrigin.Crafted && _p.Backpack.GetAmount(typeof(Bottle)) == 2, "Actual fill bottle identity/count wrong");
            Require(PotionBelts.Cell(_p, 0) == null, "Keg automatically loaded belt");
        });
        Register("final", () => Require(_tool.UsesRemaining == 19 && _keg.Held == 2 && _p.Backpack.GetAmount(typeof(Bottle)) == 2, "Final Carpentry UI state wrong"));
    }
    private static void Require(bool v, string why) => RuneChecks.Require(v, why);
    private static void Register(string suffix, Action action)
    {
        string name = "carpentry-ui-" + suffix;
        ConsoleInputHandler.RegisterCommand(name, "Actual Carpentry book and manual keg UI", _ => Core.LoopContext.Post(() =>
        {
            try { action(); File.WriteAllText(Environment.GetEnvironmentVariable("SANCTUARY_TEST_UI_STATE"), JsonSerializer.Serialize(new { uses = _tool.UsesRemaining, held = _keg?.Held ?? 0 })); _logger.Information("{Command}: passed", name); }
            catch (Exception e) { _logger.Error(e, "{Command}: failed", name); }
        }));
    }
}
