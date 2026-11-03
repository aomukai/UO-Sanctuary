// SPDX-License-Identifier: GPL-3.0-or-later
using System;
namespace Sanctuary;

internal sealed class DoubleSwingPhaseUnit(int sequenceFrame,uint flags)
{
    internal int SequenceFrame=sequenceFrame;
    internal uint Flags=flags;
}
internal sealed record DoubleSwingPhaseServices(Func<SourceGuidTarget> Target,Func<int> MeleeRange,
    Func<SourceGuidFindRequest,SourceGuidTarget> Search,Action<SourceGuidTarget> SetTarget,
    Func<int,int,int> PrepareStart32,Action<SourceGuidTarget,int> Drain);

// Complete legacy do70 release policy. The modern start68, hand context,
// world enumeration and actual start32/combat consumers remain caller services.
// This does not replace start68 or register learned Double Swing.
internal static class BarbarianDoubleSwingPhase
{
    internal static int Execute(DoubleSwingPhaseUnit unit,int skillId,int rank,DoubleSwingPhaseServices services)
    {
        var target=services.Target();if(target==null){return 0;}
        if(((unit.SequenceFrame>>8)%2)!=0)
        {
            unit.Flags|=0x40;
            target=services.Search(new(0,0,unchecked(services.MeleeRange()+4),0x20003,target.Id));
            if(target==null){return 0;}
            services.SetTarget(target);
        }
        // do70 intentionally ignores this return and still drains the chosen
        // target's combat packet. Do not turn a failed preparation into a new
        // early return or consume mana in this release helper.
        services.PrepareStart32(skillId,rank);services.Drain(target,0);return 1;
    }
}
