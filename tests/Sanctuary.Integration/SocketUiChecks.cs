using System;
using System.IO;
using System.Text.Json;
using Server;
using Server.Accounting;
using Server.Gumps;
using Server.Items;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class SocketUiChecks
{
    private static readonly ILogger _logger = LogFactory.GetLogger(typeof(SocketUiChecks));
    private static PlayerMobile _player;
    private static TinkerTools _tool;
    private static Longsword _item;
    private static SocketRequest _retry;
    private static GearState _before;
    private static int _gold;
    public static void Configure()
    {
        Register("setup", Setup);
        Register("target", () =>
        {
            Require(_player.Target != null, "Tool button did not request a target");
            _player.Target.Invoke(_player, _item);
            Require(_player.HasGump<SocketGump>(), "No socket confirmation from actual tool menu");
        });
        Register("cancelled", () =>
        {
            Require(!_item.Deleted && ClassGear.State(_item) == _before && _tool.UsesRemaining == 20
                && !_player.HasGump<SocketGump>(), "Cancel changed socket work");
            _tool.OnDoubleClick(_player);
        });
        Register("added", () =>
        {
            Require(!_item.Deleted && ClassGear.State(_item) == (_before with { TinkerSockets = 1 })
                && _tool.UsesRemaining == 19 && !_player.HasGump<SocketGump>(), "Actual attempt did not add first socket/close");
            PrepareIntuition();
        });
        Register("aborted", () =>
        {
            Require(!_item.Deleted && ClassGear.State(_item) == _before && _tool.UsesRemaining == 19
                && !_player.HasGump<SocketGump>() && !TinkeringSockets.Commit(_player, _retry, out _),
                "Actual intuition abort changed item, used charge or replayed");
            PrepareIntuition();
        });
        Register("live-final", () => Final(true)); Register("final", () => Final(false));
    }
    private static void Setup()
    {
        _player = (PlayerMobile)Accounts.GetAccount("combatui")[0]; ClassCombat.Stop(_player);
        _player.Frozen = _player.Paralyzed = false; _player.Target = null;
        var location = CombatPractice.FindFloor(Map.Trammel, 5750, 1200);
        CraftFacilities.Register(new(SkillName.Tinkering, "Trammel", location.X, location.Y, location.Z, 0xB4A));
        _player.MoveToWorld(location, Map.Trammel);
        ClassProfessions.Profiles.Confirm(_player, SkillName.Tinkering);
        _player.Skills.Tinkering.Cap = 120; _player.Skills.Tinkering.SetLockNoRelay(SkillLock.Locked);
        SocketChecks.Ready(_player); _tool = SocketChecks.Put(_player, new TinkerTools(20));
        _item = SocketChecks.Gear(_player); _before = ClassGear.State(_item);
        _gold = _player.Backpack.GetAmount(typeof(Gold)); _tool.OnDoubleClick(_player);
    }
    private static void PrepareIntuition()
    {
        _item = SocketChecks.Gear(_player, sockets: 3); _before = ClassGear.State(_item);
        var quote = SocketChecks.Quote(_player, _tool, _item);
        Require(TinkeringSockets.Commit(_player, quote, out var result, new SocketChecks.Rolls(99, 0))
            && result.Outcome == SocketOutcome.Intuition && result.UsesSpent == 0 && _tool.UsesRemaining == 19
            && ClassGear.State(_item) == _before, "Seeded initial intuition mutated/charged work");
        _retry = result.Retry; SocketGump.Display(_retry);
    }
    private static void Final(bool connected)
    {
        Require(_tool.UsesRemaining == 18 && !_player.HasGump<SocketGump>()
            && (_item.Deleted || ClassGear.State(_item) == (_before with { TinkerSockets = 4 }))
            && !TinkeringSockets.Commit(_player, _retry, out _)
            && _player.Backpack.GetAmount(typeof(Gold)) == _gold && _player.Skills.Tinkering.Base == 120,
            "Actual paid retry did not finish exactly once, charged gold or changed training");
        if (connected) { Require(_player.NetState != null, "Client disconnected during socketing"); }
    }
    private static void Require(bool condition, string message)
    { if (!condition) { throw new InvalidOperationException(message); } }
    private static void Register(string suffix, Action action)
    {
        string command = "socket-ui-" + suffix;
        ConsoleInputHandler.RegisterCommand(command, "Actual-client socket menu and intuition choices", _ => Core.LoopContext.Post(() =>
        {
            try
            {
                action(); File.WriteAllText(Environment.GetEnvironmentVariable("SANCTUARY_TEST_UI_STATE"),
                    JsonSerializer.Serialize(new { deleted = _item.Deleted, uses = _tool.UsesRemaining }));
                _logger.Information("{Command}: passed", command);
            }
            catch (Exception error) { _logger.Error(error, "{Command}: failed", command); }
        }));
    }
}
