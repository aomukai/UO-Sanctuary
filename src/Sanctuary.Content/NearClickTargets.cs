// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;
namespace Sanctuary;

internal static class NearClickTargets
{
    internal static Mobile Acquire(PlayerMobile owner,Point3D cursor,int radius,Func<Mobile,bool> valid,bool stableTies=false)
    {
        if(owner is not{Alive:true,Deleted:false,Map:not null}||owner.Map==Map.Internal||radius is <0 or >CombatProtocol.TeethRange
            ||!owner.InRange(cursor,CombatProtocol.TeethRange)||!owner.InLOS(cursor)){return null;}
        Mobile closest=null;long distance=long.MaxValue;bool tied=false;
        foreach(var target in owner.Map.GetMobilesInRange<Mobile>(cursor,radius))
        {
            long dx=target.X-cursor.X,dy=target.Y-cursor.Y,squared=dx*dx+dy*dy;
            if(squared>(long)radius*radius||!valid(target)){continue;}
            if(squared<distance){distance=squared;closest=target;tied=false;}
            else if(squared==distance){tied=true;if(stableTies&&target.Serial.Value<closest.Serial.Value){closest=target;}}
        }
        return tied&&!stableTies?null:closest;
    }
}
