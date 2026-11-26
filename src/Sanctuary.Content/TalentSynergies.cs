// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Sanctuary;

internal static partial class TalentSynergies
{
    private static readonly Dictionary<string, (string[] Incoming, string[] Improves)> _relationships = new();

    internal static (string[] Incoming, string[] Improves) For(string id) => _relationships[id];

    [GeneratedRegex("""skill\(\s*['"]([^'"]+)['"]\s*\.\s*(\w+)""", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SkillReferences();

    [GeneratedRegex(@"^pa(?:r)?\d+$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex FixedParameters();

    internal static void Load()
    {
        var skills = SkillMechanics.All.ToArray();
        var bySource = skills.ToDictionary(skill => skill.SourceKey, StringComparer.OrdinalIgnoreCase);
        var incoming = skills.ToDictionary(skill => skill, skill => Incoming(skill, bySource));
        var improves = skills.ToDictionary(skill => skill, _ => new List<SkillDefinition>());
        foreach (var (skill, references) in incoming)
            foreach (var reference in references)
                improves[reference].Add(skill);

        _relationships.Clear();
        foreach (var skill in skills)
            _relationships.Add(skill.TalentId, (Names(incoming[skill]), Names(improves[skill])));
    }

    private static string[] Names(IEnumerable<SkillDefinition> skills) =>
        skills.Select(skill => skill.Name).Order(StringComparer.OrdinalIgnoreCase).ToArray();

    private static HashSet<SkillDefinition> Incoming(SkillDefinition skill, Dictionary<string, SkillDefinition> bySource)
    {
        var references = new HashSet<SkillDefinition>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        Visit(skill.SourceFields);

        if (skill.Ability is CombatAbility.RaiseSkeleton or CombatAbility.RaiseSkeletalMage
            or CombatAbility.ClayGolem or CombatAbility.BloodGolem or CombatAbility.IronGolem or CombatAbility.FireGolem)
            references.Add(SkillMechanics.For(CombatAbility.SummonResist));

        return references;

        void Visit(IReadOnlyDictionary<string, JsonElement> fields)
        {
            foreach (var (field, value) in fields)
            {
                if (value.ValueKind != JsonValueKind.String || value.GetString() is not { Length: > 0 } text)
                    continue;

                foreach (Match reference in SkillReferences().Matches(text))
                {
                    if (!FixedParameters().IsMatch(reference.Groups[2].Value)
                        && bySource.TryGetValue(reference.Groups[1].Value, out var other)
                        && other.CharacterClass == skill.CharacterClass && other != skill)
                        references.Add(other);
                }

                // Include bonuses carried by server-side child missiles, such as Meteor's fire.
                if ((field.StartsWith("srvmissile", StringComparison.OrdinalIgnoreCase)
                        || field.StartsWith("submissile", StringComparison.OrdinalIgnoreCase)
                        || field.StartsWith("hitsubmissile", StringComparison.OrdinalIgnoreCase)
                        || field.Equals("explosionmissile", StringComparison.OrdinalIgnoreCase))
                    && visited.Add(text) && NecromancerSummons.TryFields(text, out var missile))
                    Visit(missile);
            }
        }
    }
}
