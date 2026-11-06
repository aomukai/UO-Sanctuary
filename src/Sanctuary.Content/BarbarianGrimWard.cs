// SPDX-License-Identifier: GPL-3.0-or-later
using Server;
using Server.Items;
using Server.Mobiles;
namespace Sanctuary;

public static class BarbarianGrimWard
{
    internal static bool ValidTarget(PlayerMobile owner,Serial selected)=>World.FindItem(selected) is Corpse corpse&&GrimWardActors.Target(owner,corpse);
    internal static bool Begin(PlayerMobile owner,Serial selected,Point3D point,long now)
    {
        if(!ValidTarget(owner,selected)||!SkillCombat.Begin(owner,CombatAbility.GrimWard,selected,World.FindItem(selected).Location,now,
            (p,_,id,_)=>ValidTarget(p,id))){return false;}
        // Shared start33 pays once before the cast's do event. Cancellation
        // cannot refund this payment; the corpse is reserved only on release.
        if(NecromancerEffects.Pay(owner,SkillMechanics.Values(owner,CombatAbility.GrimWard).Mana256))
        {ClassCombat.For(owner).GrimWardTarget=selected;return true;}
        ClassCombat.Stop(owner);return false;
    }
    internal static bool Release(PlayerMobile owner,Serial selected)
    {
        var action=ClassCombat.For(owner);bool paid=selected!=Serial.Zero&&action.GrimWardTarget==selected;action.GrimWardTarget=Serial.Zero;
        if(!paid||!BarbarianCombat.Available(owner,CombatAbility.GrimWard)||!ValidTarget(owner,selected)){return false;}
        var state=GrimWardActors.CreatePrepared(owner,(Corpse)World.FindItem(selected),SkillMechanics.Values(owner,CombatAbility.GrimWard),TimedEffects.Now);
        if(state==null){return false;}
        owner.PlaySound(0x1E5); // Identified cry placeholder; final Ward audio pending.
        return true;
    }
}
