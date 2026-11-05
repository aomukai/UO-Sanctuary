// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Server;
using Server.Mobiles;
namespace Sanctuary;

internal sealed record NpcEquipType(string Code,int Body1,int Body2,string Shoots,string Quiver);
internal sealed record NpcEquipTypes(string ReferenceVersion,NpcEquipType[] Types);
internal sealed record GeneratedNpcEquipState(uint Actor,uint Owner,int Body,EquipMonster Kind,uint Flags);

// Native physical storage/delivery for prepared generated equipment. D2 body
// slots are canonical;Layer.Invalid keeps the stock art placeholder from
// introducing UO hand/armor effects. Live recipient stat/skill/event admission
// remains the next boundary;this does not enable a learned summon actor.
internal static class GeneratedNpcEquipment
{
    private static readonly Lazy<Dictionary<string,NpcEquipType>> Data=new(()=>
    {
        var d=BalanceCatalog.ReadObject<NpcEquipTypes>("npc-equip-types");
        if(d is not{ReferenceVersion:"3.3.93847",Types.Length:105}||d.Types.Any(t=>t.Body1 is<0 or>12||t.Body2 is<0 or>12||t.Shoots==null||t.Quiver==null))
        {throw new InvalidDataException("Invalid owned NPC equip type metadata");}
        return d.Types.ToDictionary(t=>t.Code,StringComparer.Ordinal);
    });
    internal static NpcEquipType Type(string code)=>Data.Value[NpcItemAffixes.Pool.Basis(code).Type];
    internal static EquipPlacementItem Describe(string code,int quality,int file,bool ethereal,int[] damage,int sockets)
    {
        var raw=NpcItemAffixes.Pool.Basis(code).SourceFields;var t=Type(code);
        bool Flag(string key)=>raw.TryGetProperty(key,out var p)&&p.GetInt32()!=0;
        return new(code,4,true,quality,file,ethereal,Flag("stackable"),Flag("2handed"),Flag("1or2handed"),
            NpcItemAffixes.Pool.IsType(code,"weap"),NpcItemAffixes.Pool.IsType(code,"h2h"),t.Body1,t.Body2,t.Shoots,t.Quiver,
            Data.Value.Keys.Where(k=>NpcItemAffixes.Pool.IsType(code,k)).ToArray(),damage,sockets);
    }
    private static EquipPlacementItem Describe(GeneratedNpcItem item)
    {
        var state=item.Snapshot;var values=GeneratedNpcItem.CreationValues();var graph=item.RestoreGraph(values);
        int Stat(int id)=>EquipmentStatValues.SourceRead(graph.Unit.Stats,EquipmentStatKey.Pack(id),EquipmentStatValues.Definition(id));
        return Describe(state.Metadata.Code,state.Metadata.Quality,state.Metadata.File,state.Graph.Ethereal,
            new[]{21,22,23,24,159,160}.Select(Stat).ToArray(),Stat(194));
    }
    internal static GeneratedNpcItem[] Items(BaseCreature actor)
    {
        if(actor is not{Deleted:false,ControlMaster:PlayerMobile{Deleted:false} owner})
        {throw new NotSupportedException("Generated equipment lost its original actor/owner");}
        var items=actor.Items.OfType<GeneratedNpcItem>().Where(i=>!i.Deleted).ToArray();var slots=new HashSet<int>();EquipMonster? kind=null;
        foreach(var item in items)
        {
            var e=item.Snapshot.Equipment;
            if(e==null||e.Actor!=actor.Serial.Value||e.Owner!=owner.Serial.Value||item.Parent!=actor||!slots.Add(e.Body)||kind!=null&&kind!=e.Kind)
            {throw new NotSupportedException("Generated equipment body,kind or original owner binding changed");}
            kind=e.Kind;
        }
        return items;
    }
    internal static bool TryCreate(BaseCreature actor,PlayerMobile owner,EquipMonster kind,PreparedItemRequest request,int body,out GeneratedNpcItem item)
    {
        item=null;
        if(actor is not{Alive:true,Deleted:false}||owner is not{Alive:true,Deleted:false}||actor.ControlMaster!=owner||body is<1 or>10||!Enum.IsDefined(kind))
        {throw new NotSupportedException("NPC generated equipment needs explicit original live actor/owner,kind and primary body slot");}
        var held=Items(actor);if(held.Any(i=>i.Snapshot.Equipment.Kind!=kind)){throw new NotSupportedException("NPC equipment kind changed");}
        var created=GeneratedNpcItem.Create(request);
        try
        {
            var slots=new EquipPlacementItem[13];foreach(var old in held){slots[old.Snapshot.Equipment.Body]=Describe(old);}
            // This private body inventory has no backpack exchange grid. The
            // NPC insertion caller accepts only outcome1,never replacement,
            // swapping or stack merging. Requirements are skipped by source.
            var placement=new EquipPlacementActor(1,0,kind,true,true,true,slots,
                _=>throw new NotSupportedException("NPC creation skips player requirements"),()=>false);
            if(SourceEquipPlacement.Classify(placement,body,Describe(created),true)!=1){created.Delete();return false;}
            created.BeginEquipment(new(actor.Serial.Value,owner.Serial.Value,body,kind,0x1000));
            actor.AddItem(created);
            if(created.Deleted||created.Parent!=actor){throw new InvalidOperationException("Generated NPC item was not attached");}
            // sub_6FC61190 repairs current durability only on successful equip.
            created.CompleteEquipment();item=created;return true;
        }
        catch{if(!created.Deleted){created.Delete();}throw;}
    }
}
