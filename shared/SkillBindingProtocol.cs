namespace Sanctuary;

public enum SkillBindingRequest : byte { Poll, Left, Right, Quick, Use }
public readonly record struct NecromancerSummonCounts(byte Warriors, byte Mages, byte Revives, CombatAbility? Golem = null);
public readonly record struct DruidSummonCounts(byte Ravens, byte SpiritWolves, byte DireWolves);
public readonly record struct WarlockSummonCounts(byte LesserDaemons, byte Succubi, byte Defilers, byte BoundDemons);

public static class SkillBindingProtocol
{
    public const ushort SubCommand = 0xF7;
    public const byte LegacyVersion = 2, Version = 3;
    public const byte SummonStateVersion = 4, ExtendedSummonStateVersion = 5;
    public const int SummonCountsPollFlag = 1;
    // Legacy servers validate but otherwise ignore the ability byte in a poll.
    public const CombatAbility ExtendedSummonCountsCapability = CombatAbility.BasicAttack;
    public const int Slots = 12, PollMs = 250, RequestLength = 14, UseLength = 23;
    public const int LegacyStateLength = 25 + ClassAbilities.MaskBytes, StateLength = LegacyStateLength + 2;
    // The three count bytes describe the class named in the state packet.
    public const int SummonStateLength = StateLength + 3;
    public const int ExtendedSummonStateLength = StateLength + 4;
}
