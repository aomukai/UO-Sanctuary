// SPDX-License-Identifier: GPL-3.0-or-later
using System;
namespace Sanctuary;

internal sealed record GenericPetMonsterEventState(byte Selector,int StatCount,bool HasRoot,int Root1c,uint Rootb30,uint Flags124,int Mode);

// Current3de390 for actual monster units and the pinned null-callback table.
// A typed handler remains an explicit dependency;AI entry43b0c0 is not bypassed
// or certified by supplying a prepared GenericPet root as a test callback.
internal static class GenericPetMonsterEventRouter
{
    internal static void Run(GenericPetTimedEvent node,Func<int,GenericPetMonsterEventState> state,Action<GenericPetTimedEvent> handler)
    {
        if((uint)node.Type>14){return;}
        if(node.Type is not(3 or 4 or 5 or 8 or 9 or 12))
        {
            var live=state(node.Actor);
            if(live.Selector is <1 or >3){throw new NotSupportedException("Monster timed-event router requires selector1..3");}
            if(live.StatCount>1&&live.HasRoot&&live.Root1c<0&&(live.Rootb30&2)!=0
                &&(live.Flags124&0x10000)==0&&live.Mode is not(0 or 12)){return;}
        }
        if(node.Type is not(4 or 11 or 13 or 14)){handler(node);}
    }
}

internal sealed partial class GenericPetTimedEvents
{
    private int _cursor;private bool _dispatching;
    private int Previous(int handle)
    {
        var node=Node(handle);var bucket=_buckets[node.Frame%64];int index=bucket.IndexOf(handle);
        if(index<0){throw new NotSupportedException("Timed event is not in its bound bucket");}
        return index==0?0:bucket[index-1];
    }
    internal int Cursor=>_cursor;
    // Current32edd0 walks the supplied chain toward newer entries. The caller
    // chooses its starting node and timed/infinite branch;global tick selection
    // is separate. Reentrant dispatch and throwing callbacks remain unsupported.
    internal void Dispatch(int first,int frame,bool dueOnly,Action<GenericPetTimedEvent> route)
    {
        ArgumentNullException.ThrowIfNull(route);
        if(_dispatching){throw new NotSupportedException("Nested timed event dispatch is not qualified");}
        _cursor=0;_dispatching=true;
        try
        {
            for(int current=first;current!=0;current=_cursor)
            {
                var node=Node(current);
                if((node.Flags&2)!=0){throw new NotSupportedException("Cannot dispatch a released timed event");}
                _cursor=Previous(current);
                if(dueOnly&&node.Frame!=frame){continue;}
                Processing(current);route(Node(current));
                var after=Node(current);_nodes[current]=after with{Flags=checked((ushort)(after.Flags&0xfffe))};
                if(dueOnly||(after.Flags&8)!=0){Remove(current);}
            }
        }
        finally{_dispatching=false;}
    }
}
