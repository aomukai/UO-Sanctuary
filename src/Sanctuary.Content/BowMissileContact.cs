// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server.Mobiles;
namespace Sanctuary;

internal static class BowMissileContact
{
    internal static bool Handles(CombatAbility ability)=>ability is CombatAbility.MagicArrow or CombatAbility.MultipleShot or CombatAbility.FireArrow or CombatAbility.ColdArrow or CombatAbility.IceArrow or CombatAbility.GuidedArrow;
    internal static bool Handles(WeaponFlight flight)=>flight is{HostileSpell:false,Npc:null,Mirrored:null,Throwing:null}
        &&(flight.Ability==CombatAbility.MagicArrow&&flight.Missile=="magicarrow"
            ||flight.Ability==CombatAbility.MultipleShot&&flight.Missile is "multipleshotarrow" or "multipleshotbolt"
            ||flight.Ability==CombatAbility.FireArrow&&flight.Missile=="firearrow"
            ||flight.Ability==CombatAbility.ColdArrow&&flight.Missile=="coldarrow"
            ||flight.Ability==CombatAbility.IceArrow&&flight.Missile=="icearrow"&&flight.Damage.FreezeColdLength
            ||flight.Ability==CombatAbility.GuidedArrow&&flight.Missile=="guidedarrow"&&flight.Guided!=null);
    private static string Row(CombatAbility ability)=>ability switch
    {CombatAbility.MagicArrow=>"magicarrow",CombatAbility.FireArrow=>"firearrow",CombatAbility.ColdArrow=>"coldarrow",CombatAbility.IceArrow=>"icearrow",CombatAbility.GuidedArrow=>"guidedarrow",_=>throw new NotSupportedException("Unqualified bow impact")};
    internal static void Require(PlayerMobile owner,CombatAbility ability)
    {
        foreach(string missile in ability==CombatAbility.MultipleShot?new[]{"multipleshotarrow","multipleshotbolt"}:new[]{Row(ability)})
        {
            foreach(var(field,value)in new[]{("GetHit",1),("HitClass",ability is CombatAbility.FireArrow or CombatAbility.ColdArrow or CombatAbility.IceArrow?0:10),("pSrvDoFunc",ability==CombatAbility.GuidedArrow?7:1),("pSrvDmgFunc",ability is CombatAbility.MultipleShot or CombatAbility.GuidedArrow?0:ability==CombatAbility.IceArrow?2:1),("ToHit",ability==CombatAbility.GuidedArrow?0:1)})
            {if(NecromancerSummons.Source(missile,field)!=value){throw new NotSupportedException("Bow contact row conflict: "+missile+"/"+field);}}
        }
        if(ability==CombatAbility.IceArrow&&NecromancerSummons.Source("icearrow","dParam1")!=100){throw new NotSupportedException("Ice Arrow freeze percentage is not qualified");}
        WeaponStartEffects.Require(owner);
    }
    internal static void Contact(PlayerMobile owner,BaseCreature target,WeaponFlight flight,GearDamage damage,int rate256)
        =>PlayerWeaponMissileContact.Release(owner,target,flight,damage,rate256,NecromancerSummons.Source(flight.Missile,"HitClass"),(flight.Flags&0x10000)!=0?160u:32u);
}
