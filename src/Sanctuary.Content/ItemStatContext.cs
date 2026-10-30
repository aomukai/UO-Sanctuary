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
    internal MissileSourceStats Poison()
    {RequireItem();return ItemPoisonProjection.Read(Item,Properties());}
    internal MissileSourceStats Elements()
    {
        RequireItem();
        if(ClassGear.Slot(Item)==GearSlot.Cosmetic||ClassGear.State(Item) is not{Identified:true}||ClassGear.Broken(Item)){return new();}
        foreach(string code in new[]{"damage-fire","damage-cold","damage-lightning"})
        {if(ClassGear.Material(Item,code)!=0){throw new NotSupportedException("NPC material elemental percentage needs a verified projection: "+code);}}
        var fire=Element("fire","dmg-fire","fire-damage");var cold=Element("cold","dmg-cold","cold-damage");
        var lightning=Element("ltng","dmg-ltng","lightning-damage");var magic=Element("mag","dmg-mag",null);
        int frames=Bonus("cold-len");
        foreach(var p in Properties())
        {if(p.Property.Code is "dmg-cold" or "dmg-elem"){frames=checked(frames+NamedPropertyRuntime.Parameter(p.Property));}}
        int gemMs=GemCombat.Duration(Item,"cold-damage");
        if(gemMs%40!=0){throw new NotSupportedException("NPC socket cold duration has no exact source-frame projection");}
        frames=checked(frames+gemMs/40);
        if(frames<0){throw new NotSupportedException("NPC negative cold duration needs a verified projection");}
        return new(FireMin:fire.Min,FireMax:fire.Max,LightningMin:lightning.Min,LightningMax:lightning.Max,
            MagicMin:magic.Min,MagicMax:magic.Max,ColdMin:cold.Min,ColdMax:cold.Max,ColdFrames:frames);
    }
    private (int Min,int Max) Element(string prefix,string propertyCode,string gemCode)
    {
        int min=checked(Bonus(prefix+"-min")*256),max=checked(Bonus(prefix+"-max")*256);
        foreach(var p in Properties())
        {
            if(p.Property.Code==propertyCode||prefix!="mag"&&p.Property.Code=="dmg-elem")
            {min=checked(min+p.Roll.Value*256);max=checked(max+p.Roll.Maximum*256);}
        }
        if(gemCode!=null)
        {min=checked(min+NamedElements.To256(GemCombat.Value(Item,gemCode,0)));max=checked(max+NamedElements.To256(GemCombat.Value(Item,gemCode,1)));}
        if(min<0||max<min){throw new NotSupportedException("NPC elemental endpoint projection is not supported: "+prefix);}
        return(min,max);
    }
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
