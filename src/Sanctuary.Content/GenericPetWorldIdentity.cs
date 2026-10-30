// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Items;
using Server.Mobiles;
namespace Sanctuary;

// Explicit native-world identity projection. A context identifies the server
// world, not a facet or a difficulty. Native serials are the stable GUIDs;
// source object/missile/tile registries require separate producers.
internal sealed class GenericPetWorldIdentity
{
    internal int Game{get;}
    private readonly GenericPetRegistryServices Registry;
    internal GenericPetWorldIdentity(int game)
    {
        if(game==0){throw new ArgumentOutOfRangeException(nameof(game));}
        Game=game;
        Registry=new(First,_=>throw new NotSupportedException("No native source tile registry"),Id,_=>0);
    }
    private int First(int game,int kind,int id)
    {
        if(game!=Game){throw new NotSupportedException("World identity context is not bound");}
        if(kind is not(0 or 1 or 4)){throw new NotSupportedException("No native registry for this source unit kind");}
        if(id<=0){return 0;}
        var serial=(Serial)(uint)id;
        if(kind==4)
        {
            var item=World.FindItem(serial);
            // A native corpse is an Item, but the D2 corpse is a monster unit.
            // Do not accidentally expose that corpse through the loot registry.
            if(item is Corpse){throw new NotSupportedException("Native corpse needs a source monster identity projection");}
            if(item is not{Deleted:false}){return 0;}
            if(ClassGear.Definition(item)==null){throw new NotSupportedException("Native item has no mapped source equipment identity");}
            return id;
        }
        var mobile=World.FindMobile(serial);
        if(kind==1)
        {
            if(World.FindItem(serial) is Corpse){throw new NotSupportedException("Source corpse GUID is the original monster serial,not its native item serial");}
            if(SourceCorpseIdentity.Find(id)!=null){return id;}
            if(mobile is BaseCreature{Alive:false,Deleted:false}){throw new NotSupportedException("Dead native actor needs its saved corpse identity");}
        }
        return mobile is{Deleted:false}&&(kind==0?mobile is PlayerMobile:mobile is BaseCreature)?id:0;
    }
    private static int Id(int handle)
    {
        if(SourceCorpseIdentity.Find(handle)!=null){return handle;}
        var entity=World.FindEntity((Serial)(uint)handle);
        if(entity is not{Deleted:false}){throw new NotSupportedException("World entity disappeared during identity lookup");}
        return checked((int)entity.Serial.Value);
    }
    internal int Resolve(int game,int kind,int id)=>GenericPetRegistry.Resolve(game,kind,id,Registry);
    internal GenericPetOwnerControl PlayerOwner(BaseCreature actor)
    {
        ArgumentNullException.ThrowIfNull(actor);
        if(actor.Deleted){throw new NotSupportedException("Deleted actor has no live ownership projection");}
        // Both native getters share Master and gate it by their respective
        // flags. Reuse that policy rather than inventing two owner routes.
        var owner=actor.GetMaster();
        if(owner==null){return null;}
        if(owner is not PlayerMobile){throw new NotSupportedException("NPC-owned unit needs the actual source owner-node list");}
        return new(Game,0,checked((int)owner.Serial.Value),0);
    }
    private BaseCreature Actor(int handle)
    {
        if(World.FindMobile((Serial)(uint)handle) is not BaseCreature{Deleted:false} actor)
        {throw new NotSupportedException("Source monster has no matching live native actor");}
        return actor;
    }
    private GenericPetOwnerControl Owner(int handle,GenericPetOwnerControl prior)
    {
        var owner=PlayerOwner(Actor(handle));
        if(owner!=null){return owner with{First=prior?.First??0};}
        if(prior is{First:not 0}){throw new NotSupportedException("Unowned actor needs an explicit native owned-unit list binding");}
        return null;
    }
    internal GenericPetTraversalServices Bind(GenericPetTraversalServices original)
    {
        ArgumentNullException.ThrowIfNull(original);
        return original with{Resolve=Resolve,Unit=id=>
        {
            var u=original.Unit(id);
            return u.Kind==1&&u.HasMonsterData?u with{Ai=Owner(id,u.Ai)}:u;
        }};
    }
    internal GenericPetSearchWorldServices Bind(GenericPetSearchWorldServices original)
    {
        ArgumentNullException.ThrowIfNull(original);
        return original with{Resolve=Resolve,Unit=id=>
        {
            var u=original.Unit(id);
            return u.Position.Kind==1&&u.HasMonsterData?u with{Owner=Owner(id,u.Owner)}:u;
        }};
    }
}
