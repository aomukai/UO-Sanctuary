// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server.Items;
using Server.Mobiles;

namespace Sanctuary;

// The projectile type selects the weapon's visual effects. Firing does not use inventory ammunition.
public static class BowAmmunition
{
    public static Type AmmoType(PlayerMobile owner,BaseWeapon weapon)
    {
        if(owner is not{Alive:true,Deleted:false} || !ClassCombat.Enabled(owner) || weapon==null
            || !ClassGear.Active(owner,weapon) || ClassGear.CombatBase(weapon) is not{} basis){return null;}
        if(ClassItemTypes.Is(basis.Type,"bow")){return typeof(Arrow);}
        return ClassItemTypes.Is(basis.Type,"xbow")?typeof(Bolt):null;
    }
    public static Item Stack(PlayerMobile owner,BaseWeapon weapon)
    {
        var type=AmmoType(owner,weapon);
        return type!=null && owner.Backpack?.FindItemByType(type) is{Deleted:false,Amount:>0} stack?stack:null;
    }
    public static bool WeaponAllowed(PlayerMobile owner, BaseWeapon weapon) => AmmoType(owner, weapon) != null;
}
