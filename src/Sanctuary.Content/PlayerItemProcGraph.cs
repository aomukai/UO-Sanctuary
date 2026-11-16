// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Server;
using Server.Mobiles;
namespace Sanctuary;

internal sealed record PlayerProcItem(uint Item,int Body,ItemPropertyCreationState State);
internal sealed record PlayerProcAttachment(uint Item,bool Damage);
internal sealed record PlayerProcOwner(uint Actor,ulong Seed,PlayerProcAttachment[] Items,UnitEventState Events);
internal sealed record PlayerProcSave(int Version,string ReferenceVersion,PlayerProcItem[] Items,PlayerProcOwner[] Owners);

// Event-only actor graph. Providers retain original item-owned fields;numeric
// equipment stats and full item initialization remain their separate graph.
internal sealed class PlayerItemProcGraph
{
    private sealed record Carrier(Item Item,PlayerProcItem Source,ItemPropertyCreationContext Graph);
    private sealed record Frame(NpcItemTarget Target,Point3D Path,PreparedMonsterDamagePacket Packet,bool Uninterruptible,DateTime At,
        Func<NpcItemMissileRequest,NpcItemSpellAllocation> Allocate,List<PlayerItemProcOutcome> Results);
    private readonly PlayerMobile Owner;
    private readonly EquipmentStatUnit Unit;
    private readonly EquipmentStatValues Values;
    private readonly List<Carrier> Items=[];
    private SourceUnitEvents Events;
    private ItemEventBindings Bindings;
    private Frame Current;
    private ulong Seed;
    private static readonly EquipmentPropertyBase EmptyBasis=new(0,0,0,0,0,0,0,0);
    internal PlayerItemProcGraph(PlayerMobile owner,ulong seed)
    {
        Owner=owner;Seed=seed;Unit=new(owner,0);Unit.Stats=new(){Flags=EquipmentStatNode.Extended,OwnerKind=0,Owner=Unit};
        Values=new(u=>Items.FirstOrDefault(i=>ReferenceEquals(i.Graph.Unit,u))?.Source.Body??0,_=>null,_=>throw new NotSupportedException("Proc-only graph has no class-stat service"),Changed);
        Events=new(true,Invoke);Bindings=new(Events,ItemEventDefinitions.Bindings);
    }
    private void Changed(EquipmentStatNode node,EquipmentStatUnit source,uint packed,int oldValue,int value)
    {if(node==Unit.Stats){Bindings.Changed(packed,value);}}
    private void Attach(PlayerProcItem provider,bool damage)
    {
        if(World.FindItem((Serial)provider.Item)is not{Deleted:false} item){return;}
        var graph=ItemPropertyCreationContext.Restore(item,Values,EmptyBasis,false,false,false,provider.State);
        Items.Add(new(item,provider,graph));EquipmentStatGraph.Merge(Unit,graph.Unit,damage,Values);
    }
    internal void Refresh()
    {
        var wanted=Owner is{Alive:true,Deleted:false}?Owner.Items.Where(i=>PlayerItemProcs.Provider(i.Serial.Value)!=null&&ClassGear.Active(Owner,i)).ToArray():[];
        var ids=wanted.Select(i=>i.Serial.Value).ToHashSet();
        foreach(var old in Items.Where(i=>!ids.Contains(i.Item.Serial.Value)).ToArray())
        {EquipmentStatGraph.Detach(old.Graph.Unit.Stats,Values);Items.Remove(old);}
        foreach(var item in wanted)
        {
            bool damage=!WeaponHands.Member(Owner,item)||WeaponHands.Selected(Owner)==item;
            var current=Items.SingleOrDefault(i=>i.Item==item);
            if(current==null){Attach(PlayerItemProcs.Provider(item.Serial.Value),damage);}
            else{EquipmentStatGraph.ToggleDamage(Unit,current.Graph.Unit,damage,Values);}
        }
    }
    internal int Chance(int stat,int layer)=>Values.Query(Unit.Stats,EquipmentStatKey.Pack(stat,layer));
    private int Invoke(int function,int phase,int guid1,int guid2)
    {
        var frame=Current??throw new InvalidOperationException("Player event callback has no source frame");
        var outcome=PlayerItemProcDispatch.Run(function,unchecked((uint)guid1),Owner,frame.Target,frame.Path,frame.Packet,Chance,
            ()=>SourceRandom.Roll(ref Seed),frame.Uninterruptible,frame.At,frame.Allocate);
        frame.Results.Add(outcome);return outcome.CallbackResult;
    }
    internal PlayerItemProcOutcome[] Dispatch(string phase,NpcItemTarget target,Point3D path,PreparedMonsterDamagePacket packet,bool uninterruptible,DateTime at,
        Func<NpcItemMissileRequest,NpcItemSpellAllocation> allocate)
    {
        var results=new List<PlayerItemProcOutcome>();var previous=Current;Current=new(target,path,packet,uninterruptible,at,allocate,results);
        try{Events.Trigger(ItemEventDefinitions.Code(phase));return results.ToArray();}
        finally{Current=previous;}
    }
    internal PlayerProcOwner Capture()
    {
        if(Current!=null||Events.Dispatching){throw new NotSupportedException("Capture player proc state only between callbacks");}
        return new(Owner.Serial.Value,Seed,Items.Select(i=>new PlayerProcAttachment(i.Item.Serial.Value,!i.Graph.Unit.Stats.Is(EquipmentStatNode.Dynamic))).ToArray(),Events.Capture());
    }
    private static (byte Event,int Guid1,int Guid2,int Function,uint Queue,int Key) Signature(UnitEventEntry e)=>(e.Event,e.Guid1,e.Guid2,e.Function,e.Queue,e.Key);
    internal static void Validate(PlayerProcOwner owner,IReadOnlyDictionary<uint,PlayerProcItem> providers)
    {
        if(owner is not{Items:not null,Events:{HasOwner:true,Entries:not null}}||!((Serial)owner.Actor).IsMobile
            ||owner.Items.Any(i=>i==null||!providers.ContainsKey(i.Item))||owner.Items.Select(i=>i.Item).Distinct().Count()!=owner.Items.Length)
        {throw new InvalidDataException("Invalid player proc owner/items");}
        _=SourceUnitEvents.Restore(owner.Events,(_,_,_,_)=>throw new InvalidOperationException("Validation cannot dispatch"));
        var values=owner.Items.SelectMany(i=>providers[i.Item].State.Properties.Where(v=>i.Damage||(EquipmentStatValues.Definition(v.Stat).Flags&4)==0))
            .GroupBy(v=>v.Packed).Where(g=>g.Sum(v=>(long)v.Value)!=0).Select(g=>g.Key);
        var expected=values.SelectMany(key=>ItemEventDefinitions.Bindings((int)(key>>16)).Where(b=>b.Event>0)
            .Select(b=>((byte)b.Event,unchecked((int)key),0,(int)b.Function,2u,unchecked((int)key)))).Order();
        if(!owner.Events.Entries.Select(Signature).Order().SequenceEqual(expected))
        {throw new InvalidDataException("Player event subscriptions differ from captured item fields");}
    }
    internal static PlayerItemProcGraph Restore(PlayerMobile owner,PlayerProcOwner saved)
    {
        var graph=new PlayerItemProcGraph(owner,saved.Seed);
        foreach(var item in saved.Items){graph.Attach(PlayerItemProcs.Provider(item.Item),item.Damage);}
        var active=graph.Unit.Stats.FullValues.Where(v=>v.Value!=0).Select(v=>unchecked((int)v.Packed)).ToHashSet();
        var retained=saved.Events with{Entries=saved.Events.Entries.Where(e=>active.Contains(e.Guid1)).ToArray()};
        if(!retained.Entries.Select(Signature).Order().SequenceEqual(graph.Events.Capture().Entries.Select(Signature).Order()))
        {throw new InvalidDataException("Loaded player proc graph differs from its original subscriptions");}
        graph.Events=SourceUnitEvents.Restore(retained,graph.Invoke);graph.Bindings=new(graph.Events,ItemEventDefinitions.Bindings);return graph;
    }
}
