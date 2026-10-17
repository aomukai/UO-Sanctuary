namespace Sanctuary;
public enum LootRequest : byte { Poll, Open, Take, Close }
public static class LootProtocol
{
    public const ushort SubCommand = 0xF3;
    public const byte Version = 1;
    public const int Range = 5, SightRange = 18, MaxCorpses = 64, MaxItems = 256, PollMs = 500;
}
