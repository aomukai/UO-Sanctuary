// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server.Mobiles;
namespace Sanctuary;

internal static class BowMissileContact
{
    internal static bool Handles(CombatAbility ability)=>ability is CombatAbility.MagicArrow or CombatAbility.MultipleShot or CombatAbility.FireArrow or CombatAbility.ColdArrow or CombatAbility.IceArrow or CombatAbility.GuidedArrow or CombatAbility.ExplodingArrow or CombatAbility.FreezingArrow or CombatAbility.ImmolationArrow or CombatAbility.Strafe or CombatAbility.LightningBolt or CombatAbility.LightningFury;
    internal static bool Handles(WeaponFlight flight)=>flight is{HostileSpell:false,Npc:null,Mirrored:null,Throwing:null}
        &&(flight.Ability==CombatAbility.MagicArrow&&flight.Missile=="magicarrow"
            ||flight.Ability==CombatAbility.MultipleShot&&flight.Missile is "multipleshotarrow" or "multipleshotbolt"
            ||flight.Ability==CombatAbility.FireArrow&&flight.Missile=="firearrow"
            ||flight.Ability==CombatAbility.ColdArrow&&flight.Missile=="coldarrow"
            ||flight.Ability==CombatAbility.IceArrow&&flight.Missile=="icearrow"&&flight.Damage.FreezeColdLength
            ||flight.Ability==CombatAbility.GuidedArrow&&flight.Missile=="guidedarrow"&&flight.Guided!=null
            ||flight.Ability==CombatAbility.ExplodingArrow&&flight.Missile=="explodingarrow"
            ||flight.Ability==CombatAbility.FreezingArrow&&flight.Missile=="freezingarrow"
            ||flight.Ability==CombatAbility.ImmolationArrow&&flight.Missile=="immolationarrow"
            ||flight.Ability==CombatAbility.Strafe&&flight.Missile is "strafearrow" or "strafebolt"
            ||flight.Ability==CombatAbility.LightningBolt&&flight.Missile=="lightningjavelin"&&flight.Damage.LightningJavelinPercent==100
            ||flight.Ability==CombatAbility.LightningFury&&flight.Missile=="lightningfury");
    private static string Row(CombatAbility ability)=>ability switch
    {CombatAbility.MagicArrow=>"magicarrow",CombatAbility.FireArrow=>"firearrow",CombatAbility.ColdArrow=>"coldarrow",CombatAbility.IceArrow=>"icearrow",CombatAbility.GuidedArrow=>"guidedarrow",CombatAbility.ExplodingArrow=>"explodingarrow",CombatAbility.FreezingArrow=>"freezingarrow",CombatAbility.ImmolationArrow=>"immolationarrow",CombatAbility.LightningBolt=>"lightningjavelin",CombatAbility.LightningFury=>"lightningfury",_=>throw new NotSupportedException("Unqualified bow impact")};
    internal static void Require(PlayerMobile owner,CombatAbility ability)
    {
        foreach(string missile in ability==CombatAbility.MultipleShot?new[]{"multipleshotarrow","multipleshotbolt"}:ability==CombatAbility.Strafe?new[]{"strafearrow","strafebolt"}:new[]{Row(ability)})
        {
            foreach(var(field,value)in new[]{("GetHit",1),("HitClass",ability is CombatAbility.LightningBolt or CombatAbility.LightningFury?64:ability is CombatAbility.FireArrow or CombatAbility.ColdArrow or CombatAbility.IceArrow or CombatAbility.ExplodingArrow or CombatAbility.FreezingArrow?0:10),("pSrvDoFunc",ability==CombatAbility.GuidedArrow?7:1),("pSrvDmgFunc",ability==CombatAbility.LightningBolt?12:ability is CombatAbility.MultipleShot or CombatAbility.GuidedArrow or CombatAbility.ExplodingArrow or CombatAbility.FreezingArrow or CombatAbility.ImmolationArrow or CombatAbility.Strafe or CombatAbility.LightningFury?0:ability==CombatAbility.IceArrow?2:1),("ToHit",ability is CombatAbility.GuidedArrow or CombatAbility.LightningBolt or CombatAbility.LightningFury?0:1)})
            {if(NecromancerSummons.Source(missile,field)!=value){throw new NotSupportedException("Bow contact row conflict: "+missile+"/"+field);}}
        }
        if(ability==CombatAbility.IceArrow&&NecromancerSummons.Source("icearrow","dParam1")!=100){throw new NotSupportedException("Ice Arrow freeze percentage is not qualified");}
        if(ability is CombatAbility.ExplodingArrow or CombatAbility.FreezingArrow)
        {
            if(NecromancerSummons.Source(Row(ability),"pSrvHitFunc")!=4||NecromancerSummons.Source(Row(ability),"AlwaysExplode")!=1){throw new NotSupportedException("Explosive arrow parent row conflict");}
            string child=ability==CombatAbility.FreezingArrow?"freezingarrowexp3":"explodingarrowexp2";
            foreach(var(field,value)in new[]{("HitFlags",2),("ResultFlags",5),("HitClass",0),("Range",1),("SrcDamage",0),("pSrvHitFunc",1)})
            {if(NecromancerSummons.Source(child,field)!=value){throw new NotSupportedException("Explosive child packet row conflict: "+field);}}
        }
        if(ability==CombatAbility.ImmolationArrow)
        {
            foreach(var(missile,field,value)in new[]{("immolationarrow","pSrvHitFunc",9),("immolationarrow","AlwaysExplode",1),("immolationarrow","HitFlags",2),("immolationarrow","ResultFlags",5),("immolationfire","pSrvDoFunc",5),("immolationfire","pSrvDmgFunc",3),("immolationfire","DamageRate",41),("immolationfire","dParam1",19),("immolationfire","HitClass",32)})
            {if(NecromancerSummons.Source(missile,field)!=value){throw new NotSupportedException("Immolation row conflict: "+missile+"/"+field);}}
        }
        if(ability==CombatAbility.Strafe)
        {
            foreach(string missile in new[]{"strafearrow","strafebolt"})
            foreach(var(field,value)in new[]{("pSrvHitFunc",0),("NextHit",1),("NextDelay",4),("SrcDamage",128)})
            {if(NecromancerSummons.Source(missile,field)!=value){throw new NotSupportedException("Strafe contact row conflict: "+missile+"/"+field);}}
        }
        if(ability==CombatAbility.LightningBolt)
        {
            foreach(var(field,value)in new[]{("pSrvHitFunc",0),("dParam1",100),("Range",25),("Size",2)})
            {if(NecromancerSummons.Source("lightningjavelin",field)!=value){throw new NotSupportedException("Lightning Bolt contact row conflict: "+field);}}
            if(SkillMechanics.For(ability).Number("SrcDam")!=96){throw new NotSupportedException("Lightning Bolt weapon source changed");}
        }
        if(ability==CombatAbility.LightningFury)
        {
            foreach(var(missile,field,value)in new[]{("lightningfury","pSrvHitFunc",20),("lightningfury","Range",25),("lightningfury","Size",2),("lightningfury","AlwaysExplode",0),("furylightning","SrcDamage",-1),("furylightning","GetHit",1),("furylightning","HitClass",64),("furylightning","ToHit",0),("furylightning","CollideKill",0),("furylightning","Pierce",0),("furylightning","pSrvDmgFunc",0)})
            {if(NecromancerSummons.Source(missile,field)!=value){throw new NotSupportedException("Lightning Fury contact row conflict: "+missile+"/"+field);}}
            if(SkillMechanics.For(ability).Number("SrcDam")!=128){throw new NotSupportedException("Lightning Fury weapon source changed");}
        }
        WeaponStartEffects.Require(owner);
    }
    internal static bool AccuracyPMH(WeaponFlight flight)=>Handles(flight)&&NecromancerSummons.Source(flight.Missile,"ToHit")!=0;
    internal static void Contact(PlayerMobile owner,BaseCreature target,WeaponFlight flight,GearDamage damage,int rate256)
        =>PlayerWeaponMissileContact.Release(owner,target,flight,damage,rate256,NecromancerSummons.Source(flight.Missile,"HitClass"),(flight.Flags&0x10000)!=0?160u:32u);
}
