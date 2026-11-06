// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server.Mobiles;
namespace Sanctuary;

internal static class NpcKillRecovery
{
    internal static int Bonus(BaseCreature source,bool demonOnly)=>NpcEquipmentCombat.EventBonus(source,demonOnly?"demon-heal":"heal-kill");
    internal static bool Apply(BaseCreature source,BaseCreature target,bool demonOnly)
    {
        if(source==null||target==null||demonOnly&&!NamedCombat.Demon(target)){return false;}
        int amount=Bonus(source,demonOnly);if(amount==0){return false;}
        if(amount<0||amount>int.MaxValue/256)
        {throw new NotSupportedException("NPC kill healing needs a nonnegative signed FP256 item amount");}
        if(source is not{Alive:true,Deleted:false,ControlMaster:PlayerMobile{Alive:true,Deleted:false}}||SkillHealth.Balance256(source)<=0)
        {throw new NotSupportedException("NPC kill healing needs its original living owned source");}
        if(SkillHealth.Balance256(source)>=SkillHealth.Maximum256(source)){return false;}
        SkillHealth.Heal(source,checked(amount*256));
        source.FixedEffect(0x376A,10,10,0x21,0); // Identified healing overlay151 placeholder.
        return true;
    }
}
