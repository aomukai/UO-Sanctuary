// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
namespace Sanctuary;

internal sealed record FrozenItemGraph(int Version,int Body,EquipmentStatValue[] ItemBase);
internal sealed record FrozenStatChange(uint Packed,int Before,int After);

// Owned numeric projection, not a reconstruction of the original world's
// registration history. Resource bases and skill pools are explicit inputs.
internal sealed class FrozenNpcStatGraph
{
    internal readonly EquipmentStatUnit Actor;
    internal readonly EquipmentStatUnit[] Items;
    internal readonly List<FrozenStatChange> Changes=new();
    private readonly EquipmentStatValues Values;
    internal FrozenNpcStatGraph(FrozenEquipmentContext[] equipment,int level,int life256,int mana256,int stamina256,int battleOrders,int vigor)
    {
        if(level is <1 or >99||life256<=0||mana256<0||stamina256<0){throw new ArgumentOutOfRangeException();}
        var captured=equipment.Select(e=>(Item:e,Graph:e.GraphInput(level),Properties:e.NumericProperties(level))).ToArray();
        foreach(var item in captured)
        {
            if(item.Graph.Body is <1 or >10){throw new NotSupportedException("Frozen equipped item has no ordinary active body location");}
            foreach(var value in item.Graph.ItemBase.Concat(item.Properties))
            {if(value.Value!=0&&EquipmentStatValues.Definition(value.Stat)?.Operation is 6 or 7){throw new NotSupportedException("Frozen item graph requires an explicit world-time service");}}
        }
        Actor=new(new object(),1);Actor.Stats=new(){Flags=EquipmentStatNode.Extended,Owner=Actor,OwnerKind=1,
            BaseValues=IronGolemBodyRecipes.Fields((7,life256),(9,mana256),(11,stamina256),(12,level),(76,battleOrders),(77,battleOrders),(162,checked(battleOrders+vigor)))};
        Actor.Stats.FullValues=(EquipmentStatValue[])Actor.Stats.BaseValues.Clone();
        var locations=new Dictionary<EquipmentStatUnit,int>();
        Values=new(u=>locations.TryGetValue(u,out int body)?body:throw new NotSupportedException("Missing frozen item body service"),_=>null,_=>null,
            (node,_,key,before,after)=>{if(node==Actor.Stats){Changes.Add(new(key,before,after));}});
        foreach(int resource in new[]{7,9,11}){Values.Recalculate(Actor.Stats,EquipmentStatKey.Pack(resource));}
        Items=new EquipmentStatUnit[captured.Length];
        for(int i=0;i<captured.Length;i++)
        {
            var input=captured[i];var item=new EquipmentStatUnit(input.Item,4);locations[item]=input.Graph.Body;
            item.Stats=new(){Flags=EquipmentStatNode.Extended,OwnerKind=4,Owner=item,BaseValues=(EquipmentStatValue[])input.Graph.ItemBase.Clone(),FullValues=(EquipmentStatValue[])input.Graph.ItemBase.Clone()};
            EquipmentStatGraph.Post(item,new(){Flags=0x40,OwnerKind=4,BaseValues=(EquipmentStatValue[])input.Properties.Clone()},true,Values);
            EquipmentStatGraph.Merge(Actor,item,true,Values);Items[i]=item;
        }
    }
    internal int Read(int stat)=>EquipmentStatValues.Total(Actor.Stats,EquipmentStatKey.Pack(stat));
    internal int ItemRead(int index,int stat)=>EquipmentStatValues.Total(Items[index].Stats,EquipmentStatKey.Pack(stat));
    internal EquipmentStatValue[] Stats=>(EquipmentStatValue[])Actor.Stats.FullValues.Clone();
}
