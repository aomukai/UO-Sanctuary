// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Mobiles;
using Server.Spells;
namespace Sanctuary;

// Prepared Britannia world projection for the source Goatman search policy.
// Native rooms/order/allegiance are explicit host mappings,not recovered D2
// room lists or alignment stat nodes. Does not admit automatic AI or casting.
internal sealed class GoatmanNativeSearch
{
    private readonly WarlockGoatmanBody Body;
    private readonly PlayerMobile Owner;
    private readonly GenericPetWorldIdentity Identity;
    private readonly Map Map;
    private readonly Dictionary<int,GenericPetPathTarget> Paths=new();
    private readonly Dictionary<int,int[]> Members=new();
    private readonly Dictionary<int,int> Next=new();
    private readonly Dictionary<int,(int Room,int X,int Y)> Membership=new();
    private readonly int[] Adjacent;
    internal readonly GenericPetTraversalServices Traversal;
    internal readonly GenericPetSearchWorldServices World;
    internal readonly GenericPetSearchWrapperServices Search;
    internal readonly GenericPetInitializationServices Initialization;
    private static int Id(Mobile m)=>checked((int)m.Serial.Value);
    internal GenericPetEventBinding Bind(GenericPetActionRandom random,GenericPetCombatServices combat,GenericPetFollowServices follow,
        GenericPetFollowExecutionServices followExecution,GenericPetActionServices actions,Func<bool,int,ushort> scratch,Action<int> idle)
        =>GoatmanNativeDecision.Bind(Body,Owner,random,Position,Initialization,Search,combat,follow,followExecution,actions,scratch,Level,idle);
    internal GoatmanNativeSearch(WarlockGoatmanBody body,PlayerMobile owner)
    {
        if(body is not{Alive:true,Deleted:false,Prepared:not null}||body.ControlMaster!=owner||owner is not{Alive:true,Deleted:false}
            ||body.Map==null||body.Map==Server.Map.Internal||body.Map!=owner.Map||DeathMarkWorld.State(body)is not{Selector:1})
        {throw new NotSupportedException("Native Goatman search needs its registered living owner/body on one facet");}
        Body=body;Owner=owner;Map=body.Map;Identity=new(DeathMarkWorld.Header(body).Capture().Game);
        _=ReviveMeleeGeometry.Unit(body);var cell=Position(Id(body));var(px,py)=cell.Coordinates();
        int gx=px/32,gy=py/32;var adjacent=new List<int>();
        for(int y=gy-1;y<=gy+1;y++)for(int x=gx-1;x<=gx+1;x++)
        {if(x>=0&&y>=0&&x*32<Map.Width*2&&y*32<Map.Height*2){int room=GenericPetNativePlacement.RoomHandle(Map,x*32,y*32);Members.Add(room,[]);adjacent.Add(room);}}
        Adjacent=adjacent.ToArray();
        // This encloses every cell of the three-by-three room projection,
        // including source/native rounding at the edge of the actor's room.
        var mobiles=SkillCombat.NearbySnapshot<Mobile>(Map,body.Location,33).Where(m=>m is PlayerMobile or BaseCreature)
            .Append(owner).Append(body).DistinctBy(m=>m.Serial).Where(m=>!m.Deleted&&m.Map==Map).OrderBy(m=>m.Serial.Value).ToArray();
        foreach(var group in mobiles.GroupBy(m=>Room(Id(m))))
        {
            if(!Members.ContainsKey(group.Key)){continue;}int[] units=group.Select(Id).ToArray();Members[group.Key]=units;
            for(int i=0;i<units.Length;i++)
            {
                int id=units[i];Next.Add(id,i+1<units.Length?units[i+1]:0);var(x,y)=Position(id).Coordinates();Membership.Add(id,(group.Key,x,y));
            }
        }
        Traversal=new(TraversalUnit,Identity.Resolve,Room,room=>new(0,Members[room].FirstOrDefault(),Adjacent,[]),
            NextUnit,_=>throw new NotSupportedException("Goatman search descriptor1 does not use proxies"),
            _=>throw new NotSupportedException("Monster-owned follower traversal needs its native owner-node binding"),
            index=>index==1?new(0,null):throw new NotSupportedException("Only pinned Goatman search descriptor1 is bound"));
        var primary=GenericPetSearchTables.Bind(1);var secondary=GenericPetSecondaryTables.Table.Bind(1);
        World=new(WorldUnit,primary.Monster,primary.Level,selector=>selector==1?GenericPetMetadata.Table.StateCount:throw new NotSupportedException("Native search selector"),
            Identity.Resolve,(a,b)=>a==b?0u:Hostile(b)?8u:0u,
            (a,b)=>Hostile(b)?0:1,
            (selector,cls)=>secondary.Row(selector,cls)is{} row?new(row.Flags4,row.Flags6):null,
            (_,_)=>throw new NotSupportedException("Goatman melee search does not admit corpse-state reads"),
            id=>Size(id),id=>Size(id),
            (_,_,_,_,_,_,_,_)=>throw new NotSupportedException("Native search rays require actual endpoint height bindings"),
            (_,_)=>throw new NotSupportedException("Untagged Britannia has no D2 level approach threshold"),
            (_,_,_,_)=>throw new NotSupportedException("Goatman search point collision needs its movement adapter"));
        var callback=new GenericPetSearchServices(id=>Wrapper(id).Unit,(_,_,_)=>throw new InvalidOperationException(),
            (id,x,y)=>GenericPetSearch.Squared(Position(id),x,y),_=>null,(_,_,_)=>throw new InvalidOperationException(),
            (_,_)=>throw new InvalidOperationException(),(_,_)=>throw new InvalidOperationException(),_=>throw new InvalidOperationException(),
            _=>throw new InvalidOperationException(),(_,_)=>throw new InvalidOperationException(),(_,_)=>throw new InvalidOperationException());
        callback=GenericPetSearchWorld.Bind(callback,Identity.Game,World,_=>new(0,0,0))with
        {
            Collision27c710=Collision,
            // This adapter has no complete source stat-node producer. The
            // attack-only Goatman never asks for buff/corpse states;do not
            // silently report every unsupported native state absent.
            HasState=(_,_)=>throw new NotSupportedException("Goatman melee search has no buff-state producer")
        };
        var wrapper=new GenericPetSearchWrapperServices(Wrapper,_=>throw new InvalidOperationException(),GenericPetMetadata.Table.Bind(1).SearchServices(),callback,
            (_,_,_,_)=>throw new InvalidOperationException());
        Search=GenericPetTraversal.Bind(wrapper,Identity.Game,Traversal)with{Refresh371b10=Refresh};
        Initialization=new(Identity.Resolve,GenericPetLifePercent.For,id=>Mobile(id)is PlayerMobile?0:1,
            (_,_)=>throw new InvalidOperationException("Goatman decision binds the original native owner census"),
            new(_=>throw new NotSupportedException("Goatman Swarm0 has no per-pet target survey"),(_,_)=>throw new InvalidOperationException(),
                _=>throw new InvalidOperationException(),(_,_,_)=>throw new InvalidOperationException(),_=>throw new InvalidOperationException()));
    }
    private Mobile Mobile(int id)=>Server.World.FindMobile((Serial)(uint)id)is{Deleted:false} m?m
        :throw new NotSupportedException("Native search unit disappeared");
    private bool Hostile(int id)=>SourceCorpseIdentity.Find(id)==null&&Server.World.FindMobile((Serial)(uint)id)is{} m
        &&m.Map==Map&&ClassCombat.IsHostile(Owner,m)&&!SpellHelper.IsTown(m.Location,Owner);
    internal GenericPetDecisionUnit Position(int id)
    {
        if(SourceCorpseIdentity.Find(id)is{} corpse)
        {return new(1,corpse.Body.Map!=null&&corpse.Body.Map!=Server.Map.Internal,corpse.Body.X*2,corpse.Body.Y*2);}
        var m=Mobile(id);if(m is not(PlayerMobile or BaseCreature)){throw new NotSupportedException("Native search has no item/object/missile/tile position producer");}
        int x=m.X*2,y=m.Y*2;
        if(m is WarlockGoatmanBody goat&&DeathMarkWorld.State(goat)!=null)
        {var geometry=ReviveMeleeGeometry.Unit(goat);x=geometry.Cell.X;y=geometry.Cell.Y;}
        // UO accepted movement has a committed tile endpoint,no unread future
        // D2 path. This is the actual native endpoint,not predicted next input.
        return new(m is PlayerMobile?0:1,m.Map!=null&&m.Map!=Server.Map.Internal,x,y,m is PlayerMobile,x,y);
    }
    internal int Room(int id)
    {
        if(SourceCorpseIdentity.Find(id)is{} corpse)
        {return corpse.Body.Map==Map?GenericPetNativePlacement.RoomHandle(Map,corpse.Body.X*2,corpse.Body.Y*2):0;}
        var m=Mobile(id);if(m.Map==null||m.Map==Server.Map.Internal){return 0;}
        if(m.Map!=Map){throw new NotSupportedException("Native search unit left its bound facet");}
        var(x,y)=Position(id).Coordinates();return GenericPetNativePlacement.RoomHandle(Map,x,y);
    }
    internal int? Level(GenericPetDecisionUnit position)
    {
        Mobile m=position.Kind==0?Owner:Body;
        if(position!=Position(Id(m))){throw new NotSupportedException("Native root level requested outside its current pet/owner pose");}
        return SpellHelper.IsTown(m.Location,Owner)?1:null;
    }
    private int Class(Mobile m)=>m is SkeletonGuardian g?GenericPetSearchTables.SourceId(g.SourceMonsterId)??-1
        :m is BaseCreature c?GenericPetSearchTables.SourceId(CreatureProfiles.Definition(c).Reference)??-1:-1;
    private int Size(int id)
    {
        var m=Mobile(id);return m is PlayerMobile?2:m is BaseCreature c?ReviveMeleeGeometry.Unit(c,requireRange:false).Size
            :throw new NotSupportedException("Native search size is unbound");
    }
    private int Mode(Mobile m)=>!m.Alive?m is PlayerMobile?17:12:m is SkeletonGuardian guardian&&DeathMarkWorld.State(guardian)is{} actor?actor.Mode:1;
    private uint Flags(Mobile m)=>!m.Alive?0x10000u:m.CanBeDamaged()&&!m.Blessed?4u:0;
    private GenericPetPathTarget Path(int id)
    {
        if(Paths.TryGetValue(id,out var path)){return path;}
        if(SourceCorpseIdentity.Find(id)!=null){path=new(0,1,0);Paths.Add(id,path);return path;}
        var m=Mobile(id);Mobile target=m.Combatant;
        if(m==Owner&&ClassCombat.For(Owner).Target!=Serial.Zero){target=Server.World.FindMobile(ClassCombat.For(Owner).Target);}
        int handle=target is{Deleted:false}?Id(target):0;
        path=new(handle,target is PlayerMobile?0:1,handle);Paths.Add(id,path);return path;
    }
    private void Refresh(int id)
    {
        int before=Path(id).Target;GenericPetTraversal.Refresh(Identity.Game,id,Traversal);
        if(before!=0&&Path(id).Target==0)
        {
            var m=Mobile(id);if(m.Combatant?.Serial.Value==(uint)before){m.Combatant=null;}
            if(m==Owner&&ClassCombat.For(Owner).Target.Value==(uint)before){ClassCombat.For(Owner).Target=Serial.Zero;}
        }
    }
    private GenericPetTraversalUnit TraversalUnit(int id)
    {
        if(SourceCorpseIdentity.Find(id)!=null){return new(1,12,id,0x10000,0,Path(id),true,null);}
        var m=Mobile(id);var master=(m as BaseCreature)?.GetMaster();
        var control=master==null?null:new GenericPetOwnerControl(Identity.Game,master is PlayerMobile?0:1,Id(master),0);
        return new(m is PlayerMobile?0:1,Mode(m),id,Flags(m),0,Path(id),m is BaseCreature,control);
    }
    private int NextUnit(int id)
    {
        var(x,y)=Position(id).Coordinates();var before=Membership[id];
        if(Room(id)!=before.Room||x!=before.X||y!=before.Y)
        {throw new NotSupportedException("Native search room membership changed during its read-only traversal");}
        return Next[id];
    }
    private GenericPetSearchWrapperUnit Wrapper(int id)
    {
        if(SourceCorpseIdentity.Find(id)is{} corpse)
        {var position=Position(id);return new(new(position,12,0x10000,corpse.ClassId,1,false,false,corpse.Body.X*2*65536,corpse.Body.Y*2*65536),0,0);}
        var m=Mobile(id);var p=Position(id);var(x,y)=p.Coordinates();uint fx=(uint)x<<16,fy=(uint)y<<16;
        if(m is WarlockGoatmanBody goat&&DeathMarkWorld.State(goat)!=null){var path=DeathMarkWorld.Path(goat);fx=path.X;fy=path.Y;}
        return new(new(p,Mode(m),Flags(m),Class(m),1,m is BaseCreature,false,unchecked((int)fx),unchecked((int)fy)),Path(id).Target,0);
    }
    private GenericPetSearchWorldUnit WorldUnit(int id)
    {
        if(SourceCorpseIdentity.Find(id)is{} corpse)
        {return new(Position(id),corpse.ClassId,12,1,Room(id),0,true,0,null,0,0,null,new(-1,-1,0,0));}
        var m=Mobile(id);var t=TraversalUnit(id);byte nativeFlags=m switch
        {EncounterCreature{Grade:EncounterGrade.SuperUnique}=>10,EncounterCreature{Grade:EncounterGrade.Unique}=>8,
            EncounterCreature{Grade:EncounterGrade.Champion}=>4,_=>0};
        // The secondary source table/trait geometry is consulted via Size;
        // initialize the cache invalid,not as an invented known zero size.
        return new(Position(id),Class(m),Mode(m),1,m.Map==Map?Room(id):0,0,m is BaseCreature,nativeFlags,t.Ai,0,0,null,new(-1,-1,0,0));
    }
    private int Collision(int a,int b,int mask)
    {
        if(mask!=4){throw new NotSupportedException("Goatman native search only binds its actual LOS mask4 ray");}
        var first=Mobile(a);var second=Mobile(b);if(first.Map!=Map||second.Map!=Map){return 1;}
        var(x,y)=Position(a).Coordinates();var(tx,ty)=Position(b).Coordinates();
        var nested=new GenericPetSearchNestedServices(World,_=>throw new InvalidOperationException(),_=>throw new InvalidOperationException(),
            _=>throw new InvalidOperationException(),(_,_)=>throw new InvalidOperationException(),(_,_,_)=>throw new InvalidOperationException(),
            _=>throw new InvalidOperationException(),_=>throw new InvalidOperationException(),(_,_)=>throw new InvalidOperationException(),
            (_,_)=>throw new InvalidOperationException(),(_,sx,sy,ex,ey,_)=>Map.LineOfSight(new Point3D((sx+1)/2,(sy+1)/2,first.Z+14),new Point3D((ex+1)/2,(ey+1)/2,second.Z+14))?0:1);
        return GenericPetSearchNested.Ray(x,y,Size(a),tx,ty,Size(b),Room(a),mask,nested);
    }
}
