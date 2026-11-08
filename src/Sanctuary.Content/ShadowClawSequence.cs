// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
namespace Sanctuary;

internal sealed record ShadowClawSequenceData(string ReferenceVersion,string Code,int Mode,int GraphicsMode,
    int Points,int Frames,int BaseSpeed,int[] Skills,int[][] Rows);
internal sealed record ShadowClawSequenceState(SentrySequenceState Clock,bool ServerDo=false);
// Normal NPC mode14 consumes the previous action before advancing. Unlike
// the player callback it does not rewrite the raw point as a strike ordinal.
internal static class ShadowClawSequence
{
    internal const int Points=16*256;
    internal static bool Handles(int skill)=>ShadowChargeStrikes.Claws(skill)||skill==260;
    private static readonly Lazy<ShadowClawSequenceData> Data=new(()=>
    {
        var d=BalanceCatalog.ReadObject<ShadowClawSequenceData>("shadow-claw-sequence");
        if(d is not{ReferenceVersion:"3.3.93847",Code:"seq_swtigerfist",Mode:14,GraphicsMode:18,Points:16,Frames:16,BaseSpeed:256,Rows.Length:16}
            ||!d.Skills.SequenceEqual(new[]{259,269,274})||d.Rows.Where((r,i)=>r is not{Length:4}
                ||r[0]!=(i<7?5:11)||r[1]!=(i<7?i:i-4)||r[2]!=0||r[3]!=(i is 6 or 10?1:0)).Any())
        {throw new InvalidDataException("Unsupported owned Shadow monster claw sequence");}
        return d;
    });
    internal static ShadowClawSequenceState Initial(){_=Data.Value;return new(new(0,Points,0));}
    internal static bool Complete(ShadowClawSequenceState state)=>state.Clock.Remaining<=0;
    internal static bool Releases(ShadowClawSequenceState state)=>!Complete(state)&&state.Clock.Action==1&&!state.ServerDo;
    internal static ShadowClawSequenceState Advance(ShadowClawSequenceState state,int speed)
    {
        _=Data.Value;
        bool flag=Releases(state)?((state.Clock.Point>>8)&1)!=0:state.ServerDo;
        return new(AssassinSentrySequence.AdvanceIn(state.Clock,Points,stackalloc int[]{6,10},speed),flag);
    }
    internal static ShadowAnimationPlan Plan(int speed)
    {
        if(speed is <1 or >448){throw new NotSupportedException("Unsupported original Shadow sequence speed");}
        var events=new List<SourceAnimationEvent>();var state=Initial();int frame=1;
        for(;!Complete(state);frame++)
        {
            if(Releases(state)){events.Add(new(1,frame,1,state.Clock.Point));}
            state=Advance(state,speed);
        }
        events.Add(new(2,frame,0));return new(speed,events.ToArray());
    }
}
