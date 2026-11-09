// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
namespace Sanctuary;

// Recovery subphase from ExecuteEvents, before the existing return bridge.
// Incoming HP commitment/death dispatch still need a separate ordered bridge. Attacker on-damage effects (including Crushing Blow) belong before
// this entry. Miss/block attack-attempt callbacks remain in the outer scope.
internal static class PreparedWeaponEvents
{
    internal static void BeforeReturn(Mobile attacker,Mobile defender,int physical256,bool ranged,
        int absorbedLife256=0,Action<int> leech=null,PsychicWardReactionProposal.Ticket ward=null)
    {
        NpcLightningReturn.RequireNativeIncoming(attacker,defender,ranged);
        if(!ranged){NpcItemSlow.NativeClay(attacker,defender,TimedEffects.Now);}
        if(ranged){HexSiphonEvents.MissileDamage(attacker,defender);}
        IronMaiden.MeleeDamage(attacker,defender,physical256,ranged);
        // Attacker damage events precede defender incoming events. A lethal
        // Iron Maiden return must not become a prior lightning packet.
        if(attacker is{Alive:true,Deleted:false}){NpcLightningReturn.NativeIncoming(attacker,defender,ranged,TimedEffects.Now);}
        // Approved live ticket:captured before mitigation,consumed once here.
        // Callers without an eligible Ward registration supply null.
        ward?.Deliver(attacker,defender,ranged,TimedEffects.Now);
        ClayGolemSlow.Incoming(attacker,defender,ranged,TimedEffects.Now);
        // Frozen Armor's incoming physical event belongs before HP/death, and
        // accepts positive fractional damage. Keep the existing other-event
        // order; exact source sibling-registration order remains separate.
        ColdArmors.PhysicalMelee(attacker,defender,physical256,ranged);
        NecromancerEffects.LifeTap(attacker,defender,physical256);
        int eligible=(int)Math.Min(Math.Max(0,physical256),Math.Max(0,SkillHealth.Balance256(defender)));
        leech?.Invoke(eligible);
        // dwAbsLife is committed after the source physical clip and leech.
        // Preserve that clip when subsequent return reads the healed defender.
        if(absorbedLife256>0){SkillHealth.Heal(defender,absorbedLife256);}
        PaladinThorns.Prepared(attacker,defender,physical256,ranged,eligible);
    }
}
