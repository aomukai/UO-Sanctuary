using System.Text.Json.Serialization;

namespace Sanctuary;

public static class ClassGumpProtocol
{
    // ModernUO's deterministic type hash for Sanctuary.TalentGump.
    public const uint Talents = 0x54126F85;
    public const string MenuPrefix = "SanctuaryTalents1:";
    public const int Apply = 2100;
    public const ushort AllocationEntry = 2100;
    public const int MouseSkills = 2000;
    public const int Pets = 2001;
    public const int SkillsPerTree = 10;
    public const ushort PreviewSubCommand = 0xFB;
    public const byte PreviewVersion = 1;
    public const int SkillCount = 3 * SkillsPerTree;
    public const int PreviewRequestLength = 11 + SkillCount;
    public const int PreviewReplyHeader = 10;
    public const int MaxPreviewBytes = 1024;
}

public sealed record TalentMenuData(string Title, int Level, int Points, long Experience, long NextLevel,
    int MaxRank, string[] Trees, TalentMenuSkill[] Skills);

public sealed record TalentMenuSkill(string Name, int Icon, int Level, int Learned,
    int[] Prerequisites, string Description, string Mechanics);

[JsonSerializable(typeof(TalentMenuData))]
internal sealed partial class TalentMenuJsonContext : JsonSerializerContext { }
