// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server.Mobiles;
namespace Sanctuary;

internal static class MagicArrowContact
{
    internal static bool Handles(WeaponFlight flight)=>flight is{Ability:CombatAbility.MagicArrow,Missile:"magicarrow",HostileSpell:false,Npc:null,Mirrored:null,Throwing:null};
    internal static void Require(PlayerMobile owner)
    {
        foreach(var(field,value)in new[]{("GetHit",1),("HitClass",10),("pSrvDoFunc",1),("pSrvDmgFunc",1),("ToHit",1)})
        {if(NecromancerSummons.Source("magicarrow",field)!=value){throw new NotSupportedException("Magic Arrow contact row conflict: "+field);}}
        WeaponStartEffects.Require(owner);
    }
    internal static void Contact(PlayerMobile owner,BaseCreature target,WeaponFlight flight,GearDamage damage,int rate256)
        =>PlayerWeaponMissileContact.Release(owner,target,flight,damage,rate256,10,(flight.Flags&0x10000)!=0?160u:32u);
}
