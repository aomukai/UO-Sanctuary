// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
namespace Sanctuary;

internal sealed record GenericPetEventClock(int Frame,int Mode,byte Selector,int StatCount,bool HasStatRoot,int Root1c,uint Rootb34,uint Flags124);
internal sealed record GenericPetTimedEvent(int Handle,int Actor,int Type,int Frame,ushort Flags,int Param0,int Param1,int Param2);
internal sealed record GenericPetEventBinding(GenericPetRootContext Context,GenericPetRootServices Services);

// Current timed event producer/remover;not SourceUnitEvents subscriptions or
// SourceAnimationEvents tags. No dispatcher,timer or allocator is installed.
// This supported store contains monster events with a null callback only.
internal sealed class GenericPetTimedEvents
{
    private readonly Dictionary<int,GenericPetTimedEvent> _nodes=new();
    private readonly Dictionary<int,List<int>> _units=new(),_buckets=new();
    private int _next=1;private readonly Stack<int> _free=new();
    internal GenericPetTimedEvent[] Nodes=>_nodes.Values.OrderBy(n=>n.Handle).ToArray();
    internal int[] Unit(int actor)=>_units.TryGetValue(actor,out var list)?list.ToArray():[];
    internal int[] Bucket(int remainder)=>_buckets.TryGetValue(remainder,out var list)?list.ToArray():[];
    internal GenericPetTimedEvent Node(int handle)=>_nodes[handle];
    internal static int Frame(int current,int requested)=>requested<=current?unchecked(current+1):requested;
    internal int Schedule(int actor,int type,int requested,Func<GenericPetEventClock> live,Action deferred,int p0=0,int p1=0,int p2=0)
    {
        ArgumentNullException.ThrowIfNull(live);ArgumentNullException.ThrowIfNull(deferred);var clock=live();
        if(type>=15){return 0;}
        if(actor<=0||type<0||requested==-1){throw new NotSupportedException("Unsupported timed event actor,type or untimed sentinel");}
        int frame=Frame(clock.Frame,requested);
        if(type==2)
        {
            if(clock.Selector is <1 or >3){throw new NotSupportedException("Timed monster AI event requires selector1..3");}
            if(clock.StatCount>54&&clock.HasStatRoot&&clock.Root1c<0&&(clock.Rootb34&0x400000)!=0)
            {
                deferred();var after=live();
                if((after.Flags124&0x10000)!=0||after.Mode is 0 or 12){return 0;}
            }
        }
        if(_next==int.MaxValue&&_free.Count==0){throw new NotSupportedException("Timed event handle space exhausted");}
        int id=_free.Count>0?_free.Pop():_next++;_nodes[id]=new(id,actor,type,frame,0,p0,p1,p2);
        if(!_units.TryGetValue(actor,out var units)){_units.Add(actor,units=[]);}units.Insert(0,id);
        int bucket=frame%64;if(!_buckets.TryGetValue(bucket,out var times)){_buckets.Add(bucket,times=[]);}times.Insert(0,id);
        return id;
    }
    internal void Processing(int handle)
    {var node=Node(handle);if((node.Flags&2)!=0){throw new NotSupportedException("Released event cannot become processing");}_nodes[handle]=node with{Flags=checked((ushort)(node.Flags|1))};}
    internal void Remove(int handle)
    {
        var node=Node(handle);
        if((node.Flags&1)!=0){_nodes[handle]=node with{Flags=checked((ushort)(node.Flags|8))};return;}
        if((node.Flags&2)!=0){throw new NotSupportedException("Timed event already released");}
        _units[node.Actor].Remove(handle);_buckets[node.Frame%64].Remove(handle);_nodes[handle]=node with{Flags=2};_free.Push(handle);
    }
    internal int Idle(int actor,int ticks,Func<GenericPetEventClock> live,Action neutral,Action deferred)
    {
        ArgumentNullException.ThrowIfNull(neutral);ArgumentNullException.ThrowIfNull(live);
        if(live().Mode!=1){neutral();}
        // Source reads unit events after the neutral transition and caches next
        // before each removal. A processing node is marked rather than freed.
        foreach(int id in Unit(actor)){if(Node(id).Type==2){Remove(id);}}
        return Schedule(actor,2,unchecked(live().Frame+(ticks==0?1:ticks)),live,deferred);
    }
    internal GenericPetEventBinding Bind(GenericPetRootContext context,GenericPetRootServices services,int actor)
    {
        ArgumentNullException.ThrowIfNull(context);ArgumentNullException.ThrowIfNull(services);
        var nodes=Unit(actor);
        return new(context with{EventTypes=nodes.Select(id=>checked((byte)Node(id).Type)).ToArray()},
            services with{RemoveEvent=index=>Remove(nodes[index])});
    }
}
