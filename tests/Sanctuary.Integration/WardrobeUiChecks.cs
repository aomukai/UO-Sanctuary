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
using Server.Network;

namespace Sanctuary.Integration;

public static class WardrobeUiChecks
{
    private static readonly ILogger _logger = LogFactory.GetLogger(typeof(WardrobeUiChecks));
    private static PlayerMobile _player;
    private static string _site;
    private static ProfessionState _initial;
    private static CharacterProfile _profile;
    private static StatAllocation _stats;
    private static string _skills;
    private static uint[] _worn;
    private static WardrobeGump _stale;
    private static Item Wardrobe => TownRecognition.PublicWardrobe(_site);
    private static int Gold() => _player.Backpack.GetAmount(typeof(Gold));
    public static void Configure()
    {
        Register("setup", Setup);
        Register("guards", Guards);
        Register("unchanged", () =>
        {
            Require(ReferenceEquals(ClassProfessions.Profiles.Find(_player), _initial), "Unchanged UI selection mutated craft");
            Preserved(); Open();
        });
        Register("remote", () => _player.MoveToWorld(new Point3D(_player.X + 40, _player.Y, _player.Z), _player.Map));
        Register("rejected", () =>
        {
            Require(ReferenceEquals(ClassProfessions.Profiles.Find(_player), _initial), "Moved-away UI response switched craft");
            RecognitionChecks.Place(_player, Array.FindIndex(TownRecognition.Halls, h => h.Id == _site)); Open();
        });
        Register("switched", () =>
        {
            Require(ClassProfessions.Profiles.Find(_player) == new ProfessionState(SkillName.Tinkering, true)
                && ClassProfessions.Active(_player, SkillName.Tinkering) && !ClassProfessions.Active(_player, SkillName.Alchemy), "UI did not activate selected craft");
            Preserved(); Open();
        });
        Register("back", () =>
        {
            Require(ClassProfessions.Profiles.Find(_player) == _initial, "UI did not restore previous craft");
            Preserved(); _player.Skills.Cartography.Base = 100;
            TownRecognition.Open(_player, _site);
        });
        Register("both", () =>
        {
            Require(TownRecognition.Profiles.Find(_player).Earned == RecognitionHonor.Both, "Second ceremony failed"); Open();
        });
        Register("quote", () => Require(_player.NetState.GetGumps().Has<RespecGump>()
            && !_player.NetState.GetGumps().Has<WardrobeGump>(), "Respec did not replace wardrobe"));
        Register("live-final", () => Final(true)); Register("final", () => Final(false));
    }
    private static string Skills() => JsonSerializer.Serialize(Enumerable.Range(0, _player.Skills.Length)
        .Select(i => _player.Skills[i]).Select(s => new { s.BaseFixedPoint, s.CapFixedPoint, s.Lock }));
    private static void Preserved()
    {
        ClassCreation.Profiles.TryGet(_player.Serial, out var profile);
        Require(profile == _profile && ClassCreation.Profiles.Stats(_player.Serial) == _stats && Gold() == 6000
            && Skills() == _skills && _player.Hits < _player.HitsMax && _player.Mana < _player.ManaMax && _player.Stam < _player.StamMax
            && _player.Items.Select(i => i.Serial.Value).Order().SequenceEqual(_worn), "Client switch changed points, gold, skills, pools or gear");
        Require(!_player.NetState.GetGumps().Has<WardrobeGump>(), "Swap did not close selector");
    }
    private static void Open()
    { ((PublicRecognitionWardrobe)Wardrobe).OnDoubleClick(_player); }
    private static void Setup()
    {
        _player = (PlayerMobile)Accounts.GetAccount("combatui")[0];
        _player.Frozen = false; _player.Paralyzed = false; ClassCombat.Stop(_player); _player.Target = null;
        ClassCreation.Profiles.AdvanceLevel(_player, 80); _player.Skills.Cartography.Base = 99.9;
        _player.Skills.Alchemy.Base = 45.7; _player.Skills.Tinkering.Base = 66.6;
        ClassProfessions.Profiles.Confirm(_player, SkillName.Alchemy);
        ClassCreation.Profiles.TrySpendStat(_player, CharacterStat.Vitality);
        Require(ClassCreation.Profiles.TryTrain(_player.Serial, "necromancer.raiseskeleton", out _), "Could not allocate UI talent");
        var gold = new System.Collections.Generic.List<Item>();
        foreach (var i in _player.Backpack.FindItemsByType(typeof(Gold), true)) { gold.Add(i); }
        foreach (var i in gold) { i.Delete(); }
        _player.Backpack.DropItem(new Gold(6000));
        _site = RecognitionChecks.Place(_player, Array.FindIndex(TownRecognition.Halls, h => h.Id == "Felucca/Britain"));
        Require(TownRecognition.Claim(_player, _site, RecognitionHonor.Hero), "UI first ceremony failed");
        _initial = ClassProfessions.Profiles.Find(_player); ClassCreation.Profiles.TryGet(_player.Serial, out _profile);
        _stats = ClassCreation.Profiles.Stats(_player.Serial); _skills = Skills();
        _worn = _player.Items.Select(i => i.Serial.Value).Order().ToArray();
        _player.Hits = 12; _player.Mana = 8; _player.Stam = 10; Open();
    }
    private static void Guards()
    {
        foreach (int[] switches in new int[][] { [], [103], [1000], [103, 1000, 103], [103, 1000, 1037], [255, 1037], [103, 1018], [103, -1] })
        {
            var gump = new WardrobeGump(_player, Wardrobe);
            var reply = new RelayInfo(1, switches, [], [], []); gump.OnResponse(_player.NetState, reply);
            Require(ReferenceEquals(ClassProfessions.Profiles.Find(_player), _initial) && Gold() == 6000, "Malformed selection applied");
        }
        _stale = new WardrobeGump(_player, Wardrobe); Open();
        var valid = new RelayInfo(1, [103, 1037], [], [], []); _stale.OnResponse(_player.NetState, valid);
        Require(ReferenceEquals(ClassProfessions.Profiles.Find(_player), _initial), "Replaced gump response applied");
    }
    private static void Final(bool connected)
    {
        ClassCreation.Profiles.TryGet(_player.Serial, out var profile);
        Require(profile == _profile && ClassProfessions.Profiles.Find(_player) == _initial && Gold() == 1000
            && ClassCreation.Profiles.Stats(_player.Serial) == default && ClassCreation.Profiles.AvailableStatPoints(_player.Serial) == 395
            && ClassCreation.Profiles.AvailablePoints(_player.Serial) == 79 && _player.Skills.Cartography.BaseFixedPoint == 1000,
            "Wardrobe respec did not preserve active class/craft or refund and charge exactly once");
        if (connected)
        {
            var reply = new RelayInfo(1, [103, 1037], [], [], []); _stale.OnResponse(_player.NetState, reply);
            Require(ClassProfessions.Profiles.Find(_player) == _initial && !_player.NetState.GetGumps().Has<WardrobeGump>()
                && !_player.NetState.GetGumps().Has<RecognitionGump>() && !_player.NetState.GetGumps().Has<RespecGump>(), "Reset left a switchable wardrobe UI");
        }
    }
    private static void Require(bool condition, string message)
    { if (!condition) { throw new InvalidOperationException(message); } }
    private static void Register(string suffix, Action action)
    {
        string command = "wardrobe-ui-" + suffix;
        ConsoleInputHandler.RegisterCommand(command, "Actual-client wardrobe fixture", _ => Core.LoopContext.Post(() =>
        {
            try
            {
                action(); File.WriteAllText(Environment.GetEnvironmentVariable("SANCTUARY_TEST_UI_STATE"), JsonSerializer.Serialize(new { gold = Gold() }));
                _logger.Information("{Command}: passed", command);
            }
            catch (Exception error) { _logger.Error(error, "{Command}: failed", command); }
        }));
    }
}
