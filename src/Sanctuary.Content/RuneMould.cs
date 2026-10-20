// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using ModernUO.Serialization;
using Server;
using Server.Items;

namespace Sanctuary;

[SerializationGenerator(0)]
public partial class RuneMould : Item
{
    [SerializableField(0, setter: "private")] private int _tier;
    [SerializableField(1, setter: "private")] private string _statId;
    [SerializableField(2, setter: "private")] private string _effectCode;
    [SerializableField(3, setter: "private")] private int _baseline;
    [SerializableField(4, setter: "private")] private string _statLabel;
    [SerializableField(5, setter: "private")] private string _sourceAffix;
    [SerializableField(6, setter: "private")] private string _affixName;
    [SerializableField(7, setter: "private")] private AffixKind _affixKind;
    [SerializableField(8, setter: "private")] private string _tierName;
    [SerializableField(9, setter: "private")] private int _sourceLevel;
    [SerializableField(10, setter: "private")] private MonsterDifficulty _difficulty;
    [SerializableField(11, setter: "private")] private MouldGrade _grade;
    [Constructible] public RuneMould() : this(1, "cast-rate", 20, MonsterDifficulty.Normal) { }
    public RuneMould(int tier, string statId, int sourceLevel, MonsterDifficulty difficulty, MouldGrade grade = MouldGrade.Ordinary) : base(0x1F14)
    {
        if (MouldRewards.TierForLevel(sourceLevel) != tier || tier == 0 || !Enum.IsDefined(difficulty) || !Enum.IsDefined(grade))
        { throw new ArgumentException("Mould tier must match its source monster level."); }
        var stat = MouldRewards.Stat(statId); var band = MouldRewards.Tier(tier);
        _tier = tier; _tierName = band.Name; _statId = stat.Id; _effectCode = stat.Code;
        _baseline = stat.Baseline; _statLabel = stat.Label; _sourceAffix = stat.SourceAffix;
        _affixName = stat.AffixName; _affixKind = stat.AffixKind; _sourceLevel = sourceLevel;
        _difficulty = difficulty; _grade = grade;
        Name = stat.AffixKind == AffixKind.Suffix ? $"{band.Name} Mould {stat.AffixName}" : $"{band.Name} {stat.AffixName} Mould";
        Weight = 1; Hue = 0x835; Stackable = false;
    }
    public int Percentage(CraftQuality quality)
    {
        if (!Enum.IsDefined(quality)) { throw new ArgumentOutOfRangeException(nameof(quality)); }
        return checked(_baseline + _tier - 1 + (int)quality);
    }
    public override bool CanStackWith(Item dropped) => false;
    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);
        list.Add($"{"Declared rune stat: "}{_statLabel}");
        list.Add($"{"Mould tier: "}{_tier}{" / 8 — "}{_tierName}");
        list.Add($"{"Rune percentages (Average / High / Superior): "}{Percentage(CraftQuality.Average)}{"% / "}{Percentage(CraftQuality.HighQuality)}{"% / "}{Percentage(CraftQuality.Superior)}{"%"}");
        list.Add($"{"Source monster: Level "}{_sourceLevel}{", "}{_difficulty}{" / "}{_grade}");
        list.Add($"{"Single-use Inscription pattern. Use Craft rune on a scribe’s pen."}");
    }
    public override void OnDoubleClick(Mobile from)
    {
        if (!from.InRange(GetWorldLocation(), 2)) { from.SendLocalizedMessage(500446); return; }
        from.SendMessage($"This mould declares {_statLabel}. Use Craft rune on a scribe’s pen at a public desk. Nothing is consumed here.");
    }
    [AfterDeserialization]
    private void ValidateIdentity()
    {
        if (_tier is < 1 or > 8 || MouldRewards.TierForLevel(_sourceLevel) != _tier || !Enum.IsDefined(_difficulty)
            || !Enum.IsDefined(_grade) || !Enum.IsDefined(_affixKind) || _baseline is < 1 or > 10000
            || !MouldRewards.LegalSavedStat(_statId, _effectCode) || string.IsNullOrWhiteSpace(_statLabel)
            || string.IsNullOrWhiteSpace(_sourceAffix) || string.IsNullOrWhiteSpace(_affixName)
            || string.IsNullOrWhiteSpace(_tierName)) { throw new InvalidDataException("Invalid saved rune mould identity."); }
    }
    internal static void Copy(Item source, Item destination)
    {
        if (source is not RuneMould a || destination is not RuneMould b) { return; }
        b._tier = a._tier; b._statId = a._statId; b._effectCode = a._effectCode; b._baseline = a._baseline;
        b._statLabel = a._statLabel; b._sourceAffix = a._sourceAffix; b._affixName = a._affixName;
        b._affixKind = a._affixKind; b._tierName = a._tierName; b._sourceLevel = a._sourceLevel;
        b._difficulty = a._difficulty; b._grade = a._grade; b.InvalidateProperties();
    }
}
