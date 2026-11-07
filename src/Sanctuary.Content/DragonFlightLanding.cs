// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using Server;
using Server.Mobiles;
using Server.Spells;

namespace Sanctuary;

// Release-time world adapter. An optional caller claim runs after all landing
// checks and before committed transfer;default callers retain no payment policy.
// Reports actual transfer success,not do52's ignored return.
internal static class DragonFlightLanding
{
    private static bool Target(PlayerMobile owner,BaseCreature target)=>owner is{Deleted:false,Alive:true}
        &&target is{Deleted:false,Alive:true}&&owner.Map!=null&&owner.Map!=Map.Internal&&target.Map==owner.Map
        &&target.X!=0&&target.Y!=0&&SkillHealth.Balance256(owner)>0&&SkillHealth.Balance256(target)>0
        &&ClassCombat.IsHostile(owner,target)&&SpellHelper.CheckTown(owner.Location,owner)&&SpellHelper.CheckTown(target.Location,owner);
    internal static bool StartTarget(PlayerMobile owner,BaseCreature target)
    {
        if(!Target(owner,target)){return false;}
        long dx=2L*(owner.X-target.X),dy=2L*(owner.Y-target.Y),range=SkillMechanics.For(CombatAbility.DragonFlight).Number("Param7");
        return dx*dx+dy*dy<=range*range;
    }
    internal static bool Destination(PlayerMobile owner,BaseCreature target,out Point3D destination)
    {
        destination=default;if(!Target(owner,target)){return false;}
        var map=owner.Map;var origin=owner.Location;var points=new Dictionary<(int,int),Point3D?>();
        Point3D? Resolve(LeapCell cell)
        {
            if(cell.X<0||cell.Y<0||cell.X>=map.Width*2||cell.Y>=map.Height*2){return null;}
            // Same explicit two-source-cells-per-native-tile projection used by
            // LeapTerrain. Half-cell geometry/real D2 room topology is unproved.
            var key=(cell.X/2,cell.Y/2);
            if(!points.TryGetValue(key,out var point))
            {
                point=LeapTerrain.Ground(owner,map,key.Item1,key.Item2,target.Z,out var p)&&p!=origin?p:null;
                points.Add(key,point);
            }
            return point;
        }
        var found=SourceFreeCoordinates.Find(new(target.X*2,target.Y*2),c=>Resolve(c)!=null);
        if(found is not{} selected||Resolve(selected) is not{} landing||!TreasureHunts.CanLand(owner,map,landing)
            ||!Teleportation.TravelAllowed(owner,map,landing)){return false;}
        destination=landing;return true;
    }
    internal static bool Transfer(PlayerMobile owner,BaseCreature target,out Point3D destination,Func<Point3D,bool> claim=null)
    {
        var origin=owner?.Location??default;var map=owner?.Map;
        if(!Destination(owner,target,out destination)||!Target(owner,target)||owner.Map!=map||owner.Location!=origin
            ||!Teleportation.Space(map,destination,owner)){return false;}
        if(claim?.Invoke(destination)==false){return false;}
        Teleportation.CommitTransfer(owner,destination,origin,map);return true;
    }
}
