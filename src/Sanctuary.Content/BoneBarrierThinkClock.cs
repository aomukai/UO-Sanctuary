// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Text.Json;
using ModernUO.Serialization;
using Server;
namespace Sanctuary;

internal sealed record BarrierThinkState(DateTime BornUtc,int Deadline,int Next,int Last=0,int Thinks=0,bool DeathRequested=false);
internal interface IBarrierThinkClock
{
    BarrierThinkState Clock{get;}
    void StoreClock(BarrierThinkState state);
}

// Source pet helper: first AI event25; species skill0 deadline600;
// Fn084: strict deadline<observed frame, otherwise Idle15. One observation
// executes one callback, preserving a delayed observer's phase, not catch-up.
internal static class BoneBarrierThinkClock
{
    internal static BarrierThinkState Capture(DateTime at)
    {
        if(SkillMechanics.For(CombatAbility.BoneWall).Number("Param2")!=600
            ||SkillMechanics.For(CombatAbility.BonePrison).Number("Param2")!=600)
        {throw new NotSupportedException("Unmapped bone barrier AI deadline");}
        var s=new BarrierThinkState(at,600,25);Validate(s);return s;
    }
    internal static void Validate(BarrierThinkState s)
    {
        if(s==null||s.BornUtc.Kind!=DateTimeKind.Utc||s.Deadline!=600||s.Next<25||s.Last<0||s.Thinks<0
            ||s.Thinks==0&&(s.Next!=25||s.Last!=0||s.DeathRequested)
            ||s.Thinks>0&&!s.DeathRequested&&(s.Last>=s.Next||s.Next!=s.Last+15)
            ||s.DeathRequested&&(s.Last<=s.Deadline||s.Last<s.Next))
        {throw new InvalidDataException("Invalid saved barrier AI clock");}
    }
    internal static BarrierThinkState Observe(BarrierThinkState s,int frame)
    {
        Validate(s);if(frame<0){throw new ArgumentOutOfRangeException(nameof(frame));}
        if(s.DeathRequested||frame<s.Next){return s;}
        var next=s with{Last=frame,Thinks=checked(s.Thinks+1)};
        next=s.Deadline<frame?next with{DeathRequested=true}:next with{Next=checked(frame+15)};
        Validate(next);return next;
    }
    internal static bool Expired(IBarrierThinkClock body,DateTime at)
    {
        var s=body.Clock;Validate(s);
        int frame=(int)Math.Clamp((at-s.BornUtc).Ticks/(40*TimeSpan.TicksPerMillisecond),0,int.MaxValue-15L);
        var next=Observe(s,frame);if(next!=s){body.StoreClock(next);}return next.DeathRequested;
    }
}

// New saved subtypes opt into AI scheduling. Existing plain/prepared/maker
// types keep their original timer and layout. DEATH requests still use the
// native deletion adapter; the full source death/minion phase remains open.
[SerializationGenerator(0)]
public partial class BoneBarrierThinkingBody:BoneBarrierBody,IBarrierThinkClock
{
    public override bool UsesNativeSummonTimer=>false;
    [SerializableField(0,setter:"private")] private string _clockJson="";
    private BarrierThinkState _clock;
    BarrierThinkState IBarrierThinkClock.Clock=>_clock??(_clock=JsonSerializer.Deserialize<BarrierThinkState>(_clockJson));
    void IBarrierThinkClock.StoreClock(BarrierThinkState state)
    {BoneBarrierThinkClock.Validate(state);_clock=state;_clockJson=JsonSerializer.Serialize(state);}
    protected override bool LifetimeExpired(DateTime at)=>BoneBarrierThinkClock.Expired(this,at);
    [Constructible]public BoneBarrierThinkingBody(){((IBarrierThinkClock)this).StoreClock(BoneBarrierThinkClock.Capture(TimedEffects.Now));}
    [AfterDeserialization]private void AfterClockLoad(){BoneBarrierThinkClock.Validate(((IBarrierThinkClock)this).Clock);}
}

[SerializationGenerator(0)]
public partial class BoneWallThinkingBody:BoneWallMakerBody,IBarrierThinkClock
{
    public override bool UsesNativeSummonTimer=>false;
    [SerializableField(0,setter:"private")] private string _clockJson="";
    private BarrierThinkState _clock;
    BarrierThinkState IBarrierThinkClock.Clock=>_clock??(_clock=JsonSerializer.Deserialize<BarrierThinkState>(_clockJson));
    void IBarrierThinkClock.StoreClock(BarrierThinkState state)
    {BoneBarrierThinkClock.Validate(state);_clock=state;_clockJson=JsonSerializer.Serialize(state);}
    protected override bool LifetimeExpired(DateTime at)=>BoneBarrierThinkClock.Expired(this,at);
    [Constructible]public BoneWallThinkingBody(){((IBarrierThinkClock)this).StoreClock(BoneBarrierThinkClock.Capture(TimedEffects.Now));}
    [AfterDeserialization]private void AfterClockLoad(){BoneBarrierThinkClock.Validate(((IBarrierThinkClock)this).Clock);}
}
