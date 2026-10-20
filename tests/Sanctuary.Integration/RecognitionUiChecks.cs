using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Accounting;
using Server.Items;
using Server.Gumps;
using Server.Network;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class RecognitionUiChecks
{
    private static readonly ILogger _logger = LogFactory.GetLogger(typeof(RecognitionUiChecks));
    private static PlayerMobile _player;
    private static string _site;
    private static CharacterProfile _profile;
    private static StatAllocation _stats;
    public static void Configure()
    {
        Register("setup", Setup);
        Register("first", () =>
        {
            Require(TownRecognition.Profiles.Find(_player).Earned == RecognitionHonor.Hero && Awards() == 2
                && !ClassRespec.Ready(_player, TownRecognition.PublicWardrobe(_site)), "First ceremony granted wrong awards or early respec");
            _player.Skills[SkillName.Cartography].Base = 100;
            TownRecognition.Open(_player, _site);
        });
        Register("second", () => Require(TownRecognition.Profiles.Find(_player).Earned == RecognitionHonor.Both
            && Awards() == 3 && ClassRespec.Ready(_player, TownRecognition.PublicWardrobe(_site)), "Second ceremony did not unlock respec exactly once"));
        Register("quote", () => Require(_player.NetState.GetGumps().Has<RespecGump>()
            && !_player.NetState.GetGumps().Has<RecognitionGump>(), "Respec confirmation did not replace wardrobe"));
        Register("remote", () => _player.MoveToWorld(new Point3D(_player.X + 40, _player.Y, _player.Z), _player.Map));
        Register("rejected", () =>
        {
            Require(ClassCreation.Profiles.Stats(_player.Serial) == _stats && Gold() == 6000, "Remote/stale response charged or reset");
            RecognitionChecks.Place(_player, Array.FindIndex(TownRecognition.Halls, h => h.Id == _site));
            TownRecognition.Open(_player, _site);
        });
        Register("live-final", () => Final(true));
        Register("final", () => Final(false));
    }
    private static void Final(bool connected)
    {
        ClassCreation.Profiles.TryGet(_player.Serial, out var current);
        Require(current == _profile && ClassCreation.Profiles.Stats(_player.Serial) == default
            && ClassCreation.Profiles.AvailableStatPoints(_player.Serial) == 395
            && ClassCreation.Profiles.AvailablePoints(_player.Serial) == 79
            && _player.Skills[SkillName.Cartography].BaseFixedPoint == 1000
            && Gold() == 1000 && _player.BankBox.GetAmount(typeof(Gold)) == 10000 && Awards() == 3,
            "Client respec did not preserve level/trades, refund points or charge once");
        if (connected)
        {
            Require(_player.NetState != null && !_player.NetState.GetGumps().Has<RecognitionGump>()
                && !_player.NetState.GetGumps().Has<RespecGump>(), "Client did not leave the wardrobe UI after reset");
        }
    }

    private static int Gold() => _player.Backpack.GetAmount(typeof(Gold));
    private static int Awards() => _player.Backpack.Items.Count(i => i is RecognitionCertificate or EarnedWardrobe);
    private static void Setup()
    {
        _player = (PlayerMobile)Accounts.GetAccount("combatui")[0];
        _player.Frozen = false; _player.Paralyzed = false;
        ClassCombat.Stop(_player);
        ClassCreation.Profiles.AdvanceLevel(_player, 80);
        _player.Skills[SkillName.Cartography].Base = 99.9;
        ClassCreation.Profiles.TrySpendStat(_player, CharacterStat.Strength);
        ClassCreation.Profiles.TrySpendStat(_player, CharacterStat.Vitality);
        Require(ClassCreation.Profiles.TryTrain(_player.Serial, "necromancer.raiseskeleton", out _), "UI talent setup failed");
        var gold = new System.Collections.Generic.List<Item>();
        foreach (var item in _player.Backpack.FindItemsByType(typeof(Gold), true)) { gold.Add(item); }
        foreach (var item in _player.BankBox.FindItemsByType(typeof(Gold), true)) { gold.Add(item); }
        foreach (var item in gold) { item.Delete(); }
        _player.Backpack.DropItem(new Gold(6000)); _player.BankBox.DropItem(new Gold(10000));
        _site = RecognitionChecks.Place(_player, Array.FindIndex(TownRecognition.Halls, h => h.Id == "Felucca/Britain"));
        ClassCreation.Profiles.TryGet(_player.Serial, out _profile);
        _stats = ClassCreation.Profiles.Stats(_player.Serial);
        TownRecognition.Open(_player, _site);
    }
    private static void Require(bool condition, string message)
    { if (!condition) { throw new InvalidOperationException(message); } }
    private static void Register(string suffix, Action action)
    {
        string command = "recognition-ui-" + suffix;
        ConsoleInputHandler.RegisterCommand(command, "Actual-client recognition/respec fixture", _ => Core.LoopContext.Post(() =>
        {
            try
            {
                action();
                File.WriteAllText(Environment.GetEnvironmentVariable("SANCTUARY_TEST_UI_STATE"), JsonSerializer.Serialize(new { gold = Gold(), awards = Awards() }));
                _logger.Information("{Command}: passed", command);
            }
            catch (Exception error) { _logger.Error(error, "{Command}: failed", command); }
        }));
    }
}
