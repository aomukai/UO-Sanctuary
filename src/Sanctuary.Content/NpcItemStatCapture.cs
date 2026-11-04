// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Linq;
using Server;
namespace Sanctuary;

internal sealed record NpcItemStatCapture(int Body,EquipmentStatValue[] ItemBase,EquipmentStatValue[] Properties)
{
    internal static NpcItemStatCapture Capture(Item item)
    {
        if(item is not{Deleted:false}||ClassGear.State(item)is not{Identified:true} state||ClassGear.Broken(item)||!ClassGear.Converted(item))
        {throw new NotSupportedException("Raw NPC item graph requires an intact identified original");}
        var b=ClassGear.Base(item);bool weapon=ClassGear.Slot(item)==GearSlot.Weapon;
        var basis=new EquipmentPropertyBase(b.MinDamage,b.MaxDamage,b.TwoHandMin,b.TwoHandMax,b.MissileMin,b.MissileMax,b.MinDefense,b.MaxDefense);
        var unit=new EquipmentStatUnit(item,4);unit.Stats=new(){Flags=EquipmentStatNode.Extended,OwnerKind=4,Owner=unit,
            BaseValues=IronGolemBodyRecipes.Fields((21,basis.Minimum),(22,basis.Maximum),(23,basis.SecondaryMinimum),(24,basis.SecondaryMaximum),
                (159,basis.ThrowMinimum),(160,basis.ThrowMaximum),(31,weapon?0:state.Defense))};
        int body=IronGolemItems.Metadata(b.Code).Body1;
        var values=new EquipmentStatValues(_=>body,_=>null,_=>null,(_,_,_,_,_)=>{});
        var omissions=EquipmentRawProperties.Project(item,unit,values,basis,weapon,!weapon,false);
        if(omissions.Length!=0){throw new NotSupportedException("Unrepresented NPC item properties: "+string.Join(',',omissions.Select(o=>o.Code+": "+o.Reason)));}
        var properties=unit.Stats.Last;
        if(properties is{IsExtended:true}||properties?.Previous!=null||properties?.Last!=null)
        {throw new NotSupportedException("NPC original needs a single numeric item property list");}
        return new(body,(EquipmentStatValue[])unit.Stats.BaseValues.Clone(),properties==null?[]:(EquipmentStatValue[])properties.BaseValues.Clone());
    }
}
