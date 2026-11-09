// SPDX-License-Identifier: GPL-3.0-or-later
using System;
namespace Sanctuary;

internal sealed record GenericPetOwnerRemoveServices(GenericPetOwnerStoreServices Store,Func<int,GenericPetAiGame> Game,
    Func<int,int,int,int> Resolve,Action<int,uint> AndFlags124,Action<int,int> Special35b450,
    Action<GenericPetOwnedEntry> Free,Action<int,int,int> Broadcast);
internal static class GenericPetOwnerRemove
{
    // Complete35c720. Normal detach calls this with special=0;raw nonzero
    // branches retain their explicit35b450 service rather than invent a kill.
    internal static void Run(int game,int owner,int id,int special,GenericPetOwnerRemoveServices services)
    {
        int store=GenericPetOwnerStore.Store(owner,services.Store);if(store==0){return;}
        int type=GenericPetOwnerStore.Type(owner,id,services.Store);
        if(type==0&&special!=0){services.Special35b450(game,id);}
        if(game==0){throw new NotSupportedException("Pet removal requires a game for bucket lookup");}
        var bucket=GenericPetOwnerStore.Bucket(services.Game(game).Selector106,store,type,services.Store);if(bucket==null){return;}
        int cursor=bucket.First;GenericPetOwnedEntry previous=null;
        while(cursor!=0)
        {
            var entry=services.Store.Node(cursor);int next=entry.Next;
            if(entry.Id==id)
            {
                if(previous==null){bucket.First=next;}else{previous.Next=next;}
                services.Broadcast(game,0x35b3a0,id);
                int lookupId=entry.Id;int actor=services.Resolve(game,1,lookupId);
                if(actor!=0)
                {
                    if(special!=0){services.Special35b450(game,lookupId);}
                    else{services.AndFlags124(actor,0x7fffffff);}
                }
                services.Free(entry);
                bucket.Count=unchecked(bucket.Count-1);
                if(bucket.Shared!=null){bucket.Shared.Count=unchecked(bucket.Shared.Count-1);}
                if(bucket.Count<0){throw new NotSupportedException("Current pet removal asserts after negative bucket count");}
                break;
            }
            previous=entry;cursor=next;
        }
        services.Broadcast(game,0x35b3a0,id);
    }
    internal static GenericPetOwnerDetachServices Bind(GenericPetOwnerDetachServices original,GenericPetOwnerRemoveServices remove)
        =>original with{RemoveType=(game,owner,id,special)=>Run(game,owner,id,special,remove)};
}
