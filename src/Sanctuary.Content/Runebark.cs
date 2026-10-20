// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using ModernUO.Serialization;
using Server;

namespace Sanctuary;

[SerializationGenerator(0)]
public partial class Runebark : Item
{
    [SerializableField(0, setter: "private")] private int _tier;
    [Constructible] public Runebark() : this(1) { }
    public Runebark(int tier, int amount = 1) : base(0x318F)
    {
        if (tier is < 1 or > 8 || amount is < 1 or > 60000) { throw new ArgumentOutOfRangeException(nameof(tier)); }
        _tier = tier; Name = $"{RunebarkHarvesting.Tiers[tier - 1].Name} Runebark";
        Stackable = true; Amount = amount; Weight = .1; Hue = 0x835 + tier - 1;
    }
    public int SellPrice => RunebarkHarvesting.Tiers[_tier - 1].SellPrice;
    public override bool CanStackWith(Item dropped) => dropped is Runebark other && other._tier == _tier && base.CanStackWith(dropped);
    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);
        list.Add($"{"Runebark tier: "}{_tier}{" / 8"}");
        list.Add($"{"Suitable for this rune tier or any lower tier."}");
    }
    [AfterDeserialization]
    private void ValidateTier()
    { if (_tier is < 1 or > 8) { throw new InvalidDataException("Invalid saved Runebark tier."); } }
    internal static void Copy(Item source, Item destination)
    {
        if (source is Runebark a && destination is Runebark b) { b._tier = a._tier; b.InvalidateProperties(); }
    }
}
