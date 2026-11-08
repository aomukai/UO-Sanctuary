// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
namespace Sanctuary;

// Approved one-sequence NPC action:frame10 release/end18,no rewind or
// cross-action lease. Shared st26/do48 still owns its intra-action state.
internal static class ShadowBladeFury
{
    internal static int Start(ShadowSkillCasting.Cast cast)
    {
        var step=ShadowFuryResources.Start(cast.Body,new(false,0,0),0,new(()=>true,()=>true,()=>false,()=>{},()=>{}));
        cast.Fury=step.Success?step.State:null;return step.Success?1:0;
    }
    internal static int Release(ShadowSkillCasting.Cast cast,int rank,DateTime at)
    {
        if(cast.Fury==null){return 0;}var body=cast.Body;
        var action=ShadowActions.State(body);
        if(action?.Cast!=cast||action.Frame+1!=10){return 0;}
        var aim=cast.Aim;
        if(cast.Target is{} target)
        {
            if(target is not{Alive:true,Deleted:false}||target.Map!=body.Map||!ShadowSkillCasting.Enemy(body,target)){return 0;}
            aim=target.Location;
        }
        if(!ShadowFuryMissiles.CanLaunch(body,aim,at)){return 0;}
        var packet=ShadowFuryPackets.Capture(body,rank,false);ShadowFuryMissiles.RequirePacket(packet);
        var chances=ShadowFuryPackets.CriticalChances(body);
        if(chances.Passive<0||chances.Deadly<0||chances.Mastery<0){throw new NotSupportedException("Negative Fury critical chances are not supported");}
        long emitted=0;
        var step=ShadowFuryResources.Do(body,cast.Fury,10,new(()=>true,()=>true,()=>
        {
            ulong seed=NpcWorldAllocation.UnitSeed();
            bool critical=NpcMissileCritical.Unsigned(chances.Passive,chances.Deadly,chances.Mastery,()=>NpcEquipmentRandom.Raw(body));
            var captured=packet with{Flags=packet.Flags|(critical?2:0),Damage=packet.Damage with{Critical=critical}};
            emitted=ShadowFuryMissiles.LaunchCaptured(body,aim,rank,captured,seed,at);return emitted>0;
        },()=>{},()=>{}));
        cast.Fury=step.State;return step.Success?1:0;
    }
}
