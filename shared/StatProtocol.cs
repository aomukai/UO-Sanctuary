namespace Sanctuary;

public enum CharacterStat : byte { Strength, Dexterity, Energy, Vitality }
public enum StatField
{
    Strength, Dexterity, Energy, Vitality, Points, Level, AttackRating, Defense,
    DamageMin, DamageMax, Ias, Fcr, LifePerVitality4, ManaPerEnergy4,
    StaminaPerVitality4, Class, LifePerLevel4, ManaPerLevel4, StaminaPerLevel4, TalentPoints, RunPenalty, RunBonus, MagicFind, FoodLight, Count
}
public static class StatProtocol
{
    public const ushort SubCommand = 0xF5;
    public const byte LegacyVersion = 6;
    public const byte Version = 7;
    public const int MatchupNameBytes = 160;
    public const int MatchupFields = 6;
    public const int PollMs = 500;
    public const byte TradeSkillsRequest = 5;
    public const ushort ExperienceSubCommand = 0xFD;
    public const int ExperiencePacketLength = 18;
    public const int Values = (int)StatField.Count;
}

public enum CompanionEquipmentStyle : byte { Crossbow, Sword, Bow, MaceAndShield }
public enum CompanionStatRequest : byte { Snapshot, OpenSheet, Vitals }

public enum CompanionStatField
{
    Level, Strength, Dexterity, Life, MaximumLife, DamageMin, DamageMax, AttackRating, Defense,
    AttackSpeed, RecoveryFrames, Fire, Cold, Poison, Lightning, BlockChance, DodgeChance, AvoidChance, EvadeChance, Count
}

public readonly record struct CompanionSkillStat(CombatAbility Ability, int Rank, int First = 0, int Second = 0, int Third = 0);

public static class CompanionStatProtocol
{
    public const ushort SubCommand = 0xFF;
    public const byte LegacyVersion = 1;
    public const byte Version = 2;
    public const byte VitalsVersion = 3;
    public const int RequestLength = 11;
    public const int Values = (int)CompanionStatField.Count;
    public const int SnapshotLength = 11 + Values * 4;
    public const int VitalsLength = 5 + 1 + 4 + 2 + 6 * 4;
    public const int SkillLength = 14;
    public const int MaxSkills = 6;
    public const int ResistanceCap = 75;
}
