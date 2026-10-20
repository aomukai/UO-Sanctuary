// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using ModernUO.Serialization;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Network;

namespace Sanctuary;

public sealed record PotionBeltBase(string Type, string Code, int Rows);
public sealed record RestorationBase(RestorationKind Kind, int Tier, string Name, int Amount, int DurationMs, int Graphic, int Hue);
public static class PotionBeltCatalog
{
    private static readonly Dictionary<string, PotionBeltBase> _belts = new(StringComparer.Ordinal);
    private static readonly Dictionary<(RestorationKind, int), RestorationBase> _potions = new();
    internal static IEnumerable<PotionBeltBase> Belts => _belts.Values;
    public static PotionBeltBase Belt(Item item) => item == null ? null : _belts.GetValueOrDefault(item.GetType().Name);
    public static RestorationBase Potion(RestorationKind kind, int tier) => _potions.TryGetValue((kind, tier), out var row)
        ? row : throw new ArgumentOutOfRangeException(nameof(tier));
    public static void Load()
    {
        _belts.Clear();
        _potions.Clear();
        foreach (var row in BalanceCatalog.Read<PotionBeltBase>("potion-belts"))
        {
            if (row.Rows is < 2 or > 4 || EquipmentBalance.Base(row.Code).Type != "belt")
            {
                throw new InvalidDataException("Invalid potion belt base.");
            }
            _belts.Add(row.Type, row);
            EquipmentBalance.AddProjectBelt(row.Type, row.Code);
        }
        foreach (var row in BalanceCatalog.Read<RestorationBase>("restoration-potions"))
        {
            if (!Enum.IsDefined(row.Kind) || row.Tier is < 1 or > 5 || row.Amount < 1
                || (row.Kind is RestorationKind.Healing or RestorationKind.Mana ? row.DurationMs is < 100 or > 60000
                    : row.Kind == RestorationKind.CombatBuff ? row.DurationMs != 30000 || row.Amount is < 1 or > 100
                    : row.DurationMs != 0 || row.Amount is < 1 or > 100 || (row.Kind == RestorationKind.Rejuvenation ? row.Tier > 2 : row.Tier != 1)))
            {
                throw new InvalidDataException("Invalid restoration potion.");
            }
            _potions.Add((row.Kind, row.Tier), row);
        }
        if (_belts.Count != 5 || _potions.Count != 19)
        {
            throw new InvalidDataException("Potion belt catalog is incomplete.");
        }
    }
}

[SerializationGenerator(0)]
public abstract partial class PotionTabard : Surcoat
{
    protected PotionTabard(string name) { Name = name; }
    public override bool CanEquip(Mobile from) => from is PlayerMobile player && ClassCombat.Enabled(player) && base.CanEquip(from);
    public override void OnDoubleClick(Mobile from)
    {
        if (from is PlayerMobile player && Parent == player)
        {
            PotionBeltNetwork.Send(player);
            player.SendMessage("Potions: 1-4. Use the belt arrow to show its rows. Empty slots load from your backpack.");
        }
        else
        {
            base.OnDoubleClick(from);
        }
    }
    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);
        int rows = PotionBeltCatalog.Belt(this)?.Rows ?? 1;
        list.Add($"{"Potion belt: "}{rows}{" rows of four"}");
    }
}
[SerializationGenerator(0)]
public partial class PotionSash : PotionTabard
{
    [Constructible] public PotionSash() : base("Sash tabard") { }
}
[SerializationGenerator(0)]
public partial class PotionLightBelt : PotionTabard
{
    [Constructible] public PotionLightBelt() : base("Light belt tabard") { }
}
[SerializationGenerator(0)]
public partial class PotionBelt : PotionTabard
{
    [Constructible] public PotionBelt() : base("Belt tabard") { }
}
[SerializationGenerator(0)]
public partial class PotionHeavyBelt : PotionTabard
{
    [Constructible] public PotionHeavyBelt() : base("Heavy belt tabard") { }
}
[SerializationGenerator(0)]
public partial class PotionPlatedBelt : PotionTabard
{
    [Constructible] public PotionPlatedBelt() : base("Plated belt tabard") { }
}

[SerializationGenerator(0)]
public partial class RestorationPotion : BasePotion
{
    [SerializableField(0, setter: "private")] private RestorationKind _kind;
    [SerializableField(1, setter: "private")] private int _tier;
    [Constructible] public RestorationPotion() : this(RestorationKind.Healing, 1) { }
    public RestorationPotion(RestorationKind kind, int tier) : base(0xF0C, PotionEffect.HealLesser)
    {
        _kind = kind;
        _tier = tier;
        var row = PotionBeltCatalog.Potion(kind, tier);
        Name = row.Name;
        ItemID = row.Graphic;
        Hue = row.Hue;
        Stackable = true;
    }
    public override bool CanStackWith(Item dropped) => dropped is RestorationPotion other
        && other.Kind == Kind && other.Tier == Tier && base.CanStackWith(dropped);
    public override bool RequireFreeHand => false;
    public override bool CanDrink(Mobile from) => from is PlayerMobile player && PotionRestoration.CanUse(player, this);
    public override void Drink(Mobile from)
    {
        if (from is PlayerMobile player)
        {
            PotionRestoration.Drink(player, this);
        }
    }
    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);
        var row = PotionBeltCatalog.Potion(Kind, Tier);
        if (row.Kind == RestorationKind.CombatBuff)
        {
            list.Add($"{CombatPotions.Description(row.Tier)}");
            list.Add($"{"One potion buff; +30 seconds, at most90 seconds. A different buff replaces it."}");
        }
        else if (row.DurationMs > 0)
        {
            list.Add($"{"Restores over "}{row.DurationMs / 1000.0:0.00}{" seconds"}");
            list.Add($"{"Class-scaled amount; Vitality/Energy can double restoration"}");
        }
        else if (row.Kind == RestorationKind.Rejuvenation) { list.Add($"{"Instantly restores "}{row.Amount}{"% of maximum life and mana"}"); }
        else if (row.Kind == RestorationKind.InstantHealing) { list.Add($"{"Instantly restores "}{row.Amount}{"% of maximum life"}"); }
        else { list.Add($"{"Instantly restores "}{row.Amount}{"% of maximum mana"}"); }
    }
}

// Real items stay in the normal world-save graph. This holder is not an extra
// backpack or transferable container; all access uses the validated belt service.
[SerializationGenerator(0)]
public partial class PotionBeltStorage : Item
{
    [Constructible]
    public PotionBeltStorage() : base(0xE75)
    {
        Name = "Belt contents";
        Movable = Visible = false;
    }
    public override double DefaultWeight => 0;
    public override DeathMoveResult OnParentDeath(Mobile parent) => DeathMoveResult.RemainEquipped;
    public override bool CheckLift(Mobile from, Item item, ref LRReason reject) => false;
}
