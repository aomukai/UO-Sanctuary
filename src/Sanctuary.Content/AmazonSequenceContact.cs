// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server.Mobiles;
namespace Sanctuary;

internal static class AmazonSequenceContact
{
    internal static void Release(PlayerMobile owner,BaseCreature target,CombatAbility ability,bool basic,int attackPercent,int physicalPercent,
        Func<double> hit,Func<double> elements,Func<int,int> physical,Func<int,int> poison,Random effects,Random wear)
    {
        try
        {
            var weapon=CombatTiming.Equipped(owner);
            var contact=PlayerWeaponContact.PrepareOrdinary(owner,target,attackPercent,physicalPercent,
                ()=>AmazonJab.ValidTarget(owner,target.Serial),hit,elements,physical,poison,effects,allowNative:true,suppressWear:true);
            // The expansion writer replaces native weapon wear at its actual
            // drain phase;it does not add a second roll after return damage.
            contact=contact with{AttemptScoped=true,RangeCheck=_=>AmazonJab.ValidTarget(owner,target.Serial),WeaponWear=()=>AmazonJab.Wear(weapon,wear)};
            AmazonMeleeEffects.ReleaseContact(owner,target,contact,ability,basic);
        }
        catch(NotSupportedException e){owner.SendMessage(0x22,e.Message);}
    }
}
