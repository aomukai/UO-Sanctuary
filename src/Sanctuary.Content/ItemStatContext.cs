// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using Server;
namespace Sanctuary;

// One original item's existing numeric suppliers, evaluated for a declared
// recipient. This does not combine actor stat lists or dispatch item procs.
internal readonly struct ItemStatContext
{
    internal Item Item { get; }
    internal int RecipientLevel { get; }
    internal ItemStatContext(Item item, int recipientLevel)
    {
        if (item is not { Deleted: false } || !ClassGear.Converted(item)) { throw new NotSupportedException("No supported original equipment item"); }
        if (recipientLevel is < 1 or > 99) { throw new ArgumentOutOfRangeException(nameof(recipientLevel)); }
        Item = item; RecipientLevel = recipientLevel;
    }
    private void RequireItem()
    {
        if (Item is not { Deleted: false }) { throw new InvalidOperationException("Original equipment item no longer exists"); }
    }
    private IEnumerable<(NamedProperty Property, NamedPropertyRoll Roll)> Properties()
    {
        if (ClassGear.State(Item) is not { Identified: true } state || ClassGear.Broken(Item)) { yield break; }
        if (state.NamedItemId != null) { foreach (var p in NamedGear.IntrinsicProperties(Item)) { yield return p; } }
        else { foreach (var p in OrdinaryAffixProperties.Properties(state)) { yield return p; } }
    }
    internal int Bonus(string code) { RequireItem(); return ClassGear.Bonus(Item, code, RecipientLevel); }
    // Retain sub-hit-point eighths; rounding each whole-number bonus before
    // converting to FP256 would discard item_hp_perlevel/item_mana_perlevel.
    private int ResourceBonus256(string code)
    {
        int total = checked(Bonus(code) * 256);
        foreach (var p in Properties())
        {
            if (p.Property.Code == (code == "hp" ? "hp/lvl" : "mana/lvl"))
            {
                total = checked(total + NamedPropertyRuntime.Bonus256(p.Property, p.Roll, code, RecipientLevel)
                    - NamedPropertyRuntime.Bonus(p.Property, p.Roll, code, RecipientLevel) * 256);
            }
        }
        return total;
    }
    internal int LifeBonus256 => ResourceBonus256("hp");
    internal int ManaBonus256 => ResourceBonus256("mana");
    internal int Defense { get { RequireItem(); return ClassGear.Defense(Item, RecipientLevel); } }
    internal (int Min, int Max) WeaponDamage { get { RequireItem(); return ClassGear.Damage(Item, RecipientLevel); } }
}
