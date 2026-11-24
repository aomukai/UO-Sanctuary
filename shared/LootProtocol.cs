namespace Sanctuary;
public enum LootRequest : byte { Poll, Open, Take, Close, TakeChestGold }
public static class LootProtocol
{
    public const ushort SubCommand = 0xF3;
    public const byte Version = 1;
    // A crypt-chest announcement carries only the version and chest serial.
    public const int ChestAnnouncementLength = 5;
    public const int Range = 5, SightRange = 18, MaxCorpses = 64, MaxItems = 256, PollMs = 500;
}
