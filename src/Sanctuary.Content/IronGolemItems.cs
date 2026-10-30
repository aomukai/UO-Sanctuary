// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Mobiles;
namespace Sanctuary;

internal sealed record IronGolemItemBase(string Code,int ItemId,int Flags,int Body1,int Body2,string SourceTable);
public sealed record IronGolemItemBinding(uint Pet,uint Owner,uint Item,string BaseCode,int BodyLocation);
public sealed class IronGolemItemProfiles:GenericPersistence
{
    public IronGolemItemProfiles():base("SanctuaryIronGolemItems",188){}
    public override void Serialize(IGenericWriter writer){writer.Write(0);writer.Write(JsonSerializer.Serialize(IronGolemItems.Snapshot()));}
    public override void Deserialize(IGenericReader reader)
    {
        if(reader.ReadInt()!=0){throw new InvalidDataException("Unknown Iron Golem item binding version");}
        IronGolemItems.Restore(JsonSerializer.Deserialize<IronGolemItemBinding[]>(reader.ReadString()));
    }
}
public static class IronGolemItems
{
    private static readonly Lazy<IReadOnlyDictionary<string,IronGolemItemBase>> Bases=new(()=>
    {
        var rows=BalanceCatalog.Read<IronGolemItemBase>("iron-golem-items");
        if(rows.Length!=692||rows.Select(r=>r.Code).Distinct(StringComparer.Ordinal).Count()!=rows.Length||rows.Select(r=>r.ItemId).Distinct().Count()!=rows.Length
            ||rows.Any(r=>string.IsNullOrWhiteSpace(r.Code)||r.ItemId<0||r.Flags<0||r.Body1 is <0 or >10||r.Body2 is <0 or >10
                ||r.SourceTable is not("weapons" or "armor" or "misc")||((r.Flags&2)!=0&&r.Body1==0)))
        {throw new InvalidDataException("Invalid Iron Golem source item metadata");}
        return rows.ToDictionary(r=>r.Code,r=>r,StringComparer.Ordinal);
    });
    private static readonly Dictionary<uint,IronGolemItemBinding> States=new();
    private static readonly Dictionary<uint,uint> ByItem=new();
    public static IronGolemItemProfiles Profiles{get;private set;}
    public static void Configure()=>Profiles=new();
    internal static IronGolemItemBase Metadata(string code)=>code!=null&&Bases.Value.TryGetValue(code,out var row)?row:throw new NotSupportedException("No verified Iron Golem item base");
    internal static IronGolemItemBinding State(BaseCreature pet)=>pet==null?null:States.GetValueOrDefault(pet.Serial.Value);
    internal static ItemStatContext Equipment(BaseCreature pet,int sourceRecipientLevel)
    {
        if(pet is not{Deleted:false}||State(pet) is not{} state||World.FindItem((Serial)state.Item) is not{Deleted:false} item
            ||item.Parent!=pet||(ClassGear.State(item)?.BaseCode??ClassGear.Definition(item)?.BaseCode)!=state.BaseCode)
        {throw new NotSupportedException("No intact original Iron Golem equipment binding");}
        return new(item,sourceRecipientLevel);
    }
    // Build an explicit transient stat-list context around the real original
    // objects. Callers must supply their verified stat arrays and services;
    // this does not guess a complete NPC stat projection or replay it on load.
    internal static (EquipmentStatUnit Recipient,EquipmentStatUnit Item) StatUnits(BaseCreature pet,int sourceRecipientLevel,
        EquipmentStatValue[] recipientBase,EquipmentStatValue[] itemBase,EquipmentStatValue[] itemFull)
    {
        var original=Equipment(pet,sourceRecipientLevel).Item;
        ArgumentNullException.ThrowIfNull(recipientBase);ArgumentNullException.ThrowIfNull(itemBase);ArgumentNullException.ThrowIfNull(itemFull);
        var recipient=new EquipmentStatUnit(pet,1);var held=new EquipmentStatUnit(original,4);
        recipient.Stats=new(){Flags=EquipmentStatNode.Extended,OwnerKind=1,Owner=recipient,BaseValues=(EquipmentStatValue[])recipientBase.Clone(),FullValues=(EquipmentStatValue[])recipientBase.Clone()};
        held.Stats=new(){Flags=EquipmentStatNode.Extended,Owner=held,BaseValues=(EquipmentStatValue[])itemBase.Clone(),FullValues=(EquipmentStatValue[])itemFull.Clone()};
        return(recipient,held);
    }
    private sealed class Target(Item item):IronGolemTargetServices
    {
        internal string Reason;
        internal IronGolemItemBase Base;
        internal override bool ResolveTarget(){Reason="Select an item.";return item is{Deleted:false};}
        internal override int TargetType()=>4;
        internal override int TargetMode(){Reason="The item must be on the ground.";return item.Parent==null&&item.Map!=null&&item.Map!=Map.Internal?3:0;}
        internal override bool Metal()
        {
            if(!ClassGear.Converted(item)){throw new NotSupportedException("This item type is not supported yet.");}
            Base=Metadata(ClassGear.State(item)?.BaseCode??ClassGear.Definition(item)?.BaseCode);
            Reason="Iron Golem requires a metal item.";return (Base.Flags&2)!=0;
        }
        internal override bool Identified(){Reason="Identify the item first.";return ClassGear.State(item)?.Identified??true;}
        internal override bool HasStatOwner(){Reason="That item is already bound.";return ByItem.ContainsKey(item.Serial.Value);}
    }
    internal static bool Eligible(Item item,out string reason)
    {
        var target=new Target(item);
        try{bool yes=IronGolemItemCast.Eligible(target);reason=yes?null:target.Reason;return yes;}
        catch(NotSupportedException e){reason=e.Message;return false;}
    }
    // Storage adapter only: retain the original object and all existing item
    // serializers/gear rolls. This is not the complete stat/proc/actor bridge.
    internal static bool TryAttach(PlayerMobile owner,BaseCreature pet,Item item,out string reason)
    {
        if(!Eligible(item,out reason)){return false;}
        if(owner is not{Alive:true,Deleted:false}||pet is not{Alive:true,Deleted:false}||pet.ControlMaster!=owner
            ||owner.Map==null||owner.Map==Map.Internal||pet.Map!=owner.Map||item.Map!=owner.Map||States.ContainsKey(pet.Serial.Value))
        {reason="The summon is not ready to receive that item.";return false;}
        if(!item.Movable||ClassGear.Slot(item)==GearSlot.Cosmetic||item.Layer==Layer.Invalid||pet.FindItemOnLayer(item.Layer)!=null)
        {reason="This item cannot be transferred in this context yet.";return false;}
        var row=Metadata(ClassGear.State(item)?.BaseCode??ClassGear.Definition(item)?.BaseCode);
        var savedMap=item.Map;var savedPoint=item.Location;bool visible=item.Visible,movable=item.Movable;
        try
        {
            item.Visible=false;item.Movable=false;pet.AddItem(item);
            if(item.Deleted||item.Parent!=pet){throw new InvalidOperationException("Original item was not attached");}
            var state=new IronGolemItemBinding(pet.Serial.Value,owner.Serial.Value,item.Serial.Value,row.Code,row.Body1);
            States.Add(state.Pet,state);ByItem.Add(state.Item,state.Pet);reason=null;return true;
        }
        catch
        {
            States.Remove(pet.Serial.Value);ByItem.Remove(item.Serial.Value);
            if(!item.Deleted){item.MoveToWorld(savedPoint,savedMap);item.Visible=visible;item.Movable=movable;}
            reason="The item could not be transferred.";return false;
        }
    }
    internal static void DestroyBoundItem(BaseCreature pet)
    {
        if(State(pet) is not{} state){return;}
        var item=World.FindItem((Serial)state.Item);
        if(item is{Deleted:false})
        {
            if(item.Parent!=pet){throw new NotSupportedException("Iron Golem item no longer belongs to its bound actor");}
            item.Delete();
        }
        States.Remove(state.Pet);ByItem.Remove(state.Item);
        NpcEquipmentRandom.Forget(pet);
    }
    // Worker snapshots only read/filter. Real actor callbacks own lifecycle
    // cleanup; a missing/moved item is not silently replaced or duplicated.
    public static IronGolemItemBinding[] Snapshot()=>States.Values.Where(s=>World.FindMobile((Serial)s.Pet) is BaseCreature{Deleted:false}).OrderBy(s=>s.Pet).ToArray();
    public static void Restore(IronGolemItemBinding[] saved)
    {
        if(saved==null){throw new InvalidDataException("Missing Iron Golem item bindings");}
        var states=new Dictionary<uint,IronGolemItemBinding>();var items=new Dictionary<uint,uint>();
        foreach(var s in saved)
        {
            if(s==null||!((Serial)s.Pet).IsMobile||!((Serial)s.Owner).IsMobile||s.Owner==s.Pet||!((Serial)s.Item).IsItem
                ||s.BaseCode==null||!Bases.Value.TryGetValue(s.BaseCode,out var row)||(row.Flags&2)==0||s.BodyLocation!=row.Body1
                ||!states.TryAdd(s.Pet,s)||!items.TryAdd(s.Item,s.Pet))
            {throw new InvalidDataException("Invalid or duplicate Iron Golem item binding");}
        }
        States.Clear();ByItem.Clear();foreach(var r in states){States.Add(r.Key,r.Value);}foreach(var r in items){ByItem.Add(r.Key,r.Value);}
    }
}
