// SPDX-License-Identifier: GPL-3.0-or-later
using System;

namespace Sanctuary;

internal enum SentryDecision { Die=1,Idle=2,Fire=3 }
internal sealed record SentryAiState(int RemainingShots=-1);
internal sealed record SentryAiResult(SentryAiState State,SentryDecision Decision,int IdleFrames,bool ClearInferno);

// Complete AI101 decision/shot component. Owner/room/target, formula and chance
// services are explicit. Actual sequence entry and projectile creation are separate.
internal static class AssassinSentryAi
{
    internal static SentryAiResult Think(SentryAiState state,bool owner,bool room,bool town,bool skillRow,bool skill,
        Func<int> shotCount,bool target,int distance,Func<bool> attackRoll,bool inferno,int range=25,int activeStall=10,int inactiveStall=15)
        =>Think(state,owner,room,town,skillRow,skill,shotCount,()=>(target,distance),attackRoll,inferno,range,activeStall,inactiveStall);
    internal static SentryAiResult Think(SentryAiState state,bool owner,bool room,bool town,bool skillRow,bool skill,
        Func<int> shotCount,Func<(bool Has,int Distance)> selectTarget,Func<bool> attackRoll,bool inferno,int range=25,int activeStall=10,int inactiveStall=15)
    {
        ArgumentNullException.ThrowIfNull(state);ArgumentNullException.ThrowIfNull(shotCount);ArgumentNullException.ThrowIfNull(attackRoll);ArgumentNullException.ThrowIfNull(selectTarget);
        if(state.RemainingShots< -1 || range<=0 || activeStall<=0 || inactiveStall<=0){throw new ArgumentOutOfRangeException();}
        SentryAiResult Dead()=>new(state,SentryDecision.Die,0,false);
        if(!owner || !room || town){return Dead();}
        if(state.RemainingShots<0)
        {
            if(!skillRow || !skill){return Dead();}
            state=state with{RemainingShots=shotCount()};
        }
        if(state.RemainingShots<=0 || !skill){return Dead();}
        var target=selectTarget();if(target.Distance<0){throw new ArgumentOutOfRangeException();}
        if(!target.Has || target.Distance>=range){return new(state,SentryDecision.Idle,inactiveStall,inferno);}
        if(!attackRoll()){return new(state,SentryDecision.Idle,activeStall,inferno);}
        // Decrement before UseSkill. A failed mode entry still spends the shot;
        // remaining0 is allowed to dispatch this final attempt.
        return new(state with{RemainingShots=state.RemainingShots-1},SentryDecision.Fire,0,inferno);
    }
}
