// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
namespace Sanctuary;

internal sealed record GenericPetOwnerPlayer(int Kind,bool HasData,bool HasStore);
internal sealed record GenericPetOwnedNode(byte Flags,int Id,int Next);
internal sealed record GenericPetOwnerIteratorServices(Func<int,GenericPetOwnerPlayer> Player,
    Func<byte> Selector,Func<byte,int> TypeCount,Func<int,int,int> First,
    Func<int,GenericPetOwnedNode> Node,Func<int,int,int,int> Resolve);

// Current35e6d0/35e510. Player pet stores are distinct from the monster-owner
// list used by GenericPetTraversal.Owned. Raw container allocation is managed.
internal static class GenericPetOwnerIterator
{
    internal static void Bucket(int game,int first,Action<int> callback,GenericPetOwnerIteratorServices services)
    {
        ArgumentNullException.ThrowIfNull(callback);ArgumentNullException.ThrowIfNull(services);
        var snapshot=new List<int>();int node=first;
        while(node!=0)
        {
            var entry=services.Node(node)??throw new NotSupportedException("Missing source owned-pet node");
            int next=entry.Next; // Cached before registry lookup,not after callback.
            if((entry.Flags&1)==0)
            {
                if(game==0){throw new NotSupportedException("Current active pet-node lookup requires a game");}
                int unit=services.Resolve(game,1,entry.Id);if(unit!=0){snapshot.Add(unit);}
            }
            node=next;
        }
        // No deduplication or re-resolution. Mutations in one callback do not
        // alter this bucket's collected sequence;later buckets remain live.
        foreach(int unit in snapshot){callback(unit);}
    }
    internal static void Run(int game,int owner,Action<int> callback,GenericPetOwnerIteratorServices services)
    {
        ArgumentNullException.ThrowIfNull(callback);ArgumentNullException.ThrowIfNull(services);
        if(owner==0){throw new NotSupportedException("Current pet iterator requires a player owner");}
        var player=services.Player(owner);
        if(player?.Kind!=0||!player.HasData){throw new NotSupportedException("Current pet iterator requires player data");}
        if(!player.HasStore){return;}
        if(game==0){throw new NotSupportedException("Current pet-type table lookup requires a game");}
        byte selector=services.Selector();int type=1;
        for(;;)
        {
            if(selector is <1 or >3){throw new NotSupportedException("Current pet iterator requires selector1..3");}
            if(type>=services.TypeCount(selector)){return;}
            if(type<services.TypeCount(selector))
            {
                Bucket(game,services.First(owner,type),callback,services);
                selector=services.Selector();
            }
            type=unchecked(type+1);
        }
    }
    internal static GenericPetInitializationServices Bind(GenericPetInitializationServices services,int game,GenericPetOwnerIteratorServices iterator)
    {
        ArgumentNullException.ThrowIfNull(services);ArgumentNullException.ThrowIfNull(iterator);
        return services with{OwnerUnits=(owner,visit)=>Run(game,owner,visit,iterator)};
    }
}
