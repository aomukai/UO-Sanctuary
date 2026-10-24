// SPDX-License-Identifier: GPL-3.0-or-later
using System;

namespace Sanctuary;

internal sealed record SentryControlState(int Frame=0,int Mode=1,int NextThink=25,SentryAiState Ai=null,
    SentrySequenceState Sequence=null,bool ServerDo=false,uint Target=0,int Thinks=0,int Attempts=0,int Dispatches=0);
internal readonly record struct SentryTarget(uint Serial,int Distance);

// Mode14 selects stru_6FD28738[14].unk0x00: the infinite per-frame
// MODECHANGE event, NOT sub_6FCBCE70's sparse animation-event schedule.
// At speed256, entry2 is consumed on the third subsequent monster phase;
// the end check precedes advancement, so neutral follows on phase16.
internal static class AssassinSentryController
{
    internal static SentryControlState Start()=>new(Ai:new());
    internal static SentryControlState Advance(SentryControlState state,bool owner,bool room,bool town,
        bool skillRow,bool skill,Func<int> shots,Func<SentryTarget?> target,Func<bool> chance,Func<bool> start,Action<uint> dispatch)
    {
        ArgumentNullException.ThrowIfNull(state);ArgumentNullException.ThrowIfNull(shots);ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(chance);ArgumentNullException.ThrowIfNull(start);ArgumentNullException.ThrowIfNull(dispatch);
        Validate(state);var c=state with{Frame=checked(state.Frame+1)};
        if(c.Mode==0){return c;}
        // Infinite monster events execute before scheduled AI events. A newly
        // installed event first executes on the following frame.
        if(c.Mode==14)
        {
            if(AssassinSentrySequence.Complete(c.Sequence))
            {c=c with{Mode=1,Sequence=null,Target=0,NextThink=c.NextThink>c.Frame?c.NextThink:checked(c.Frame+15)};}
            else
            {
                if(!c.ServerDo && c.Sequence.Action==1)
                {dispatch(c.Target);c=c with{ServerDo=true,Dispatches=checked(c.Dispatches+1)};}
                c=c with{Sequence=AssassinSentrySequence.Advance(c.Sequence)};
            }
        }
        if(c.NextThink==c.Frame)
        {
            SentryTarget? selected=null;
            var result=AssassinSentryAi.Think(c.Ai,owner,room,town,skillRow,skill,shots,()=>
            {selected=target();return(selected.HasValue,selected?.Distance??0);},chance,false);
            c=c with{Ai=result.State,NextThink=0,Thinks=checked(c.Thinks+1)};
            switch(result.Decision)
            {
                case SentryDecision.Die:return c with{Mode=0,Sequence=null,Target=0};
                case SentryDecision.Idle:return c with{Mode=1,Sequence=null,Target=0,NextThink=checked(c.Frame+result.IdleFrames)};
                case SentryDecision.Fire:
                    c=c with{Attempts=checked(c.Attempts+1),ServerDo=false,Target=selected.Value.Serial};
                    return start()?c with{Mode=14,Sequence=new()}
                        :c with{Mode=1,Sequence=null,Target=0,NextThink=checked(c.Frame+10)};
                default:throw new InvalidOperationException("Unsupported sentry AI decision");
            }
        }
        return c;
    }
    internal static void Validate(SentryControlState c)
    {
        if(c==null || c.Frame<0 || c.Mode is not(0 or 1 or 14) || c.NextThink<0 || c.NextThink!=0&&c.NextThink<=c.Frame
            || c.Ai==null || c.Ai.RemainingShots< -1 || c.Thinks<0 || c.Attempts<0 || c.Dispatches<0 || c.Dispatches>c.Attempts
            || c.Mode==14&&(c.Sequence==null || c.Target==0 || c.Sequence.Point is <0 or >=AssassinSentrySequence.Points
                || c.Sequence.Point%256!=0 || c.Sequence.Remaining is <0 or >AssassinSentrySequence.Points
                || c.Sequence.Remaining%256!=0 || c.Sequence.Action is <0 or >1)
            || c.Mode!=14&&(c.Sequence!=null || c.Target!=0))
        {throw new ArgumentException("Invalid sentry controller state");}
    }
}
