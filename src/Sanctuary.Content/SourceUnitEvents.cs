// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Sanctuary;

internal sealed record UnitEventEntry(int Id, byte Event, int Guid1, int Guid2, int Function, uint Queue, int Key, ushort Flags=0);
internal sealed record UnitEventState(bool HasOwner, int NextId, UnitEventEntry[] Entries);

// Complete older unit-event list semantics in the defined callback domain.
// The dispatcher is mandatory; callback return values do not control traversal.
internal sealed class SourceUnitEvents(bool hasOwner, Func<int,int,int,int,int> dispatch)
{
    internal sealed class Node(UnitEventEntry value)
    {
        internal UnitEventEntry Value=value;
        internal Node Previous,Next;
        internal bool Freed;
    }
    private Node Head;
    private int NextId,Depth;
    internal bool HasOwner=>hasOwner;
    internal bool Dispatching=>Depth!=0;
    internal Node Register(byte phase,int guid1,int guid2,int function,uint queue,int key)
    {
        if(!hasOwner){return null;}
        if(dispatch==null){throw new ArgumentNullException(nameof(dispatch));}
        var node=new Node(new(++NextId,phase,guid1,guid2,function,queue,key)){Next=Head};
        if(Head!=null){Head.Previous=node;}Head=node;return node;
    }
    internal Node RegisterFunction(byte phase,int guid1,int guid2,int function,uint queue,int key)
        =>function is >=1 and <=31?Register(phase,guid1,guid2,function,queue,key):null;
    private void Free(Node node)
    {
        if(node.Freed){throw new NotSupportedException("Legacy event double-free has no defined source contract");}
        if(node.Previous!=null){node.Previous.Next=node.Next;}else{Head=node.Next;}
        if(node.Next!=null){node.Next.Previous=node.Previous;}
        node.Freed=true;
    }
    internal void Clear()
    {
        if(!hasOwner){return;}
        if(Depth!=0){throw new NotSupportedException("Freeing a whole active event list needs its current-engine removal contract");}
        for(var node=Head;node!=null;){var next=node.Next;node.Freed=true;node=next;}
        Head=null;
    }
    internal void Unregister(int queue,int key)
    {
        if(!hasOwner){return;}
        for(var node=Head;node!=null;)
        {
            var next=node.Next;
            if(node.Value.Queue==unchecked((uint)queue)&&node.Value.Key==key)
            {
                if((node.Value.Flags&1)!=0){node.Value=node.Value with{Flags=(ushort)(node.Value.Flags|2)};}
                else{Free(node);}
            }
            node=next;
        }
    }
    internal Node Get(int queue,int key,int guid1)
    {
        if(!hasOwner){return null;}
        for(var node=Head;node!=null;node=node.Next)
        {if(node.Value.Queue==unchecked((uint)queue)&&node.Value.Key==key&&node.Value.Guid1==guid1){return node;}}
        return null;
    }
    internal void Trigger(byte phase)
    {
        if(!hasOwner){return;}
        Depth++;
        try
        {
            for(var node=Head;node!=null;)
            {
                if(node.Freed){throw new NotSupportedException("A callback freed the cached successor;legacy allocator behavior is undefined");}
                var next=node.Next;
                if(node.Value.Event==phase)
                {
                    node.Value=node.Value with{Flags=(ushort)(node.Value.Flags|1)};
                    try{dispatch(node.Value.Function,phase,node.Value.Guid1,node.Value.Guid2);}
                    finally
                    {
                        node.Value=node.Value with{Flags=(ushort)(node.Value.Flags&~1)};
                        if((node.Value.Flags&2)!=0||node.Value.Queue==0){Free(node);}
                    }
                }
                node=next;
            }
        }
        finally{Depth--;}
    }
    internal UnitEventState Capture()
    {
        if(Depth!=0){throw new NotSupportedException("Capture an event list only between synchronous callbacks");}
        var rows=new List<UnitEventEntry>();for(var n=Head;n!=null;n=n.Next){rows.Add(n.Value);}
        return new(hasOwner,NextId,rows.ToArray());
    }
    internal static SourceUnitEvents Restore(UnitEventState state,Func<int,int,int,int,int> dispatch)
    {
        if(state is not{Entries:not null}||state.NextId<0||!state.HasOwner&&state.Entries.Length!=0
            ||state.Entries.Any(r=>r==null||r.Id<=0||r.Id>state.NextId||r.Flags!=0)||state.Entries.Select(r=>r.Id).Distinct().Count()!=state.Entries.Length)
        {throw new InvalidDataException("Invalid inactive unit-event snapshot");}
        var result=new SourceUnitEvents(state.HasOwner,dispatch){NextId=state.NextId};Node last=null;
        foreach(var row in state.Entries)
        {
            var node=new Node(row){Previous=last};if(last==null){result.Head=node;}else{last.Next=node;}last=node;
        }
        return result;
    }
}
