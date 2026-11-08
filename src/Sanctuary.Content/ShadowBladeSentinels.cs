// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;
namespace Sanctuary;

// Prepared do44 factory. Shared source helper ascends the caster chain to
// the root player before base/passive and later first-missile inheritance.
// Own S2 cast/localdelay admission remains separate.
internal static class ShadowBladeSentinels
{
    internal static BladeSentinelBody CreatePrepared(AssassinShadowBody caster,Point3D destination,DateTime at)
    {
        var r=ItemSkillExecution.Skill(257);
        if(r is not{Start:0,Do:44,InTown:false,UseManaOnDo:false,Lob:false,Missile:-1,DecrementQuantity:false}
            ||r.Text("monanim")!="S2"||r.Number("SrcDam")!=96||r.Text("srvmissilea")!="blade creeper")
        {throw new NotSupportedException("Unqualified intrinsic Blade Sentinel factory row");}
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
