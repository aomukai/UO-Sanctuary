// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Items;
using Server.Mobiles;
namespace Sanctuary;

internal static class NearClickTargets
{
    private static bool ValidCursor(PlayerMobile owner,Point3D cursor,int radius)
        =>owner is{Alive:true,Deleted:false,Map:not null}&&owner.Map!=Map.Internal&&radius is >=0 and <=CombatProtocol.TeethRange
            &&owner.InRange(cursor,CombatProtocol.TeethRange)&&owner.InLOS(cursor);
    private sealed class Selection<T>(Point3D cursor,int radius,Func<T,bool> valid,bool stableTies) where T:class,IEntity
    {
        private T _closest;private long _distance=long.MaxValue;private bool _tied;
        internal void Visit(T target)
        {
            long dx=target.Location.X-cursor.X,dy=target.Location.Y-cursor.Y,squared=dx*dx+dy*dy;
            if(squared>(long)radius*radius||!valid(target)){return;}
            if(squared<_distance){_distance=squared;_closest=target;_tied=false;}
            else if(squared==_distance){_tied=true;if(stableTies&&target.Serial.Value<_closest.Serial.Value){_closest=target;}}
        }
        internal T Result=>_tied&&!stableTies?null:_closest;
    }
    internal static Mobile Acquire(PlayerMobile owner,Point3D cursor,int radius,Func<Mobile,bool> valid,bool stableTies=false)
    {
        if(!ValidCursor(owner,cursor,radius)){return null;}
        var selection=new Selection<Mobile>(cursor,radius,valid,stableTies);
        foreach(var target in owner.Map.GetMobilesInRange<Mobile>(cursor,radius)){selection.Visit(target);}
        return selection.Result;
    }
    internal static Corpse AcquireCorpse(PlayerMobile owner,Point3D cursor,int radius,Func<Corpse,bool> valid,bool stableTies=false)
    {
        if(!ValidCursor(owner,cursor,radius)){return null;}
        var selection=new Selection<Corpse>(cursor,radius,valid,stableTies);
        foreach(var target in owner.Map.GetItemsInRange<Corpse>(cursor,radius)){selection.Visit(target);}
        return selection.Result;
    }
}
