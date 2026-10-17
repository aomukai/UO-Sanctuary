namespace Sanctuary;

public enum CharacterStat : byte { Strength, Dexterity, Energy, Vitality }
public enum StatField
{
    Strength, Dexterity, Energy, Vitality, Points, Level, AttackRating, Defense,
    DamageMin, DamageMax, Ias, Fcr, LifePerVitality4, ManaPerEnergy4,
    StaminaPerVitality4, Class, LifePerLevel4, ManaPerLevel4, StaminaPerLevel4, TalentPoints, RunPenalty, RunBonus, Count
}
public static class StatProtocol
{
    public const ushort SubCommand = 0xF5;
    public const byte Version = 4;
    public const int PollMs = 500;
    public const int Values = (int)StatField.Count;
}
