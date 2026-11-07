// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
namespace Sanctuary;

internal sealed record GeneratedNpcEventState(int Version,uint Actor,uint Owner,string Originals,uint[] ActiveItems,UnitEventState Events);

// Registrations belong to the actual full recipient graph, not another set of
// event-only carrier items. The synchronous callback body is a caller service.
internal sealed class GeneratedNpcItemEvents
{
    private readonly GeneratedNpcStatGraph Graph;
    private readonly uint Actor,Owner;
    private readonly string Originals;
    private SourceUnitEvents Events;
    private ItemEventBindings Bindings;
    private Func<int,int,int,int,int> Current;
    internal bool Dispatching=>Events.Dispatching;
    internal GeneratedNpcItemEvents(GeneratedNpcStatGraph graph,uint actor,uint owner,string originals)
    {
        Graph=graph;Actor=actor;Owner=owner;Originals=originals;
        Events=new(true,Invoke);Bindings=new(Events,ItemEventDefinitions.Bindings);
        // Supplied actor bases predate this graph. Their initialization order
        // is not an item replay service and must be supplied separately.
        if(graph.Actor.Stats.BaseValues.Any(v=>v.Value!=0&&Registered(v.Stat)))
        {throw new NotSupportedException("Original NPC event binding needs explicit innate registration history");}
    }
    private int Invoke(int function,int phase,int guid1,int guid2)
        =>(Current??throw new NotSupportedException("Original NPC callback needs its synchronous event service"))(function,phase,guid1,guid2);
    private static bool Registered(int stat)=>ItemEventDefinitions.Bindings(stat)is{Length:>0} row&&row[0].Event>0;
    internal void Changed(EquipmentStatNode node,uint key,int value)
    {if(node==Graph.Actor.Stats){Bindings.Changed(key,value);}}
    internal void Trigger(byte phase,Func<int,int,int,int,int> dispatch)
    {
        ArgumentNullException.ThrowIfNull(dispatch);_=Graph.Stats;
        var before=Current;Current=dispatch;
        try{Events.Trigger(phase);}finally{Current=before;}
    }
    internal GeneratedNpcEventState Capture()
        =>new(0,Actor,Owner,Originals,Graph.ActiveItems,Events.Capture());
    internal static void Validate(GeneratedNpcEventState saved)
    {
        if(saved is not{Version:0,Originals:{Length:64},ActiveItems:not null,Events:{HasOwner:true,Entries:not null}}
            ||!((Server.Serial)saved.Actor).IsMobile||!((Server.Serial)saved.Owner).IsMobile||saved.Actor==saved.Owner
            ||saved.Originals.Any(c=>!Uri.IsHexDigit(c))||saved.ActiveItems.Distinct().Count()!=saved.ActiveItems.Length
            ||saved.ActiveItems.Any(id=>!((Server.Serial)id).IsItem))
        {throw new InvalidDataException("Invalid original NPC event binding");}
        _=SourceUnitEvents.Restore(saved.Events,(_,_,_,_)=>throw new InvalidOperationException("Validation cannot dispatch"));
        foreach(var row in saved.Events.Entries)
        {
            uint key=unchecked((uint)row.Guid1);var definition=ItemEventDefinitions.Bindings((int)(key>>16));
            if(row.Queue!=2||row.Key!=row.Guid1||row.Guid2!=0||definition==null
                ||!definition.Any(e=>e.Event>0&&e.Event==row.Event&&e.Function==row.Function))
            {throw new InvalidDataException("Saved original NPC event is not an actual stat binding");}
        }
    }
    internal void Restore(GeneratedNpcEventState saved)
    {
        Validate(saved);var current=Capture();
        var signature=static(UnitEventEntry row)=>(row.Event,row.Guid1,row.Guid2,row.Function,row.Queue,row.Key);
        if(saved.Actor!=Actor||saved.Owner!=Owner||saved.Originals!=Originals||!saved.ActiveItems.SequenceEqual(current.ActiveItems)
            ||!saved.Events.Entries.Select(signature).Order().SequenceEqual(current.Events.Entries.Select(signature).Order()))
        {throw new InvalidDataException("Saved NPC event history lost its original item/recipient graph");}
        // Replayed registrations prove membership; the saved list owns order,
        // IDs and head history. Never replace it with sorted stat traversal.
        Events=SourceUnitEvents.Restore(saved.Events,Invoke);Bindings=new(Events,ItemEventDefinitions.Bindings);
    }
}
