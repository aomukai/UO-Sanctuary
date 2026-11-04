// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server.Mobiles;
namespace Sanctuary;

// FillDamageValues setup, not a registered hit event. Qualified owned NPCs
// use the existing persistent base-armor loss before physical damage RNG.
internal static class NpcArmorOnHit
{
    internal static int Delta(BaseCreature source)=>NpcEquipmentCombat.EventBonus(source,"dmg-ac");
    internal static int Require(BaseCreature source,BaseCreature target)
    {
        int delta=Delta(source);WeaponStartEffects.RequireArmorDelta(delta);if(delta==0){return 0;}
        if(source is not{Alive:true,Deleted:false,ControlMaster:PlayerMobile{Alive:true,Deleted:false}})
        {throw new NotSupportedException("NPC armor setup needs its reviewed player-owned good-alignment source");}
        if(target is not{Alive:true,Deleted:false}){throw new NotSupportedException("NPC armor setup needs a living monster recipient");}
        if(checked(CreatureProfiles.Defense(target)+NpcEquipmentCombat.Defense(target))<0)
        {throw new NotSupportedException("Negative recipient base armor needs its explicit stat mutation adapter");}
        _=NpcCombatEvents.ForSource(source);
        // Preflight every ordinary offensive supplier before mutating armor;
        // the actual roll keeps its existing live read/RNG ordering.
        if(source is SkeletonGuardian guardian)
        {
            _=NpcEquipmentDamage.Capture(guardian,target)??throw new NotSupportedException("Missing NPC armor-setup physical supplier");
            _=NpcEquipmentCritical.Capture(guardian);_=NpcEquipmentElements.Capture(guardian);_=NpcEquipmentPoison.Capture(guardian);
        }
        else{_=ReviveEquipmentOffense.Capture(source,target);}
        return delta;
    }
    internal static void Apply(BaseCreature source,BaseCreature target)
    {
        int delta=Require(source,target);if(delta==0){return;}
        WeaponStartEffects.ApplyArmorDelta(target,delta,TimedEffects.Now);
        if(target is not{Alive:true,Deleted:false})
        {throw new NotSupportedException("Recipient died while settling prior effects before NPC armor allocation");}
    }
}
