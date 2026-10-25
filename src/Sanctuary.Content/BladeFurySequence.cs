// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Linq;

namespace Sanctuary;

internal sealed record BladeFurySequenceState(int Mode,bool UsedSkill,int Frame,int ActionOrdinal,
    BladeFuryChannelState Channel,SourceAnimationState Animation);
internal sealed record BladeFurySequenceStep(BladeFurySequenceState State,bool Success);
internal sealed record BladeFurySequenceServices(Func<int> Mana256,Func<int,bool> Debit,
    Func<bool> Allocate,Func<bool> Aim,Func<bool> Emit);

// Ordinary, living-player SQ23 server event composition. This consumes explicit
// accepted start/stop intents; it does not infer a client heartbeat/reentry gate.
// Sequence initialization and the flag64 clear belong to fresh mode entry.
internal static class BladeFurySequence
{
    internal const int SequenceMode=18,NeutralMode=1,RemoveState=3;
    private static void Validate(BladeFurySequenceState state,int frame)
    {
        ArgumentNullException.ThrowIfNull(state);ArgumentNullException.ThrowIfNull(state.Channel);
        ArgumentNullException.ThrowIfNull(state.Animation);
        if(frame<state.Frame||frame<0||frame>(int.MaxValue>>8)-32
            ||state.Mode is not(SequenceMode or NeutralMode)||state.UsedSkill!=(state.Mode==SequenceMode)
            ||state.Animation.Events.Any(e=>e.Frame<state.Frame))
        {throw new ArgumentException("Unsupported Blade Fury sequence context or clock");}
    }
    private static BladeFuryResourceServices Resources(BladeFurySequenceServices services,Action rewind)
    {
        ArgumentNullException.ThrowIfNull(services);
        return new(services.Mana256,services.Debit,services.Allocate,services.Aim,services.Emit,rewind);
    }
    private static SourceAnimationState AppendExpiry(SourceAnimationState animation,BladeFuryChannelState channel)
        =>animation with{Events=[..animation.Events,new(RemoveState,channel.Expires,0)]};

    internal static BladeFurySequenceStep Enter(int frame,int rank,BladeFurySequenceServices services,uint flags=64)
    {
        if(frame<0||frame>(int.MaxValue>>8)-32){throw new ArgumentOutOfRangeException(nameof(frame));}
        var animation=SourceAnimationEvents.Initial(BladeFuryAnimation.Create(frame*256,256),frame);
        var fresh=new BladeFuryChannelState(false,0,0,flags&~64u){Lease=false};
        var step=BladeFuryResources.Start(fresh,frame,rank,Resources(services,
            ()=>throw new InvalidOperationException("Fresh Fury entry unexpectedly rewound")));
        if(step.State.HasLease){animation=AppendExpiry(animation,step.State);}
        var result=new BladeFurySequenceState(SequenceMode,true,frame,0,step.State,animation);
        return new(step.Success?result:End(result),step.Success);
    }
    // This is the start callback in a supplied existing sequence context, not
    // a guessed replay of fresh mode entry for every network hold heartbeat.
    internal static BladeFurySequenceStep StartCallback(BladeFurySequenceState state,int frame,int rank,
        BladeFurySequenceServices services)
    {
        Validate(state,frame);
        if(state.Animation.Events.Any(e=>e.Frame<frame))
        {throw new ArgumentException("Drain older Fury events before accepting a start callback");}
        if(!state.UsedSkill||!state.Channel.HasLease){return new(state with{Frame=frame},false);}
        if(frame-(state.Animation.Epoch256>>8)<2)
        {throw new NotSupportedException("Fury reentry would address a negative sequence point");}
        var animation=state.Animation;bool eligible=services.Mana256()>=BladeFuryResources.Cost256(rank);
        if(eligible){animation=AppendExpiry(animation,state.Channel with{Expires=checked(frame+7)});}
        var step=BladeFuryResources.Start(state.Channel,frame,rank,Resources(services,
            ()=>animation=SourceAnimationEvents.Renew(animation,frame)));
        // Source SetEvent appends, including duplicate deadlines. Old removal
        // callbacks remain harmless while the stat list has a later deadline.
        var result=state with{Frame=frame,Channel=step.State,Animation=animation};
        return new(step.Success?result:End(result),step.Success);
    }
    private static BladeFurySequenceState End(BladeFurySequenceState state)
        =>state with{Mode=NeutralMode,UsedSkill=false,Animation=state.Animation with
            {Events=state.Animation.Events.Where(e=>e.Type is not(SourceAnimationEvents.ModeChange or SourceAnimationEvents.EndAnimation)).ToArray()}};
    internal static BladeFurySequenceState Stop(BladeFurySequenceState state,int frame)
    {
        Validate(state,frame);
        if(state.Animation.Events.Any(e=>e.Frame<frame))
        {throw new ArgumentException("Drain older Fury events before accepting a stop intent");}
        return state with{Frame=frame,Channel=BladeFuryChannel.Release(state.Channel)};
    }
    internal static BladeFurySequenceState Cancel(BladeFurySequenceState state,int frame)
    {
        Validate(state,frame);
        if(state.Animation.Events.Any(e=>e.Frame<frame))
        {throw new ArgumentException("Drain older Fury events before changing mode");}
        return End(state with{Frame=frame});
    }
    internal static BladeFurySequenceState Advance(BladeFurySequenceState state,int frame,int rank,
        BladeFurySequenceServices services)
    {
        Validate(state,frame);
        // The supplied per-owner event list retains source FIFO insertion
        // order at equal frames. Rewind may replace future mode/end events.
        while(true)
        {
            int index=-1,first=int.MaxValue;
            for(int i=0;i<state.Animation.Events.Length;i++)
            {var item=state.Animation.Events[i];if(item.Frame<=frame&&item.Frame<first){index=i;first=item.Frame;}}
            if(index<0){return state with{Frame=frame};}
            var selected=state.Animation.Events[index];
            var events=state.Animation.Events.Where((_,i)=>i!=index).ToArray();
            state=state with{Frame=selected.Frame,Animation=state.Animation with{Events=events}};
            switch(selected.Type)
            {
                case RemoveState:
                    if(state.Channel.HasLease&&state.Channel.Expires<=selected.Frame)
                    {state=state with{Channel=BladeFuryChannel.Expire(state.Channel)};}
                    break;
                case SourceAnimationEvents.EndAnimation:
                    state=End(state);break;
                case SourceAnimationEvents.ModeChange:
                    if(!state.UsedSkill){state=End(state);break;}
                    state=state with{ActionOrdinal=selected.Param};
                    if((state.Channel.Flags&64u)!=0||selected.Kind is not(1 or 2)){break;}
                    var animation=state.Animation;
                    var step=BladeFuryResources.Do(state.Channel,selected.Frame,rank,Resources(services,
                        ()=>animation=SourceAnimationEvents.Renew(animation,selected.Frame)));
                    state=state with{Channel=step.State,Animation=animation};break;
                default:throw new NotSupportedException("Unknown Fury sequence event type");
            }
        }
    }
}
