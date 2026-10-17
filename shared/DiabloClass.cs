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
    Assassin = 107
}

public static class DiabloClasses
{
    public static bool TryFromWire(int value, out DiabloClass characterClass)
    {
        characterClass = (DiabloClass)value;
        return value >= (int)DiabloClass.Amazon && value <= (int)DiabloClass.Assassin;
    }
}
