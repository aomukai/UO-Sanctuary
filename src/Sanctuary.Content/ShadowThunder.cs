// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;
using Server.Spells;
namespace Sanctuary;

public static partial class MartialLightning
{
    internal static ThunderPacket CaptureShadow(AssassinShadowBody body,MartialCharge charge)
    {
        if(charge==null){return null;}
        if(charge.Owner!=body.Serial.Value||charge.Ability!=CombatAbility.ClawsOfThunder||!ShadowSkillCasting.Attached(body)
            ||NecromancerSummons.Source(body.SourceMonsterId,"Crit")!=0)
        {throw new NotSupportedException("Unqualified own Shadow Thunder source");}
        var graph=OriginalNpcEquipment.Graph(body);int rank=Math.Max(charge.InitialRank,body.OwnedSkillRank(269));
        return Capture(ShadowSkillCasting.OwnValues(body,rank,269),charge.Count,graph.Read(330),graph.Read(334),n=>NpcEquipmentRandom.Limited(body,n));
    }
    internal static long AfterShadow(AssassinShadowBody body,Point3D center,ThunderPacket packet,DateTime at)
    {
        if(packet is not{Count:>=2}||!ShadowSkillCasting.Attached(body)||body.ControlMaster is not PlayerMobile owner||SpellHelper.IsTown(center,owner)){return 0;}
        if(at.Kind!=DateTimeKind.Utc){throw new ArgumentException("Shadow Thunder release needs UTC");}
        return Emit(owner,center,body.Map,packet,at,()=>unchecked((int)NpcEquipmentRandom.Raw(body)),body);
    }
    private static (int Loss,ulong Seed) NpcImpact(PlayerMobile owner,BaseCreature target,ThunderFlight s)
    {
        var body=MartialPhoenix.NpcActor(owner,s.Npc,s.Map,true);if(body==null){return(0,s.Seed);}
        _=NpcElementSpellHit.Require(body,owner,target,s.NextUtc,"Shadow Thunder");
        if(ClassAvoidance.Missile(target)){return(0,s.Seed);}
        ulong seed=s.Seed;int raw=WeaponMissileDamage.RollValue(s.Min256,s.Max256,0,n=>(int)SourceRandom.Limited(ref seed,n));
        var result=NpcElementSpellHit.Deliver(body,owner,target,"ltng",raw,0,s.NextUtc,"Shadow Thunder",hitClass:64,pierce:e=>e=="ltng"?s.Pierce:0);
        return(Math.Max(0,result.Before256-result.AfterHealth256),seed);
    }
}
