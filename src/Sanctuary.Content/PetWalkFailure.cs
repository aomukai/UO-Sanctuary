// SPDX-License-Identifier: GPL-3.0-or-later
using System;
namespace Sanctuary;

internal sealed record PetWalkFailureServices(Action DeleteThink,Action MarkVisionReset,Func<int> Roll,
    Action<int> Idle,Action<int> WalkClose);

// Failed-mode branch of AITACTICS_MoveToTarget. These are tactic flags, not
// path types. Keep ordered services so NPC controllers can share the policy.
internal static class PetWalkFailure
{
    internal static bool Apply(int flags,PetWalkFailureServices s)
    {
        if(flags is <0 or >7||s?.DeleteThink==null||s.MarkVisionReset==null||s.Roll==null||s.Idle==null||s.WalkClose==null)
        {throw new ArgumentException("Unsupported pet walk failure flags/services");}
        if((flags&4)!=0){s.DeleteThink();}
        if((flags&1)!=0){s.MarkVisionReset();}
        if((flags&2)==0){return false;}
        int roll=s.Roll();if(roll is <0 or >99){throw new ArgumentException("Invalid pet walk percentage roll");}
        if(roll>=70){s.Idle(10);}else{s.WalkClose(4);}
        return true;
    }
}
