// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Mobiles;
using Server.Spells;
namespace Sanctuary;

public sealed class PlayerItemProcProfiles:GenericPersistence
{
    private PlayerProcSave Snapshot;
    public PlayerItemProcProfiles():base("SanctuaryPlayerItemProcs",257){}
    public override void Serialize(){Snapshot=PlayerItemProcs.Capture();base.Serialize();}
    public override void Serialize(IGenericWriter writer){writer.Write(0);writer.Write(JsonSerializer.Serialize(Snapshot));}
    public override void Deserialize(IGenericReader reader)
    {if(reader.ReadInt()!=0){throw new InvalidDataException("Unknown player item-proc version");}PlayerItemProcs.Restore(JsonSerializer.Deserialize<PlayerProcSave>(reader.ReadString()));}
}

// Persistent source fields belong to the original item;source event RNG and
// registration history belong to each player. Enrollment/seed initialization
// remain explicit until complete original-item projection/world binding.
internal static class PlayerItemProcs
{
    private static Dictionary<uint,PlayerProcItem> Providers=new();
    private static Dictionary<uint,PlayerProcOwner> Owners=new();
    private static readonly Dictionary<uint,PlayerItemProcGraph> Graphs=new();
    public static void Configure()=>_=new PlayerItemProcProfiles();
    private static ItemPropertyCreationState Copy(ItemPropertyCreationState state)=>state with{ItemBase=(EquipmentStatValue[])state.ItemBase.Clone(),Properties=(EquipmentStatValue[])state.Properties.Clone()};
    internal static PlayerProcItem Provider(uint item)=>Providers.TryGetValue(item,out var p)?p with{State=Copy(p.State)}:null;
    private static void Validate(PlayerProcItem item)
    {
        if(item is not{State:{ItemBase.Length:0,Properties:not null}}||!((Serial)item.Item).IsItem||item.Body is<0 or>10
            ||item.State.Ethereal||!item.State.HasProperties&&item.State.Properties.Length!=0
            ||item.State.Properties.Select(v=>v.Packed).Distinct().Count()!=item.State.Properties.Length
            ||item.State.Properties.Any(v=>v.Stat is not(195 or 196 or 198 or 201)||v.Value<=0||((ushort)v.Packed>>6)!=47||(v.Packed&63)==0))
        {throw new InvalidDataException("Player proc provider needs its complete explicit Fire Ball event-only fields");}
    }
    internal static void Register(Item item,int body,ItemPropertyCreationState state,bool refresh=true)
    {
        if(item is not{Deleted:false}||!ClassGear.Converted(item)){throw new NotSupportedException("Player proc provider needs its original converted native item");}
        var saved=new PlayerProcItem(item.Serial.Value,body,state);Validate(saved);saved=saved with{State=Copy(state)};
        if(Providers.TryGetValue(saved.Item,out var old))
        {if(JsonSerializer.Serialize(old)!=JsonSerializer.Serialize(saved)){throw new InvalidOperationException("Original proc fields cannot be replaced or rerolled");}return;}
        Providers.Add(saved.Item,saved);if(refresh&&item.Parent is PlayerMobile owner){Refresh(owner);}
    }
    internal static bool RegisterOriginal(Item original)
    {
        var fields=OriginalItemProcProjection.Capture(original);
        if(fields.Events.Properties.Length==0){return false;}
        Register(original,fields.Full.Body,fields.Events,refresh:false);return true;
    }
    internal static void InitializeActor(PlayerMobile owner,ulong seed)
    {
        if(owner is not{Deleted:false}||ClassCreation.Profiles.TryGet(owner.Serial,out _)!=true){throw new NotSupportedException("Player proc seed needs its actual class actor");}
        if(Owners.ContainsKey(owner.Serial.Value)){throw new InvalidOperationException("Player proc RNG already initialized");}
        Owners.Add(owner.Serial.Value,new(owner.Serial.Value,seed,[],new(true,0,[])));Refresh(owner);
    }
    internal static bool Initialized(PlayerMobile owner)=>owner!=null&&Owners.ContainsKey(owner.Serial.Value);
    internal static bool ActiveProvider(PlayerMobile owner)=>owner is{Alive:true,Deleted:false}
        &&owner.Items.Any(i=>Providers.ContainsKey(i.Serial.Value)&&ClassGear.Active(owner,i));
    private static PlayerItemProcGraph Require(PlayerMobile owner)
    {
        if(owner==null||!Owners.TryGetValue(owner.Serial.Value,out var saved)){throw new NotSupportedException("Player item procs need explicit initialized source RNG");}
        if(!Graphs.TryGetValue(owner.Serial.Value,out var graph)){Graphs.Add(owner.Serial.Value,graph=PlayerItemProcGraph.Restore(owner,saved));}
        return graph;
    }
    internal static void Refresh(PlayerMobile owner)
    {
        if(World.Loading||owner is not{Deleted:false}){return;}
        foreach(var original in owner.Items.Where(OriginalItemProcProjection.HasDeclaredProc)){RegisterOriginal(original);}
        if(Owners.ContainsKey(owner.Serial.Value)){Require(owner).Refresh();}
    }
    internal static int Chance(PlayerMobile owner,int stat,int layer)
    {var graph=Require(owner);graph.Refresh();return graph.Chance(stat,layer);}
    internal static PlayerItemProcOutcome[] Dispatch(PlayerMobile owner,string phase,NpcItemTarget target,Point3D path,PreparedMonsterDamagePacket packet,
        bool uninterruptible,DateTime at,Func<NpcItemMissileRequest,NpcItemSpellAllocation> allocate,bool? actions=null,int procBonus=0)
    {
        ArgumentNullException.ThrowIfNull(allocate);
        if(phase is not("domeleeattack"or"domissileattack"or"domeleedamage"or"domissiledamage"or"damagedinmelee"or"damagedbymissile")||at.Kind!=DateTimeKind.Utc)
        {throw new NotSupportedException("Player proc host needs its exact qualified source phase and UTC frame");}
        var graph=Require(owner);graph.Refresh();
        if(World.Loading||owner is not{Alive:true,Deleted:false}||owner.Map==null||owner.Map==Map.Internal||SpellHelper.IsTown(owner.Location,owner)
            ||!(actions??owner.NetState is{Running:true})){return [];}
        if(target!=null){_=target.Point(owner.Map);}
        return graph.Dispatch(phase,target,path,packet,uninterruptible,at,allocate,procBonus);
    }
    internal static PlayerProcSave Capture()
    {
        var items=Providers.Values.Where(p=>World.FindItem((Serial)p.Item)is{Deleted:false}).OrderBy(p=>p.Item).Select(p=>p with{State=Copy(p.State)}).ToArray();
        var owners=new List<PlayerProcOwner>();
        foreach(var saved in Owners.Values.OrderBy(p=>p.Actor))
        {if(World.FindMobile((Serial)saved.Actor)is PlayerMobile{Deleted:false} p){var graph=Require(p);graph.Refresh();owners.Add(graph.Capture());}}
        var result=new PlayerProcSave(0,"3.3.93847",items,owners.ToArray());Validate(result);return result;
    }
    private static void Validate(PlayerProcSave state)
    {
        if(state is not{Version:0,ReferenceVersion:"3.3.93847",Items:not null,Owners:not null}||state.Items.Any(i=>i==null)||state.Owners.Any(o=>o==null||o.Items==null||o.Items.Any(i=>i==null))
            ||state.Items.Select(i=>i.Item).Distinct().Count()!=state.Items.Length||state.Owners.Select(o=>o.Actor).Distinct().Count()!=state.Owners.Length
            ||state.Owners.SelectMany(o=>o.Items??[]).Select(i=>i.Item).Distinct().Count()!=state.Owners.Sum(o=>o.Items?.Length??0))
        {throw new InvalidDataException("Invalid player proc provider/owner snapshot");}
        var providers=state.Items.ToDictionary(i=>i.Item);foreach(var item in state.Items){Validate(item);}foreach(var owner in state.Owners){PlayerItemProcGraph.Validate(owner,providers);}
    }
    internal static void Restore(PlayerProcSave state)
    {
        Validate(state);var items=state.Items.ToDictionary(i=>i.Item,i=>i with{State=Copy(i.State)});
        var owners=state.Owners.ToDictionary(i=>i.Actor,i=>i with{Items=(PlayerProcAttachment[])i.Items.Clone(),Events=i.Events with{Entries=(UnitEventEntry[])i.Events.Entries.Clone()}});
        Providers=items;Owners=owners;Graphs.Clear();
    }
}
