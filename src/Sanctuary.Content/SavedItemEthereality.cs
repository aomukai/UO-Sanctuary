// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Linq;
using Server;
namespace Sanctuary;

// Property23 and its durability guard. The existing physical writer owns
// flag-before-base-write order and integer three-halves arithmetic.
internal static class SavedItemEthereality
{
    internal static bool Supports(EquipmentPropertyPlan plan)=>plan is{Code:"ethereal",Steps.Length:1}&&plan.Steps[0] is{Function:23,Stat:-1};
    internal static bool HasDurability(bool item,int kind,bool row,bool noDurability,int durability,Func<int> maximum,Func<int> indestructible)
        =>item&&kind==4&&row&&!noDurability&&durability!=0&&maximum()!=0&&indestructible()<=0;
    internal static bool Apply(bool property,bool item,int kind,bool ethereal,Func<bool> durable,EquipmentPropertyServices target)
    {
        if(!property||!item||kind!=4||ethereal||!durable()){return false;}
        EquipmentPropertyWriter.Ethereality(target);return true;
    }
    internal static (int Durability,bool NoDurability) Facts(string code)
    {
        var basis=NpcItemAffixes.Pool.Basis(code);var facts=CurrentItemInitialization.For(code);
        return(facts.Base.Durability,basis.SourceFields.TryGetProperty("nodurability",out var value)&&value.GetInt32()!=0);
    }
    internal static ItemPropertyCreationContext Guard(Item original,EquipmentStatUnit unit,EquipmentStatValues values,int initializedDurability)
    {
        // Supply the shared item op13 reader with explicit durability fields.
        // Keep this private guard graph separate from existing combat arrays.
        uint key=EquipmentStatKey.Pack(73);bool hasBase=unit.Stats.BaseValues.Any(s=>s.Packed==key);
        var prefix=unit.Stats.Last?.BaseValues??[];
        int Read(int stat)=>EquipmentStatValues.Raw(prefix,EquipmentStatKey.Pack(stat));
        int Root(int stat)=>EquipmentStatValues.Raw(unit.Stats.BaseValues,EquipmentStatKey.Pack(stat));
        var properties=IronGolemBodyRecipes.Fields((73,Read(73)),(75,checked(Root(75)+Read(75))),(152,Read(152)));
        return ItemPropertyCreationContext.Restore(original,values,new(0,0,0,0,0,0,0,0),false,false,false,
            new(0,false,properties.Length>0,IronGolemBodyRecipes.Fields((73,hasBase?Root(73):initializedDurability),(152,Root(152))),properties));
    }
}
