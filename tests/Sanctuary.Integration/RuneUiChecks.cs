using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Accounting;
using Server.Engines.Craft;
using Server.Gumps;
using Server.Items;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class RuneUiChecks
{
    private static readonly ILogger _logger = LogFactory.GetLogger(typeof(RuneUiChecks));
    private static PlayerMobile _player;
    private static ScribesPen _pen;
    private static RuneMould _mould;
    private static Runebark _bark;
    private static InscribedRune _rune;
    private static string _identity;
    private static Func<Mobile, CraftSystem, CraftItem, Type, BaseTool, int, object, bool> _original;
    private static RuneCraftGump _retained;
    public static void Configure()
    {
        Register("setup", Setup);
        Register("target", () =>
        {
            Require(_player.Target != null, "Rune menu did not request mould target");
            _player.Target.Invoke(_player, _mould); Require(_player.HasGump<RuneCraftGump>(), "Rune confirmation absent");
            _retained = _player.FindGump<RuneCraftGump>();
        });
        Register("cancelled", () =>
        { Unspent(); _pen.OnDoubleClick(_player); });
        Register("seed", () =>
        {
            _original = TradeSkillHooks.CompleteCraft;
            // Only the random tickets are fixed; the real client drives target, confirmation and native craft timer.
            TradeSkillHooks.CompleteCraft = (from, system, recipe, material, tool, hue, ticket) =>
            {
                if (from != _player || recipe.ItemType != typeof(InscribedRune)) { return _original(from, system, recipe, material, tool, hue, ticket); }
                TradeSkillHooks.CompleteCraft = _original;
                Require(ticket is CraftTransactions.Request, "Native timer lost rune transaction");
                return CraftTransactions.Complete((CraftTransactions.Request)ticket, new RuneChecks.Rolls(0, 0));
            };
        });
        Register("crafted", () =>
        {
            _rune = _player.Backpack.Items.OfType<InscribedRune>().Single();
            Require(_mould.Deleted && _bark.Amount == 2 && _pen.UsesRemaining == 9 && _rune.Quality == CraftQuality.Superior
                && _rune.Tier == 8 && _rune.StatId == "cast-rate" && _rune.Percentage == 19 && _rune.Origin == ItemOrigin.Crafted,
                "Actual craft output/quality/cost mismatch");
            _identity = RuneChecks.Identity(_rune);
            _retained.OnResponse(_player.NetState, new RelayInfo(1, [], [], [], []));
            Require(_pen.UsesRemaining == 9 && _bark.Amount == 2, "Retained confirmation replayed");
            _player.Backpack.OnDoubleClick(_player); _rune.Location = new Point3D(45, 65, 0);
        });
        Register("final", () => Require(_rune != null && !_rune.Deleted && RuneChecks.Identity(_rune) == _identity
            && ClassGear.State(_rune) == null, "Owned rune changed or applied as equipment"));
    }
    private static void Setup()
    {
        _player = Accounts.GetAccount("combatui")?[0] as PlayerMobile;
        Require(_player?.NetState != null, "Actual player missing"); ClassCombat.Stop(_player);
        foreach (var item in _player.Backpack.Items.ToArray()) { item.Delete(); }
        _player.MoveToWorld(CombatPractice.FindFloor(Map.Trammel, 5450, 1160), Map.Trammel); _player.RawStr = 200;
        ClassProfessions.Profiles.Select(_player, SkillName.Inscribe); _player.Skills.Inscribe.Cap = 120; _player.Skills.Inscribe.Base = 120;
        _player.Skills.Inscribe.SetLockNoRelay(SkillLock.Locked);
        _pen = new ScribesPen { UsesRemaining = 10 }; _player.Backpack.DropItem(_pen);
        _mould = RuneChecks.Mould(_player, 8); _bark = new Runebark(8, 10); _player.Backpack.DropItem(_bark);
        _pen.OnDoubleClick(_player);
    }
    private static void Unspent() => Require(!_mould.Deleted && _bark.Amount == 10 && _pen.UsesRemaining == 10, "Cancel spent inputs");
    private static void Require(bool value, string message) => RuneChecks.Require(value, message);
    private static void Register(string suffix, Action action)
    {
        string name = "rune-ui-" + suffix;
        ConsoleInputHandler.RegisterCommand(name, "Actual rune crafting UI with native timer", _ => Core.LoopContext.Post(() =>
        {
            try
            {
                action(); File.WriteAllText(Environment.GetEnvironmentVariable("SANCTUARY_TEST_UI_STATE"), JsonSerializer.Serialize(new
                { rune = _rune?.Serial.Value, mould = _mould.Serial.Value, bark = _bark.Amount, uses = _pen.UsesRemaining }));
                _logger.Information("{Command}: passed", name);
            }
            catch (Exception error) { _logger.Error(error, "{Command}: failed", name); }
        }));
    }
}
