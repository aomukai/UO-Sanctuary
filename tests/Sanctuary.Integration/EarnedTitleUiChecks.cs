// SPDX-License-Identifier: GPL-3.0-or-later
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

public static class EarnedTitleUiChecks
{
    private static readonly ILogger _log = LogFactory.GetLogger(typeof(EarnedTitleUiChecks));
    private static PlayerMobile _owner;
    private static Item _wardrobe;
    private static string _initial;
    private static void Require(bool value, string why) => GearDropChecks.Require(value, why);
    public static void Configure()
    {
        Command("setup", () =>
        {
            _owner = (PlayerMobile)Accounts.GetAccount("combatui")[0]; ClassCombat.Stop(_owner); _owner.Target = null;
            _owner.Frozen = _owner.Paralyzed = false; _owner.Blessed = true; _owner.Name = "AlexandertheExtremelyPersistent";
            ClassCreation.Profiles.AdvanceLevel(_owner, 99);
            string site = RecognitionChecks.Place(_owner, Array.FindIndex(TownRecognition.Halls, h => h.Id == "Felucca/Britain"));
            TownRecognition.Claim(_owner, site, RecognitionHonor.Hero); _wardrobe = TownRecognition.PublicWardrobe(site);
            Swap(DiabloClass.Sorceress); ClassCreation.Profiles.AdvanceLevel(_owner, 99); Swap(DiabloClass.Necromancer);
            _owner.Skills.Blacksmith.Cap = 120; _owner.Skills.Blacksmith.Base = 120; _owner.Skills.Mining.Base = 100;
            _initial = Gameplay(); WardrobeGump.DisplayTo(_owner, _wardrobe);
        });
        Command("opened", () => Require(_owner.NetState.GetGumps().Has<EarnedTitleGump>(), "Wardrobe title button did not open selector"));
        Command("guards", () =>
        {
            var before = EarnedTitles.Profiles.Find(_owner.Serial);
            foreach (int[] switches in new int[][] { [], [102], [102, 1007], [102, 1007, 2003, 2002], [102, 1007, 2003, 9999], [107, 1007, 2003], [102, 1018, 2003] })
            {
                var g = new EarnedTitleGump(_owner); g.OnResponse(_owner.NetState, new RelayInfo(1, switches, [], [], []));
                Require(ReferenceEquals(before, EarnedTitles.Profiles.Find(_owner.Serial)), "Malformed or unearned title response applied");
            }
            var stale = new EarnedTitleGump(_owner); EarnedTitleGump.DisplayTo(_owner);
            stale.OnResponse(_owner.NetState, new RelayInfo(1, [102, 1007, 2003], [], [], []));
            Require(ReferenceEquals(before, EarnedTitles.Profiles.Find(_owner.Serial)), "Replaced title session applied");
        });
        Command("selected", () => Check(DiabloClass.Sorceress, (byte)SkillName.Blacksmith, 3));
        Command("hidden", () => Check(0, 255, 0));
        Command("suffix", () => Check(0, (byte)SkillName.Blacksmith, 2));
        Command("prefix", () => Check(DiabloClass.Necromancer, 255, 0));
        Command("open", () => EarnedTitleGump.DisplayTo(_owner));
        Command("preview", () => Require(!_owner.NetState.GetGumps().Has<EarnedTitleGump>() && Gameplay() == _initial,
            "Paperdoll preview did not leave selector or changed gameplay"));
        Command("swap", () => { Swap(DiabloClass.Sorceress); Check(DiabloClass.Sorceress, (byte)SkillName.Blacksmith, 3, false); });
        Command("final", () => Check(DiabloClass.Sorceress, (byte)SkillName.Blacksmith, 3, false));
    }
    private static string Gameplay() => JsonSerializer.Serialize(new { _owner.RawStr, _owner.RawDex, _owner.RawInt, _owner.Fame, _owner.Karma, _owner.Kills,
        Skills = EarnedTitles.Skills.Select(s => new { Skill = s, _owner.Skills[s].BaseFixedPoint, _owner.Skills[s].CapFixedPoint }) });
    private static void Check(DiabloClass prefix, byte skill, byte tier, bool gameplay = true)
    {
        var state = EarnedTitles.Profiles.Find(_owner.Serial);
        Require(state.Prefix == prefix && state.Skill == skill && state.Tier == tier && (!gameplay || Gameplay() == _initial), "Actual UI chose wrong slots/rank or altered gameplay");
    }
    private static void Swap(DiabloClass c)
    {
        ClassCreation.Profiles.TryGet(_owner.Serial, out var p); _owner.Hits = _owner.HitsMax;
        Require(WardrobeSwitch.Commit(_owner, _wardrobe, TownRecognition.BeginSession(_owner), p, ClassProfessions.Profiles.Find(_owner), c, SkillName.Alchemy), "UI title fixture class swap failed");
    }
    private static void Command(string suffix, Action action)
    {
        string name = "earned-title-ui-" + suffix;
        ConsoleInputHandler.RegisterCommand(name, "Actual earned title selector and full paperdoll text", _ => Core.LoopContext.Post(() =>
        {
            try
            {
                action(); File.WriteAllText(Environment.GetEnvironmentVariable("SANCTUARY_TEST_UI_STATE"), JsonSerializer.Serialize(new { name = EarnedTitles.Display(_owner)?.Name(_owner.Name) }));
                _log.Information("{Command}: passed", name);
            }
            catch (Exception error) { _log.Error(error, "{Command}: failed", name); }
        }));
    }
}
