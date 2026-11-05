// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Mobiles;
namespace Sanctuary;

// Numeric recipient graph around actual original native items. Actor bases,
// clock and value/rate/vital effects are mandatory explicit caller services.
// This is not admission of a learned actor or an implicit UO-stat conversion.
internal sealed class GeneratedNpcStatGraph
{
    internal readonly EquipmentStatUnit Actor;
    internal readonly EquipmentStatValues Values;
    private readonly BaseCreature Native;
    private readonly uint OwnerId;
    private readonly GeneratedNpcItem[] Originals;
    private readonly string[] Payloads;
    private readonly Dictionary<uint,ItemPropertyCreationContext> Carriers=new();
    private readonly Dictionary<EquipmentStatUnit,int> Bodies=new();
    private readonly EquipMonster Kind;
    private readonly bool HasPath;
    private readonly Action<BaseCreature> Rate,Clamp;
    private readonly Services Dispatch;
    private int? ResourceRegeneration;
    internal GeneratedNpcStatGraph(BaseCreature actor,EquipmentStatValue[] basis,Func<EquipmentStatUnit,int?> clock,
        Action<EquipmentStatNode,EquipmentStatUnit,uint,int,int> changed,bool dynamicPath,Action<BaseCreature> updateRate,Action<BaseCreature> clampVitals,
        int? sourceDamageRegeneration=null,uint[] activeOrder=null,Action<GeneratedNpcStatGraph> beforeReplay=null)
    {
        ArgumentNullException.ThrowIfNull(basis);ArgumentNullException.ThrowIfNull(clock);ArgumentNullException.ThrowIfNull(changed);
        ArgumentNullException.ThrowIfNull(updateRate);ArgumentNullException.ThrowIfNull(clampVitals);
        SetResourceCallbacks(sourceDamageRegeneration);
        if(actor is not{Alive:true,Deleted:false,ControlMaster:PlayerMobile{Deleted:false} owner}
            ||basis.Select(v=>v.Packed).Distinct().Count()!=basis.Length||basis.Any(v=>EquipmentStatValues.Definition(v.Stat)==null))
        {throw new NotSupportedException("Generated item graph needs its original living actor/owner and explicit known unique base stats");}
        Native=actor;OwnerId=owner.Serial.Value;Originals=GeneratedNpcEquipment.Items(actor);Payloads=Originals.Select(i=>i.SavedText).ToArray();
        activeOrder??=Originals.Select(i=>i.Serial.Value).ToArray();
        if(activeOrder.Distinct().Count()!=activeOrder.Length||activeOrder.Any(id=>!Originals.Any(i=>i.Serial.Value==id)))
        {throw new NotSupportedException("Generated active item order is not a unique subset of the original inventory");}
        Kind=Originals.FirstOrDefault()?.Snapshot.Equipment.Kind??EquipMonster.Other;HasPath=dynamicPath;Rate=updateRate;Clamp=clampVitals;
        Actor=new(actor,1);var fields=basis.OrderBy(v=>v.Packed).ToArray();Actor.Stats=new(){Flags=EquipmentStatNode.Extended,OwnerKind=1,Owner=Actor,BaseValues=fields,FullValues=(EquipmentStatValue[])fields.Clone()};
        int? Clock(EquipmentStatUnit unit)
        {var value=clock(unit);if(value is<0 or>359){throw new NotSupportedException("Generated item graph needs a source day angle0..359");}return value;}
        void RequireClock(EquipmentStatUnit unit,IEnumerable<EquipmentStatValue> values)
        {
            if(values.Any(v=>v.Value!=0&&EquipmentStatValues.Definition(v.Stat)?.Operation is 6 or 7)&&Clock(unit)==null)
            {throw new NotSupportedException("Generated item graph has a time-dependent stat without an explicit world clock");}
        }
        RequireClock(Actor,basis);
        // Validate every raw item/time dependency before replaying callbacks.
        foreach(var original in Originals)
        {var s=original.Snapshot;RequireClock(new(original,4),s.Graph.ItemBase.Concat(s.Graph.Properties));}
        Values=new(u=>Bodies.TryGetValue(u,out int body)?body:throw new NotSupportedException("Generated item has no original body service"),Clock,
            _=>throw new NotSupportedException("NPC graph does not supply player class coefficients"),(node,source,key,old,next)=>
            {
                changed(node,source,key,old,next);
                if(ResourceRegeneration is{} regeneration&&node==Actor.Stats)
                {NpcItemResourceCallbacks.Changed(Values,node,key,old,next,regeneration);}
            });Dispatch=new(this);
        void Restore(GeneratedNpcItem original)
        {
            var graph=original.RestoreGraph(Values);Carriers.Add(original.Serial.Value,graph);Bodies.Add(graph.Unit,original.Snapshot.Equipment.Body);
        }
        beforeReplay?.Invoke(this);
        // Preserve raw item construction/attachment callback interleaving.
        // Saved logical attachment order may differ from physical inventory.
        foreach(uint id in activeOrder)
        {Restore(Originals.Single(i=>i.Serial.Value==id));SourceItemStatUpdates.Update(Dispatch,Carriers[id].Unit,Actor,false);}
        foreach(var original in Originals.Where(i=>!Carriers.ContainsKey(i.Serial.Value))){Restore(original);}
    }
    internal void SetResourceCallbacks(int? regeneration)
    {
        if(regeneration is <0 or >32767){throw new NotSupportedException("NPC item resources require an explicit supported monster regeneration coefficient");}
        ResourceRegeneration=regeneration;
    }
    private void Current()
    {
        if(Native is not{Alive:true,Deleted:false,ControlMaster:PlayerMobile{Deleted:false} owner}||owner.Serial.Value!=OwnerId
            ||!GeneratedNpcEquipment.Items(Native).SequenceEqual(Originals)||Originals.Where((item,i)=>item.SavedText!=Payloads[i]).Any())
        {throw new NotSupportedException("Generated numeric graph lost its original actor/owner/items or immutable item payload frame");}
    }
    internal int Read(int stat,int layer=0)
    {Current();return EquipmentStatValues.SourceRead(Actor.Stats,EquipmentStatKey.Pack(stat,layer),EquipmentStatValues.Definition(stat));}
    internal EquipmentStatValue[] Stats{get{Current();return(EquipmentStatValue[])Actor.Stats.FullValues.Clone();}}
    internal ItemPropertyCreationContext Item(uint serial){Current();return Carriers.TryGetValue(serial,out var c)?c:throw new NotSupportedException("Item is not in the original recipient graph");}
    internal void Update(uint serial,bool force=false){var item=Item(serial);SourceItemStatUpdates.Update(Dispatch,item.Unit,Actor,force);}
    // Logical stat removal precedes native inventory transfer in the source.
    // Changing physical inventory invalidates this frame;the caller rebuilds
    // against its new original inventory rather than retaining a stale cache.
    internal void Transferred(uint serial,bool force=true,bool updateVitals=true)
    {var item=Item(serial);SourceItemStatUpdates.Transferred(Dispatch,item.Unit,Actor,force,updateVitals);}
    private sealed class Services(GeneratedNpcStatGraph graph):ItemStatUpdateServices<EquipmentStatUnit>
    {
        private static NotSupportedException Missing()=>new("NPC raw-item graph does not admit gem/set/player/world callback services");
        private GeneratedNpcItem Original(EquipmentStatUnit item)=>item?.Entity as GeneratedNpcItem??throw Missing();
        internal override int Kind(EquipmentStatUnit unit)=>unit.Kind;
        internal override int Class(EquipmentStatUnit unit)=>unit==graph.Actor?(int)graph.Kind:throw Missing();
        internal override bool PlayerBody(EquipmentStatUnit unit)=>throw Missing();
        internal override bool IsType(EquipmentStatUnit item,string type)=>NpcItemAffixes.Pool.IsType(Original(item).Snapshot.Metadata.Code,type);
        internal override int Quality(EquipmentStatUnit item)=>Original(item).Snapshot.Metadata.Quality;
        // Qualified native creation has no BROKEN flag;durability-loss/flag
        // mutation requires a separately admitted mutable item state.
        internal override bool Broken(EquipmentStatUnit item){_=Original(item).Snapshot;return false;}
        internal override bool DynamicPath(EquipmentStatUnit unit)=>graph.HasPath;
        internal override EquipmentStatUnit Owner(EquipmentStatUnit item)=>EquipmentStatGraph.Owner(item,out _);
        internal override void Merge(EquipmentStatUnit unit,EquipmentStatUnit item,bool damage)=>EquipmentStatGraph.Merge(unit,item,damage,graph.Values);
        internal override void Expire(EquipmentStatUnit unit,EquipmentStatUnit item)=>EquipmentStatGraph.Detach(item.Stats,graph.Values);
        internal override void UpdateRate(EquipmentStatUnit unit)=>graph.Rate(graph.Native);
        internal override void ClampVitals(EquipmentStatUnit unit)=>graph.Clamp(graph.Native);
        internal override bool BaseRecord(EquipmentStatUnit item)=>throw Missing();internal override bool GemRecord(EquipmentStatUnit item)=>throw Missing();
        internal override EquipmentStatUnit MainWeapon(EquipmentStatUnit unit)=>throw Missing();internal override void InvalidItem()=>throw Missing();
        internal override void GemProperties(int mode,EquipmentStatUnit owner,EquipmentStatUnit item,EquipmentStatUnit target)=>throw Missing();
        internal override void Sets(EquipmentStatUnit unit,EquipmentStatUnit item,bool remove)=>throw Missing();
        internal override void Books(EquipmentStatUnit unit,EquipmentStatUnit item,bool add)=>throw Missing();
    }
}
