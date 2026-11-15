// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;
using Server.Spells;
namespace Sanctuary;

// Prepared native-body projection. A bound NPC retains its actual AI and
// equipment; it never receives an invented GenericPet path/header/controller.
// Full Bind admission must include this floor/one-native-tile projection in
// its composite proposal, rather than claim a recovered D2R binder footprint.
internal static class DeathMarkBoundWorld
{
    internal static void Require(PlayerMobile owner,BaseCreature body)
    {
        if(BoundDemonHosts.State(body)is not{} state||state.Owner!=owner.Serial.Value
            ||!WarlockDemonRecipients.Active(owner,body)||body.AIObject==null||body.Flying)
        {throw new NotSupportedException("Death Mark needs a qualified active ground bound demon");}
    }
    private static int Place(PlayerMobile owner,BaseCreature body,Point2D target)
    {
        var map=body.Map;var point=new GenericPetNearbyPoint(target.X,target.Y);
        Point3D landing=default;
        bool Fit(int x,int y)
        {
            var tile=new Point3D((x+1)/2,(y+1)/2,body.Z);
            return SigilActors.Ground(owner,map,tile,out landing)&&Teleportation.Space(map,landing,body,allowAllies:true);
        }
        var search=new GenericPetNearbyServices((_,x,y)=>x>0&&y>0&&x<map.Width*2&&y<map.Height*2?1:0,
            r=>Fit(r.X,r.Y)?0:1);
        if(GenericPetNearbySearch.Find(1,point,1,0x1c09,0,50,1,search)==0||!Fit(point.X,point.Y)){return 0;}
        // No ownership, birth stats, wounds or inventory changes. Clear only
        // the interrupted native action and stale movement intent on commit.
        if(body.Spell is Spell spell){spell.Disturb(DisturbType.Kill);}
        body.AIObject.ResetApproachState();body.AIObject.ClearMoveIntent();body.MoveToWorld(landing,map);return 1;
    }
    internal static bool ApplyPrepared(PlayerMobile owner,BaseCreature body,DeathMarkDirective directive,DateTime at)
    {
        if(at.Kind!=DateTimeKind.Utc||directive==null){throw new ArgumentException("Death Mark needs a UTC directive");}
        Require(owner,body);DeathMarkControlStates.Require(body,directive,at);
        if(directive.ClearControl&&(body.Frozen||body.Paralyzed))
        {throw new NotSupportedException("UO freeze/paralysis has no qualified Death Mark source control list");}
        int before=directive.Remaining;Mobile target=null;
        var services=new DeathMarkPetServices(point=>Place(owner,body,point),_=>body.FixedEffect(0x3728,10,15),
            (kind,id)=>
            {
                target=World.FindMobile((Serial)unchecked((uint)id));
                if(kind is not(1 or 2)||target is not{Alive:true,Deleted:false}||target.Map!=body.Map||!ClassCombat.IsHostile(owner,target))
                {throw new NotSupportedException("Bound Death Mark command lost its hostile recipient");}
            },_=>
            {
                body.Combatant=body.FocusMob=null;
                body.IssueOrder(target==null?OrderType.Guard:OrderType.Attack,null,target);
            },()=>DeathMarkControlStates.Root(body,at),(_,list)=>DeathMarkControlStates.Remove(body,list,at),(_,_)=>{});
        DeathMarkPets.Run(new(true,0,1,1),directive,services);return directive.Remaining<before;
    }
}
