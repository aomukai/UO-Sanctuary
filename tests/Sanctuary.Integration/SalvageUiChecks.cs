using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Server;
using Server.Accounting;
using Server.Gumps;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using Server.Network;

namespace Sanctuary.Integration;

public static class SalvageUiChecks
{
    private static readonly ILogger _logger = LogFactory.GetLogger(typeof(SalvageUiChecks));
    private static PlayerMobile _player;
    private static SmithHammer _hammer;
    private static FletcherTools _wrongTool;
    private static Dagger _gear, _vendor;
    private static Ruby _contents;
    private static EssenceQuote _quote;
    private static ProfessionState _profession;
    private static int _gold;
    private static string _skills;
    public static void Configure()
    {
        Register("setup", Setup);
        Register("target", () =>
        {
            Target(_gear);
            var gump = _player.FindGump<EssenceGump>(); Require(gump != null, "Tool did not open confirmation");
            _quote = (EssenceQuote)typeof(EssenceGump).GetField("_quote", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(gump);
            Require(_quote.Tool == _hammer.Serial && _quote.Gold == 0, "Confirmation lost actual tool or charged gold");
        });
        Register("cancelled", () =>
        {
            Require(!EquipmentEconomy.Commit(_player, _quote, out _) && !_gear.Deleted && !_contents.Deleted, "Cancel quote could be replayed");
            Preserved(); _wrongTool.OnDoubleClick(_player);
        });
        Register("wrong-target", () =>
        {
            Target(_gear); Require(!_player.HasGump<EssenceGump>() && !_gear.Deleted, "Wrong tool offered salvage");
            _hammer.OnDoubleClick(_player);
        });
        Register("vendor-target", () =>
        {
            Target(_vendor); Require(!_player.HasGump<EssenceGump>() && !_vendor.Deleted, "Vendor item offered salvage");
            _hammer.OnDoubleClick(_player);
        });
        Register("remove-tool", () => _hammer.Internalize());
        Register("rejected", () =>
        {
            Require(!_gear.Deleted && !_contents.Deleted, "Missing-tool confirmation destroyed gear/contents"); Preserved();
            _player.Backpack.DropItem(_hammer); _hammer.OnDoubleClick(_player);
        });
        Register("live-final", () => Final(true)); Register("final", () => Final(false));
    }
    private static void Setup()
    {
        _player = (PlayerMobile)Accounts.GetAccount("combatui")[0]; ClassCombat.Stop(_player);
        _player.Frozen = _player.Paralyzed = false; _player.Target = null;
        _player.MoveToWorld(CombatPractice.FindFloor(Map.Trammel, 5555, 1180), Map.Trammel);
        Require(!CraftFacilities.Allows(_player, SkillName.Blacksmith, false), "UI fixture is at a public smithy");
        ClassProfessions.Profiles.Confirm(_player, SkillName.Alchemy); _profession = ClassProfessions.Profiles.Find(_player);
        _player.Skills.Blacksmith.Base = 0;
        _hammer = new SmithHammer(20); _wrongTool = new FletcherTools(20);
        _player.Backpack.DropItem(_hammer); _player.Backpack.DropItem(_wrongTool);
        _gear = SalvageChecks.Gear(_player, new Dagger()); _vendor = SalvageChecks.Gear(_player, new Dagger(), ItemOrigin.Vendor);
        _contents = new Ruby(5); _gear.AddItem(_contents);
        _gold = _player.Backpack.GetAmount(typeof(Gold));
        _skills = Skills(); _hammer.OnDoubleClick(_player);
    }
    private static string Skills() => JsonSerializer.Serialize(Enumerable.Range(0, _player.Skills.Length)
        .Select(i => _player.Skills[i]).Select(s => new { s.BaseFixedPoint, s.CapFixedPoint, s.Lock }));
    private static void Target(Item item)
    {
        Require(_player.Target != null, "Actual tool button did not request a target"); _player.Target.Invoke(_player, item);
    }
    private static void Preserved() => Require(_player.Backpack.GetAmount(typeof(Gold)) == _gold
        && _hammer.UsesRemaining == 20 && Skills() == _skills && ReferenceEquals(ClassProfessions.Profiles.Find(_player), _profession),
        "Client salvage spent gold/uses or changed training/profession");
    private static void Final(bool connected)
    {
        Require(_gear.Deleted && _contents.Deleted && !_vendor.Deleted && !EquipmentEconomy.Commit(_player, _quote, out _),
            "Successful salvage retained contents, affected vendor gear or replayed"); Preserved();
        if (connected) { Require(!_player.HasGump<EssenceGump>(), "Confirmation stayed open after completion"); }
    }
    private static void Require(bool condition, string message)
    { if (!condition) { throw new InvalidOperationException(message); } }
    private static void Register(string suffix, Action action)
    {
        string command = "salvage-ui-" + suffix;
        ConsoleInputHandler.RegisterCommand(command, "Actual-client tool salvage fixture", _ => Core.LoopContext.Post(() =>
        {
            try
            {
                action(); File.WriteAllText(Environment.GetEnvironmentVariable("SANCTUARY_TEST_UI_STATE"),
                    JsonSerializer.Serialize(new { deleted = _gear.Deleted, uses = _hammer.UsesRemaining }));
                _logger.Information("{Command}: passed", command);
            }
            catch (Exception error) { _logger.Error(error, "{Command}: failed", command); }
        }));
    }
}
