// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using ModernUO.Serialization;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Network;

namespace Sanctuary;

public sealed record FortificationSettings(int SchemaVersion, int DropDenominator, int DropTickets, int DropQuantity,
    bool MagicFindAffects, bool GradeAffects, string[] Species, double RequiredSkill, int ChitinCost,
    int MaximumRestored, string Ceiling, bool RestoreCurrent);
public sealed class FortifyRequest
{
    internal PlayerMobile Owner;
    internal Item Powder, Item;
    internal object PowderParent, ItemParent;
    internal int Amount, Uses, Ceiling, Added;
    internal ItemOrigin Origin;
    internal GearState State;
    internal NativeGearDetails Native;
    internal NetState Connection;
    internal bool Claimed;
}

public static class Fortification
{
    public static FortificationSettings Settings { get; private set; }
    private static Type[] _species = [];
    public static void Load()
    {
        var rows = BalanceCatalog.Read<FortificationSettings>("fortification");
        if (rows.Length != 1) { throw new InvalidDataException("One fortification settings row is required."); }
        Settings = rows[0];
        if (Settings is not { SchemaVersion: 1, MagicFindAffects: false, GradeAffects: false, DropQuantity: 1,
                Ceiling: "NaturalWithCraftBonus", RestoreCurrent: false }
            || Settings.DropDenominator != 10000 || Settings.DropTickets is < 0 or > 10000
            || Settings.Species is not { Length: > 0 and <= 100 } || Settings.RequiredSkill is < 0 or > 119
            || Settings.ChitinCost is < 1 or > 1000 || Settings.MaximumRestored is < 1 or > 100)
        { throw new InvalidDataException("Invalid fortification rules."); }
        var seen = new System.Collections.Generic.HashSet<Type>(); _species = new Type[Settings.Species.Length];
        for (int i = 0; i < _species.Length; i++)
        {
            var type = typeof(BaseCreature).Assembly.GetType(Settings.Species[i]);
            if (type == null || type.IsAbstract || !typeof(BaseCreature).IsAssignableFrom(type)
                || typeof(BaseVendor).IsAssignableFrom(type) || !seen.Add(type))
            { throw new InvalidDataException("Invalid chitin species."); }
            _species[i] = type;
        }
    }
    public static void Configure()
    {
        ClassGearHooks.FortifyUsed = (from, powder) =>
        {
            if (!ClassProfessions.Affected(from)) { return false; }
            if (from is PlayerMobile owner) { FortificationGump.Target(owner, powder); }
            return true;
        };
    }
    internal static bool Species(Type type)
    {
        foreach (var basis in _species) { if (basis.IsAssignableFrom(type)) { return true; } }
        return false;
    }
    internal static void Award(RewardLife life, Random random)
    {
        if (Species(life.Creature.GetType()) && random.Next(Settings.DropDenominator) < Settings.DropTickets)
        { life.Corpse.DropItem(ItemOrigins.Create(ItemOrigin.Drop, () => new Chitin(Settings.DropQuantity))); }
    }
    public static int Ceiling(Item item)
    {
        int natural = item switch { BaseWeapon { CanFortify: true } weapon => weapon.InitMaxHits,
            BaseArmor { CanFortify: true } armor => armor.InitMaxHits, BaseClothing { CanFortify: true } clothing => clothing.InitMaxHits,
            BaseJewel jewel => jewel.InitMaxHits, _ => 0 };
        if (natural <= 0 || ClassGear.CosmeticClothing(item)) { return 0; }
        int bonus = ClassGear.State(item) is { CraftedEquipment: true } state ? state.DurabilityPercent : 0;
        return Math.Clamp((int)Math.Ceiling(natural * (100 + bonus) / 100.0) + NamedGear.Bonus(item, "dur"), 0, 65535);
    }
    private static bool PowderReady(PlayerMobile owner, Item powder) => EquipmentRepairs.Ready(owner)
        && RuneCrafting.Accessible(owner, powder) && powder is { IsLockedDown: false, IsSecure: false, Movable: true }
        && (powder is FortifyingPowder { Amount: > 0 } || powder is PowderOfTemperament { UsesRemaining: > 0 });
    private static bool ItemReady(PlayerMobile owner, Item item) => item is { Deleted: false, Amount: 1, Movable: true,
            IsLockedDown: false, IsSecure: false } && RuneCrafting.Accessible(owner, item)
        && (item.BlessedFor == null || item.BlessedFor == owner) && ClassGear.Converted(item)
        && ClassGear.State(item) is not { Identified: false } && !ClassGear.CosmeticClothing(item);
    internal static FortifyRequest Quote(PlayerMobile owner, Item powder, Item item)
    {
        if (!PowderReady(owner, powder) || !ItemReady(owner, item) || NamedGear.Ethereal(item)) { return null; }
        var native = GearDetails.Read(item); int ceiling = Ceiling(item);
        if (native.MaximumDurability is not > 0 || native.Durability is not >= 0 || native.MaximumDurability >= ceiling) { return null; }
        return new() { Owner = owner, Powder = powder, Item = item, PowderParent = powder.Parent, ItemParent = item.Parent,
            Amount = powder.Amount, Uses = (powder as PowderOfTemperament)?.UsesRemaining ?? 0,
            Origin = powder.Origin, Native = native, State = ClassGear.State(item), Connection = owner.NetState,
            Ceiling = ceiling, Added = Math.Min(Settings.MaximumRestored, ceiling - native.MaximumDurability.Value) };
    }
    internal static bool Commit(FortifyRequest q)
    {
        if (q is not { Claimed: false }) { return false; }
        q.Claimed = true;
        if (!ReferenceEquals(q.Connection, q.Owner.NetState) || !PowderReady(q.Owner, q.Powder) || !ItemReady(q.Owner, q.Item)
            || q.Powder.Parent != q.PowderParent || q.Item.Parent != q.ItemParent || q.Powder.Amount != q.Amount
            || q.Powder.Origin != q.Origin || ((q.Powder as PowderOfTemperament)?.UsesRemaining ?? 0) != q.Uses
            || !ReferenceEquals(q.State, ClassGear.State(q.Item)) || GearDetails.Read(q.Item) != q.Native || Ceiling(q.Item) != q.Ceiling)
        { return false; }
        int maximum = q.Native.MaximumDurability.Value + q.Added;
        switch (q.Item)
        {
            case BaseWeapon weapon: weapon.MaxHitPoints = maximum; break;
            case BaseArmor armor: armor.MaxHitPoints = maximum; break;
            case BaseClothing clothing: clothing.MaxHitPoints = maximum; break;
            case BaseJewel jewel: jewel.MaxHitPoints = maximum; break;
            default: return false;
        }
        if (q.Powder is PowderOfTemperament legacy)
        { if (--legacy.UsesRemaining == 0) { legacy.Delete(); } }
        else { q.Powder.Consume(1); }
        q.Item.InvalidateProperties(); q.Owner.PlaySound(0x247);
        q.Owner.SendMessage($"Maximum durability increased by {q.Added}. Current durability is unchanged; ordinary repair is still available.");
        return true;
    }
}

[SerializationGenerator(0)]
public partial class Chitin : Item
{
    [Constructible] public Chitin(int amount = 1) : base(0x26B4)
    { Name = "chitin"; Hue = 0x835; Weight = 0.1; Stackable = true; Amount = amount; }
}

[SerializationGenerator(0)]
public partial class FortifyingPowder : Item
{
    [Constructible] public FortifyingPowder(int amount = 1) : base(0x1006)
    { Name = "powder of fortifying"; Hue = 0x973; Weight = 0.1; Stackable = true; Amount = amount; }
    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);
        list.Add($"{"Adds up to "}{Fortification.Settings.MaximumRestored}{" maximum durability; current durability unchanged."}");
        list.Add($"{"Limited by the equipment's natural durability ceiling, including its crafted bonus."}");
    }
    public override void OnDoubleClick(Mobile from)
    { if (from is PlayerMobile owner) { FortificationGump.Target(owner, this); } }
}
