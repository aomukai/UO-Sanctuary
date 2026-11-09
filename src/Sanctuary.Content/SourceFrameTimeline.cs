// SPDX-License-Identifier: GPL-3.0-or-later
using System;
namespace Sanctuary;

internal readonly record struct SourceFramePosition(int Frame,DateTime NextUtc);
// Shared native transport math extracted from the existing ground-pet clock.
// Active delivery and inactive skipping have different consumers;this helper
// never executes actions or chooses how a suspended event should be handled.
internal static class SourceFrameTimeline
{
    internal const long IntervalTicks=40*TimeSpan.TicksPerMillisecond;
    internal const int MaximumFrame=int.MaxValue-100;
    internal static void Validate(SourceFramePosition position)
    {
        if(position.Frame is<0 or>MaximumFrame||position.NextUtc.Kind!=DateTimeKind.Utc)
        {throw new ArgumentException("Unsupported source-frame position");}
    }
    internal static SourceFramePosition Skip(SourceFramePosition position,DateTime observed)
    {
        Validate(position);if(observed.Kind!=DateTimeKind.Utc){throw new ArgumentException("Source-frame observation must be UTC");}
        if(observed<position.NextUtc){return position;}
        long count=(observed-position.NextUtc).Ticks/IntervalTicks+1;
        if(count>MaximumFrame-position.Frame){throw new NotSupportedException("Source-frame timeline exhausted");}
        return new(checked(position.Frame+(int)count),position.NextUtc.AddTicks(checked(count*IntervalTicks)));
    }
    internal static SourceFramePosition Step(SourceFramePosition position)
    {
        Validate(position);if(position.Frame==MaximumFrame){throw new NotSupportedException("Source-frame timeline exhausted");}
        return new(position.Frame+1,position.NextUtc.AddTicks(IntervalTicks));
    }
}

// Prepared per-world frame delivery. The caller supplies observations and the
// typed router;no timer,world registration,save schema or startup is installed.
// Oldest-to-newest bucket delivery is an explicit native host mapping,not a
// claim that the protected D2R global caller has been recovered.
internal sealed class GenericPetFramePump(GenericPetTimedEvents events,SourceFramePosition position)
{
    private SourceFramePosition _position=Valid(position);
    private bool _advancing;
    internal SourceFramePosition Position=>_position;
    private static SourceFramePosition Valid(SourceFramePosition value){SourceFrameTimeline.Validate(value);return value;}
    internal void Advance(DateTime observed,Action<GenericPetTimedEvent,int,DateTime> route)
    {
        ArgumentNullException.ThrowIfNull(events);ArgumentNullException.ThrowIfNull(route);
        if(_advancing){throw new NotSupportedException("Nested pet frame delivery is not qualified");}
        var end=SourceFrameTimeline.Skip(_position,observed);_advancing=true;
        try
        {
            while(_position.Frame<end.Frame)
            {
                DateTime at=_position.NextUtc;_position=SourceFrameTimeline.Step(_position);
                int frame=_position.Frame;var bucket=events.Bucket(frame%64);
                if(bucket.Length!=0){events.Dispatch(bucket[^1],frame,true,node=>route(node,frame,at));}
            }
        }
        finally{_advancing=false;}
    }
}
