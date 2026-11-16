// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server.Mobiles;
namespace Sanctuary;

internal static class BowMissileContact
{
    internal static bool Handles(CombatAbility ability)=>ability is CombatAbility.MagicArrow or CombatAbility.MultipleShot;
    internal static bool Handles(WeaponFlight flight)=>flight is{HostileSpell:false,Npc:null,Mirrored:null,Throwing:null}
        &&(flight.Ability==CombatAbility.MagicArrow&&flight.Missile=="magicarrow"
            ||flight.Ability==CombatAbility.MultipleShot&&flight.Missile is "multipleshotarrow" or "multipleshotbolt");
    internal static void Require(PlayerMobile owner,CombatAbility ability)
    {
        foreach(string missile in ability==CombatAbility.MultipleShot?new[]{"multipleshotarrow","multipleshotbolt"}:new[]{"magicarrow"})
        {
            foreach(var(field,value)in new[]{("GetHit",1),("HitClass",10),("pSrvDoFunc",1),("pSrvDmgFunc",ability==CombatAbility.MultipleShot?0:1),("ToHit",1)})
            {if(NecromancerSummons.Source(missile,field)!=value){throw new NotSupportedException("Bow contact row conflict: "+missile+"/"+field);}}
        }
        WeaponStartEffects.Require(owner);
    }
    internal static void Contact(PlayerMobile owner,BaseCreature target,WeaponFlight flight,GearDamage damage,int rate256)
        =>PlayerWeaponMissileContact.Release(owner,target,flight,damage,rate256,10,(flight.Flags&0x10000)!=0?160u:32u);
}
