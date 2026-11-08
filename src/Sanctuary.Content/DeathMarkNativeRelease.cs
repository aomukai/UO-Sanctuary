// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Mobiles;
using Server.Spells;
namespace Sanctuary;

internal sealed record DeathMarkNativeResult(int Result,bool ServerDoRan,int EligibleDemons,Point3D Destination);

// Proposed native-world bridge;no learned handler is enabled. Source table
// selectors/counts and castid stay explicit in the qualified cast context.
// Native safety/floor/LOS and saved summon order replace unavailable D2 room
// flags/list ordering only under the separately documented adaptation.
internal static class DeathMarkNativeRelease
{
    internal static SkeletonGuardian[] Roster(PlayerMobile owner)
    {
        var pets=new List<SkeletonGuardian>();var seen=new HashSet<uint>();
        foreach(var follower in owner.AllFollowers??[])
        {
            if(follower is not SkeletonGuardian{Alive:true,Deleted:false} body||body is not(WarlockGoatmanBody or WarlockTaintedBody or WarlockDefilerBody)
                ||body.ControlMaster!=owner||body.Map!=owner.Map||MinionSafeAreas.Parked(body)) {continue;}
            if(!seen.Add(body.Serial.Value)){throw new NotSupportedException("Duplicate native demon follower");}
            if(!WarlockDemonPlacement.Prepared(body)||WarlockDemonPlacement.Owner(body)!=owner.Serial.Value||!body.Permitted(owner)
                ||DeathMarkWorld.State(body)==null)
            {throw new NotSupportedException("Live original demon has no qualified saved controller registration");}
            pets.Add(body);
        }
        return pets.OrderBy(p=>BattleCommand.PetOrder(p)>0?BattleCommand.PetOrder(p):long.MaxValue).ThenBy(p=>p.Serial.Value).ToArray();
    }
    internal static bool TargetPoint(PlayerMobile owner,Serial selected,Point3D requested,out Point3D ground,out Mobile target)
    {
        target=null;ground=requested;
        if(owner is not{Alive:true,Deleted:false,Map:not null}||owner.Map==Map.Internal||MinionSafeAreas.Contains(owner)) {return false;}
        var entity=World.FindEntity(selected);
        if(entity is{Deleted:false}&&entity!=owner)
        {
            if(entity.Map!=owner.Map||entity is not Mobile mobile||!owner.CanSee(mobile)){return false;}
            target=mobile;requested=mobile.Location;
        }
        // The shared current destination helper uses a live non-self path
        // target,otherwise the requested path destination. Reuse the Sigil
        // host's floor/static/LOS projection,then do155's own38unit range gate.
        return SigilActors.TargetPoint(owner,requested,out ground)&&ground.X!=0&&ground.Y!=0;
    }
    internal static DeathMarkNativeResult RunPrepared(PlayerMobile owner,DeathMarkCastContext qualified,Serial selected,Point3D requested,DateTime at)
    {
        ArgumentNullException.ThrowIfNull(qualified);
        if(!TargetPoint(owner,selected,requested,out var point,out var target)){return new(0,false,0,point);}
        var pets=Roster(owner);if(pets.Length==0){return new(0,false,0,point);}
        bool ran=false;var map=owner.Map;
        int? Level(Mobile actor)=>actor is{Deleted:false,Map:not null}&&actor.Map==map
            ?SpellHelper.IsTown(actor.Location,actor)?1:0:null; // Town predicate sentinel,not a D2level mapping.
        DeathMarkTarget CurrentTarget()=>target==null?null:new(target.Serial.Value,target is PlayerMobile?0:1,target==owner);
        bool NativeTarget(DeathMarkTarget t)=>World.FindMobile((Serial)t.Id)is{} actor&&actor==target&&actor.Map==map&&ClassCombat.IsHostile(owner,actor);
        void Unbound()=>throw new NotSupportedException("Native Death Mark effect was not connected");
        var environment=new DeathMarkCastServices(()=>true,()=>owner.Map==map&&map!=Map.Internal,()=>ran=true,
            _=>throw new NotSupportedException("Unbound native Death Mark formula"),()=>new(point.X*2,point.Y*2),()=>Level(owner),
            ()=>1, // Explicit native adaptation:preflight floor/LOS,then actual per-pet source placement.
            (_,_)=>throw new NotSupportedException("Native floor policy does not impersonate D2level mode2"),()=>{},CurrentTarget,NativeTarget,
            t=>Level(World.FindMobile((Serial)t.Id)),(_,_)=>Unbound(),_=>throw new NotSupportedException("Unbound recipient allocation"),Unbound,Unbound,(_,_)=>Unbound());
        int result=DeathMarkWorldRelease.RunPrepared(owner,qualified,environment,pets,at);
        return new(result,ran,pets.Length,point);
    }
}
