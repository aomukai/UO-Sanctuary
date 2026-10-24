// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;

namespace Sanctuary;

internal sealed record BladeFollowPosition(int X,int Y,int Frame,bool Alive=true);

// Pinned do20 -> collision -> hit37 control flow. Room masks, ordered unit
// finding, hit success, state allocation and damage are explicit consumers.
internal static class BladeSentinelContact
{
    internal static BladeFollowPosition Advance(BladeFollowPosition state,Point2D? owner,bool ownerDead,bool room,bool path,
        int activation,Func<Point2D,int> relocate,Func<int> collisionMask,Func<uint?> find,
        Func<uint,bool> attack,Action<uint,int> nextHit,Action<uint?> hitEvent,Action<uint> damage)
    {
        if(owner==null || ownerDead){hitEvent(null);return state with{Alive=false};}
        int mask=0;
        if(path){mask=relocate(owner.Value);state=state with{X=owner.Value.X,Y=owner.Value.Y};}
        state=state with{Frame=9}; // do20 writes10; the collision routine decrements it.
        if(!room || !path || (mask&5)!=0 || state.Frame>activation || mask==0){return state;}
        int world=collisionMask();if(world==0){return state;}
        uint? target=find();
        if(target is{} id)
        {
            bool success=attack(id);nextHit(id,25);hitEvent(id);
            if(success){damage(id);} // hit37 returns2, permitting the damage lane.
        }
        else if((world&4)!=0){hitEvent(null);}
        // do20 ignores the collision/router return value, including a miss or
        // barrier teardown request. The live owner controls missile lifetime.
        return state;
    }
}
