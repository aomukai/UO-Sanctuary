// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
namespace Sanctuary;

internal sealed record GrimWardCursePacket(int Rank,int Duration,int Radius,int Velocity,int AttackRate,int OtherRate,int Physical);
internal sealed record GrimWardCurseList(int State,int Skill,int Rank,int Creator,int Expires,int Velocity,int AttackRate,int OtherRate,int Physical);
internal sealed record GrimWardCurseTarget(int Kind,int Alignment,bool AiAllowed,int CurseResistance,int BasePhysical,bool Hireling,bool Attract);
internal sealed record GrimWardCurseResult(bool Accepted,GrimWardCurseList List,int PermissionCalls,int AiCalls,int Removed,int Events,int AnimationUpdates,int[] Operations);

// Legacy do30/recipient/factory stat-list branch specialized to pinned Ward
// stats and admitted hostile recipients. Formula context is explicit: this must not choose whether a missile
// reads live owner skills, a birth snapshot, or its own skill book.
internal static class GrimWardCurseOperation
{
    internal static GrimWardCursePacket Packet(SkillValues values,MonsterDifficulty difficulty)
    {
        if(values?.Ability!=CombatAbility.GrimWard||values.Rank is <1 or >255)
        {throw new NotSupportedException("Prepared Ward curse requires an explicit rank/context");}
        var row=SkillMechanics.For(CombatAbility.GrimWard);
        if(row.Number("srvdofunc")!=75||row.Text("auratargetstate")!="terror"
            ||row.Text("aurastat1")!="velocitypercent"||row.Text("aurastat2")!="attackrate"
            ||row.Text("aurastat3")!="other_animrate"||row.Text("aurastat4")!="damageresist")
        {throw new NotSupportedException("Unmapped pinned Ward curse stats");}
        return new(values.Rank,NecromancerCurseCasts.AiFrames(checked((int)values.Eval("auralencalc")),difficulty),
            checked((int)values.Eval("aurarangecalc")),checked((int)values.Eval("aurastatcalc1")),
            checked((int)values.Eval("aurastatcalc2")),checked((int)values.Eval("aurastatcalc3")),checked((int)values.Eval("aurastatcalc4")));
    }
    internal static GrimWardCurseResult Apply(GrimWardCursePacket packet,GrimWardCurseTarget target,GrimWardCurseList old,int creator,int frame)
    {
        ArgumentNullException.ThrowIfNull(packet);
        var operations=new List<int>();
        GrimWardCurseResult No(int calls)=>new(false,old,calls,0,0,0,0,operations.ToArray());
        if(target==null||target.Kind!=1||target.Alignment==0){return No(0);}
        operations.Add(1);
        if(!target.AiAllowed||packet.Velocity==0){return No(1);}
        operations.Add(2);
        if(target.CurseResistance>=100||target.Attract){return No(1);}
        int duration=packet.Duration-SourcePercentage.Calculate(packet.Duration,target.CurseResistance,100);
        // Reuse the existing curse factory's rank/creator/expiry selection.
        // These admitted inputs are future,nonzero durations. Other raw
        // permanent/expired-list contexts are outside this prepared adapter.
        if(duration<=0||frame<0||frame>int.MaxValue-duration)
        {throw new NotSupportedException("Prepared Ward duration/frame outside supported source domain");}
        var at=DateTime.UnixEpoch.AddMilliseconds(frame*40L);
        if(old!=null&&old.Expires<=frame)
        {throw new NotSupportedException("Prepared Ward old list must still be active");}
        var previous=old==null?null:new NecromancerCurse(old.State==56&&old.Skill==150?CombatAbility.GrimWard:CombatAbility.Terror,
            unchecked((uint)old.Creator),old.Rank,DateTime.UnixEpoch.AddMilliseconds(old.Expires*40L),0,0);
        int physical=NecromancerCurseCasts.PhysicalPenalty(packet.Physical,target.BasePhysical,!target.Hireling);
        var selected=NecromancerCurseCasts.Replacement(previous,CombatAbility.GrimWard,unchecked((uint)creator),packet.Rank,physical,
            at.AddMilliseconds(duration*40L),at,0,0);
        if(selected==null){return No(1);}
        bool retained=old is{State:56,Skill:150}&&old.Rank==packet.Rank;
        var list=new GrimWardCurseList(56,150,selected.Rank,unchecked((int)selected.Source),frame+duration,
            retained?old.Velocity:packet.Velocity,packet.AttackRate,packet.OtherRate,physical);
        // The callback restarts AI before writing secondary stats,then asks
        // for animation/velocity recalculation. Equal-rank refresh retains
        // the primary velocity and creator but rewrites all three others.
        if(old!=null&&!retained){operations.Add(3);}
        if(!retained){operations.Add(4);}
        operations.AddRange([5,6,7,8,9]);
        return new(true,list,1,1,old!=null&&!retained?1:0,1,1,operations.ToArray());
    }
}
