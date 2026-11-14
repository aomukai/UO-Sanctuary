// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
namespace Sanctuary;

// Source-motion/native-position transport for an explicitly registered path.
// Planning,velocity/mode selection,clock ownership and automatic AI are not
// inferred. Reuses the shared ground/collision and room/update-list adapters.
internal static class GoatmanNativeWalking
{
    internal static ActorMotionResult Advance(SkeletonGuardian body,ActorMotionState motion)
    {
        ArgumentNullException.ThrowIfNull(motion);
        if(body is not{Alive:true,Deleted:false}||DeathMarkWorld.State(body)is not{Selector:1})
        {throw new NotSupportedException("Native walking needs an original registered ground demon");}
        int size=DemonPathWorld.Size(body);
        var path=DeathMarkWorld.Path(body);
        if(path.X!=motion.X||path.Y!=motion.Y||path.Size5c!=size||(path.Flags&1)==0)
        {throw new NotSupportedException("Walking kernel does not match its qualified active source path");}
        var result=SourceActorMotion.Advance(motion,(x,y)=>DemonPathWorld.Collision(body,x,y),DemonPathWorld.Mask(body));
        if(result.State.X!=motion.X||result.State.Y!=motion.Y)
        {DeathMarkWorld.WalkPrepared(body,result.State.X,result.State.Y);}
        return result;
    }
}
