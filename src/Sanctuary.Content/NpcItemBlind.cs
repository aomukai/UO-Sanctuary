// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server.Mobiles;
namespace Sanctuary;

// Event09 shares Raven's arithmetic but applies a real single-target Dim
// Vision curse/AI state through the existing receiver,without a spell cast.
internal static class NpcItemBlind
{
    internal static int Bonus(BaseCreature source)=>NpcEquipmentCombat.EventBonus(source,"stupidity");
    internal static bool Apply(BaseCreature source,BaseCreature target,bool ranged,DateTime at,Func<uint> raw=null)
    {
        if(source==null||target==null){return false;}
        int bonus=Bonus(source);if(bonus==0){return false;}
        if(source is not{Alive:true,Deleted:false,ControlMaster:PlayerMobile{Alive:true,Deleted:false}}||target.Deleted||at.Kind!=DateTimeKind.Utc)
        {throw new NotSupportedException("NPC blind needs its original owned source,target and UTC event");}
        uint draw=(raw??(()=>NpcEquipmentRandom.Raw(source)))();
        int rank=NamedControl.BlindLevel(ClassStats.Level(source),ClassStats.Level(target),bonus,ranged,(int)(draw%100));
        if(rank<=0){return false;}
        // Source returns1after a positive chance even if the downstream
        // curse factory declines its target or existing stronger curse.
        _=NecromancerCurseCasts.NpcBlind(source,target,rank,at);return true;
    }
}
