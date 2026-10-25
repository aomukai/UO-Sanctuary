// SPDX-License-Identifier: GPL-3.0-or-later
using System;

namespace Sanctuary;

internal sealed record InfernoControlState(int Frame=0,int Mode=1,int NextThink=25,int EndAt=0,
    uint Target=0,SentryAiState Ai=null,InfernoBurstState Burst=null,bool Inferno=false,
    int Thinks=0,int Attempts=0,int Emissions=0);

// AI101 plus the distinct NPC53/95 channel. Range13 and fixed shot count come
// from the current Inferno rows. World acquisition, start-path preparation and
// missile allocation are explicit services; retirement remains a separate gate.
internal static class AssassinInfernoControl
{
    internal static InfernoControlState Start(bool emerging=false)=>new(Mode:emerging?8:1,EndAt:emerging?15:0,Ai:new());
    internal static InfernoControlState Inactive(InfernoControlState state,int frames)
    {
        Validate(state);if(frames<1){throw new ArgumentOutOfRangeException(nameof(frames));}
        int frame=checked(state.Frame+frames);
        if(state.Mode==0){return state with{Frame=frame};}
        if(state.Mode==8 && frame<state.EndAt){return state with{Frame=frame};}
        bool birth=state.Thinks==0 && state.NextThink==25 && frame<25;
        // Approved offline adapter discards an unfinished paid/debited action,
        // with no shot/RNG replay or refund when the owner returns.
        return state with{Frame=frame,Mode=1,NextThink=birth?25:checked(frame+1),EndAt=0,Target=0,Burst=null,Inferno=false};
    }
    internal static InfernoControlState Advance(InfernoControlState state,bool owner,bool room,bool town,bool skillRow,bool skill,
        int hardWake,Func<int> shots,Func<SentryTarget?> select,Func<bool> chance,Action start,Func<uint,int,InfernoEmission> emit)
    {
        Validate(state);ArgumentNullException.ThrowIfNull(shots);ArgumentNullException.ThrowIfNull(select);
        ArgumentNullException.ThrowIfNull(chance);ArgumentNullException.ThrowIfNull(start);ArgumentNullException.ThrowIfNull(emit);
        if(hardWake is <0 or >255){throw new ArgumentOutOfRangeException(nameof(hardWake));}
        var c=state with{Frame=checked(state.Frame+1)};
        if(c.Mode==0){return c;}
        if(c.Mode==8 && c.Frame==c.EndAt)
        {c=c with{Mode=1,EndAt=0,NextThink=AssassinSentryEmergence.NeutralThink(c.Frame,c.NextThink)};}
        if(c.Mode==14)
        {
            if(c.Burst.HardWake!=hardWake){throw new NotSupportedException("Inferno captured hard Wake changed during a burst");}
            var burst=AssassinInfernoBurst.Advance(c.Burst,c.Inferno,frame=>emit(c.Target,frame));
            c=c with{Burst=burst,Emissions=checked(c.Emissions+burst.Emissions-state.Burst.Emissions)};
            if(burst.Ended)
            {c=c with{Mode=1,Burst=null,Target=0,NextThink=AssassinSentryEmergence.NeutralThink(c.Frame,c.NextThink)};}
        }
        if(c.Frame!=c.NextThink){Validate(c);return c;}
        SentryTarget? selected=null;
        var result=AssassinSentryAi.Think(c.Ai,owner,room,town,skillRow,skill,shots,()=>
        {selected=select();return(selected.HasValue,selected?.Distance??0);},chance,c.Inferno,13,10,15);
        c=c with{Ai=result.State,NextThink=0,Thinks=checked(c.Thinks+1),Inferno=result.ClearInferno?false:c.Inferno};
        switch(result.Decision)
        {
            case SentryDecision.Die:return c with{Mode=0,Target=0,Burst=null};
            case SentryDecision.Idle:return c with{Mode=1,Target=0,Burst=null,NextThink=checked(c.Frame+result.IdleFrames)};
            case SentryDecision.Fire:
                if(selected is not{Serial:>0}){throw new InvalidOperationException("Inferno needs a retained target identity");}
                start();
                return c with{Mode=14,Target=selected.Value.Serial,Inferno=true,Attempts=checked(c.Attempts+1),Burst=AssassinInfernoBurst.Start(c.Frame,hardWake)};
            default:throw new InvalidOperationException("Unsupported Inferno AI decision");
        }
    }
    internal static void Validate(InfernoControlState c)
    {
        if(c==null || c.Frame<0 || c.Mode is not(0 or 1 or 8 or 14) || c.Ai==null || c.Ai.RemainingShots< -1
            || c.NextThink<0 || c.NextThink!=0 && c.NextThink<=c.Frame || c.Thinks<0 || c.Attempts<0 || c.Attempts>c.Thinks || c.Emissions<0
            || c.Mode==14&&(c.Target==0 || c.Burst==null || c.Burst.Ended || c.Burst.Frame!=c.Frame || c.NextThink!=0)
            || c.Mode!=14&&(c.Target!=0 || c.Burst!=null)
            || c.Mode==8&&(c.Frame>=15 || c.EndAt!=15 || c.NextThink!=25 || c.Thinks!=0 || c.Ai.RemainingShots!=-1 || c.Inferno)
            || c.Mode!=8 && c.EndAt!=0)
        {throw new ArgumentException("Invalid prepared Inferno controller state");}
        if(c.Burst!=null){AssassinInfernoBurst.Validate(c.Burst);}
    }
}
