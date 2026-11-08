// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
namespace Sanctuary;

internal sealed record ShadowFurySequenceData(string ReferenceVersion,string Code,int Mode,int Points,int Speed,int ActionFrame,int EndFrame,int[][] Rows);

// Original monstats slot2 uses mode14,overriding generic skill metadata SC.
// This clock does not choose emission/critical/RNG or own the inferno lease.
internal static class ShadowFuryClock
{
    private static readonly Lazy<ShadowFurySequenceData> Data=new(()=>
    {
        var d=BalanceCatalog.ReadObject<ShadowFurySequenceData>("shadow-fury-sequence");
        if(d is not{ReferenceVersion:"3.3.93847",Code:"seq_swbladefury",Mode:14,Points:17,Speed:256,ActionFrame:10,EndFrame:18,Rows.Length:17}
            ||d.Rows.Where((r,i)=>r is not{Length:4}||r[0]!=7||r[1]!=i||r[2]!=0||r[3]!=(i==9?1:0)).Any())
        {throw new NotSupportedException("Unqualified original Shadow Fury sequence");}
        var row=ShadowFuryPackets.Row();
        if(row.Text("seqtrans")!="A1"||row.Number("UseAttackRate")!=0)
        {throw new NotSupportedException("Original Fury no longer uses the qualified Other sequence-rate branch");}
        return d;
    });
    internal static SentrySequenceState Initial(){_=Data.Value;return new(0,17*256,0);}
    internal static SentrySequenceState Advance(SentrySequenceState state)=>AssassinSentrySequence.AdvanceIn(state,17*256,stackalloc int[]{9},Data.Value.Speed);
    internal static ShadowAnimationPlan Plan(AssassinShadowBody body)
    {
        _=ShadowEquipmentDefense.Require(body);
        if(body.OwnedSkills.Used!=266||body.OwnedSkills.Find(266)?.Mode!=14||ShadowEquipmentDefense.Composite(body)!=13)
        {throw new NotSupportedException("Original Fury clock needs its actual slot2 mode14 and original active dual claws");}
        var state=Initial();var events=new List<SourceAnimationEvent>();int frame=1;
        for(;state.Remaining>0;frame++)
        {
            if(state.Action==1){events.Add(new(1,frame,1,state.Point));}state=Advance(state);
        }
        events.Add(new(2,frame,0));
        if(events.Count!=2||events[0].Frame!=Data.Value.ActionFrame||frame!=Data.Value.EndFrame){throw new NotSupportedException("Original Fury sequence projection differs from the complete NPC clock");}
        return new(Data.Value.Speed,events.ToArray());
    }
}
