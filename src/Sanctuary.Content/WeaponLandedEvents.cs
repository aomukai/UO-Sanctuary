// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;
namespace Sanctuary;

internal readonly record struct WeaponLandedContact(PlayerMobile Source,BaseCreature Target,bool Ranged);

// Callers have already resolved accuracy/block/dodge. Zero life damage is a
// valid landed contact. This boundary only attaches/captures state; secondary
// damage belongs to SkillDamageEvents, after mitigation and before life loss.
// No production consumer is installed while Purge's rule remains undecided.
internal static class WeaponLandedEvents
{
    internal static event Action<WeaponLandedContact> BeforeDamage;
    internal static bool Notify(Mobile source,Mobile target,bool ranged)
    {
        var notify=BeforeDamage;
        if(notify==null||source is not PlayerMobile{Alive:true,Deleted:false} owner
            ||target is not BaseCreature{Alive:true,Deleted:false} npc||!target.CanBeDamaged()
            ||owner.Map==null||owner.Map==Map.Internal||target.Map!=owner.Map||!ClassCombat.IsHostile(owner,target)
            ||!CombatItemEvents.Allowed(source,target)){return false;}
        int before=SkillHealth.Balance256(target);if(before<=0){return false;}
        notify(new(owner,npc,ranged));
        if(target is not{Alive:true,Deleted:false}||SkillHealth.Balance256(target)!=before)
        {throw new NotSupportedException("Landed weapon observers must defer life changes to the damage phase");}
        return true;
    }
}
