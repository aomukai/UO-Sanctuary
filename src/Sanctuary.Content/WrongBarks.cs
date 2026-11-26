// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using Server.Mobiles;

namespace Sanctuary;

internal static class WrongBarks
{
    private sealed record CookLines(string Opening, string[] Combat, string Death);
    private static readonly CookLines[] _cooks =
    [
        new("How fortunate. I was just lamenting the limitations of our pantry.",
            [
                "Rosemary, perhaps. Though I must first establish what you’ve been eating.",
                "Please refrain from perspiring. I prefer to administer the salt myself.",
                "An adventurous bouquet. Have you recently been resurrected?",
                "I assure you, the marinade will accomplish what your upbringing could not."
            ], "My notes… do not let Uruk… improvise…"),
        new("Welcome. Have you a preference regarding doneness? I shall, of course, exercise my own judgement.",
            [
                "A brief sear, followed by a period of rest. The latter should come naturally to you.",
                "Do stop moving. An uneven crust reflects poorly upon the establishment.",
                "Well-done? Certainly not. We are civilised.",
                "Wonk would drown you in sauce. A tragic lack of confidence in the ingredients."
            ], "Take me… off the heat…"),
        new("An intriguing specimen. Let us discover where your merits lie.",
            [
                "The shoulder suggests a rustic preparation. The cheeks, however, show considerable promise.",
                "Something acidic to accompany you. You strike me as rather rich.",
                "I should serve you in several courses. It would be discourteous to overlook your versatility.",
                "Uruk sees a meal. I see a tasting menu."
            ], "Such talent… wasted on… an ungrateful palate…")
    ];

    private static CookLines Lines(WrongEncounter actor) => _cooks[actor.Role - WrongRole.Wonk];
    internal static void CookOpening(WrongEncounter actor) => Say(actor, "opening", Lines(actor).Opening);
    internal static void CookDeath(WrongEncounter actor) => Say(actor, "death", Lines(actor).Death);
    internal static void CookCombat(WrongEncounter actor)
    {
        var lines = Lines(actor).Combat;
        int index = Random.Shared.Next(lines.Length);
        Say(actor, $"combat-{index + 1}", lines[index]);
    }

    internal static void Say(WrongEncounter actor, string id, string text)
    {
        string speaker = $"wrong.{actor.Role.ToString().ToLowerInvariant()}";
        AuthoredWriting.Bark(actor, $"{speaker}.{id}", speaker, text);
    }
}

internal sealed class WrongDeduction
{
    private static readonly string[] _ordinals =
    [
        "", "first", "second", "third", "fourth", "fifth", "sixth", "seventh", "eighth", "ninth", "tenth",
        "eleventh", "twelfth", "thirteenth", "fourteenth", "fifteenth", "sixteenth", "seventeenth",
        "eighteenth", "nineteenth"
    ];
    private static readonly string[] _decades = ["", "", "twenty", "thirty", "forty", "fifty", "sixty", "seventy", "eighty", "ninety"];

    private readonly List<SkillDefinition> _seen = new(2);
    private bool _raceSpoken, _skillSpoken, _classSpoken, _levelSpoken, _concluded;
    private readonly long _starting = Server.Core.TickCount;

    internal WrongDeduction(PlayerMobile subject) => Subject = subject;
    internal PlayerMobile Subject { get; }
    internal bool SawSkeleton { get; set; }

    internal void Observe(CombatAbility ability)
    {
        if (_seen.Count >= 2 || !SkillMechanics.TryFor(ability, out var skill)
            || !ClassCreation.Profiles.TryGet(Subject.Serial, out var actual))
            return;

        // Item-granted off-class abilities cannot establish someone's training.
        if (skill.CharacterClass == actual.Class && ClassCreation.Profiles.GetRank(Subject.Serial, skill.TalentId) > 0
            && !_seen.Contains(skill))
            _seen.Add(skill);
    }

    internal (string Id, string Text)? Next(long now)
    {
        if (_concluded)
            return null;
        if (!_raceSpoken && !Subject.IsBodyMod)
        {
            _raceSpoken = true;
            if (Subject.Race == Server.Race.Human)
                return ("observe-human", "Rounded ears, no horns. Human. An elementary beginning.");
            if (Subject.Race == Server.Race.Elf)
                return ("observe-elf", "Pointed ears. Elven ancestry. Let us proceed beyond the obvious.");
            if (Subject.Race == Server.Race.Gargoyle)
                return ("observe-gargoyle", "Horns and wings. Gargoyle. I shall spare us both the pretence that this required deduction.");
        }

        if (!_skillSpoken && _seen.Count > 0)
        {
            _skillSpoken = true;
            if (_seen[0].TalentId == "paladin.zeal" && _seen.Count == 2 && _seen[1].TalentId == "paladin.holyshield")
                return ("observe-zeal-holy-shield", "Zeal, followed by Holy Shield. Your training is becoming quite apparent.");
            return ($"observe-{_seen[0].TalentId}", $"{_seen[0].Name}. A most instructive choice.");
        }

        if (!_classSpoken && _seen.Count == 2 && _seen[0].CharacterClass == _seen[1].CharacterClass)
        {
            _classSpoken = true;
            var characterClass = _seen[0].CharacterClass;
            if (characterClass == DiabloClass.Necromancer && SawSkeleton)
                return ("observe-class-necromancer-summons", "A necromancer, then. Your companions had already suggested as much.");
            string name = DiabloClasses.DisplayName(characterClass, Subject.Female).ToLowerInvariant();
            string article = "aeiou".Contains(name[0]) ? "An" : "A";
            return ($"observe-class-{name}", $"{article} {name}, then. Two demonstrations are quite sufficient.");
        }

        if (!_levelSpoken && (now - _starting >= 5200 || _classSpoken))
        {
            _levelSpoken = true;
            if (ClassCreation.Profiles.TryGet(Subject.Serial, out var actual) && actual.Level is >= 1 and <= 99)
                return ($"observe-level-{actual.Level}", $"I would place your development at precisely the {Ordinal(actual.Level)} degree. I take some pride in these assessments.");
        }
        if (!_concluded && (_classSpoken && _levelSpoken || now - _starting >= 12000))
        {
            _concluded = true;
            return ("observe-conclusion", "Excellent. You shall provide my darlings with a suitably demanding practical examination.");
        }

        return null;
    }

    private static string Ordinal(int value)
    {
        if (value < _ordinals.Length)
            return _ordinals[value];
        string decade = _decades[value / 10];
        return value % 10 == 0 ? decade[..^1] + "ieth" : $"{decade}-{_ordinals[value % 10]}";
    }
}
