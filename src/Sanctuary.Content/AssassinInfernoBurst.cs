// SPDX-License-Identifier: GPL-3.0-or-later
using System;

namespace Sanctuary;

internal enum InfernoEmission { Success,NoCoordinates,AllocationFailed }
internal sealed record InfernoBurstState(int Frame,int EntryFrame,int HardWake,int Deadline,SentrySequenceState Sequence,
    int ModeAt=-1,int EndAt=0,bool Ended=false,int ModeCalls=0,int EmissionCalls=0,int Creates=0,int Emissions=0);

// Current NPC311 single-burst clock only. The complete source composition
// identifies the distinct paused sequence and sparse mode callbacks. AI101,
// mode-end/neutral, actor ownership and actual beam creation remain callers.
internal static class AssassinInfernoBurst
{
    internal const int Points=14*256;
    internal static InfernoBurstState Start(int frame,int hardWake)
    {
        if(frame<0 || hardWake is <0 or >255){throw new ArgumentOutOfRangeException();}
        return new(frame,frame,hardWake,checked(frame+15+hardWake),new(0,Points,0));
    }
    internal static InfernoBurstState Advance(InfernoBurstState state,bool infernoActive,Func<int,InfernoEmission> emit)
    {
        Validate(state);ArgumentNullException.ThrowIfNull(emit);
        var next=state with{Frame=checked(state.Frame+1)};
        if(next.Ended){return next;}
        // Infinite monster events precede finite events. Once do95 replaces
        // the infinite event, only its explicit three-frame request advances
        // this sequence; ordinary intervening frames leave it unchanged.
        if(next.ModeAt==-1 || next.ModeAt==next.Frame)
        {
            next=next with{ModeCalls=checked(next.ModeCalls+1)};
            if(next.Sequence.Remaining<=0)
            {return next with{Ended=true,ModeAt=0,EndAt=0};}
            if(next.Sequence.Action==1)
            {
                var outcome=emit(next.Frame);
                if(outcome is not(InfernoEmission.Success or InfernoEmission.NoCoordinates or InfernoEmission.AllocationFailed))
                {throw new NotSupportedException("Unmapped NPC311 emission outcome");}
                bool made=outcome==InfernoEmission.Success;
                next=next with
                {
                    EmissionCalls=checked(next.EmissionCalls+1),
                    Creates=checked(next.Creates+(outcome==InfernoEmission.NoCoordinates?0:1)),
                    Emissions=checked(next.Emissions+(made?1:0)),
                    ModeAt=made && infernoActive && next.Frame<next.Deadline?checked(next.Frame+3):0,
                    EndAt=made && infernoActive && next.Frame<next.Deadline?0:checked(next.Frame+14)
                };
                if(made)
                {
                    // Complete UNITS_SetAnimationFrame keeps the fraction;
                    // SKILLS_SetInfernoFrame sets remaining time to10 frames.
                    // Total sequence length remains14 frames.
                    next=next with{Sequence=new(8*256+(next.Sequence.Point&255),10*256,next.Sequence.Action)};
                }
            }
            next=next with{Sequence=AssassinSentrySequence.AdvanceIn(next.Sequence,Points,9)};
        }
        if(next.EndAt==next.Frame){next=next with{Ended=true,ModeAt=0,EndAt=0};}
        Validate(next);return next;
    }
    internal static void Validate(InfernoBurstState s)
    {
        if(s==null || s.Frame<0 || s.EntryFrame<0 || s.Frame<s.EntryFrame || s.HardWake is <0 or >255
            || s.Deadline!=(long)s.EntryFrame+15+s.HardWake || s.Sequence==null
            || s.Sequence.Point<0 || s.Sequence.Point>=Points || s.Sequence.Point%256!=0
            || s.Sequence.Remaining<0 || s.Sequence.Remaining>Points || s.Sequence.Remaining%256!=0
            || s.Sequence.Action is <0 or >1 || s.ModeAt< -1 || s.ModeAt>0 && s.ModeAt<=s.Frame
            || s.EndAt<0 || s.EndAt>0 && s.EndAt<=s.Frame || s.ModeCalls<0 || s.EmissionCalls<0
            || s.Emissions<0 || s.Creates<s.Emissions || s.EmissionCalls<s.Creates
            || s.EmissionCalls>s.ModeCalls || s.Ended && (s.ModeAt!=0 || s.EndAt!=0)
            || !s.Ended && (s.ModeAt==0)==(s.EndAt==0))
        {throw new ArgumentException("Invalid current-row NPC311 burst state");}
    }
}
