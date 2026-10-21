// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using Server.Mobiles;

namespace Sanctuary;

public sealed record NamedSkillBinding(int SourceId, string SourceKey, string TalentId,
    string CharacterClass, string ClassCode, int SourceTab, string Element = null);
public static class NamedSkills
{
    private static readonly Dictionary<string, NamedSkillBinding> _byTalent = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, NamedSkillBinding> _bySource = new(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> _working = new(StringComparer.Ordinal);
    public static void Load()
    {
        _byTalent.Clear(); _bySource.Clear(); _working.Clear();
        foreach (var binding in BalanceCatalog.Read<NamedSkillBinding>("named-skill-bindings"))
        {
            _byTalent.Add(binding.TalentId, binding);
            _bySource.Add(binding.SourceId.ToString(System.Globalization.CultureInfo.InvariantCulture), binding);
            _bySource.Add(binding.SourceKey, binding);
        }
        foreach (string talent in BalanceCatalog.Read<string>("named-active-skills"))
        { if (!_byTalent.ContainsKey(talent) || !_working.Add(talent)) { throw new System.IO.InvalidDataException("Unknown/duplicate named active skill."); } }
    }
    public static bool Supported(NamedProperty property) => !property.SourceEnabled || property.Code switch
    {
        "skill" => property.Parameter is { } param && _bySource.TryGetValue(param.ToString(), out var skill) && _working.Contains(skill.TalentId),
        "skilltab" => NamedPropertyRuntime.Parameter(property) is >= 0 and < 21,
        _ => true
    };
    internal static (string Skill, string CharacterClass) Description(NamedProperty property)
    {
        if (property.Code == "skill" && property.Parameter is { } parameter
            && _bySource.TryGetValue(parameter.ToString(), out var binding))
        { return (binding.SourceKey, binding.CharacterClass); }
        int tab = NamedPropertyRuntime.Parameter(property);
        if (property.Code == "skilltab" && tab is >= 0 and < 21)
        {
            var characterClass = Enum.Parse<DiabloClass>(new[] { "Amazon", "Sorceress", "Necromancer", "Paladin", "Barbarian", "Druid", "Assassin" }[tab / 3]);
            return (TalentCatalog.ForClass(characterClass).Trees[tab % 3].Name, characterClass.ToString());
        }
        return ("Skill", "Class");
    }
    public static int Rank(PlayerMobile player, string talentId, int baseline = -1)
    {
        if (baseline < 0) { baseline = ClassCreation.Profiles.GetRank(player.Serial, talentId); }
        if (!_byTalent.TryGetValue(talentId, out var talent)
            || !ClassCreation.Profiles.TryGet(player.Serial, out var profile) || profile.Class.ToString() != talent.CharacterClass)
        { return baseline; }
        int direct = 0, general = 0;
        foreach (var value in NamedGear.ActiveProperties(player))
        {
            var property = value.Property;
            if (property.Code == "skill" && property.Parameter is { } parameter
                && _bySource.TryGetValue(parameter.ToString(), out var grant) && grant.TalentId == talentId)
            { direct += value.Roll.Value; }
            else if (property.Code == "allskills" || property.Code == talent.ClassCode
                || property.Code == "fireskill" && talent.Element == "fire"
                || property.Code == "skilltab" && NamedPropertyRuntime.Parameter(property) == talent.SourceTab)
            { general += value.Roll.Value; }
        }
        // General +skills raises learned/granted skills without spending points
        // or granting every unlearned skill. Specific staffmods may grant a skill.
        return baseline + direct > 0 ? Math.Max(0, baseline + direct + general) : 0;
    }
}
