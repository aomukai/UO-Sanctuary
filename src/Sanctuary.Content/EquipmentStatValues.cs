// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
namespace Sanctuary;

internal sealed record EquipmentStatOperation(int Operation,int Stat,int Base,int Parameter);
internal sealed record EquipmentStatDefinition(int Id,string Name,uint Flags,bool KeepZero,int Minimum,int Shift,bool BaseOfOther,bool HasOperations,bool AppliesToItem,int Operation,int Parameter,int[] Targets,int[] Dependents,EquipmentStatOperation[] Operations);
internal readonly record struct EquipmentClassCoefficients(int Mana,int Stamina,int Life);

// Transient numeric graph only. World-time/class metadata and value-change
// effects are explicit caller services; this does not modify a live actor.
internal sealed class EquipmentStatValues:EquipmentStatServices
{
    private static readonly Lazy<IReadOnlyDictionary<int,EquipmentStatDefinition>> Definitions=new(()=>
    {
        var rows=BalanceCatalog.Read<EquipmentStatDefinition>("equipment-stat-costs");
        if(rows.Length!=368||rows.Select(r=>r.Id).Distinct().Count()!=368||rows.Any(r=>r.Id is <0 or >=368||r.Shift is <0 or >31||r.Targets.Length!=3||r.Operations.Length>16||r.Dependents.Length>64
            ||r.Targets.Any(n=>n!=65535&&(n<0||n>=368))||r.Dependents.Any(n=>n<0||n>=368)||r.Operations.Any(o=>o.Operation is <1 or >13||o.Stat is <0 or >=368||o.Base!=65535&&(o.Base<0||o.Base>=368)||o.Parameter is <0 or >31)))
        {throw new InvalidDataException("Invalid source equipment stat operations");}
        return rows.ToDictionary(r=>r.Id);
    });
    internal static EquipmentStatDefinition Definition(int id)=>Definitions.Value.GetValueOrDefault(id);
    private readonly Func<EquipmentStatUnit,int> Body;
    private readonly Func<EquipmentStatUnit,int?> Clock;
    private readonly Func<EquipmentStatUnit,EquipmentClassCoefficients?> Classes;
    private readonly Action<EquipmentStatNode,EquipmentStatUnit,uint,int,int> Changed;
    private int Depth;
    internal EquipmentStatValues(Func<EquipmentStatUnit,int> body,Func<EquipmentStatUnit,int?> clock,
        Func<EquipmentStatUnit,EquipmentClassCoefficients?> classes,Action<EquipmentStatNode,EquipmentStatUnit,uint,int,int> changed)
    {Body=body??throw new ArgumentNullException(nameof(body));Clock=clock??throw new ArgumentNullException(nameof(clock));Classes=classes??throw new ArgumentNullException(nameof(classes));Changed=changed??throw new ArgumentNullException(nameof(changed));}
    internal override int BodyLocation(EquipmentStatUnit unit)=>Body(unit);
    internal override EquipmentStatCost? Cost(ushort stat)=>Definition(stat) is{} d?new((d.Flags&4)!=0,d.AppliesToItem):null;
    private static int Find(EquipmentStatValue[] values,uint packed)=>Array.FindIndex(values,v=>v.Packed==packed);
    internal static int Raw(EquipmentStatValue[] values,uint packed){int i=Find(values,packed);return i<0?0:values[i].Value;}
    private static void Store(ref EquipmentStatValue[] values,uint packed,int value,bool keepZero=false)
    {
        int i=Find(values,packed);
        if(value==0&&!keepZero){if(i>=0){values=values.Where((_,n)=>n!=i).ToArray();}return;}
        if(i>=0){values[i]=new(packed,value);return;}
        values=values.Append(new(packed,value)).OrderBy(v=>v.Packed).ToArray();
    }
    internal static int SourceMinimum(int value,bool hasMinimum,int minimum,int shift,int flags,int? unitKind)
    {
        // Compare the raw value with the unshifted minimum before ValShift.
        return hasMinimum&&flags<0&&unitKind is 0 or 1&&value<minimum?unchecked(minimum<<shift):value;
    }
    private static int Clamp(int value,EquipmentStatDefinition cost,EquipmentStatNode node)=>cost==null?value:
        SourceMinimum(value,(cost.Flags&1024)!=0,cost.Minimum,cost.Shift,node==null?0:unchecked((int)node.Flags),node?.Owner?.Kind);
    internal static int SourceIndex(IReadOnlyList<EquipmentStatValue> values,uint packed)
    {
        int first=0,last=values.Count;
        while(first<last){int mid=first+(last-first)/2;if(unchecked((int)values[mid].Packed)<unchecked((int)packed)){first=mid+1;}else{last=mid;}}
        return first<values.Count&&values[first].Packed==packed?first:-1;
    }
    internal static int SourceRead(EquipmentStatNode node,uint packed,EquipmentStatDefinition cost)
    {
        if(node==null){throw new NotSupportedException("Current stat reader dereferences a missing node");}
        var values=node.Values;int index=SourceIndex(values,packed);return index<0?0:Clamp(values[index].Value,cost,node);
    }
    internal static int Base(EquipmentStatNode node,uint packed,bool clamp=true)=>Find(node.BaseValues,packed)<0?0:Clamp(Raw(node.BaseValues,packed),clamp?Definition(EquipmentStatKey.Stat(packed)):null,node);
    internal static int Total(EquipmentStatNode node,uint packed,bool clamp=true)=>Find(node.Values,packed)<0?0:Clamp(Raw(node.Values,packed),clamp?Definition(EquipmentStatKey.Stat(packed)):null,node);
    private void WriteFull(EquipmentStatNode node,uint packed,int value,EquipmentStatDefinition cost,EquipmentStatUnit source)
    {
        int index=Find(node.FullValues,packed);if(index<0&&value==0){return;}
        int prior=index<0?0:node.FullValues[index].Value;
        Store(ref node.FullValues,packed,value,cost.KeepZero);
        if((value!=0||cost.KeepZero)&&cost.AppliesToItem){node.Flags|=EquipmentStatNode.Permanent;}
        if(prior!=value&&(cost.Flags&2048)!=0){Changed(node,source,packed,prior,value);}
    }
    private int Percentage(int value,EquipmentStatNode node,int stat)
    {int percent=Query(node,EquipmentStatKey.Pack(stat));return percent==0?0:(int)(value*(percent/100.0));}
    internal static int ByTime(int amount,int time)
    {
        if(time is <0 or >359){throw new NotSupportedException("Unmapped source day angle");}
        int period=amount&3,minimum=((amount>>2)&1023)-256,maximum=((amount>>12)&1023)-256;
        int difference=Math.Abs(time-period*90),rounded=(difference+7)/15*15,clamped=Math.Clamp(rounded,0,359);
        if(clamped>180){clamped=360-clamped;}
        return maximum-(maximum-minimum)*clamped/180;
    }
    internal int Query(EquipmentStatNode node,uint packed)
    {
        if(++Depth>128){Depth--;throw new NotSupportedException("Unmapped cyclic/deep stat operation graph");}
        try
        {
            var d=Definition(EquipmentStatKey.Stat(packed));if(d==null){return 0;}
            int value=Base(node,packed,false);
            if(node.IsExtended)
            {
                int count=0;for(var child=node.Last;child!=null;child=child.Previous)
                {
                    if(++count>512){throw new NotSupportedException("Unmapped stat sibling cycle");}
                    if((d.Flags&4)==0||!child.Is(EquipmentStatNode.Dynamic)){value=checked(value+Total(child,packed,false));}
                }
            }
            if(!d.HasOperations){return value;}
            int previous=value;
            foreach(var op in d.Operations)
            {
                switch(op.Operation)
                {
                    case 0:return value;
                    case 1:
                        if(node.Owner!=null)
                        {
                            if(node.Owner.Kind==4){previous=node.Owner.Stats==null?0:Base(node.Owner.Stats,EquipmentStatKey.Pack(d.Id));}
                            if(previous!=0){value=checked(value+Percentage(previous,node,op.Stat));}
                        }
                        break;
                    case 2:case 3:
                        if(node.OwnerKind is 0 or 1&&op.Base!=65535&&Definition(op.Base) is{} baseCost)
                        {
                            int n=Raw(node.Values,EquipmentStatKey.Pack(op.Base))>>baseCost.Shift;
                            if(n>0)
                            {
                                int other=Query(node,EquipmentStatKey.Pack(op.Stat));
                                if(other!=0){int amount=checked(n*other)>>op.Parameter;value=op.Operation==2?checked(value+amount):(int)(value+checked(amount*previous)/100.0);}
                            }
                        }
                        break;
                    case 4:case 5:
                        if(node.Recipient is{Kind:0 or 1,Stats:not null} recipient&&op.Base!=65535&&Definition(op.Base) is{} recipientCost)
                        {
                            int n=Total(recipient.Stats,EquipmentStatKey.Pack(op.Base))>>recipientCost.Shift;
                            if(n>0)
                            {
                                int other=Query(node,EquipmentStatKey.Pack(op.Stat));
                                if(other!=0){int amount=checked(n*other)>>op.Parameter;value=op.Operation==4?checked(value+amount):(int)(value+checked(previous*amount)/100.0);}
                            }
                        }
                        break;
                    case 6:case 7:
                        if(node.Owner!=null&&Clock(node.Owner) is int time)
                        {
                            int other=Query(node,EquipmentStatKey.Pack(op.Stat));
                            if(other!=0){int amount=ByTime(other,time);value=op.Operation==6?checked(value+amount):(int)(value+checked(value*amount)/100.0);}
                        }
                        break;
                    case 8:case 9:
                        if(node.Owner is{Kind:0} owner&&Classes(owner) is{} coefficients)
                        {
                            int bonus=checked(Query(node,EquipmentStatKey.Pack(op.Stat))-Raw(node.BaseValues,EquipmentStatKey.Pack(op.Stat)));
                            int factor=op.Operation==8?coefficients.Mana:d.Id==11?coefficients.Stamina:coefficients.Life;
                            if(bonus!=0){value=checked(value+(checked(bonus*factor)<<6));}
                        }
                        break;
                    case 11:
                        if(previous!=0&&node.Owner is{Kind:0 or 1}){value=checked(value+Percentage(previous,node,op.Stat));}
                        break;
                    case 13:
                        if(node.Owner is{Kind:4} item)
                        {previous=item.Stats==null?0:Base(item.Stats,EquipmentStatKey.Pack(d.Id));if(previous!=0){value=checked(value+Percentage(previous,node,op.Stat));}}
                        break;
                    // The complete source query ignores the other operation IDs.
                }
            }
            return value;
        }
        finally{Depth--;}
    }
    internal override void Recalculate(EquipmentStatNode node,uint packed)=>Recalculate(node,packed,null);
    internal int Recalculate(EquipmentStatNode node,uint packed,EquipmentStatUnit source)
    {
        if(++Depth>128){Depth--;throw new NotSupportedException("Unmapped cyclic/deep stat recalculation graph");}
        try
        {
            var d=Definition(EquipmentStatKey.Stat(packed))??throw new NotSupportedException("No source stat cost");int value=Query(node,packed);
            if(!d.BaseOfOther){WriteFull(node,packed,value,d,source);return value;}
            if(value==0&&Find(node.FullValues,packed)>=0){WriteFull(node,packed,value,d,source);}
            bool update=true;
            for(int i=0;i<3;i++)
            {
                int target=d.Targets[i];if(target==65535){break;}
                var targetCost=Definition(target)??throw new NotSupportedException("No source dependent cost");
                int targetValue=Recalculate(node,EquipmentStatKey.Pack(target),source);
                if(Find(node.FullValues,EquipmentStatKey.Pack(target))<0&&targetValue==0){continue;}
                WriteFull(node,EquipmentStatKey.Pack(target),targetValue,targetCost,source);
                if(targetValue==0){continue;}
                // Source uses the same indexed incoming-op slot here, distinct
                // from the outgoing target ID. Preserve that indexing exactly.
                int opBase=i<d.Operations.Length?d.Operations[i].Base:0;
                switch(d.Operation)
                {
                    case 2:case 3:
                        if(node.OwnerKind is 0 or 1&&opBase!=65535&&Find(node.Values,EquipmentStatKey.Pack(opBase))>=0&&Raw(node.Values,EquipmentStatKey.Pack(opBase))>0){update=false;}
                        break;
                    case 4:case 5:
                        if(node.Recipient is{Kind:0 or 1} recipient&&opBase!=65535&&Definition(opBase)!=null)
                        {update=Total(recipient.Stats,EquipmentStatKey.Pack(opBase))<=0;}
                        break;
                    case 6:case 7:if(node.OwnerKind is 0 or 1){update=false;}break;
                    case 13:if(node.OwnerKind==4){update=false;}break;
                    case 0:case 8:case 9:case 10:case 11:break;
                    default:update=false;break;
                }
            }
            if(update&&value!=0)
            {
                WriteFull(node,packed,value,d,source);
                foreach(int dependent in d.Dependents)
                {int result=Recalculate(node,EquipmentStatKey.Pack(dependent),source);WriteFull(node,EquipmentStatKey.Pack(dependent),result,Definition(dependent),source);}
            }
            return value;
        }
        finally{Depth--;}
    }
    internal override void Propagate(EquipmentStatNode node,uint packed,int delta,EquipmentStatUnit source)
    {
        if(delta==0){return;}
        var d=Definition(EquipmentStatKey.Stat(packed));if(d==null||node.Is(EquipmentStatNode.Set)){return;}
        var parent=node.IsExtended?node:node.Parent;int count=0;
        while(parent!=null)
        {
            if(++count>128){throw new NotSupportedException("Unmapped stat parent cycle");}
            if(d.BaseOfOther||d.HasOperations){Recalculate(parent,packed,source);}
            else{WriteFull(parent,packed,checked(Raw(parent.FullValues,packed)+delta),d,source);}
            if(parent.Is(EquipmentStatNode.Set)||((d.Flags&4)!=0&&parent.Is(EquipmentStatNode.Dynamic))){break;}
            parent=parent.Parent;
        }
    }
    internal bool SetBase(EquipmentStatNode node,uint packed,int value,EquipmentStatUnit source=null)
    {
        if(node==null){return false;}
        if(node.OwnerKind==0){throw new NotSupportedException("Player ModStats registration is not integrated with this NPC graph");}
        int index=Find(node.BaseValues,packed);if(index<0&&value==0){return false;}
        int difference=checked(value-Raw(node.BaseValues,packed));if(difference==0){return false;}
        Store(ref node.BaseValues,packed,value);Propagate(node,packed,difference,source);return true;
    }
}
