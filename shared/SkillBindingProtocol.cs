namespace Sanctuary;

public enum SkillBindingRequest : byte { Poll, Left, Right, Quick, Use }
public readonly record struct NecromancerSummonCounts(byte Warriors, byte Mages, byte Revives);

public static class SkillBindingProtocol
{
    public const ushort SubCommand = 0xF7;
    public const byte LegacyVersion = 2, Version = 3;
    public const byte SummonStateVersion = 4;
    // The slot byte is otherwise unused in polls. Older servers ignore it there.
    public const int SummonCountsPollFlag = 1;
    public const int Slots = 12, PollMs = 250, RequestLength = 14, UseLength = 23;
    public const int LegacyStateLength = 25 + ClassAbilities.MaskBytes, StateLength = LegacyStateLength + 2;
    public const int SummonStateLength = StateLength + 3;
}
