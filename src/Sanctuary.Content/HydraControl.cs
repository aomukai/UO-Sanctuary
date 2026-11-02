// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Linq;
namespace Sanctuary;

internal sealed record HydraThink(HydraDecision Decision,ulong Seed,int IdleFrames=0);
public sealed record HydraControlState(int Frame,int Mode,int ExpiryFrame,int NextThink,int ActionAt,int EndAt,
    uint Target,ulong Seed,int Thinks=0,int Attempts=0,int Dispatches=0);

// Source-compared AI/ordinary animation component. The caller supplies target
// acquisition, actual mode admission, shot dispatch and death/GUID retirement.
// This is intentionally distinct from Assassin sentry shot accounting.
internal static class HydraControl
{
    internal static int Percentage(ref ulong seed){SourceRandom.Roll(ref seed);return(int)(seed%100);}
    internal static WakeFireClock Clock(bool attack,int speed=256)
    {
        if(speed is <0 or >32767){throw new ArgumentOutOfRangeException(nameof(speed));}
        var flags=new int[12];if(attack){flags[8]=2;}
        var events=SourceAnimationEvents.Initial(new(0,speed,12*256,flags,[]),0).Events;
        return new(events.Where(x=>x.Type==SourceAnimationEvents.ModeChange).Select(x=>x.Frame).DefaultIfEmpty(-1).Single(),
            events.Single(x=>x.Type==SourceAnimationEvents.EndAnimation).Frame);
    }
    internal static HydraThink Think(int frame,int expiry,int? distance,ulong seed)
    {
        if(frame<0||expiry<0||distance<0){throw new ArgumentOutOfRangeException();}
        if(expiry<frame){return new(HydraDecision.Die,seed);}
        if(distance==null||distance>=25){return new(HydraDecision.Idle,seed,10);}
        // SEED_RollPercentage uses the full rolled64bit result. Limited's
        // low32bit policy is a different source function.
        int chance=Percentage(ref seed);return chance<60?new(HydraDecision.Shoot,seed):new(HydraDecision.Idle,seed,10);
    }
    internal static HydraControlState Start(int frames,ulong seed)
    {
        if(frames!=250){throw new NotSupportedException("Unsupported Hydra duration");}
        return new(0,9,frames,25,0,Clock(false).End,0,seed);
    }
    internal static HydraControlState Advance(HydraControlState state,Func<SentryTarget?> target,
        Func<uint,bool> enterAttack,Action<uint> dispatch)
    {
        Validate(state);ArgumentNullException.ThrowIfNull(target);ArgumentNullException.ThrowIfNull(enterAttack);ArgumentNullException.ThrowIfNull(dispatch);
        var c=state with{Frame=checked(state.Frame+1)};if(c.Mode==0){return c;}
        if(c.Mode==4&&c.Frame==c.ActionAt){dispatch(c.Target);c=c with{ActionAt=0,Dispatches=checked(c.Dispatches+1)};}
        if(c.Mode is 4 or 9&&c.Frame==c.EndAt)
        {c=c with{Mode=1,ActionAt=0,EndAt=0,Target=0,NextThink=AssassinWakeFire.NeutralThink(c.Frame,c.NextThink)};}
        if(c.Frame!=c.NextThink){return c;}
        // Expiry is tested before acquiring a target or consuming a chance.
        var selected=c.ExpiryFrame<c.Frame?null:target();
        if(selected is{Serial:0}){throw new ArgumentException("Hydra needs a target identity");}
        var thought=Think(c.Frame,c.ExpiryFrame,selected?.Distance,c.Seed);c=c with{Seed=thought.Seed,NextThink=0,Thinks=checked(c.Thinks+1)};
        if(thought.Decision==HydraDecision.Die){return c with{Mode=0,Target=0,ActionAt=0,EndAt=0};}
        if(thought.Decision==HydraDecision.Shoot)
        {
            if(selected==null){throw new InvalidOperationException("Hydra shot lost its acquired target");}
            c=c with{Attempts=checked(c.Attempts+1)};
            if(enterAttack(selected.Value.Serial))
            {
                var clock=Clock(true);return c with{Mode=4,Target=selected.Value.Serial,ActionAt=checked(c.Frame+clock.Action),EndAt=checked(c.Frame+clock.End)};
            }
        }
        return c with{Mode=1,Target=0,ActionAt=0,EndAt=0,NextThink=checked(c.Frame+10)};
    }
    internal static HydraControlState Inactive(HydraControlState state,int frames)
    {
        Validate(state);if(frames<1){throw new ArgumentOutOfRangeException(nameof(frames));}
        int frame=Math.Max(state.Frame,(int)Math.Min((long)state.Frame+frames,state.ExpiryFrame+1L));
        // Approved offline rule: lifetime advances,combat does not. Skipped
        // shots/decisions are not replayed and neither seed nor expiry resets.
        if(state.Mode==0||frame>state.ExpiryFrame){return state with{Frame=frame,Mode=0,NextThink=0,ActionAt=0,EndAt=0,Target=0};}
        if(state.Mode==9&&frame<state.EndAt){return state with{Frame=frame};}
        return state with{Frame=frame,Mode=1,ActionAt=0,EndAt=0,Target=0,NextThink=state.NextThink>frame?state.NextThink:frame+1};
    }
    internal static void Validate(HydraControlState c)
    {
        if(c==null||c.Frame<0||c.Frame>1_000_000||c.ExpiryFrame!=250||c.Mode is not(0 or 1 or 4 or 9)
            ||c.NextThink<0||c.NextThink!=0&&c.NextThink<=c.Frame||c.Thinks<0||c.Attempts<0||c.Dispatches<0||c.Dispatches>c.Attempts
            ||c.Mode==9&&(c.Frame>=12||c.EndAt!=12||c.ActionAt!=0||c.Target!=0||c.NextThink!=25||c.Thinks!=0)
            ||c.Mode==4&&(c.Target==0||c.EndAt<=c.Frame||c.ActionAt!=0&&(c.ActionAt<=c.Frame||c.ActionAt!=c.EndAt-4)||c.NextThink!=0)
            ||c.Mode is 0 or 1&&(c.Target!=0||c.EndAt!=0||c.ActionAt!=0)||c.Mode==0&&c.NextThink!=0||c.Mode==1&&c.NextThink==0)
        {throw new ArgumentException("Invalid saved Hydra control state");}
    }
}
