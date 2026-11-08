// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;
namespace Sanctuary;

// do45's shared factory uses the ultimate player for pet registration and
// base/passive inheritance,but the casting Shadow supplies its own rank.
// Reuse the actual player-owned sentry backend;never proxy a paid player cast.
internal static class ShadowSentries
{
    internal static bool Handles(int id)=>id is 261 or 262 or 271 or 272 or 276;
    internal static CombatAbility Ability(int id)=>id switch
    {261=>CombatAbility.ChargedBoltSentry,262=>CombatAbility.WakeOfFire,271=>CombatAbility.LightningSentry,
        272=>CombatAbility.WakeOfInferno,276=>CombatAbility.DeathSentry,_=>throw new NotSupportedException("Unqualified intrinsic Shadow sentry")};
    internal static ItemExecutionMetadata Row(int id)
    {
        _=Ability(id);var r=ItemSkillExecution.Skill(id);
        if(r is not{Start:0,Do:45,InTown:false,UseManaOnDo:false,Lob:false,DecrementQuantity:false,Missile:-1}
            ||r.Text("monanim")!="S2"||r.Number("UseAttackRate")!=1||ShadowSkillBook.Metadata(id).Mode!=9
            ||r.Number("TargetableOnly")!=0||r.Number("TargetCorpse")!=0||r.Number("TargetAlly")!=0||r.Number("LineOfSight")!=4
            ||r.Text("pettype")!="assassintrap"||r.Number("petmax")!=5
            ||r.HasDelay("delay")||r.HasDelay("localdelay")||r.HasDelay("globaldelay")||r.HasDelay("perdelay"))
        {throw new NotSupportedException("Unqualified current Shadow do45 sentry metadata");}
        return r;
    }
    internal static bool Destination(AssassinShadowBody body,BaseCreature target,Point3D cached,out Point3D point)
        =>AssassinSentryCasts.Destination(body,target?.Serial??Serial.Zero,cached,out point);
    internal static int Release(AssassinShadowBody body,BaseCreature target,Point3D cached,int skill,int rank,DateTime at)
    {
        _=Row(skill);
        if(!ShadowSkillCasting.Attached(body)||body.OwnedSkills.Used!=skill||rank!=body.OwnedSkillRank(skill)
            ||!Destination(body,target,cached,out var point)){return 0;}
        var owner=(PlayerMobile)body.ControlMaster;
        // Full factory source resolves pOwner before BOTH base and passive
        // helpers. Rank is still nSkillLevel from the NPC action dispatcher.
        var captured=AssassinSentryCreation.Capture(owner,Ability(skill),rank);
        _=AssassinSentryBody.CreateCaptured(owner,point,at,NpcWorldAllocation.UnitSeed(),captured);
        body.PlaySound(0x23B); // Existing identified assassin_summon placeholder.
        return 1;
    }
}
