// SPDX-License-Identifier: GPL-3.0-or-later
using System;
namespace Sanctuary;

// Intrinsic NPC resource/state adapter. The approved mana exception removes
// the fresh six-mana threshold;the shared source state/lease/deadline logic
// remains unchanged. SC mode/callback/rewind/world emission are caller services.
internal static class ShadowFuryResources
{
    private static int Require(AssassinShadowBody body)
    {
        var row=ShadowFuryPackets.Row();_=ShadowEquipmentDefense.Require(body,false);
        if(body.OwnedSkills.Used!=266||body.OwnedSkillRank(266)is <1 or >255)
        {throw new NotSupportedException("Own Fury state needs its selected intrinsic skill");}
        return row.Number("Param4");
    }
    private static BladeFuryChannelServices Free(BladeFuryChannelServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return new(services.Allocate,services.Aim,services.Emit,()=>{},services.Rewind);
    }
    internal static BladeFuryChannelStep Start(AssassinShadowBody body,BladeFuryChannelState state,int frame,BladeFuryChannelServices services)
        =>BladeFuryChannel.Start(state,frame,true,true,506,0,0,Require(body),Free(services));
    internal static BladeFuryChannelStep Do(AssassinShadowBody body,BladeFuryChannelState state,int frame,BladeFuryChannelServices services)
        =>BladeFuryChannel.Do(state,frame,true,true,506,Require(body),Free(services));
}
