// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Gumps;
using Server.Network;
using Server.Mobiles;
using Server.Logging;

namespace Sanctuary;

public sealed class TalentGump : DynamicGump
{
    private static readonly ILogger _logger = LogFactory.GetLogger(typeof(TalentGump));
    private static readonly int[] _tiers = [1, 6, 12, 18, 24, 30];
    private readonly Serial _owner;
    private readonly CharacterProfile _profile;
    private readonly int _tree;
    private readonly int _selected;

    public override bool Singleton => true;

    private TalentGump(Mobile owner, CharacterProfile profile, int tree, int selected) : base(10, 15)
    {
        _owner = owner.Serial;
        _profile = profile;
        _tree = tree;
        _selected = selected;
    }

    public static void DisplayTo(Mobile from, int tree = 0, int selected = 0)
    {
        if (from?.NetState == null || !ClassCreation.Profiles.TryGet(from.Serial, out var profile))
        {
            return;
        }

        if (tree is < 0 or > 2 || selected is < 0 or > 9)
        {
            return;
        }

        from.SendGump(new TalentGump(from, profile, tree, selected));
    }

    protected override void BuildLayout(ref DynamicGumpBuilder builder)
    {
        var catalog = TalentCatalog.ForClass(_profile.Class);
        var profiles = ClassCreation.Profiles;
        var skills = catalog.Trees[_tree].Skills;
        var selected = skills[_selected];
        var points = profiles.AvailablePoints(_owner);

        builder.AddPage();
        builder.AddBackground(0, 0, 610, 440, 5054);
        builder.AddLabel(18, 12, 0x481, $"{_profile.Class} — Level {_profile.Level}");
        builder.AddLabel(18, 34, 0x47E, $"Talent points: {points}     Experience: {_profile.Experience}");
        builder.AddButton(570, 12, 4017, 4019, 0);

        for (var tree = 0; tree < 3; tree++)
        {
            var x = 14 + tree * 196;
            builder.AddBackground(x, 62, 190, 35, tree == _tree ? 2620 : 5120);
            builder.AddButton(x + 5, 65, 4005, 4007, tree + 1);
            builder.AddHtml(x + 35, 69, 150, 22,
                $"<BASEFONT COLOR=#FFFFFF>{catalog.Trees[tree].Name}</BASEFONT>", background: false, scrollbar: false);
        }

        var columns = new int[6];
        for (var index = 0; index < skills.Length; index++)
        {
            var talent = skills[index];
            var row = Array.IndexOf(_tiers, talent.RequiredLevel);
            var column = columns[row]++;
            var x = 22 + column * 118;
            var y = 108 + row * 49;
            var rank = profiles.GetRank(_owner, talent.Id);
            var color = index == _selected ? "#FFD37A" : rank > 0 ? "#90D8A0"
                : _profile.Level < talent.RequiredLevel ? "#A0A0A0" : "#FFFFFF";
            builder.AddBackground(x, y, 114, 46, 5120);
            builder.AddButton(x + 3, y + 28, 4005, 4007, 100 + index);
            builder.AddHtml(x + 5, y + 3, 104, 27,
                $"<BASEFONT COLOR={color}>{talent.Name}</BASEFONT>", size: 2, background: false, scrollbar: false);
            builder.AddLabel(x + 32, y + 29, 0x47E, $"{rank}/{TalentCatalog.MaxRank}  L{talent.RequiredLevel}");
        }

        builder.AddBackground(384, 108, 210, 291, 5120);
        builder.AddHtml(395, 118, 188, 46, $"<BASEFONT COLOR=#FFD37A>{selected.Name}</BASEFONT>", background: false, scrollbar: false);
        builder.AddLabel(395, 165, 0x47E, $"Rank {profiles.GetRank(_owner, selected.Id)} / {TalentCatalog.MaxRank}");
        builder.AddLabel(395, 187, 0x47E, $"Required level: {selected.RequiredLevel}");
        builder.AddHtml(395, 211, 188, 100, PrerequisiteText(selected), background: false, scrollbar: selected.Prerequisites.Length > 5);

        var requirement = profiles.TrainingRequirement(_owner, selected.Id);
        if (requirement == null)
        {
            builder.AddButton(395, 316, 4005, 4007, 1000 + _selected);
            builder.AddLabel(430, 318, 0x481, "Learn — 1 point");
        }
        else
        {
            builder.AddHtml(395, 313, 188, 46, $"<BASEFONT COLOR=#C4C4C4>{requirement}</BASEFONT>", background: false, scrollbar: false);
        }

        if (selected.Id == "necromancer.teeth" && World.FindMobile(_owner) is PlayerMobile owner)
        {
            var source = owner.FindItemOnLayer(Layer.OneHanded) is Server.Items.Dagger
                ? "Includes +1 from dagger." : "Equip a dagger for +1.";
            builder.AddHtml(395, 366, 188, 27,
                $"<BASEFONT COLOR=#C4C4C4>Teeth prototype: rank {ClassCombat.TeethRank(owner)}.<BR>{source}</BASEFONT>",
                size: 2, background: false, scrollbar: false);
        }
        else if (selected.Id == "necromancer.raiseskeleton")
        {
            builder.AddHtml(395, 366, 188, 27, "<BASEFONT COLOR=#C4C4C4>Raise a guardian from a nearby corpse.</BASEFONT>", size: 2, background: false, scrollbar: false);
        }
        else if (selected.Id == "necromancer.skeletonmastery")
        {
            builder.AddHtml(395, 366, 188, 27, "<BASEFONT COLOR=#C4C4C4>New skeletons: +8 base life, +2 base damage, +15 attack/defense per rank.</BASEFONT>", size: 2, background: false, scrollbar: true);
        }
        else
        {
            builder.AddHtml(395, 366, 188, 27, "<BASEFONT COLOR=#C4C4C4>Effects inactive.</BASEFONT>", background: false, scrollbar: false);
        }
        builder.AddButton(22, 408, 4005, 4007, 2000);
        builder.AddLabel(57, 410, 0x481, "Mouse skills");
        builder.AddButton(205, 408, 4005, 4007, 2001);
        builder.AddLabel(240, 410, 0x481, "Pets");
        builder.AddLabel(320, 410, 0x47E, $"Next level: {(_profile.Level < 99 ? ExperienceLevels.Threshold(_profile.Level + 1) : _profile.Experience)} XP");
    }

    private string PrerequisiteText(TalentDefinition talent)
    {
        if (talent.Prerequisites.Length == 0)
        {
            return "<BASEFONT COLOR=#FFFFFF>Prerequisites: None</BASEFONT>";
        }

        var names = new string[talent.Prerequisites.Length];
        for (var i = 0; i < names.Length; i++)
        {
            TalentCatalog.TryGet(_profile.Class, talent.Prerequisites[i], out var required);
            var learned = ClassCreation.Profiles.GetRank(_owner, required.Id) > 0;
            names[i] = $"<BASEFONT COLOR={(learned ? "#90D8A0" : "#C4C4C4")}>{required.Name}</BASEFONT>";
        }

        return "<BASEFONT COLOR=#FFFFFF>Prerequisites:</BASEFONT><BR>" + string.Join("<BR>", names);
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        var from = sender.Mobile;
        _logger.Information("Talent response: character={Character}, owner={Owner}, button={Button}, gump={Gump}",
            from?.Serial, _owner, info.ButtonID, Serial);
        if (from == null || from.Serial != _owner || !ClassCreation.Profiles.TryGet(_owner, out var profile)
            || profile.Class != _profile.Class)
        {
            return;
        }

        var button = info.ButtonID;
        if (button == 0)
        {
            return;
        }

        if (button == 2000)
        {
            BindingGump.DisplayTo(from);
            return;
        }
        if (button == 2001)
        {
            PetStatsGump.DisplayTo(from);
            return;
        }
        if (button is >= 1 and <= 3)
        {
            DisplayTo(from, button - 1);
            return;
        }

        if (button is >= 100 and < 110)
        {
            DisplayTo(from, _tree, button - 100);
            return;
        }

        if (button is >= 1000 and < 1010)
        {
            var index = button - 1000;
            var talent = TalentCatalog.ForClass(profile.Class).Trees[_tree].Skills[index];
            if (!ClassCreation.Profiles.TryTrain(_owner, talent.Id, out var reason))
            {
                from.SendMessage(reason);
                _logger.Information("Talent allocation refused: character={Character}, talent={Talent}, reason={Reason}",
                    _owner, talent.Id, reason);
            }
            else
            {
                from.SendMessage($"Learned {talent.Name}: rank {ClassCreation.Profiles.GetRank(_owner, talent.Id)}.");
                _logger.Information("Talent learned: character={Character}, talent={Talent}", _owner, talent.Id);
            }

            DisplayTo(from, _tree, index);
        }
    }
}
