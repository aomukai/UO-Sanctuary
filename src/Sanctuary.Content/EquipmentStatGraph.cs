// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
namespace Sanctuary;

internal readonly record struct EquipmentStatValue(uint Packed,int Value)
{internal ushort Stat=>(ushort)Packed;}
internal readonly record struct EquipmentStatCost(bool DamageRelated,bool AppliesToItem);
internal sealed class EquipmentStatUnit(object entity,int kind)
{
    internal object Entity{get;}=entity;
    internal int Kind{get;}=kind;
    internal EquipmentStatNode Stats;
}
internal sealed class EquipmentStatNode
{
    internal const uint NewLength=2,TempOnly=4,Set=0x2000,Permanent=0x20000000,Dynamic=0x40000000,Extended=0x80000000;
    internal uint Flags;
    internal int State;
    internal EquipmentStatValue[] BaseValues=[],FullValues=[];
    internal EquipmentStatNode Parent,Previous,Next,Last,Sets;
    internal EquipmentStatUnit Owner,Recipient;
    internal Action<EquipmentStatUnit,int,EquipmentStatNode> Removed;
    internal bool Is(uint flag)=>(Flags&flag)!=0;
    internal bool IsExtended=>Is(Extended);
    internal EquipmentStatValue[] Values=>IsExtended?FullValues:BaseValues;
}
internal abstract class EquipmentStatServices
{
    internal abstract int BodyLocation(EquipmentStatUnit unit);
    internal abstract EquipmentStatCost? Cost(ushort stat);
    internal abstract void Recalculate(EquipmentStatNode node,uint packed);
    internal abstract void Propagate(EquipmentStatNode node,uint packed,int delta,EquipmentStatUnit sourceOwner);
}

// Original stat-list ownership and callback ordering. Numeric recalculation,
// propagation and native recipient effects are mandatory external services.
internal static class EquipmentStatGraph
{
    private static EquipmentStatNode Extended(EquipmentStatNode node)=>node is{IsExtended:true}?node:null;
    private static void RecalculateApplying(EquipmentStatNode node,EquipmentStatServices services)
    {
        var ids=new List<uint>();
        foreach(var value in node.FullValues){if(services.Cost(value.Stat) is{AppliesToItem:true}){ids.Add(value.Packed);}}
        if(ids.Count>16){throw new NotSupportedException("Source item-operation scratch capacity exceeded");}
        foreach(uint id in ids){if(services.Cost((ushort)id)!=null){services.Recalculate(node,id);}}
    }
    internal static void Detach(EquipmentStatNode node,EquipmentStatServices services)
    {
        if(node==null){return;}
        var parent=node.Parent;
        if(parent!=null)
        {
            if(parent.IsExtended)
            {
                if(parent.Last==node){parent.Last=node.Previous;}
                if(parent.Sets==node){parent.Sets=node.Previous;}
            }
            node.Parent=null;
        }
        if(node.Next!=null){node.Next.Previous=node.Previous;}
        if(node.Previous!=null){node.Previous.Next=node.Next;}
        node.Previous=node.Next=null;
        var recipient=node.Recipient;node.Recipient=null;
        if(node.Is(EquipmentStatNode.Set)){return;}
        var owner=node.IsExtended?node.Owner:null;
        if(node.IsExtended&&node.Is(EquipmentStatNode.Permanent)){RecalculateApplying(node,services);}
        if(parent!=null)
        {
            if(!parent.IsExtended){throw new NotSupportedException("Source stat parent must be extended");}
            foreach(var value in node.Values)
            {
                if(!node.Is(EquipmentStatNode.Dynamic)||services.Cost(value.Stat) is{DamageRelated:false})
                {services.Propagate(parent,value.Packed,-value.Value,owner);}
            }
        }
        if(recipient!=null&&node.Removed!=null){node.Removed(recipient,node.State,node);}
    }
    internal static void Post(EquipmentStatUnit recipient,EquipmentStatNode node,bool includeDamage,EquipmentStatServices services)
    {
        var root=Extended(recipient.Stats);if(root==null){return;}
        Detach(node,services);
        var ancestor=root;while(ancestor!=null&&ancestor!=node){ancestor=ancestor.Parent;}
        if(ancestor!=null){return;}
        if(node.Is(EquipmentStatNode.TempOnly)){recipient.Stats.Flags|=EquipmentStatNode.NewLength;}
        if(node.Is(EquipmentStatNode.Set))
        {
            node.Previous=recipient.Stats.Sets;if(node.Previous!=null){node.Previous.Next=node;}
            node.Next=null;recipient.Stats.Sets=node;node.Parent=recipient.Stats;node.Recipient=recipient;return;
        }
        node.Previous=recipient.Stats.Last;if(node.Previous!=null){node.Previous.Next=node;}
        node.Parent=recipient.Stats;recipient.Stats.Last=node;node.Recipient=recipient;
        var owner=node.IsExtended?node.Owner:null;
        if(node.IsExtended){RecalculateApplying(node,services);}
        if(includeDamage){node.Flags&=~EquipmentStatNode.Dynamic;}
        else{node.Flags|=EquipmentStatNode.Dynamic;}
        foreach(var value in node.Values)
        {
            if(includeDamage||services.Cost(value.Stat) is{DamageRelated:false})
            {services.Propagate(root,value.Packed,value.Value,owner);}
        }
    }
    internal static void Merge(EquipmentStatUnit recipient,EquipmentStatUnit source,bool includeDamage,EquipmentStatServices services)
    {
        if(recipient==null||source?.Stats==null){return;}
        var node=source.Stats;
        if(source.Kind==4&&(services.BodyLocation(source)==11||services.BodyLocation(source)==12))
        {Detach(node,services);}
        else if(node.Recipient==recipient)
        {
            if(includeDamage&&node.Is(EquipmentStatNode.Dynamic)){ToggleDamage(recipient,source,true,services);}
            else if(!includeDamage&&!node.Is(EquipmentStatNode.Dynamic)){ToggleDamage(recipient,source,false,services);}
        }
        else
        {
            Post(recipient,node,includeDamage,services);
            // Deliberately retain the source write even when Post refuses a
            // nonextended target. Parent ownership remains a distinct query.
            node.Recipient=recipient;
        }
    }
    internal static EquipmentStatUnit Owner(EquipmentStatUnit source,out bool includesDamage)
    {
        if(source?.Stats?.Parent is not{IsExtended:true} parent){includesDamage=false;return null;}
        includesDamage=!source.Stats.Is(EquipmentStatNode.Dynamic);return parent.Owner;
    }
    internal static bool ToggleDamage(EquipmentStatUnit recipient,EquipmentStatUnit source,bool includeDamage,EquipmentStatServices services)
    {
        if(recipient==null||source?.Stats==null){return false;}
        var node=source.Stats;
        if(node.Recipient!=recipient){Merge(recipient,source,includeDamage,services);return false;}
        if(recipient.Stats is not{IsExtended:true}){return false;}
        uint previous=node.Flags;
        if(includeDamage){node.Flags&=~EquipmentStatNode.Dynamic;}else{node.Flags|=EquipmentStatNode.Dynamic;}
        if(previous==node.Flags){return false;}
        foreach(var value in node.Values)
        {
            if(services.Cost(value.Stat) is{DamageRelated:true})
            {services.Propagate(recipient.Stats,value.Packed,includeDamage?value.Value:-value.Value,source);}
        }
        return true;
    }
}
