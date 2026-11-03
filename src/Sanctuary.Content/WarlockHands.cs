// SPDX-License-Identifier: GPL-3.0-or-later
using Server;
using Server.Items;
using Server.Mobiles;
namespace Sanctuary;

// Owned CharStats3.3.93847:Grimoire-only companion for a two-handed weapon;
// TwoHandedDamageAsOneHanded is unset. This is innate,independent of Mastery.
internal static class WarlockHands
{
    internal static bool Innate(PlayerMobile owner)=>owner!=null&&ClassCreation.Profiles?.TryGet(owner.Serial,out var profile)==true
        &&profile.Class==DiabloClass.Warlock;
    private static bool TwoHandWeapon(Item item)=>item is BaseWeapon&&ClassGear.Converted(item)
        &&ClassGear.Slot(item)==GearSlot.Weapon&&ClassGear.Base(item) is{TwoHanded:true};
    internal static bool CanPair(PlayerMobile owner,Item first,Item second)=>Innate(owner)
        &&(TwoHandWeapon(first)&&GrimoireEquipment.Ready(second)||TwoHandWeapon(second)&&GrimoireEquipment.Ready(first));
    internal static bool Forbidden(PlayerMobile owner,Item first,Item second)=>Innate(owner)
        &&ClassGear.Slot(first) is GearSlot.Weapon or GearSlot.Shield&&ClassGear.Slot(second) is GearSlot.Weapon or GearSlot.Shield
        &&(TwoHandWeapon(first)||TwoHandWeapon(second))&&!CanPair(owner,first,second);
    internal static bool TwoHandedDamage(Item item)=>item.Parent is PlayerMobile owner&&Innate(owner)&&TwoHandWeapon(item);
    internal static void Normalize(PlayerMobile owner)
    {
        Item weapon=null,book=null;int hands=0;
        foreach(var item in owner.Items)
        {
            if(item.Layer is not(Layer.OneHanded or Layer.TwoHanded)){continue;}hands++;
            if(TwoHandWeapon(item)){weapon=item;}else if(GrimoireEquipment.Ready(item)){book=item;}
        }
        if(weapon==null){return;}
        var layer=hands==2&&book!=null?Layer.OneHanded:Layer.TwoHanded;
        if(weapon.Layer!=layer&&(owner.FindItemOnLayer(layer) is not{} current||current==weapon)){weapon.Layer=layer;}
    }
}
