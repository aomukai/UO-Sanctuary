// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;

namespace Sanctuary;

internal sealed record BladeAiState(int Expiry=-1,bool Forward=true,bool MissileAttempted=false);
internal sealed record BladeAiResult(BladeAiState State,bool Dead,int IdleFrames);
internal readonly record struct BladeWalkRequest(int Path,int Distance,int Velocity);
internal sealed record BladeWalkParameters(int Path=0,int Velocity=0,int Distance=0)
{
    internal BladeWalkParameters Set(int path,int velocity,int distance)
    {
        if(path is <0 or >17 || velocity is <-126 or >126 || distance is <0 or >255){throw new ArgumentOutOfRangeException();}
        return new(path!=0?(path==1?7:path):Path,velocity!=0?velocity:Velocity,distance!=0?Math.Min(distance,77):Distance);
    }
    internal (BladeWalkParameters Remaining,BladeWalkRequest Request) TakeWalk()
    {
        if(Path is <0 or >17 || Velocity is <-126 or >126 || Distance is <0 or >77){throw new ArgumentOutOfRangeException();}
        // Walk mode-info defaults to101, which ModeChange maps to13. Overrides
        // are cleared before path calculation, including a failed calculation.
        return (new(),new(Path!=0?Path:13,Distance!=0?Distance:5,Velocity));
    }
    internal static bool Calculate(BladeWalkRequest request,Func<BladeWalkRequest,bool> calculate)
    {
        ArgumentNullException.ThrowIfNull(calculate);
        if(calculate(request)){return true;}
        return request.Path is 2 or 7 or 9 or 13 && calculate(request with{Path=15});
    }
}

// Full BladeCreeper decision routine. Walk/missile/owner services remain explicit
// so that this does not substitute a player attack or a generic pet chase.
internal static class BladeSentinelAi
{
    internal static BladeAiResult Think(BladeAiState state,int frame,int duration,bool skillKnown,bool validMissile,
        bool hasCommand,Point2D origin,Point2D destination,Action createMissile,Action prepareWalk,Func<Point2D,bool> walk,Func<bool> walkOwner)
    {
        ArgumentNullException.ThrowIfNull(state);ArgumentNullException.ThrowIfNull(createMissile);
        ArgumentNullException.ThrowIfNull(prepareWalk);ArgumentNullException.ThrowIfNull(walk);ArgumentNullException.ThrowIfNull(walkOwner);
        if(frame<0 || duration<=0 || state.Expiry< -1){throw new ArgumentOutOfRangeException();}
        if(!skillKnown){return new(state,true,0);}
        if(state.Expiry<0){state=state with{Expiry=checked(frame+duration)};}
        if(state.Expiry<frame){return new(state,true,0);}
        if(!state.MissileAttempted && validMissile)
        {
            createMissile();state=state with{MissileAttempted=true};
        }
        if(!hasCommand){return new(state,false,3);}
        // SetVelocity(15,0,20) occurs once. ModeChange consumes that override
        // even on failure: the second walk is NOT automatically path15/20.
        prepareWalk();
        if(walk(state.Forward?destination:origin)){return new(state,false,0);}
        state=state with{Forward=!state.Forward};
        if(walk(state.Forward?destination:origin)){return new(state,false,0);}
        return new(state,false,walkOwner()?0:5);
    }
    // Same four source RNG draws and signed square-edge offsets used by both
    // WanderToTarget(owner,5) and the ownerless WalkCloseToUnit(self,2) branch.
    internal static Point2D OwnerDestination(Point2D center,int distance,ref ulong seed)
    {
        if(distance<=0){throw new ArgumentOutOfRangeException(nameof(distance));}
        int x,y;
        if((SourceRandom.Roll(ref seed)&1)!=0){x=distance;y=(int)SourceRandom.Limited(ref seed,distance);}
        else{x=(int)SourceRandom.Limited(ref seed,distance);y=distance;}
        if((SourceRandom.Roll(ref seed)&1)!=0){x=-x;}
        if((SourceRandom.Roll(ref seed)&1)!=0){y=-y;}
        // Source target storage is uint16; the world adapter must validate it.
        return new(unchecked((ushort)(center.X+x)),unchecked((ushort)(center.Y+y)));
    }
}
