// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using Server;
using Server.Mobiles;
namespace Sanctuary;

// Opt-in Kind1 recipient writer. Callers supply already mitigated HP damage;
// this does not dispatch a second weapon event or replay native pet modifiers.
internal static class ReviveEquipmentIncoming
{
    internal const string Profile="frozen-equipment-incoming-v1";
    internal static ReviveNativeSetup Prepare(ReviveNativeSetup setup,ReviveOriginalState original)
        =>ReviveEquipmentEvents.Prepare(setup,original)with{EquipmentIncomingProfile=Profile};
    internal static void Validate(ReviveNativeState state)
    {
        if(state.Setup.EquipmentIncomingProfile==null){return;}
        try
        {
            if(state.Setup.EquipmentIncomingProfile!=Profile||Prepare(state.Setup,state.Original)!=state.Setup)
            {throw new InvalidDataException("Invalid original Revive incoming profile");}
        }
        catch(Exception e)when(e is NotSupportedException or OverflowException)
        {throw new InvalidDataException("Unavailable original Revive incoming profile",e);}
    }
    internal static bool Handles(BaseCreature actor)=>ReviveEquipmentEvents.Handles(actor)
        &&ReviveNativeActivation.State(actor)is{Setup.EquipmentIncomingProfile:Profile};
    internal static void RequireBody(BaseCreature actor)
    {
        if(actor.GetType().GetMethod(nameof(BaseCreature.Damage))?.DeclaringType!=typeof(BaseCreature)
            ||actor.GetType().GetMethod(nameof(BaseCreature.OnDamage))?.DeclaringType!=typeof(BaseCreature))
        {throw new NotSupportedException("Original incoming damage/notification override needs its own adapter");}
    }
    internal static int Damage(Mobile source,BaseCreature target,int damage256)
    {
        if(!Handles(target)){throw new NotSupportedException("No admitted original Revive incoming writer");}
        RequireBody(target);PreparedMonsterNativeDamage.RequireBaseModifiers(source,target);
        int before=SkillHealth.Balance256(target),displayed=target.Hits;
        int after=(int)Math.Max(0,(long)before-damage256);if(after<256){after=0;}
        using var credit=PreparedMonsterCredit.CaptureOwner(source,target);
        if(source==null){PreparedMonsterNativeDamage.NotifyAnonymous(target,after);}
        else{PreparedMonsterNativeDamage.Notify(source,target,after);}
        SkillHealth.SetBalance256(target,after);
        if(target is not{Alive:true,Deleted:false}||SkillHealth.Balance256(target)!=after)
        {throw new NotSupportedException("Original incoming health setter changed the prepared balance");}
        CombatDamage.Finish(target,source);
        // Preserve SkillHealth's whole native-display loss return contract.
        return Math.Max(0,displayed-target.Hits);
    }
}
