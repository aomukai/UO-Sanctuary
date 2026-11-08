// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
namespace Sanctuary;

internal sealed class SigilTrackingState
{
    internal readonly SortedDictionary<(int Stat,int Layer),int> Stats=new();
    internal readonly List<int[]> Writes=new();
    internal float Expiry;internal uint Flags;
    internal int Get(int stat,int layer)=>Stats.GetValueOrDefault((stat,layer));
    internal void Set(int stat,int layer,int value){Stats[(stat,layer)]=value;Writes.Add([stat,layer,value]);}
}
internal sealed class SigilTrackedMissile(int kind,bool data,float lifetime)
{internal int Kind=kind;internal bool HasData=data;internal float Lifetime=lifetime;}
internal sealed record SigilTrackingAcquisition(SigilTrackingState State,bool Reschedule);
internal sealed record SigilTrackingServices(Func<SigilTrackingAcquisition> Acquire,Func<int,SigilTrackedMissile> Resolve);

// Current core28fa10and its actual stable small-vector sort. The state-list
// factory/stat writers/missile resolver are explicit services. Normal Sigils
// limit the roster to1; foreign/corrupt rosters above8slots are unsupported.
internal static class SigilTracking
{
    internal static bool Run(int? newMissile,int limit,int start,int end,SigilTrackingServices services)
    {
        ArgumentNullException.ThrowIfNull(services);if(limit<=0){return false;}
        var acquired=services.Acquire();var state=acquired?.State;if(state==null){return false;}
        int maximum=unchecked((ushort)state.Get(355,0)),free=0;
        if(maximum>8){throw new NotSupportedException("Unqualified foreign Sigil tracking list exceeds inline8slot domain");}
        var active=new List<(int Slot,int Time)>();
        void Clear(int slot){state.Set(355,slot,0);state.Set(354,slot,0);free=slot;}
        for(int slot=1;slot<=maximum;slot++)
        {
            int time=state.Get(355,slot);if(time<=0){continue;}
            int guid=state.Get(354,slot);if(guid is 0 or -1){continue;}
            if(services.Resolve(guid)==null){Clear(slot);}else{active.Add((slot,time));}
        }
        // The captured insertion sort is descending and stable. Removing
        // the final entry therefore preserves the source equal-time tie.
        active=active.OrderByDescending(e=>e.Time).ToList();
        while(active.Count!=0&&active.Count+1>limit)
        {
            var old=active[^1];active.RemoveAt(active.Count-1);
            var actor=services.Resolve(state.Get(354,old.Slot));
            if(actor is{Kind:3,HasData:true}){actor.Lifetime=0;}
            Clear(old.Slot);
        }
        maximum=free;foreach(var old in active){maximum=Math.Max(maximum,old.Slot);}
        int selected=free!=0?free:unchecked((ushort)(maximum+1));
        int nextMaximum=free!=0?maximum:selected;
        state.Set(355,selected,start);state.Set(354,selected,newMissile??-1);state.Set(355,0,nextMaximum);
        bool reschedule=acquired.Reschedule;
        float expiry=state.Expiry;
        int oldExpiry=!float.IsFinite(expiry)||expiry>=2147483648f||expiry< -2147483648f?int.MinValue:unchecked((int)expiry);
        if(end>oldExpiry)
        {
            if(end>0){state.Flags|=2;}
            state.Expiry=end;reschedule=true;
        }
        return reschedule; // Entries may change even when this is false.
    }
}
