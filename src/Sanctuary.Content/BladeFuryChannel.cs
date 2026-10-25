// SPDX-License-Identifier: GPL-3.0-or-later
using System;

namespace Sanctuary;

internal sealed record BladeFuryChannelState(bool Active,int Expires,int NextShot,uint Flags=64);
internal sealed record BladeFuryChannelStep(BladeFuryChannelState State,bool Success);
internal sealed record BladeFuryChannelServices(Func<bool> Allocate,Func<bool> Aim,Func<bool> Emit,Action Consume,Action Rewind);

// Bounded st26/do48 state component. Generic mana wrappers, SQ23 animation,
// network held input, world missiles and persistence belong to its caller.
// No learned handler is registered by this component.
internal static class BladeFuryChannel
{
    private static void Validate(BladeFuryChannelState state,int frame,BladeFuryChannelServices services)
    {
        ArgumentNullException.ThrowIfNull(state);ArgumentNullException.ThrowIfNull(services);
        if(frame<0||frame>int.MaxValue-21||state.NextShot<0||state.Expires<0||state.Active&&state.Expires==0)
        {throw new ArgumentException("Unsupported Blade Fury channel clock/state");}
        ArgumentNullException.ThrowIfNull(services.Allocate);ArgumentNullException.ThrowIfNull(services.Aim);
        ArgumentNullException.ThrowIfNull(services.Emit);ArgumentNullException.ThrowIfNull(services.Consume);ArgumentNullException.ThrowIfNull(services.Rewind);
    }
    internal static BladeFuryChannelStep Start(BladeFuryChannelState state,int frame,bool record,bool learned,int missile,
        int mana256,int startThreshold256,int calculation,BladeFuryChannelServices services)
    {
        Validate(state,frame,services);
        if(!record||missile<0||!learned){return new(state,false);}
        if(state.Active)
        {
            state=state with{Expires=checked(frame+7)};
            if(state.NextShot<=frame)
            {
                var step=Do(state,frame,record,learned,missile,calculation,services);
                if(step.Success){services.Consume();}
                return step;
            }
            services.Rewind();return new(state,true);
        }
        // This is an availability threshold, not a six-mana debit. Existing
        // state bypasses it; generic resource eligibility is a separate layer.
        if((startThreshold256>0&&mana256<startThreshold256)||!services.Allocate()){return new(state,false);}
        return new(state with{Active=true,Expires=checked(frame+21),NextShot=0},true);
    }
    internal static BladeFuryChannelStep Do(BladeFuryChannelState state,int frame,bool record,bool learned,int missile,
        int calculation,BladeFuryChannelServices services)
    {
        Validate(state,frame,services);
        if(!learned||!record||missile<0||calculation<=1){return new(state,false);}
        state=state with{Flags=state.Flags&~64u};
        if(!services.Aim()){return new(state,false);}
        bool attempted=false;
        if(state.NextShot<frame)
        {
            // Source reports an attempt even when allocation returns null.
            // Payment is decided by the composed caller, never by this bool.
            services.Emit();attempted=true;
            state=state with{NextShot=checked(frame+calculation-1)};
        }
        if(state.Active){services.Rewind();}
        return new(state,attempted);
    }
    internal static BladeFuryChannelState Expire(BladeFuryChannelState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return state with{Active=false,Expires=0,Flags=state.Flags|64u};
    }
}
