using System;

namespace Sanctuary;

// Reserved creation-packet identifiers; these must not overlap stock UO professions.
public enum DiabloClass : byte
{
    Amazon = 101,
    Sorceress = 102,
    Necromancer = 103,
    Paladin = 104,
    Barbarian = 105,
    Druid = 106,
    Assassin = 107,
    Warlock = 108
}

public static class DiabloClasses
{
    public static string DisplayName(DiabloClass characterClass, bool female) => characterClass switch
    {
        DiabloClass.Amazon => female ? "Amazon" : "Gargarean",
        DiabloClass.Sorceress => female ? "Sorceress" : "Sorcerer",
        DiabloClass.Necromancer => "Necromancer",
        DiabloClass.Paladin => "Paladin",
        DiabloClass.Barbarian => "Barbarian",
        DiabloClass.Druid => female ? "Druidess" : "Druid",
        DiabloClass.Assassin => "Assassin",
        DiabloClass.Warlock => female ? "Witch" : "Warlock",
        _ => throw new ArgumentOutOfRangeException(nameof(characterClass))
    };

    // Class selection precedes the appearance/sex controls.
    public static string SelectionName(DiabloClass characterClass) => characterClass switch
    {
        DiabloClass.Amazon => "Gargarean / Amazon",
        DiabloClass.Sorceress => "Sorcerer / Sorceress",
        DiabloClass.Druid => "Druid / Druidess",
        DiabloClass.Warlock => "Warlock / Witch",
        _ => DisplayName(characterClass, false)
    };

    public static bool TryFromDisplayName(ReadOnlySpan<char> name, out DiabloClass characterClass)
    {
        for (int id = (int)DiabloClass.Amazon; id <= (int)DiabloClass.Warlock; id++)
        {
            var candidate = (DiabloClass)id;
            if (name.SequenceEqual(DisplayName(candidate, false)) || name.SequenceEqual(DisplayName(candidate, true)))
            {
                characterClass = candidate;
                return true;
            }
        }
        characterClass = default;
        return false;
    }

    public static bool TryFromWire(int value, out DiabloClass characterClass)
    {
        characterClass = (DiabloClass)value;
        return value >= (int)DiabloClass.Amazon && value <= (int)DiabloClass.Warlock;
    }
}
