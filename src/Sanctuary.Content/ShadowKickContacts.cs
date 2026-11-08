// SPDX-License-Identifier: GPL-3.0-or-later
using System.Linq;
using Server.Mobiles;
namespace Sanctuary;

// Shared original kick preflight/accuracy/block/allocation. Skills retain
// their distinct startup,clock,callback,knockback and damage policies.
internal static class ShadowKickContacts
{
    internal static bool Range(AssassinShadowBody body,BaseCreature target,int bonus)=>IronGolemWorld.Melee(body,
        new(body.Map.MapID,DruidRavenMotion.Center(body.X*2,body.Y*2),TimedEffects.Now),target,bonus);
    internal static IronGolemContact.Prepared Allocate(ShadowSkillCasting.Cast cast,int skill,bool knockback,System.DateTime at)
    {
        var body=cast.Body;var target=cast.Target;var owner=(PlayerMobile)body.ControlMaster;var row=ShadowKickPackets.Row(skill);
        OriginalNpcMelee.RequireAllocation(body,target);var rules=IronGolemContact.Require(body,owner,target);
        if((rules.Metadata.Modes&(1<<PreparedMonsterReturn.Knockback))!=0&&SourceKnockbacks.Rule(target)is not{Knockback:true,GetHit:true,Distance:5})
        {throw new System.NotSupportedException("Shadow kick needs its mapped recipient knockback trajectory");}
        var states=ShadowFinisherDamage.States(body,target,at);var v=ShadowSkillCasting.OwnValues(body,body.OwnedSkillRank(skill),skill);
        int flags=NpcMeleeResult.Resolve(body,target,b=>Range(body,target,b),()=>ClassCombat.IsHostile(owner,target),
            n=>NpcEquipmentRandom.Limited(body,n),checked((int)v.Formula("toht")+states.Sum(s=>s.AttackPercent)),guaranteed:states.Length>0&&row.Number("Param8")==1);
        if(knockback&&(flags&1)!=0){flags|=12;}
        return new(rules,ShadowKickPackets.Prepare(body,target,flags,at,skill).Impact);
    }
}
