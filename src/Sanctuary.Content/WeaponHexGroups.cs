// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
namespace Sanctuary;

// Prepared Sanctuary proposal for current groups6/7. No learned route or
// automatic event dispatcher opts into this policy before approval.
internal static class WeaponHexGroups
{
    internal static bool Approved=>false;
    private static void Require(CombatAbility incoming)
    {if(incoming is not(CombatAbility.HexBane or CombatAbility.HexPurge or CombatAbility.HexSiphon)){throw new ArgumentException("Not a weapon Hex");}}
    internal static void ReplaceBuff(Mobile owner,CombatAbility incoming)
    {
        Require(incoming);ArgumentNullException.ThrowIfNull(owner);
        if(incoming!=CombatAbility.HexBane){HexBane.ClearBuff(owner);}
        if(incoming!=CombatAbility.HexPurge){HexPurge.ClearBuff(owner);}
        if(incoming!=CombatAbility.HexSiphon){HexSiphon.ClearBuff(owner);}
    }
    internal static void ReplaceTarget(Mobile target,CombatAbility incoming)
    {
        Require(incoming);ArgumentNullException.ThrowIfNull(target);
        if(incoming!=CombatAbility.HexBane){HexBane.ClearDebuff(target);}
        if(incoming!=CombatAbility.HexPurge){HexPurge.ClearDebuff(target);}
        if(incoming!=CombatAbility.HexSiphon){HexSiphon.ClearDebuff(target);}
    }
}
