// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
namespace Sanctuary;

internal sealed class GenericPetPathTarget(int target,int type,int id)
{internal int Target=target;internal readonly int Type=type,Id=id;}
internal sealed record GenericPetOwnerControl(int Game,int Type,int Id,int First);
internal sealed record GenericPetTraversalUnit(int Kind,int Mode,int Id,uint Flags124,uint Flags128,
    GenericPetPathTarget Path,bool HasMonsterData,GenericPetOwnerControl Ai);
internal sealed record GenericPetOwnerNode(int Id,int Next);
internal sealed class GenericPetTraversalProxy(byte flags,int target,int game,int type,int id)
{internal readonly byte Flags510=flags;internal int CachedTarget=target;internal readonly int Game=game,Type=type,Id=id;}
internal sealed record GenericPetTraversalRoom(int Level,int First,IReadOnlyList<int> Adjacent,IReadOnlyList<int> Proxies);
internal sealed record GenericPetTraversalDescriptor(int Kind,Func<int,long> Callback);
internal sealed record GenericPetRegistryServices(Func<int,int,int,int> BucketFirst,Func<int,int> TileFirst,
    Func<int,int> Id,Func<int,int> Next158);
internal static class GenericPetRegistry
{
    internal static int Resolve(int game,int type,int id,GenericPetRegistryServices s)
    {
        if(game==0){throw new NotSupportedException("Current registry requires a game");}
        int unit=type==5?s.TileFirst(game):s.BucketFirst(game,type,id);
        while(unit!=0){if(s.Id(unit)==id){return unit;}unit=s.Next158(unit);}return 0;
    }
}
internal sealed record GenericPetTraversalServices(Func<int,GenericPetTraversalUnit> Unit,Func<int,int,int,int> Resolve,
    Func<int,int> RoomForUnit,Func<int,GenericPetTraversalRoom> Room,Func<int,int> Next160,
    Func<int,GenericPetTraversalProxy> Proxy,Func<int,GenericPetOwnerNode> OwnerNode,
    Func<int,GenericPetTraversalDescriptor> Descriptor);
internal static class GenericPetTraversal
{
    internal static void Refresh(int game,int subject,GenericPetTraversalServices s)
    {
        if(subject==0||game==0){throw new NotSupportedException("Current path refresh requires game and unit");}
        var unit=s.Unit(subject);
        if(unit.Kind==5||unit.Path==null){throw new NotSupportedException("Current path refresh requires a non-tile path");}
        var path=unit.Path;if(path.Target==0){return;}
        int resolved=s.Resolve(game,path.Type,path.Id);if(resolved!=path.Target){path.Target=0;}
        // Even a different resolved item is inspected; stale identity was
        // already cleared, and the key/type fields themselves stay unchanged.
        if(resolved!=0&&s.Unit(resolved) is{Kind:4,Mode:1 or 2}){path.Target=0;}
    }
    private static int Owner(int subject,GenericPetTraversalServices s)
    {
        if(subject==0){return 0;}var unit=s.Unit(subject);
        if(unit.Kind!=1||!unit.HasMonsterData||unit.Ai==null||unit.Ai.Game==0){return 0;}
        return s.Resolve(unit.Ai.Game,unit.Ai.Type,unit.Ai.Id);
    }
    internal static void Owned(int subject,Action<int> callback,GenericPetTraversalServices s)
    {
        int owner=Owner(subject,s);if(owner==0||s.Unit(owner).Kind!=1){return;}
        var unit=s.Unit(owner);
        if(!unit.HasMonsterData||unit.Ai==null){throw new NotSupportedException("Current owned enumeration dereferences missing owner AI");}
        var ai=unit.Ai;int node=ai.First;
        while(node!=0)
        {
            var entry=s.OwnerNode(node);int next=entry.Next;
            if(ai.Game==0){throw new NotSupportedException("Current owned enumeration requires a game for nonempty nodes");}
            int target=s.Resolve(ai.Game,1,entry.Id);if(target!=0){callback(target);}node=next;
        }
    }
    private static bool Alive(GenericPetTraversalUnit unit)=>unit.Kind is 0 or 1&&(unit.Flags124&0x10000)==0
        &&unit.Mode!=0&&unit.Mode!=(unit.Kind==0?17:12);
    private static bool Town(int level)=>level is 1 or 40 or 75 or 103 or 109;
    private static GenericPetTraversalRoom RequireRoom(int room,GenericPetTraversalServices s)
    {if(room==0){throw new NotSupportedException("Current traversal requires nonnull adjacent rooms");}return s.Room(room);}
    private static void ValidateProxies(IReadOnlyList<int> proxies)
    {foreach(int proxy in proxies){if(proxy==0){throw new NotSupportedException("Current traversal requires nonnull room proxy entries");}}}
    private static long Rooms(int game,int subject,Func<int,long> callback,int mode,GenericPetTraversalServices s)
    {
        if(mode==2&&(subject==0||!Alive(s.Unit(subject)))){return 0;}
        int start=s.RoomForUnit(subject);if(start==0){return 0;}
        var adjacent=s.Room(start).Adjacent;
        foreach(int id in adjacent)
        {
            var room=RequireRoom(id,s);
            if(mode==2&&Town(room.Level)){continue;}
            if(mode is 1 or 2){ValidateProxies(room.Proxies);}
            if(mode==1)
            {
                foreach(int entry in room.Proxies)
                {
                    var proxy=s.Proxy(entry);int target=proxy.CachedTarget;
                    if((proxy.Flags510&1)==0)
                    {
                        if(target==0){continue;}target=s.Resolve(proxy.Game,proxy.Type,proxy.Id);
                        if(target==0){proxy.CachedTarget=0;}
                    }
                    if(target!=0){long result=callback(target);if(result!=0){return result;}}
                }
                continue;
            }
            if(mode==2&&room.Proxies.Count==0){continue;}
            int unit=room.First;
            while(unit!=0)
            {
                long result=callback(unit);if(result!=0){return result;}
                // The source fetches next160 after the callback, unlike the
                // owner-node iterator which caches its next link beforehand.
                unit=s.Next160(unit);
            }
        }
        return 0;
    }
    internal static long Visit(int game,int subject,int index,Func<int,long> supplied,GenericPetTraversalServices s)
    {
        ArgumentNullException.ThrowIfNull(s);var row=s.Descriptor(index);var callback=row.Callback??supplied;
        if(callback==null){return 0;}
        switch(row.Kind)
        {
            case 1:return Rooms(game,subject,callback,1,s);
            case 2:return Rooms(game,subject,callback,2,s);
            case 3:
            {
                int owner=Owner(subject,s);if(owner==0||s.Unit(owner).Kind!=1){return 0;}
                long found=0;Owned(owner,unit=>{if(found==0){long value=callback(unit);if(value!=0){found=value;}}},s);return found;
            }
            default:return Rooms(game,subject,callback,0,s);
        }
    }
    internal static GenericPetSearchWrapperServices Bind(GenericPetSearchWrapperServices original,int game,GenericPetTraversalServices services)
    {
        ArgumentNullException.ThrowIfNull(original);ArgumentNullException.ThrowIfNull(services);
        return original with
        {
            Unit=id=>
            {
                var snapshot=original.Unit(id);var live=services.Unit(id);
                if(snapshot.Unit.Position.Kind!=live.Kind||snapshot.Unit.Mode!=live.Mode||snapshot.Unit.Flags124!=live.Flags124
                    ||snapshot.Unit.Position.HasPath!=(live.Path!=null))
                {throw new NotSupportedException("Search snapshots disagree with the bound traversal unit");}
                // Refresh mutates this path store. An earlier wrapper snapshot
                // must not resurrect the stale pointer it just cleared.
                return new(snapshot.Unit,live.Path?.Target??0,live.Flags128);
            },
            Refresh371b10=unit=>Refresh(game,unit,services),
            Traverse40d9f0=(unit,_,index,visit)=>{Visit(game,unit,index,target=>visit(target),services);}
        };
    }
}
