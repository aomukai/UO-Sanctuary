// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;
namespace Sanctuary;

// Prepared do44 factory. Shared source helper ascends the caster chain to
// the root player before base/passive and later first-missile inheritance.
// Own S2 action uses the approved no-extra NPC cooldown policy.
internal static class ShadowBladeSentinels
{
    // Approved explicit NPC cooldown adaptation;player/item policies are separate.
    internal static bool AutomaticReady=>ShadowNpcCooldowns.Approved(257);
    internal static ItemExecutionMetadata Row()
    {
        var r=ItemSkillExecution.Skill(257);
        if(r is not{Start:0,Do:44,InTown:false,UseManaOnDo:false,Lob:false,Missile:-1,DecrementQuantity:false}
            ||r.Text("monanim")!="S2"||r.Number("UseAttackRate")!=1||ShadowSkillBook.Metadata(257).Mode!=9
            ||r.Number("TargetableOnly")!=0||r.Number("TargetCorpse")!=0||r.Number("TargetAlly")!=0||r.Number("LineOfSight")!=0
            ||r.Number("SrcDam")!=96||r.Text("srvmissilea")!="blade creeper"||r.Number("localdelay")!=25
            ||r.HasDelay("delay")||r.HasDelay("globaldelay")||r.HasDelay("perdelay"))
        {throw new NotSupportedException("Unqualified intrinsic Blade Sentinel action row");}
        return r;
    }
    internal static bool Destination(AssassinShadowBody body,BaseCreature target,Point3D cached,out Point3D point)
    {
        point=cached;
        if(target is{Deleted:false}){if(target.Map!=body.Map){return false;}point=target.Location;}
        // A moving,zero-footprint blade does not use the stationary sentries'
        // bounded occupied-tile placement adaptation. Row LOS is zero.
        return LobProjectiles.Valid(body,point);
    }
    internal static int Release(AssassinShadowBody body,BaseCreature target,Point3D cached,int rank,DateTime at)
    {
        _=Row();
        if(!ShadowSkillCasting.Attached(body)||body.OwnedSkills.Used!=257||rank!=body.OwnedSkillRank(257)
            ||!Destination(body,target,cached,out var point)){return 0;}
        _=CreatePrepared(body,point,at);body.PlaySound(0x23B);return 1; // Identified shared summon-audio placeholder.
    }
    internal static BladeSentinelBody CreatePrepared(AssassinShadowBody caster,Point3D destination,DateTime at)
    {
        _=Row();
        if(at.Kind!=DateTimeKind.Utc||!ShadowSkillCasting.Attached(caster)||caster.ControlMaster is not PlayerMobile owner
            ||caster.OwnedSkills.Used!=257||caster.OwnedSkillRank(257)is <1 or >255)
        {throw new NotSupportedException("Shadow Sentinel factory needs its actual intrinsic rank and attached original caster");}
        int rank=caster.OwnedSkillRank(257),rootLevel=ClassStats.Level(owner);
        var capture=BladeSentinelSummons.Capture(owner,rank);
        capture=capture with{Creation=capture.Creation with{Level=OriginalNpcEquipment.Level(caster)},Context=capture.Context with{BaseOwnerLevel=rootLevel,Caster=caster.Serial.Value}};
        // Root player keeps credit and the existing blade pet roster. Caster
        // supplies origin/current level,not root weapon stats or passive inputs.
        return BladeSentinelSummons.CreateCaptured(owner,destination,at,capture,caster.Location,NpcWorldAllocation.UnitSeed());
    }
}
