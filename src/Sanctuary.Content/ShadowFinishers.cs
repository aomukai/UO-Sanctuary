// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Linq;
using Server.Mobiles;
namespace Sanctuary;

// Ordinary own NPC Attack: Tiger before the full weapon fill, Cobra after it,
// then mitigation/allocation and progressive consumption before contact.
internal static class ShadowFinishers
{
    internal static bool Builder(int id)=>id is 254 or 265 or 280 or 259 or 269 or 274;
    internal static IronGolemContact.Prepared Prepare(AssassinShadowBody body,PlayerMobile owner,BaseCreature target,
        Func<int,bool> range,DateTime at,IronGolemContact.Rules rules,int skill=0)
    {
        if(skill is not(0 or 260)){throw new NotSupportedException("Unqualified own Shadow finisher skill");}
        var policy=skill==0?null:ShadowDragonClaw.Row();
        var skillValues=skill==0?null:ShadowSkillCasting.OwnValues(body,body.OwnedSkillRank(skill),skill);
        var states=ShadowFinisherDamage.States(body,target);
        int flags=NpcMeleeResult.Resolve(body,target,range,()=>ClassCombat.IsHostile(owner,target),
            n=>NpcEquipmentRandom.Limited(body,n),checked(states.Sum(s=>s.AttackPercent)+(skillValues==null?0:(int)skillValues.Formula("toht"))),
            guaranteed:skill==260&&states.Length>0&&policy.Number("Param8")==1);
        if(!NpcEquipmentImpact.Fills(flags)){return new(rules,NpcEquipmentImpact.Prepare(body,target,flags));}
        int tiger=AssassinCharges.TigerPercent(states.FirstOrDefault(s=>s.Ability==CombatAbility.TigerStrike),body.OwnedSkillRank(254));
        FistsFirePacket fire=null;
        ThunderPacket thunder=null;
        IcePacket ice=null;
        var impact=OriginalNpcMelee.Impact(body,target,flags,new(AdditionalPercent:checked(tiger+(skillValues==null?0:(int)skillValues.Eval("calc1"))),HitFlags:2),sourceDamage:policy?.Number("SrcDam")??128,afterFill:fill=>
            ShadowFinisherDamage.Inline(body,states,fill,out fire,out ice,out thunder));
        var prepared=new IronGolemContact.Prepared(rules,impact);
        // The source progressive-release routine checks tighter melee range
        // than GetResultFlags. Modern Attack's consume-one policy is shared
        // with players; later contact cancellation does not refund allocation.
        if(range(0))
        {
            if(skill==260){ShadowDragonClaw.Progressive(body);}
            var center=target.Location;var map=target.Map;
            var phoenix=states.FirstOrDefault(s=>s.Ability==CombatAbility.PhoenixStrike);
            if(phoenix!=null){_=MartialPhoenix.AfterShadow(body,target,center,phoenix,at);}
            if(fire is{Count:>=2}||ice is{Count:>=2})
            {
                // First callback38 drains the parent; later drain attempts
                // find its packet already freed. Continue remaining state
                // callbacks once, in source order, after that one delivery.
                prepared.AfterContact=()=>
                {
                    if(fire is{Count:>=2}){FistsFireRelease.AfterShadow(body,center,map,fire,at);}
                    if(ice is{Count:>=2}){MartialCold.AfterShadow(body,target,center,map,ice,at);}
                    if(thunder is{Count:>=2}){_=MartialLightning.AfterShadow(body,center,thunder,at);}
                };
            }
            else if(thunder is{Count:>=2}){_=MartialLightning.AfterShadow(body,center,thunder,at);}
            foreach(var state in states){AssassinCharges.Consume(body,state,policy?.Number("prgchargesconsumed")??CommonMeleeSkills.Attack.Number("prgchargesconsumed"));}
        }
        return prepared;
    }
}
