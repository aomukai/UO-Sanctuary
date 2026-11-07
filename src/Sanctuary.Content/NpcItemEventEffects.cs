// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server.Mobiles;
namespace Sanctuary;

// Shared effect bodies for event-only carriers and full original graphs.
// Chance-to-cast callbacks keep their own graph/metadata/skill services.
internal static class NpcItemEventEffects
{
    internal static int Run(int function,int phase,SkeletonGuardian actor,BaseCreature other,ref PreparedMonsterDamagePacket packet,DateTime at,Func<int,int> roll)
    {
        if(function==7)
        {
            var result=NpcItemKnockback.Apply(actor,other,packet);
            packet=result.Packet;return result.Success?1:0;
        }
        if(function==8){return NpcItemFear.Apply(actor,other,at)?1:0;}
        if(function==9){return NpcItemBlind.Apply(actor,other,phase==6,at)?1:0;}
        if(function==10){return NpcLightningReturn.Apply(actor,other,at)?1:0;}
        if(function==19){return NpcItemSlow.Apply(actor,other,phase==6,at)?1:0;}
        if(function==29){return NpcRestInPeace.Apply(actor,other)?1:0;}
        if(function==17){return 0;} // NPC sources fail the player-only mana-after-kill guard.
        if(function is 18 or 28){return NpcKillRecovery.Apply(actor,other,function==18)?1:0;}
        if(function==13){return 0;} // Damage-to-mana is player-only before all packet/stat/RNG services.
        if(function==14){return NpcItemFreeze.Apply(actor,other,phase==6,at)?1:0;}
        if(function==15){NpcOpenWounds.Apply(actor,other,at,phase==6,roll);return 1;}
        if(function==16){NpcCrushing.Apply(actor,other,phase==6,roll);return 1;}
        throw new NotSupportedException("NPC callback has no admitted shared effect body:"+function);
    }
}
