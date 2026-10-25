// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;

namespace Sanctuary;

internal sealed record SourceAnimationEvent(int Type,int Frame,int Kind,int Param=0);
internal sealed record SourceAnimationState(int Epoch256,int Speed,int FrameCount256,
    int[] FrameEvents,SourceAnimationEvent[] Events);

// Event planning for a supplied sequence/frame-flag row. Callers supply mode
// entry, frame bonus, rate, event dispatch and state-expiry ordering. These
// arrays do not register timers or persist a production cast.
internal static class SourceAnimationEvents
{
    internal const int ModeChange=1,EndAnimation=2;

    private static void Validate(SourceAnimationState state,int frame)
    {
        ArgumentNullException.ThrowIfNull(state);ArgumentNullException.ThrowIfNull(state.FrameEvents);
        ArgumentNullException.ThrowIfNull(state.Events);
        if(frame<0||frame>(int.MaxValue>>8)-32||state.Epoch256<0||state.Speed<0||state.Speed>65536
            ||state.FrameCount256<=0||state.FrameEvents.Length==0
            ||state.FrameCount256>(long)state.FrameEvents.Length*256)
        {throw new ArgumentException("Unsupported source animation clock, rate or frame row");}
    }

    internal static SourceAnimationState Initial(SourceAnimationState state,int frame,int frameBonus=0)
    {
        Validate(state,frame);
        if(frameBonus<0||frameBonus>=state.FrameEvents.Length){throw new ArgumentOutOfRangeException(nameof(frameBonus));}
        var events=new List<SourceAnimationEvent>(state.Events);
        if(state.Speed==0)
        {events.Add(new(EndAnimation,frame+1,0));return state with{Events=events.ToArray()};}
        int current=frame,index=frameBonus,ordinal=0;
        for(long point=(long)state.Speed+frameBonus*256;point<state.FrameCount256;point+=state.Speed)
        {
            current++;
            while(index<=point/256 && index<144)
            {
                int kind=state.FrameEvents[index++];
                if(kind is 1 or 2 or 4){events.Add(new(ModeChange,current,kind,ordinal++));}
                else if(kind==3){events.Add(new(ModeChange,current,kind));}
            }
        }
        if(current==frame){current++;}
        events.Add(new(EndAnimation,current+1,0));
        return state with{Epoch256=frame*256,Events=events.ToArray()};
    }

    internal static SourceAnimationState Renew(SourceAnimationState state,int frame,int amount=1)
    {
        Validate(state,frame);
        if(amount<=0){return state;}
        int calc=checked(frame-(state.Epoch256>>8)-amount);
        int epoch=checked((frame-calc)*256);
        // Source can index calc-1. Refuse an unsupported caller epoch instead
        // of padding the row with an invented pre-sequence event.
        if(calc<0||epoch<0||(state.Speed>0&&(long)calc*256+state.Speed<state.FrameCount256&&calc<1))
        {throw new ArgumentException("Source rewind would address a negative sequence point");}
        var events=new List<SourceAnimationEvent>();
        foreach(var item in state.Events)
        {if(item.Type is not (ModeChange or EndAnimation)){events.Add(item);}}
        int current=frame;
        if(state.Speed>0)
        {
            int index=calc-1;
            for(long point=(long)calc*256+state.Speed;point<state.FrameCount256;point+=state.Speed)
            {
                current++;int kind=0;
                while(index<=point/256)
                {
                    kind=state.FrameEvents[index++];
                    if(kind is >0 and <=4){events.Add(new(ModeChange,current,kind));}
                }
            }
            if(current==frame){current++;}
            events.Add(new(EndAnimation,current+1,0));
        }
        else{events.Add(new(EndAnimation,frame+1,0));}
        return state with{Epoch256=epoch,Events=events.ToArray()};
    }
}
