// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Linq;
using Server.Mobiles;
namespace Sanctuary.Integration;

// Older continuation scenarios need a specific legal budget,not the retired
// player launch-RNG override. Select an action index through the approved
// source primitive; preserve every other original player's saved counter.
internal static class PlayerPierceFixtures
{
    internal static uint NextBudget(PlayerMobile owner,int wanted)
    {
        if(!PlayerPierceActions.Approved||wanted is<0 or>4){throw new InvalidOperationException("Pierce fixture policy/budget");}
        int chance=WeaponMissiles.CurrentPierce(owner);uint current=PlayerPierceActions.Index(owner);
        for(uint offset=1;offset<=100000;offset++)
        {
            uint next=unchecked(current+offset);
            if(NamedProjectiles.SeededPierceBudget(chance,unchecked((int)next)).Budget!=wanted){continue;}
            PlayerPierceActions.Restore(PlayerPierceActions.Snapshot().Where(r=>r.Owner!=owner.Serial.Value).Append(new(owner.Serial.Value,unchecked(next-1))).ToArray());
            return next;
        }
        throw new InvalidOperationException("No legal indexed Pierce budget for fixture");
    }
}
