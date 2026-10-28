// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;
namespace Sanctuary;

internal static class WeaponHexes
{
    internal static bool Equipped(PlayerMobile p)=>CombatTiming.Equipped(p) is{} w&&WarlockLevitation.Weapon(w)&&ClassGear.Active(p,w);
    internal static bool CanCast(PlayerMobile p)=>p is{Alive:true,Deleted:false,IsDeadBondedPet:false}&&p.Map!=null&&p.Map!=Map.Internal
        &&ClassCreation.Profiles.TryGet(p.Serial,out var c)&&c.Class==DiabloClass.Warlock&&Equipped(p)
        &&!ClassItemTypes.Is(ClassGear.Base(CombatTiming.Equipped(p)).Type,"h2h");
    // Current groups6/7are new. Do not silently choose replacement or stacking
    // when multiple prepared hexes become available. No learned route uses this.
    internal static void GuardBuff(Mobile owner,CombatAbility ability)
    {
        if(ability!=CombatAbility.HexBane&&HexBane.Buff(owner)!=null||ability!=CombatAbility.HexSiphon&&HexSiphon.Buff(owner)!=null||ability!=CombatAbility.HexPurge&&HexPurge.Buff(owner)!=null)
        {throw new NotSupportedException("Modern cross-hex weapon-buff replacement remains unverified");}
    }
    internal static void GuardTarget(Mobile target,CombatAbility ability)
    {
        if(ability!=CombatAbility.HexBane&&HexBane.Debuff(target)!=null||ability!=CombatAbility.HexSiphon&&HexSiphon.Debuff(target)!=null||ability!=CombatAbility.HexPurge&&HexPurge.Debuff(target)!=null)
        {throw new NotSupportedException("Modern cross-hex target-state replacement remains unverified");}
    }
}
