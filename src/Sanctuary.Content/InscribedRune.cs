// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using ModernUO.Serialization;
using Server;
using Server.Mobiles;

namespace Sanctuary;

// The mould's declared identity is frozen into the rune; later catalogue tuning cannot reroll it.
public sealed record RunePattern(int Tier, string TierName, string StatId, string EffectCode, string StatLabel,
    int Baseline, string SourceAffix, string AffixName, AffixKind AffixKind)
{
    public static RunePattern From(RuneMould mould) => new(mould.Tier, mould.TierName, mould.StatId, mould.EffectCode,
        mould.StatLabel, mould.Baseline, mould.SourceAffix, mould.AffixName, mould.AffixKind);
    public string Name => AffixKind == AffixKind.Suffix ? $"{TierName} Rune {AffixName}" : $"{TierName} {AffixName} Rune";
}

[SerializationGenerator(0)]
public partial class InscribedRune : Item
{
    [SerializableField(0, setter: "private")] private int _tier;
    [SerializableField(1, setter: "private")] private string _tierName;
    [SerializableField(2, setter: "private")] private string _statId;
    [SerializableField(3, setter: "private")] private string _effectCode;
    [SerializableField(4, setter: "private")] private string _statLabel;
    [SerializableField(5, setter: "private")] private int _baseline;
    [SerializableField(6, setter: "private")] private string _sourceAffix;
    [SerializableField(7, setter: "private")] private string _affixName;
    [SerializableField(8, setter: "private")] private AffixKind _affixKind;
    [SerializableField(9, setter: "private")] private CraftQuality _quality;
    public int Percentage => checked(_baseline + _tier - 1 + (int)_quality);
    public RunePattern Pattern => new(_tier, _tierName, _statId, _effectCode, _statLabel, _baseline, _sourceAffix, _affixName, _affixKind);
    [Constructible] public InscribedRune() : this(DefaultPattern(), CraftQuality.Average) { }
    private static RunePattern DefaultPattern()
    {
        var stat = MouldRewards.Stat("cast-rate");
        return new(1, MouldRewards.Tier(1).Name, stat.Id, stat.Code, stat.Label, stat.Baseline,
            stat.SourceAffix, stat.AffixName, stat.AffixKind);
    }
    public InscribedRune(RunePattern pattern, CraftQuality quality) : base(0x1F19)
    {
        _tier = pattern.Tier; _tierName = pattern.TierName; _statId = pattern.StatId; _effectCode = pattern.EffectCode;
        _statLabel = pattern.StatLabel; _baseline = pattern.Baseline; _sourceAffix = pattern.SourceAffix;
        _affixName = pattern.AffixName; _affixKind = pattern.AffixKind; _quality = quality;
        ValidateIdentity(); Name = pattern.Name; Hue = 0x489; Weight = 1; Stackable = false; PlayerConstructed = true;
    }
    public override bool CanStackWith(Item dropped) => false;
    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);
        list.Add($"{_statLabel}{": +"}{Percentage}{"%"}");
        list.Add($"{"Rune tier: "}{_tier}{" / 8 — "}{_tierName}");
        list.Add($"{"Quality: "}{CraftTransactions.QualityName(_quality)}");
        list.Add($"{"Double-click to permanently install in an empty equipment socket."}");
    }
    public override void OnDoubleClick(Mobile from)
    { if (from is PlayerMobile owner) { RuneInsertionGump.Target(owner, this); } }
    [AfterDeserialization]
    private void ValidateIdentity()
    {
        if (_tier is < 1 or > 8 || !Enum.IsDefined(_quality) || !Enum.IsDefined(_affixKind)
            || _baseline is < 1 or > 10000 || !MouldRewards.LegalSavedStat(_statId, _effectCode)
            || string.IsNullOrWhiteSpace(_tierName) || string.IsNullOrWhiteSpace(_statLabel)
            || string.IsNullOrWhiteSpace(_sourceAffix) || string.IsNullOrWhiteSpace(_affixName))
        { throw new InvalidDataException("Invalid saved rune identity."); }
    }
    internal void SetQuality(CraftQuality quality)
    {
        if (!Enum.IsDefined(quality)) { throw new ArgumentOutOfRangeException(nameof(quality)); }
        _quality = quality; InvalidateProperties();
    }
    internal static void Copy(Item source, Item destination)
    {
        if (source is not InscribedRune a || destination is not InscribedRune b) { return; }
        b._tier = a._tier; b._tierName = a._tierName; b._statId = a._statId; b._effectCode = a._effectCode;
        b._statLabel = a._statLabel; b._baseline = a._baseline; b._sourceAffix = a._sourceAffix;
        b._affixName = a._affixName; b._affixKind = a._affixKind; b._quality = a._quality; b.InvalidateProperties();
    }
}
