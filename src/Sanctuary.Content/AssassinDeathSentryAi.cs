// SPDX-License-Identifier: GPL-3.0-or-later
using System;
namespace Sanctuary;
internal enum DeathSentryDecision { None=0,Die=1,Idle=2,Explode=3,Lightning=4 }
internal sealed record DeathSentryAiState(int RemainingShots,int LastCorpse);
internal sealed record DeathSentryAiResult(DeathSentryAiState State,DeathSentryDecision Decision,int IdleFrames=0,int Target=0);
// Complete AI104 decision component. Lookup, target/corpse search, chance and
// native mode/event services remain explicit, as in the source differential.
internal static class AssassinDeathSentryAi
{
    // The shared source initializer stores the current game frame, not zero
    // or a fabricated corpse sentinel. Native frame/identity mapping is separate.
    internal static DeathSentryAiState Start(int gameFrame)=>new(-1,gameFrame);
    internal static DeathSentryAiResult Think(DeathSentryAiState state,Func<bool> ownerValid,
        bool row,bool skill,int rank,Func<int> shotCount,Func<(bool Has,int Id,int Distance)> selectEnemy,
        Func<(bool Has,int Id,int Distance)> selectCorpse,Func<bool> chance,int rangeBase=10,int rangePerRank=1,
        int activeDistance=16,int idleFrames=0)
    {
        ArgumentNullException.ThrowIfNull(state);ArgumentNullException.ThrowIfNull(ownerValid);
        ArgumentNullException.ThrowIfNull(shotCount);ArgumentNullException.ThrowIfNull(selectEnemy);
        ArgumentNullException.ThrowIfNull(selectCorpse);ArgumentNullException.ThrowIfNull(chance);
        DeathSentryAiResult Result(DeathSentryDecision decision,int target=0)=>new(state,decision,decision==DeathSentryDecision.Idle?idleFrames:0,target);
        bool Lost(bool debit)
        {
            if(!ownerValid()){return true;}
            if(state.RemainingShots<0)
            {
                if(!row||!skill){return true;}
                state=state with{RemainingShots=shotCount()};
            }
            if(state.RemainingShots<=0){return true;}
            if(debit){state=state with{RemainingShots=state.RemainingShots-1};}
            return false;
        }
        if(Lost(false)){return Result(DeathSentryDecision.Die);}
        if(!skill||rank<=0){return Result(DeathSentryDecision.None);}
        var enemy=selectEnemy();if(!enemy.Has){return Result(DeathSentryDecision.Idle);}
        var corpse=selectCorpse();int maxDistance=row?rangeBase+(rank-1)*rangePerRank:0;
        if(corpse.Has&&corpse.Id!=state.LastCorpse&&corpse.Distance<maxDistance/2)
        {
            if(Lost(true)){return Result(DeathSentryDecision.Die);}
            state=state with{LastCorpse=corpse.Id};return Result(DeathSentryDecision.Explode,corpse.Id);
        }
        if(enemy.Distance<activeDistance&&chance())
        {
            if(Lost(true)){return Result(DeathSentryDecision.Die);}
            return Result(DeathSentryDecision.Lightning,enemy.Id);
        }
        return Result(DeathSentryDecision.Idle);
    }
}
