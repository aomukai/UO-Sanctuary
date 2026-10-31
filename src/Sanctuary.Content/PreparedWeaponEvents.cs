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
        int absorbedLife256=0,Action<int> leech=null)
    {
        ClayGolemSlow.Incoming(attacker,defender,ranged,TimedEffects.Now);
        NecromancerEffects.LifeTap(attacker,defender,physical256);
        int eligible=(int)Math.Min(Math.Max(0,physical256),Math.Max(0,SkillHealth.Balance256(defender)));
        leech?.Invoke(eligible);
        // dwAbsLife is committed after the source physical clip and leech.
        // Preserve that clip when subsequent return reads the healed defender.
        if(absorbedLife256>0){SkillHealth.Heal(defender,absorbedLife256);}
        PaladinThorns.Prepared(attacker,defender,physical256,ranged,eligible);
    }
}
