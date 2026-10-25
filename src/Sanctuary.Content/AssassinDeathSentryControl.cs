// SPDX-License-Identifier: GPL-3.0-or-later
using System;

namespace Sanctuary;

internal readonly record struct DeathCorpseTarget(uint Serial,int Guid,int Distance);
internal readonly record struct DeathSentryClock(int Action,int End);
internal sealed record DeathSentryControlState(int Frame=0,int Mode=1,int NextThink=25,
    DeathSentryAiState Ai=null,SentrySequenceState Sequence=null,uint Target=0,uint Corpse=0,
    int ActionAt=0,int EndAt=0,bool ServerDo=false,int Thinks=0,int Attempts=0,int Dispatches=0);

// AI104 selects two distinct mode paths: direct S2 corpse action2 and the
// shared mode14 lightning sequence. Both debit the same shot pool before
// attempting a cast; a later missing/consumed target cannot refund it.
internal static class AssassinDeathSentryControl
{
    internal const string RetirementFault="Death Sentry retirement/death/GUID removal is not connected";
    internal static DeathSentryClock Clock(int speed=256)
    {
        if(speed is <0 or >32767){throw new ArgumentOutOfRangeException(nameof(speed));}
        if(speed==0){return new(-1,1);}
        int frame=0,index=0,action=-1;
        for(int point=speed;point<15*256;point=checked(point+speed))
        {
            frame++;
            for(int i=point>>8;index<=i;index++){if(index==0){action=frame;}}
        }
        return new(action,checked(Math.Max(1,frame)+1));
    }
    internal static DeathSentryControlState Start(int gameFrame,bool emerging=false)
        =>new(Mode:emerging?8:1,Ai:AssassinDeathSentryAi.Start(gameFrame),EndAt:emerging?15:0);
    internal static DeathSentryControlState Inactive(DeathSentryControlState c,int frames)
    {
        Validate(c);if(frames<1){throw new ArgumentOutOfRangeException(nameof(frames));}
        int frame=checked(c.Frame+frames);
        if(c.Mode==0){return c with{Frame=frame};}
        if(c.Mode==8 && frame<c.EndAt){return c with{Frame=frame};}
        bool birth=c.Thinks==0 && c.NextThink==25 && frame<25;
        return c with{Frame=frame,Mode=1,NextThink=birth?25:checked(frame+1),Sequence=null,Target=0,Corpse=0,ActionAt=0,EndAt=0};
    }
    internal static DeathSentryControlState Advance(DeathSentryControlState state,Func<bool> owner,
        bool row,bool skill,int rank,Func<int> shots,Func<SentryTarget?> enemy,Func<DeathCorpseTarget?> corpse,
        Func<bool> chance,Func<DeathSentryDecision,uint,bool> start,Action<uint> explode,Action<uint> lightning,
        int rangeBase=10,int rangePerRank=1,int activeDistance=16,int idleFrames=0)
    {
        Validate(state);ArgumentNullException.ThrowIfNull(enemy);ArgumentNullException.ThrowIfNull(corpse);
        ArgumentNullException.ThrowIfNull(start);ArgumentNullException.ThrowIfNull(explode);ArgumentNullException.ThrowIfNull(lightning);
        var c=state with{Frame=checked(state.Frame+1)};
        if(c.Mode==0){return c;}
        if(c.Mode==8 && c.Frame==c.EndAt)
        {c=c with{Mode=1,EndAt=0,NextThink=AssassinWakeFire.NeutralThink(c.Frame,c.NextThink)};}
        if(c.Mode==9 && c.ActionAt==c.Frame)
        {explode(c.Corpse);c=c with{ActionAt=0,Dispatches=checked(c.Dispatches+1)};}
        if(c.Mode==9 && c.EndAt==c.Frame)
        {c=c with{Mode=1,Corpse=0,ActionAt=0,EndAt=0,NextThink=AssassinWakeFire.NeutralThink(c.Frame,c.NextThink)};}
        if(c.Mode==14)
        {
            if(AssassinSentrySequence.Complete(c.Sequence))
            {c=c with{Mode=1,Sequence=null,Target=0,NextThink=AssassinWakeFire.NeutralThink(c.Frame,c.NextThink)};}
            else
            {
                if(!c.ServerDo && c.Sequence.Action==1)
                {lightning(c.Target);c=c with{ServerDo=true,Dispatches=checked(c.Dispatches+1)};}
                c=c with{Sequence=AssassinSentrySequence.Advance(c.Sequence)};
            }
        }
        if(c.NextThink!=c.Frame){return c;}
        SentryTarget? selectedEnemy=null;DeathCorpseTarget? selectedCorpse=null;
        var result=AssassinDeathSentryAi.Think(c.Ai,owner,row,skill,rank,shots,
            ()=>{selectedEnemy=enemy();return(selectedEnemy.HasValue,checked((int)(selectedEnemy?.Serial??0)),selectedEnemy?.Distance??0);},
            ()=>{selectedCorpse=corpse();return(selectedCorpse.HasValue,selectedCorpse?.Guid??0,selectedCorpse?.Distance??0);},
            chance,rangeBase,rangePerRank,activeDistance,idleFrames);
        c=c with{Ai=result.State,NextThink=0,Thinks=checked(c.Thinks+1)};
        switch(result.Decision)
        {
            case DeathSentryDecision.None:return c;
            case DeathSentryDecision.Die:return c with{Mode=0,Sequence=null,Target=0,Corpse=0,ActionAt=0,EndAt=0};
            case DeathSentryDecision.Idle:
                // Complete AITACTICS_Idle turns a zero delay into one frame.
                return c with{Mode=1,Sequence=null,Target=0,Corpse=0,ActionAt=0,EndAt=0,NextThink=checked(c.Frame+Math.Max(1,result.IdleFrames))};
            case DeathSentryDecision.Explode:
                if(selectedCorpse is not{Serial:>0}){throw new InvalidOperationException("Missing selected corpse identity");}
                start(result.Decision,selectedCorpse.Value.Serial);var clock=Clock();
                return c with{Mode=9,Target=0,Corpse=selectedCorpse.Value.Serial,Sequence=null,ServerDo=false,
                    ActionAt=checked(c.Frame+clock.Action),EndAt=checked(c.Frame+clock.End),Attempts=checked(c.Attempts+1)};
            case DeathSentryDecision.Lightning:
                if(selectedEnemy is not{Serial:>0}){throw new InvalidOperationException("Missing selected lightning enemy");}
                c=c with{Attempts=checked(c.Attempts+1),ServerDo=false};
                return start(result.Decision,selectedEnemy.Value.Serial)
                    ?c with{Mode=14,Target=selectedEnemy.Value.Serial,Corpse=0,Sequence=new(),ActionAt=0,EndAt=0}
                    :c with{Mode=1,Target=0,Corpse=0,Sequence=null,ActionAt=0,EndAt=0,NextThink=checked(c.Frame+10)};
            default:throw new InvalidOperationException("Unknown Death Sentry decision");
        }
    }
    internal static void Validate(DeathSentryControlState c)
    {
        if(c==null || c.Frame<0 || c.Mode is not(0 or 1 or 8 or 9 or 14) || c.Ai is not{RemainingShots:>=-1}
            || c.NextThink<0 || c.NextThink!=0&&c.NextThink<=c.Frame || c.Thinks<0 || c.Attempts<0 || c.Dispatches<0 || c.Dispatches>c.Attempts
            || c.Mode==8&&(c.Frame>=15 || c.EndAt!=15 || c.ActionAt!=0 || c.Sequence!=null || c.Target!=0 || c.Corpse!=0 || c.NextThink!=25 || c.Thinks!=0 || c.Ai.RemainingShots!=-1)
            || c.Mode==9&&(c.Corpse==0 || c.Target!=0 || c.Sequence!=null || c.EndAt<=c.Frame || c.ActionAt!=0&&(c.ActionAt<=c.Frame || c.ActionAt!=c.EndAt-14) || c.NextThink!=0)
            || c.Mode==14&&(c.Target==0 || c.Corpse!=0 || c.Sequence==null || c.Sequence.Point is <0 or >=AssassinSentrySequence.Points
                || c.Sequence.Point%256!=0 || c.Sequence.Remaining is <0 or >AssassinSentrySequence.Points || c.Sequence.Remaining%256!=0 || c.Sequence.Action is <0 or >1 || c.ActionAt!=0 || c.EndAt!=0 || c.NextThink!=0)
            || c.Mode is 0 or 1&&(c.Target!=0 || c.Corpse!=0 || c.Sequence!=null || c.ActionAt!=0 || c.EndAt!=0))
        {throw new ArgumentException("Invalid Death Sentry controller state");}
    }
}
