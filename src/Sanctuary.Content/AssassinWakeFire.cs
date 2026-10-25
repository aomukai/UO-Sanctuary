// SPDX-License-Identifier: GPL-3.0-or-later
using System;

namespace Sanctuary;

internal readonly record struct WakeFireClock(int Action,int End);
internal sealed record WakeFirePlan(int Rank,bool Wave,Point2D Origin,Point2D Aim,int Frames,int VelocityFp8,
    int Minimum256,int Maximum256,int DamageRate);
internal sealed record WakeFireControlState(int Frame=0,int Mode=1,int NextThink=25,int ActionAt=0,int EndAt=0,
    uint Target=0,SentryAiState Ai=null,int Thinks=0,int Attempts=0,int Dispatches=0);

// Prepared components only. Learned placement, real wave movement/collision and
// retiring actor/stat ownership remain separate gates; no handler is enabled.
internal static class AssassinWakeFire
{
    internal static WakeFireClock Clock(int speed=256)
    {
        if(speed is <0 or >32767){throw new ArgumentOutOfRangeException(nameof(speed));}
        if(speed==0){return new(-1,1);}
        int frame=0,index=0,action=-1;
        for(int point=speed;point<512;point=checked(point+speed))
        {
            frame++;
            for(int i=point>>8;index<=i;index++){if(index==1){action=frame;}}
        }
        return new(action,checked(Math.Max(1,frame)+1));
    }
    internal static int NeutralThink(int frame,int existing,bool stunned=false)
    {
        if(frame<0 || existing<0){throw new ArgumentOutOfRangeException();}
        return existing>frame?existing:checked(frame+(stunned?45:15));
    }
    internal static Point2D Perpendicular(Point2D aim)=>new(checked(-aim.Y),aim.X);
    internal static (int Min,int Max) Fire(SkillValues values,int mastery)
    {
        Validate(values);
        return(AmazonMelee.EnhanceEndpoint(values.Damage256(false),mastery),
            AmazonMelee.EnhanceEndpoint(values.Damage256(true),mastery));
    }
    internal static WakeFirePlan Plan(SkillValues values,bool wave,Point2D origin,Point2D relativeAim,int mastery,int velocityPercent=100)
    {
        Validate(values);
        string key=wave?"wake of destruction":"wake of destruction maker";
        int x=relativeAim.X,y=relativeAim.Y;
        int velocity=SlowMissilePayload.VelocityFp8(NecromancerSummons.Source(key,"Vel"),
            NecromancerSummons.Source(key,"LevVel"),values.Rank,2,0,NecromancerSummons.Source(key,"CanSlow")!=0,velocityPercent);
        if(velocity!=0)
        {
            if(Math.Abs((long)x)>=100 || Math.Abs((long)y)>=100){return null;}
            if(x==0 && y==0){x=y=1;}
        }
        var damage=Fire(values,mastery);
        return new(values.Rank,wave,origin,new(checked(origin.X+x),checked(origin.Y+y)),
            NecromancerSummons.Formula(key,"Range",values),velocity,damage.Min,damage.Max,NecromancerSummons.Source(key,"DamageRate"));
    }
    private static void Validate(SkillValues values)
    {
        ArgumentNullException.ThrowIfNull(values);
        if(values.Ability!=CombatAbility.WakeOfFire || values.Rank is <1 or >255)
        {throw new ArgumentException("Expected an effective Wake of Fire rank1..255",nameof(values));}
    }
    internal static WakeFireControlState Start(bool emerging=false)=>new(Mode:emerging?8:1,EndAt:emerging?15:0,Ai:new());
    internal static WakeFireControlState Inactive(WakeFireControlState state,int frames)
    {
        Validate(state);if(frames<1){throw new ArgumentOutOfRangeException(nameof(frames));}
        int frame=checked(state.Frame+frames);
        if(state.Mode==0){return state with{Frame=frame};}
        if(state.Mode==8 && frame<state.EndAt){return state with{Frame=frame};}
        bool birth=state.Thinks==0 && state.NextThink==25 && frame<25;
        return state with{Frame=frame,Mode=1,ActionAt=0,EndAt=0,Target=0,NextThink=birth?25:checked(frame+1)};
    }
    internal static WakeFireControlState Advance(WakeFireControlState state,bool owner,bool room,bool town,bool skillRow,bool skill,
        Func<int> shots,Func<SentryTarget?> target,Func<bool> chance,Action start,Action<uint> dispatch)
    {
        Validate(state);ArgumentNullException.ThrowIfNull(shots);ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(chance);ArgumentNullException.ThrowIfNull(start);ArgumentNullException.ThrowIfNull(dispatch);
        var c=state with{Frame=checked(state.Frame+1)};
        if(c.Mode==0){return c;}
        if(c.Mode==9 && c.Frame==c.ActionAt)
        {dispatch(c.Target);c=c with{ActionAt=0,Dispatches=checked(c.Dispatches+1)};}
        if(c.Mode is 8 or 9 && c.Frame==c.EndAt)
        {c=c with{Mode=1,ActionAt=0,EndAt=0,Target=0,NextThink=NeutralThink(c.Frame,c.NextThink)};}
        if(c.Frame!=c.NextThink){return c;}
        SentryTarget? selected=null;
        int Field(string field)=>NecromancerSummons.Source("wakeofdestruction",field);
        var result=AssassinSentryAi.Think(c.Ai,owner,room,town,skillRow,skill,shots,()=>
        {selected=target();return(selected.HasValue,selected?.Distance??0);},chance,false,
            Field("aip4"),Field("aip2"),Field("aip3"));
        c=c with{Ai=result.State,NextThink=0,Thinks=checked(c.Thinks+1)};
        switch(result.Decision)
        {
            case SentryDecision.Die:return c with{Mode=0,Target=0,ActionAt=0,EndAt=0};
            case SentryDecision.Idle:return c with{Mode=1,Target=0,ActionAt=0,EndAt=0,NextThink=checked(c.Frame+result.IdleFrames)};
            case SentryDecision.Fire:
                if(selected is not{Serial:>0}){throw new InvalidOperationException("Wake needs a retained target identity");}
                // Generic direct-mode entry ignores a false start result. The
                // caller supplies the start side effect, not sequence success.
                start();var clock=Clock();
                return c with{Mode=9,Target=selected.Value.Serial,ActionAt=checked(c.Frame+clock.Action),
                    EndAt=checked(c.Frame+clock.End),Attempts=checked(c.Attempts+1)};
            default:throw new InvalidOperationException("Unsupported Wake AI decision");
        }
    }
    internal static void Validate(WakeFireControlState c)
    {
        if(c==null || c.Frame<0 || c.Mode is not(0 or 1 or 8 or 9) || c.Ai==null || c.Ai.RemainingShots< -1
            || c.NextThink<0 || c.NextThink!=0 && c.NextThink<=c.Frame || c.Thinks<0 || c.Attempts<0 || c.Dispatches<0 || c.Dispatches>c.Attempts
            || c.Mode==9&&(c.Target==0 || c.EndAt<=c.Frame || c.ActionAt!=0&&(c.ActionAt<=c.Frame || c.ActionAt!=c.EndAt-1) || c.NextThink!=0)
            || c.Mode==8&&(c.Frame>=15 || c.EndAt!=15 || c.ActionAt!=0 || c.Target!=0 || c.NextThink!=25 || c.Thinks!=0 || c.Ai.RemainingShots!=-1)
            || c.Mode is 0 or 1 && (c.Target!=0 || c.EndAt!=0 || c.ActionAt!=0))
        {throw new ArgumentException("Invalid prepared Wake controller state");}
    }
}
