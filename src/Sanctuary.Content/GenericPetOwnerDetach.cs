// SPDX-License-Identifier: GPL-3.0-or-later
using System;
namespace Sanctuary;

internal sealed class GenericPetOwnedEntry(uint flags,int id,uint field08,ushort field0c,int field10,int next)
{
    internal uint Flags=flags,Field08=field08;
    internal int Id=id,Field10=field10,Next=next;
    internal ushort Field0c=field0c;
    internal GenericPetOwnedNode View()=>new(unchecked((byte)Flags),Id,Next);
}
internal sealed class GenericPetOwnerBucket(int first,int count=0)
{
    internal int First=first,Count=count;
    internal GenericPetOwnerBucket Shared;
}
internal sealed class GenericPetOwnerClient(int id){internal int Id=id;}
internal sealed record GenericPetDetachNotice(ushort Field0c,int Result);
internal sealed record GenericPetOwnerStoreServices(Func<int,GenericPetOwnerPlayer> Player,Func<int,int> Store,
    Func<int,byte> OwnerSelector,Func<byte,int> TypeCount,Func<int,int,GenericPetOwnerBucket> At,Func<int,GenericPetOwnedEntry> Node);
internal static class GenericPetOwnerStore
{
    internal static int Store(int owner,GenericPetOwnerStoreServices services)
    {
        if(owner==0){throw new NotSupportedException("Pet store requires a player owner");}
        var player=services.Player(owner);
        if(player is not{Kind:0,HasData:true}){throw new NotSupportedException("Pet store requires player data");}
        if(!player.HasStore){return 0;}
        int store=services.Store(owner);return store!=0?store:throw new NotSupportedException("Inconsistent pet-store view");
    }
    // Complete35b600. Negative type returns before selector validation.
    internal static GenericPetOwnerBucket Bucket(byte selector,int store,int type,GenericPetOwnerStoreServices services)
    {
        if(type<0){return null;}
        if(selector is <1 or >3){throw new NotSupportedException("Pet bucket requires selector1..3");}
        return type<services.TypeCount(selector)?services.At(store,type):null;
    }
    // Complete35d0e0/35d410 share the same first-match scan. Inactive entries
    // still participate;the existing iterator alone skips flag1 entries.
    private static (int Type,GenericPetOwnedEntry Entry) Find(int owner,int id,GenericPetOwnerStoreServices services)
    {
        int store=Store(owner,services);if(store==0){return(0,null);}
        byte selector=services.OwnerSelector(owner);
        for(int type=1;;type=unchecked(type+1))
        {
            if(selector is <1 or >3){throw new NotSupportedException("Pet lookup requires selector1..3");}
            if(type>=services.TypeCount(selector)){return(0,null);}
            var slot=services.At(store,type);int node=slot?.First??0;
            while(node!=0)
            {var entry=services.Node(node);if(entry.Id==id){return(type,entry);}node=entry.Next;}
        }
    }
    internal static int Type(int owner,int id,GenericPetOwnerStoreServices services)=>Find(owner,id,services).Type;
    internal static GenericPetOwnedEntry Data(int owner,int id,GenericPetOwnerStoreServices services)=>Find(owner,id,services).Entry;
    // The source iterator retains its initial backing store across callbacks.
    // Supply that captured handle explicitly;do not re-read player.Store when
    // a later bucket is visited or share a mutable traversal cache.
    internal static GenericPetOwnerIteratorServices Iterator(GenericPetOwnerStoreServices services,int capturedStore,Func<byte> selector,Func<int,int,int,int> resolve)
        =>new(services.Player,selector,services.TypeCount,(_,type)=>services.At(capturedStore,type)?.First??0,
            node=>services.Node(node).View(),resolve);
}
internal static class GenericPetPackedContext
{
    // Complete3706f0;the same expression already used by full AI entry.
    // Query data is not read by this helper;its R8 argument is preserved.
    internal static ulong Read(GenericPetAiGame game,GenericPetAiUnit unit,byte unwrittenTail)
    {
        if(game==null){throw new NotSupportedException("Packed pet context requires its game even when the unit has a snapshot");}
        return unit is{Kind:1,HasData:true}&&(unit.Flags30&4)!=0?unit.Snapshot8c:game.Snapshot(unwrittenTail);
    }
}
internal sealed record GenericPetOwnerDetachServices(GenericPetOwnerStoreServices Store,Func<int,int> Id,
    Func<int,GenericPetAiGame> Game,Func<int,GenericPetAiUnit> Unit,
    Action<int,int,int,int> RemoveType,Func<int,bool,int,int> Apply,
    Func<int,GenericPetOwnerClient> Client,Action<GenericPetOwnerClient> Flush,
    Action<int,GenericPetDetachNotice> Send,Action<int,int,int> Broadcast);
internal static class GenericPetOwnerDetach
{
    // Complete35c970 over qualified store helpers and mandatory external
    // status/count/client/broadcast services. No UO/D2 protocol alias is made.
    internal static void Run(int game,int owner,int pet,byte unwrittenTail,GenericPetOwnerDetachServices services)
    {
        int store=GenericPetOwnerStore.Store(owner,services.Store);if(store==0){return;}
        int id=pet==0?-1:services.Id(pet);int type=GenericPetOwnerStore.Type(owner,id,services.Store);
        if(type!=7){services.RemoveType(game,owner,id,0);}
        if(game==0){throw new NotSupportedException("Pet detachment requires a game for its bucket lookup");}
        var bucket=GenericPetOwnerStore.Bucket(services.Game(game).Selector106,store,type,services.Store);if(bucket==null){return;}
        int cursor=bucket.First;GenericPetOwnedEntry found=null;
        while(cursor!=0){var node=services.Store.Node(cursor);if(node.Id==id){found=node;break;}cursor=node.Next;}
        if(found!=null)
        {
            var data=GenericPetOwnerStore.Data(owner,pet==0?-1:services.Id(pet),services.Store);found.Flags|=1;
            ulong packed=GenericPetPackedContext.Read(services.Game(game),pet==0?null:services.Unit(pet),unwrittenTail);
            if(data==null){throw new NotSupportedException("Detached pet has no returned data field");}
            int result=services.Apply(pet,((packed>>40)&255)!=1,data.Field10);
            var after=services.Store.Player(owner);
            if(after.Kind==0)
            {
                if(!after.HasData){throw new NotSupportedException("Detached player lost its client data");}
                var client=services.Client(owner);
                if(client!=null){var notice=new GenericPetDetachNotice(found.Field0c,result);services.Flush(client);services.Send(client.Id,notice);}
            }
        }
        services.Broadcast(game,0x35b3a0,id);
    }
    internal static GenericPetOwnerLossServices Bind(GenericPetOwnerLossServices original,int game,byte unwrittenTail,GenericPetOwnerDetachServices detach)
        =>original with{Detach35c970=(owner,actor)=>Run(game,owner,actor,unwrittenTail,detach)};
}
