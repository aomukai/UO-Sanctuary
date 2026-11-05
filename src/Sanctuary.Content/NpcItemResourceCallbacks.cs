// SPDX-License-Identifier: GPL-3.0-or-later
using System;
namespace Sanctuary;

// MAXHP/MAXMANA/MAXSTAMINA branches of the server item-stat callback.
// Operates on source FP256 fields, not native UO attributes. Other branches
// (events, skills, auras, charges and states) need their own consumers.
internal static class NpcItemResourceCallbacks
{
    internal static void Changed(EquipmentStatValues values,EquipmentStatNode recipient,uint packed,int previous,int next,int damageRegeneration)
    {
        int maximum=EquipmentStatKey.Stat(packed);
        if(maximum is not(7 or 9 or 11)||previous==next||previous<=0){return;}
        int current=EquipmentStatValues.Total(recipient,EquipmentStatKey.Pack(maximum-1));
        if(current<=0){return;}
        // Reject unverified nonpositive maxima/overflow before any write.
        // This is the live NPC domain, not a new source clamping rule.
        if(next is <1 or >16640000||damageRegeneration is <0 or >32767)
        {throw new NotSupportedException("Item resource callback exceeds the verified positive NPC resource/regeneration domain");}
        values.SetBase(recipient,EquipmentStatKey.Pack(maximum-1),BattleOrders.Rescale256(current,previous,next));
        if(maximum==7&&recipient.Owner?.Kind==1&&damageRegeneration!=0)
        {
            int old=EquipmentStatValues.Total(recipient,EquipmentStatKey.Pack(74));
            values.SetBase(recipient,EquipmentStatKey.Pack(74),BattleOrders.RegenerationAfter(previous,next,current,damageRegeneration,old));
        }
    }
}
