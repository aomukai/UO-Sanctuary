// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server.Mobiles;
namespace Sanctuary;

internal static class NpcItemKnockback
{
    internal static int Bonus(BaseCreature source)=>NpcEquipmentCombat.EventBonus(source,"knock");
    // Callback07 changes the current packet. Movement and mode eligibility
    // belong to the later shared return phase, including immune-mode fallback.
    internal static (bool Success,int Flags) Evaluate(bool source,bool target,bool packet,int value,int targetKind,
        bool metadata,bool large,bool small,int flags,Func<uint> raw)
    {
        if(!source||!target||!packet||value<=0){return(false,flags);}
        int chance=targetKind==1&&metadata?(large?32:small?128:64):64;
        bool success=(raw()&127)<chance;
        return(success,success?flags|8:flags);
    }
    internal static void Require(BaseCreature target)
    {
        if(SourceKnockbacks.Rule(target)is not{Small:not null,Large:not null})
        {throw new NotSupportedException("NPC item knockback needs pinned recipient size flags");}
    }
    internal static (bool Success,PreparedMonsterDamagePacket Packet) Apply(BaseCreature source,BaseCreature target,
        PreparedMonsterDamagePacket packet,Func<uint> raw=null)
    {
        if(source==null||target==null||packet==null){return(false,packet);}
        int value=Bonus(source);if(value<=0){return(false,packet);}
        if(source is not{Alive:true,Deleted:false,ControlMaster:PlayerMobile{Alive:true,Deleted:false}})
        {throw new NotSupportedException("NPC item knockback needs its original owned source");}
        if(SourceKnockbacks.Rule(target)is not{Small:not null,Large:not null} rule)
        {throw new NotSupportedException("NPC item knockback needs pinned recipient size flags");}
        var result=Evaluate(true,true,true,value,1,true,rule.Large.Value,rule.Small.Value,packet.ResultFlags,
            raw??(()=>NpcEquipmentRandom.Raw(source)));
        return(result.Success,result.Flags==packet.ResultFlags?packet:packet with{ResultFlags=result.Flags});
    }
}
