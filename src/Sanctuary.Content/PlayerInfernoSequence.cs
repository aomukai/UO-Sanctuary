// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Linq;
namespace Sanctuary;

internal sealed record PlayerInfernoSequenceState(CombatAbility Ability,PlayerInfernoCostModel CostModel,int Mode,bool UsedSkill,
    int Frame,int ActionOrdinal,PlayerInfernoState Channel,SourceAnimationState Animation)
{public string Profile{get;init;}="source-player-sq15-st11-do19-supplied-intents-v1";}
internal sealed record PlayerInfernoSequenceStep(PlayerInfernoSequenceState State,bool Success);
internal sealed record PlayerInfernoSequenceServices(Func<int> Mana256,Func<int,bool> Debit,Func<bool> Allocate,
    Func<bool> Aim,Func<int,bool> Emit);

// Ordinary player event composition with explicit accepted intents. The normal
//256rate is supplied; this does not infer modern input heartbeat or FCR rules.
internal static class PlayerInfernoSequence
{
    private const int SequenceMode=18,NeutralMode=1,RemoveState=3;
    private static void Validate(PlayerInfernoSequenceState s,int frame)
    {
        ArgumentNullException.ThrowIfNull(s);ArgumentNullException.ThrowIfNull(s.Channel);ArgumentNullException.ThrowIfNull(s.Animation);
        PlayerInfernoResources.Row(s.Ability);
        if(frame<s.Frame||frame<0||frame>(int.MaxValue>>8)-32||s.Mode is not(SequenceMode or NeutralMode)
            ||s.UsedSkill!=(s.Mode==SequenceMode)||!Enum.IsDefined(s.CostModel)||s.Animation.Events==null
            ||s.Animation.Events.Any(e=>e.Frame<s.Frame)||s.Animation.Speed!=256||s.Animation.FrameCount256!=15*256
            ||s.Animation.FrameEvents==null||s.Animation.FrameEvents.Length!=15
            ||s.Animation.FrameEvents.Where((value,index)=>value!=(index==10?1:0)).Any()
            ||s.Profile!="source-player-sq15-st11-do19-supplied-intents-v1")
        {throw new ArgumentException("Unsupported player Inferno sequence state/clock");}
    }
    private static SourceAnimationState AppendExpiry(SourceAnimationState a,int frame)=>a with{Events=[..a.Events,new(RemoveState,frame,0)]};
    private static PlayerInfernoSequenceState End(PlayerInfernoSequenceState s)=>s with{Mode=NeutralMode,UsedSkill=false,
        Animation=s.Animation with{Events=s.Animation.Events.Where(e=>e.Type is not(SourceAnimationEvents.ModeChange or SourceAnimationEvents.EndAnimation)).ToArray()}};
    internal static PlayerInfernoSequenceStep EnterFresh(int frame,CombatAbility ability,int rank,PlayerInfernoCostModel model,
        PlayerInfernoSequenceServices services,uint flags=64)
    {
        if(frame<0||frame>(int.MaxValue>>8)-32){throw new ArgumentOutOfRangeException(nameof(frame));}
        PlayerInfernoResources.Row(ability);ArgumentNullException.ThrowIfNull(services);
        var points=new int[15];points[10]=1;
        var animation=SourceAnimationEvents.Initial(new(frame*256,256,15*256,points,[]),frame);
        var channel=new PlayerInfernoState(Flags:flags&~64u);
        var resource=new PlayerInfernoResourceServices(services.Mana256,services.Debit,new(
            ()=>{bool made=services.Allocate();if(made){animation=AppendExpiry(animation,checked(frame+20));}return made;},
            services.Aim,services.Emit,()=>throw new NotSupportedException("Fresh player channel cannot discard an existing shared lease")));
        var step=PlayerInfernoResources.Start(channel,frame,ability,rank,model,resource);
        var result=new PlayerInfernoSequenceState(ability,model,SequenceMode,true,frame,0,step.State,animation);
        return new(step.Success?result:End(result),step.Success);
    }
    internal static PlayerInfernoSequenceStep StartCallback(PlayerInfernoSequenceState s,int frame,int rank,PlayerInfernoSequenceServices services)
    {
        Validate(s,frame);
        if(s.Animation.Events.Any(e=>e.Frame<frame)){throw new ArgumentException("Drain older player channel events before a start callback");}
        if(!s.UsedSkill){return new(s with{Frame=frame},false);}
        if(s.Channel.Lease&&frame-(s.Animation.Epoch256>>8)<3)
        {throw new NotSupportedException("Player channel double rewind would address a pre-sequence point");}
        var animation=s.Animation;int rewinds=0;
        var resource=new PlayerInfernoResourceServices(services.Mana256,services.Debit,new(
            ()=>{bool made=services.Allocate();if(made){animation=AppendExpiry(animation,checked(frame+20));}return made;},
            services.Aim,services.Emit,()=>
            {
                animation=SourceAnimationEvents.Renew(animation,frame);
                // Existing-list start rewinds BEFORE posting its new expiry;
                // successful nested do then rewinds AGAIN. Ordering matters
                // at expiry/action ties and differs from Blade Fury's start.
                if(++rewinds==1&&s.Channel.Lease){animation=AppendExpiry(animation,checked(frame+6));}
            }));
        var step=PlayerInfernoResources.Start(s.Channel,frame,s.Ability,rank,s.CostModel,resource);
        var result=s with{Frame=frame,Channel=step.State,Animation=animation};
        return new(step.Success?result:End(result),step.Success);
    }
    internal static PlayerInfernoSequenceState Stop(PlayerInfernoSequenceState s,int frame)
    {
        Validate(s,frame);if(s.Animation.Events.Any(e=>e.Frame<frame)){throw new ArgumentException("Drain older events before player channel stop");}
        return s with{Frame=frame,Channel=PlayerInfernoChannel.Release(s.Channel)};
    }
    internal static PlayerInfernoSequenceState Cancel(PlayerInfernoSequenceState s,int frame)
    {
        Validate(s,frame);if(s.Animation.Events.Any(e=>e.Frame<frame)){throw new ArgumentException("Drain older events before changing player channel mode");}
        return End(s with{Frame=frame});
    }
    internal static PlayerInfernoSequenceState Advance(PlayerInfernoSequenceState s,int frame,int rank,PlayerInfernoSequenceServices services)
    {
        Validate(s,frame);
        while(true)
        {
            int index=-1,first=int.MaxValue;
            for(int i=0;i<s.Animation.Events.Length;i++)
            {var e=s.Animation.Events[i];if(e.Frame<=frame&&e.Frame<first){index=i;first=e.Frame;}}
            if(index<0){return s with{Frame=frame};}
            var selected=s.Animation.Events[index];s=s with{Frame=selected.Frame,
                Animation=s.Animation with{Events=s.Animation.Events.Where((_,i)=>i!=index).ToArray()}};
            switch(selected.Type)
            {
                case RemoveState:
                    if(s.Channel.Lease&&s.Channel.Expires<=selected.Frame){s=s with{Channel=PlayerInfernoChannel.Expire(s.Channel)};}
                    break;
                case SourceAnimationEvents.EndAnimation:s=End(s);break;
                case SourceAnimationEvents.ModeChange:
                    if(!s.UsedSkill){s=End(s);break;}
                    s=s with{ActionOrdinal=selected.Param};
                    if((s.Channel.Flags&64u)!=0||selected.Kind is not(1 or 2)){break;}
                    var animation=s.Animation;
                    var resource=new PlayerInfernoResourceServices(services.Mana256,services.Debit,new(services.Allocate,services.Aim,
                        services.Emit,()=>animation=SourceAnimationEvents.Renew(animation,selected.Frame)));
                    var step=PlayerInfernoResources.Do(s.Channel,selected.Frame,s.Ability,rank,resource);
                    s=s with{Channel=step.State,Animation=animation};break;
                default:throw new NotSupportedException("Unknown player Inferno event type");
            }
        }
    }
}
