// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server.Mobiles;
namespace Sanctuary;

internal readonly record struct NpcElementPierce(int Fire,int Cold,int Lightning,int Magic)
{
    internal int? For(string element)=>element switch{"fire"=>Fire,"cold"=>Cold,"ltng"=>Lightning,"mag"=>Magic,_=>null};
}

// Allocation only: it may consume existing shield pools, but does not commit
// HP/cold/poison/events. Qualified ordered contact remains a separate consumer.
internal static class ReviveEquipmentImpact
{
    internal static NpcWeaponImpact Prepare(BaseCreature actor,BaseCreature target,int flags,Func<int,int> limited=null,Func<uint> raw=null,int attackSlot=1)
    {
        if(actor is not{Alive:true,Deleted:false}||target is not{Alive:true,Deleted:false}||(flags&~0xffff)!=0)
        {throw new ArgumentException("Revive impact allocation requires living source/recipient and result flags");}
        if(ReviveNativeActivation.State(actor)is not{Setup.EquipmentLifeProfile:ReviveEquipmentCombat.Profile})
        {throw new NotSupportedException("No prepared original Revive impact source");}
        if(!NpcEquipmentImpact.Fills(flags))
        {return new(actor.Serial.Value,target.Serial.Value,NpcEquipmentImpact.Packet(flags,0,null,default,0,default,0),default,0);}
        if(ReviveNativeActivation.State(target)is{Setup.EquipmentLifeProfile:not null}&&!ReviveEquipmentDefenses.Handles(target))
        {throw new NotSupportedException("Frozen equipped recipient needs its resistance/duration/mitigation adapter");}
        _=IronGolemContact.Events(target); // Reuse converted-recipient capability admission before RNG.
        var snapshot=ReviveEquipmentOffense.Capture(actor,target,attackSlot);
        limited??=n=>NpcEquipmentRandom.Limited(actor,n);raw??=()=>NpcEquipmentRandom.Raw(actor);
        var rolled=ReviveEquipmentOffense.Roll(snapshot,limited,raw);
        // Physical modifiers are already in the captured NPC percentage pool.
        return NpcEquipmentImpact.FromRoll(actor,target,flags,rolled,snapshot.Pierce.For);
    }
}
