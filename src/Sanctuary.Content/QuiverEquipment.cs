// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using ModernUO.Serialization;
using Server;
using Server.Items;
using Server.Mobiles;

namespace Sanctuary;

public enum QuiverElement : byte { None, Fire, Cold, Lightning, Poison, Magic }

[SerializationGenerator(0)]
[Flippable(0x2FB7, 0x3171)]
public partial class SanctuaryQuiver : ElvenQuiver
{
    [SerializableField(0)] private QuiverElement _element;
    [SerializableField(1)] private CraftResource _resource;

    [Constructible]
    public SanctuaryQuiver()
    {
        Name = "quiver";
        DamageIncrease = 0;
        Resource = CraftResource.RegularLeather;
    }

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);
        list.Add($"+{QuiverEquipment.AttackBonus(this)}% bow and crossbow attack rating");
        if (Element != QuiverElement.None)
            list.Add($"Converts 10% of physical damage to {Element.ToString().ToLowerInvariant()}");
    }
}

internal static class QuiverEquipment
{
    private static readonly string[] Codes = ["qvr", "qv2", "qv3"];
    private const string TypeCode = "sqvr";
    internal const string NormalCode = "qvr";
    internal const int SocketLimit = 2;
    internal const int DropRoll = 20;

    internal static void Register(Dictionary<string, GearItemType> types, Dictionary<string, NormalBase> bases,
        Dictionary<string, GearDefinition> gear)
    {
        types.Add(TypeCode, new(TypeCode, [], false, false, null));
        foreach (var tier in Enum.GetValues<GearTier>())
        {
            int qualityLevel = tier switch { GearTier.Normal => 1, GearTier.Exceptional => 60, _ => 80 };
            string name = tier == GearTier.Normal ? "Quiver" : $"{tier} Quiver";
            bases.Add(Codes[(int)tier], new(Codes[(int)tier], name, TypeCode, qualityLevel, 0, 0,
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, SocketLimit, tier.ToString()));
        }
        gear.Add(nameof(SanctuaryQuiver), new(nameof(SanctuaryQuiver), "Quiver", TypeCode, GearWeight.None,
            "Normal", false, SocketLimit, "Sanctuary quiver using native Elven Quiver art", null, GearSlot.Quiver, NormalCode, true));
    }

    internal static readonly EquipmentCraftScope CraftScope = new(nameof(SanctuaryQuiver), GearMaterial.Leather, "Tailored quiver", null);
    internal static readonly EquipmentRecipe[] Recipes =
    [
        new("quiver/normal", nameof(SanctuaryQuiver), "Quiver", SkillName.Tailoring, GearTier.Normal, NormalCode, 65, 115, [new("Leather", 28)]),
        new("quiver/exceptional", nameof(SanctuaryQuiver), "Quiver", SkillName.Tailoring, GearTier.Exceptional, Codes[1], 90, 140, [new("Leather", 34)]),
        new("quiver/elite", nameof(SanctuaryQuiver), "Quiver", SkillName.Tailoring, GearTier.Elite, Codes[2], 110, 160, [new("Leather", 40)])
    ];

    internal static bool IsDrop(string leaf) => leaf is "aqv" or "cqv";

    internal static bool BowCarrier(BaseCreature creature, Corpse corpse)
    {
        static bool IsBow(Item item) => item is BaseRanged { Deleted: false } and not BaseThrown;

        // Movable gear may already be on the corpse when rewards are rolled.
        foreach (var item in creature.Items)
            if (IsBow(item))
                return true;
        return creature.Backpack?.FindItemByType<BaseRanged>(predicate: IsBow) != null
            || corpse?.FindItemByType<BaseRanged>(predicate: IsBow) != null;
    }

    internal static SanctuaryQuiver Drop(int level, Random random)
    {
        var tier = level >= 80 ? GearTier.Elite : level >= 60 ? GearTier.Exceptional : GearTier.Normal;
        var item = ItemOrigins.Create(ItemOrigin.Drop, () => new SanctuaryQuiver());
        try
        {
            item.Element = (QuiverElement)random.Next((int)QuiverElement.Magic + 1);
            ClassGear.Profiles.FromMonster(item, level, GearRarity.Normal, Codes[(int)tier], tier, random);
            string name = item.Element == QuiverElement.None ? "quiver" : $"{item.Element.ToString().ToLowerInvariant()} quiver";
            item.Name = tier == GearTier.Normal ? name : $"{tier} {name}";
            return item;
        }
        catch { item.Delete(); throw; }
    }

    private static bool UsesQuiver(BaseWeapon weapon) =>
        CombatTiming.Weapon(weapon).Family is AttackFamily.Bow or AttackFamily.Crossbow;

    private static SanctuaryQuiver Equipped(Mobile owner) => owner switch
    {
        PlayerMobile player when !DruidFormAttacks.Active(player) && UsesQuiver(CombatTiming.Equipped(player))
            && player.FindItemOnLayer(Layer.Cloak) is SanctuaryQuiver item && ClassGear.Active(player, item) => item,
        BaseCreature companion when companion is IClassCompanion && MercenaryCare.CompanionLevel(companion).HasValue
            && companion.Weapon is BaseRanged weapon && UsesQuiver(weapon)
            && companion.FindItemOnLayer(Layer.Cloak) is SanctuaryQuiver item && CompanionBuild.Worn(item) => item,
        _ => null
    };

    internal static int AttackBonus(SanctuaryQuiver item) => 5 * (1 + (int)(ClassGear.State(item)?.Tier ?? GearTier.Normal));
    internal static int AttackPercent(Mobile owner) => Equipped(owner) is { } item ? AttackBonus(item) : 0;
    internal static QuiverElement Element(Mobile owner) => Equipped(owner)?.Element ?? QuiverElement.None;

    internal static GearDamage Convert(GearDamage damage, QuiverElement element)
    {
        if (element == QuiverElement.None)
            return damage;

        double converted = (NamedElements.To256(damage.Physical) / 10) / 256.0;
        damage = damage with { Physical = damage.Physical - converted };
        return element switch
        {
            QuiverElement.Fire => damage with { Fire = damage.Fire + converted },
            QuiverElement.Cold => damage with { Cold = damage.Cold + converted },
            QuiverElement.Lightning => damage with { Lightning = damage.Lightning + converted },
            QuiverElement.Poison => damage with { ImpactPoison = damage.ImpactPoison + converted },
            QuiverElement.Magic => damage with { Magic = damage.Magic + converted },
            _ => throw new ArgumentOutOfRangeException(nameof(element))
        };
    }
}
