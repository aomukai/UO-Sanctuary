namespace Sanctuary;

public enum SkillBindingRequest : byte { Poll, Left, Right, Quick, Use }
public static class SkillBindingProtocol
{
    public const ushort SubCommand = 0xF7;
    public const byte LegacyVersion = 2, Version = 3;
    public const int Slots = 12, PollMs = 250, RequestLength = 14, UseLength = 23;
    public const int LegacyStateLength = 25 + ClassAbilities.MaskBytes, StateLength = LegacyStateLength + 2;
}
