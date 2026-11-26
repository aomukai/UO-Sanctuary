// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
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
    private readonly int[] _openedRanks;
    private long _nextPreview;

    public override bool Singleton => true;

    private TalentGump(Mobile owner, CharacterProfile profile, int tree, int selected) : base(10, 27)
    {
        _owner = owner.Serial;
        _profile = profile;
        _tree = tree;
        _selected = selected;
        _openedRanks = TalentCatalog.ForClass(profile.Class).Trees.SelectMany(t => t.Skills)
            .Select(t => ClassCreation.Profiles.GetRank(_owner, t.Id)).ToArray();
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
        builder.AddBackground(0, 0, 928, 514, 5054);
        builder.AddLabel(18, 12, 0x481, $"{DiabloClasses.DisplayName(_profile.Class, World.FindMobile(_owner)?.Female == true)} — Level {_profile.Level}");
        builder.AddLabel(18, 34, 0x47E, $"Talent points: {points}     Experience: {_profile.Experience}");
        builder.AddButton(888, 12, 4017, 4019, 0);

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
            var icon = ClassSkillIcons.ForTalent(talent.Id);
            if (icon != 0)
            {
                builder.AddImage(x + 1, y + 1, icon);
            }
            var textX = x + (icon == 0 ? 5 : 47);
            builder.AddHtml(textX, y + 1, x + 112 - textX, 31,
                $"<BASEFONT COLOR={color}>{talent.Name}</BASEFONT>", size: 2, background: false, scrollbar: false);
            builder.AddLabel(textX, y + 29, 0x47E, $"{rank}/{TalentCatalog.MaxRank} L{talent.RequiredLevel}");
        }

        builder.AddBackground(384, 108, 528, 346, 5120);
        builder.AddHtml(395, 118, 506, 30, $"<BASEFONT COLOR=#FFD37A>{selected.Name}</BASEFONT>", background: false, scrollbar: false);
        int learned = profiles.GetRank(_owner, selected.Id);
        int effective = World.FindMobile(_owner) is PlayerMobile player ? NamedSkills.Rank(player, selected.Id) : learned;
        if (effective != learned)
        { builder.AddLabel(395, 150, 0x47E, $"Rank {learned}/{TalentCatalog.MaxRank} (+{effective - learned} gear)"); }
        else { builder.AddLabel(395, 150, 0x47E, $"Rank {learned}/{TalentCatalog.MaxRank}"); }

        var skill = SkillMechanics.All.FirstOrDefault(row => row.TalentId == selected.Id);
        var (synergies, improves) = TalentSynergies.For(selected.Id);
        var content = new List<string>
        {
            Detail(TalentCatalog.Description(selected.Id)),
            PrerequisiteText(selected)
        };
        if (synergies.Length > 0)
            content.Add(Detail($"Synergies: {string.Join(", ", synergies)}"));
        if (improves.Length > 0)
            content.Add(Detail($"Improves: {string.Join(", ", improves)}"));
        var mechanics = TalentDetails.For(skill);
        if (mechanics.Length > 0)
            content.Add(Detail(mechanics));
        builder.AddHtml(395, 178, 506, 222, string.Join("<BR><BR>", content), size: 2, background: false, scrollbar: false);

        var requirement = profiles.TrainingRequirement(_owner, selected.Id);
        if (requirement == null)
        {
            builder.AddButton(395, 417, 4005, 4007, 1000 + _selected);
            builder.AddLabel(430, 419, 0x481, "Learn — 1 point");
        }
        else
        {
            builder.AddHtml(395, 416, 506, 31, Detail(requirement), background: false, scrollbar: false);
        }

        builder.AddButton(22, 475, 4005, 4007, ClassGumpProtocol.MouseSkills);
        builder.AddLabel(57, 477, 0x481, "Mouse skills");
        builder.AddButton(205, 475, 4005, 4007, ClassGumpProtocol.Pets);
        builder.AddLabel(240, 477, 0x481, "Pets");
        builder.AddLabel(320, 477, 0x47E, $"Next level: {(_profile.Level < 99 ? ExperienceLevels.Threshold(_profile.Level + 1) : _profile.Experience)} XP");
        // Older clients get only the original layout. Current clients consume the data before parsing page controls.
        if (World.FindMobile(_owner) is PlayerMobile viewer && StatNetwork.SupportsCurrentProtocol(viewer))
        {
            builder.AddPage(99);
            builder.AddTextEntry(0, 0, 1, 1, 0, ClassGumpProtocol.AllocationEntry, "");
            builder.AddHtml(0, 0, 1, 1, BuildMenuData(catalog, viewer), background: false, scrollbar: false);
        }

    }

    private string BuildMenuData(ClassTalents catalog, PlayerMobile player)
    {
        var talents = catalog.Trees.SelectMany(tree => tree.Skills).ToArray();
        var indices = talents.Select((talent, index) => (talent.Id, index)).ToDictionary(pair => pair.Id, pair => pair.index);
        var skills = talents.Select((talent, index) =>
        {
            var skill = SkillMechanics.All.FirstOrDefault(row => row.TalentId == talent.Id);
            int hard = _openedRanks[index];
            string mechanics = TalentDetails.For(skill);
            var (synergies, improves) = TalentSynergies.For(talent.Id);
            return new TalentMenuSkill(talent.Name, ClassSkillIcons.ForTalent(talent.Id), talent.RequiredLevel,
                hard, talent.Prerequisites.Select(id => indices[id]).ToArray(), TalentCatalog.Description(talent.Id), mechanics,
                TalentCatalog.Dependencies(talent).Select(dependency => indices[dependency.Id]).ToArray(), synergies, improves);
        }).ToArray();
        return ClassGumpProtocol.MenuPrefix + JsonSerializer.Serialize(new TalentMenuData(
            DiabloClasses.DisplayName(_profile.Class, player.Female), _profile.Level,
            ClassCreation.Profiles.AvailablePoints(_owner), _profile.Experience,
            _profile.Level < 99 ? ExperienceLevels.Threshold(_profile.Level + 1) : _profile.Experience,
            TalentCatalog.MaxRank, catalog.Trees.Select(t => t.Name).ToArray(), skills), TalentMenuJsonContext.Default.TalentMenuData);
    }

    private bool TryReadDraft(string draft, out Dictionary<string, int> allocations, out string reason)
    {
        allocations = new();
        var profiles = ClassCreation.Profiles;
        var talents = TalentCatalog.ForClass(_profile.Class).Trees.SelectMany(t => t.Skills).ToArray();
        if (draft?.Length != talents.Length)
        {
            reason = "Invalid talent allocation.";
            return false;
        }

        for (var i = 0; i < talents.Length; i++)
        {
            string id = talents[i].Id;
            int points = draft[i] - 'A';
            if (points < 0 || points > TalentCatalog.MaxRank || profiles.GetRank(_owner, id) != _openedRanks[i])
            {
                reason = "Your talent allocation changed. Reopen Talents to review your points.";
                return false;
            }
            if (points > 0)
            {
                allocations.Add(id, points);
            }
        }
        reason = null;
        return true;
    }

    internal string Preview(PlayerMobile from, int index, string draft)
    {
        if (from.Serial != _owner || (uint)index >= _openedRanks.Length || Core.TickCount < _nextPreview
            || !ClassCreation.Profiles.TryGet(_owner, out var current) || current.Class != _profile.Class)
        {
            return null;
        }
        _nextPreview = Core.TickCount + 75;
        if (!TryReadDraft(draft, out var allocations, out var reason)
            || !ClassCreation.Profiles.CanTrainBatch(_owner, allocations, out reason))
        {
            return reason;
        }

        var talent = TalentCatalog.ForClass(current.Class).Trees[index / ClassGumpProtocol.SkillsPerTree]
            .Skills[index % ClassGumpProtocol.SkillsPerTree];
        var skill = SkillMechanics.All.FirstOrDefault(row => row.TalentId == talent.Id);
        var next = new Dictionary<string, int>(allocations)
        {
            [talent.Id] = allocations.GetValueOrDefault(talent.Id) + 1
        };
        return TalentDetails.Preview(from, skill, allocations,
            ClassCreation.Profiles.CanTrainBatch(_owner, next, out _) ? next : null);
    }

    private void ApplyAllocation(Mobile from, string draft)
    {
        var profiles = ClassCreation.Profiles;
        if (!TryReadDraft(draft, out var allocations, out var reason)
            || !profiles.TryTrainBatch(_owner, allocations, out reason))
        {
            from.SendMessage(reason);
            return;
        }
        from.SendMessage($"Talent allocation saved. {profiles.AvailablePoints(_owner)} unspent points.");
    }

    private static string Detail(string text) => $"<BASEFONT COLOR=#C4C4C4>{WebUtility.HtmlEncode(text).Replace("\n", "<BR>")}</BASEFONT>";

    private static string PrerequisiteText(TalentDefinition talent)
    {
        var dependencies = TalentCatalog.Dependencies(talent);
        string names = dependencies.Length > 0 ? ", " + string.Join(", ", dependencies.Select(dependency => dependency.Name)) : "";
        return Detail($"Requires character level {talent.RequiredLevel}{names}.");
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

        if (button == ClassGumpProtocol.MouseSkills)
        {
            BindingGump.DisplayTo(from);
            return;
        }
        if (button == ClassGumpProtocol.Pets)
        {
            PetStatsGump.DisplayTo(from);
            return;
        }
        if (button == ClassGumpProtocol.Apply)
        {
            ApplyAllocation(from, info.GetTextEntry(ClassGumpProtocol.AllocationEntry));
            DisplayTo(from);
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
