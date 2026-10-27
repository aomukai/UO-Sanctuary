// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
namespace Sanctuary;

internal sealed record GrizzlySequenceState(int NpcRank,int Speed,uint Target,SentrySequenceState Clock,
    BearSmitePrepared Packet,int Mode=14,bool ServerDo=false,int Dispatches=0)
{
    public string Profile{get;init;}="bearsmite-sequence-prepared-v1";
}

// Mode14's per-frame callback consumes the previous action before advancing.
// The packet is prepared at entry; dispatch never rolls or mitigates it again.
// World target/range/events and the neutral/AI deadline are separate services.
internal static class DruidGrizzlySequence
{
    internal const int Points=17*256,ActionIndex=8;
    internal static GrizzlySequenceState Enter(int npcRank,int speed,uint target,Func<BearSmitePrepared> prepare)
    {
        ArgumentNullException.ThrowIfNull(prepare);
        _=DruidNpcSkills.BearSmite(npcRank);
        if(speed is <1 or >Points||target==0){throw new NotSupportedException("Unsupported BearSmite sequence entry");}
        var packet=prepare();
        // Failed start owns no later action. The caller supplies the source
        // failed-mode-change/neutral policy; there is no automatic refund.
        if(packet==null){return null;}
        var state=new GrizzlySequenceState(npcRank,speed,target,new(Remaining:Points),packet);
        Validate(state);return state;
    }
    internal static GrizzlySequenceState Cancel(GrizzlySequenceState state)
    {Validate(state);return state with{Mode=1,Target=0,Clock=null,Packet=null};}
    internal static GrizzlySequenceState Advance(GrizzlySequenceState state,Func<uint,BearSmitePrepared,bool> release)
    {
        ArgumentNullException.ThrowIfNull(release);Validate(state);
        if(state.Mode!=14){return state;}
        if(AssassinSentrySequence.Complete(state.Clock)){return Cancel(state);}
        var next=state;
        if(!state.ServerDo&&state.Clock.Action==1)
        {
            bool marked=release(state.Target,state.Packet);
            next=state with{ServerDo=marked,Dispatches=checked(state.Dispatches+1)};
        }
        next=next with{Clock=AssassinSentrySequence.AdvanceIn(next.Clock,Points,ActionIndex,next.Speed)};
        Validate(next);return next;
    }
    internal static void Validate(GrizzlySequenceState state)
    {
        if(state==null||state.Profile!="bearsmite-sequence-prepared-v1"||state.NpcRank is <1 or >255
            ||state.Speed is <1 or >Points||state.Mode is not(1 or 14)||state.Dispatches is <0 or >1
            ||state.ServerDo&&state.Dispatches!=1
            ||state.Mode==1&&(state.Target!=0||state.Clock!=null||state.Packet!=null))
        {throw new InvalidDataException("Invalid saved BearSmite sequence state");}
        if(state.Mode==1){return;}
        var clock=state.Clock;var p=state.Packet;
        if(state.Target==0||clock==null||p==null||clock.Point is <0 or >=Points
            ||clock.Remaining>Points||clock.Remaining<=-state.Speed||clock.Action is <0 or >1
            ||clock.Point!=(Points-clock.Remaining)%Points
            ||p.ResultFlags is <0 or >65535||p.Physical256<0||p.Fire256<0||p.Lightning256<0||p.Cold256<0
            ||p.ColdFrames<0||p.Total256<0||p.AbsorbedLife256<0||p.StunFrames!=0
            ||p.HitClass!=(p.Critical?0x10:0)
            ||(long)p.Fire256+p.Lightning256+p.Cold256>p.Total256
            ||(long)p.Physical256+p.Fire256+p.Lightning256+p.Cold256<p.Total256)
        {throw new InvalidDataException("Unsupported saved BearSmite clock or prepared packet");}
    }
}
