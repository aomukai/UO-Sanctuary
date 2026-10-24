// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server.Items;
using Server.Mobiles;

namespace Sanctuary;

// Native backpack stacks project D2's equipped quiver into the existing port inventory.
// Multiple Shot consumes once after its accepted fan. Basic attacks retain their existing port policy.
public static class BowAmmunition
{
    public static Type AmmoType(PlayerMobile owner,BaseWeapon weapon)
    {
        if(owner is not{Alive:true,Deleted:false} || !ClassCombat.Enabled(owner) || weapon==null
            || !ClassGear.Active(owner,weapon) || ClassGear.Base(weapon) is not{} basis){return null;}
        if(ClassItemTypes.Is(basis.Type,"bow")){return typeof(Arrow);}
        return ClassItemTypes.Is(basis.Type,"xbow")?typeof(Bolt):null;
    }
    public static Item Stack(PlayerMobile owner,BaseWeapon weapon)
    {
        var type=AmmoType(owner,weapon);
        return type!=null && owner.Backpack?.FindItemByType(type) is{Deleted:false,Amount:>0} stack?stack:null;
    }
    public static bool CanRelease(PlayerMobile owner,BaseWeapon weapon)=>Stack(owner,weapon)!=null;
    public static bool Consume(PlayerMobile owner,BaseWeapon weapon)
    {
        if(Stack(owner,weapon) is not{} stack){return false;}
        // Once per accepted fan, after emission. No UO lower-ammo-cost/recovery RNG or extra missile.
        stack.Consume();return true;
    }
}
