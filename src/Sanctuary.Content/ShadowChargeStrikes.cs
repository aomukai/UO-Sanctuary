// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Linq;
using Server;
using Server.Mobiles;
namespace Sanctuary;

// Own NPC start23/do34. Reuses the existing source charge lifecycle and full
// original contact; A2 action timing and progressive finishers are separate.
internal static class ShadowChargeStrikes
{
    internal static bool Handles(int id)=>id is 254 or 265 or 280;
    internal static MartialCharge Charge(AssassinShadowBody body,int id)=>AssassinCharges.State(body,id switch
        {254=>CombatAbility.TigerStrike,265=>CombatAbility.CobraStrike,280=>CombatAbility.PhoenixStrike,
            _=>throw new NotSupportedException("Unqualified Shadow progressive state")});
    internal static bool State(AssassinShadowBody body,int state)
    {
        string name=GenericPetMetadata.Table.State(state)?.Name;
        return new[]{254,265,280}.Any(id=>ItemSkillExecution.Skill(id).Text("aurastate")==name&&Charge(body,id)!=null);
    }
    internal static ItemExecutionMetadata Row(int id)
    {
        var r=ItemSkillExecution.Skill(id);
        if(!Handles(id)||r is not{Start:23,Do:34,InTown:false,UseManaOnDo:false,Lob:false,DecrementQuantity:false,Missile:-1}
            ||r.Number("TargetableOnly")!=1||r.Number("TargetCorpse")!=0||r.Number("TargetAlly")!=0||r.Number("LineOfSight")!=0
            ||r.Text("monanim")!="A2"||r.Number("UseAttackRate")!=1||ShadowSkillBook.Metadata(id).Mode!=5||r.Number("SrcDam")!=128
            ||r.Text("aurastat2")!="progressive_tohit"||r.Number("progressive")!=1
            ||r.HasDelay("delay")||r.HasDelay("localdelay")||r.HasDelay("globaldelay")||r.HasDelay("perdelay"))
        {throw new NotSupportedException("Unsupported original Shadow charge-builder fields");}
        return r;
    }
    internal static bool Start(AssassinShadowBody body,BaseCreature target)
    {
        if(target is not{Alive:true,Deleted:false}||target.Map!=body.Map){return false;}
        return Range(body,target,0);
    }
    private static bool Range(AssassinShadowBody body,BaseCreature target,int bonus)
        =>IronGolemWorld.Melee(body,new(body.Map.MapID,DruidRavenMotion.Center(body.X*2,body.Y*2),TimedEffects.Now),target,bonus);
    internal static int Release(AssassinShadowBody body,BaseCreature target,int id,int rank,DateTime at)
    {
        var row=Row(id);
        if(!ShadowSkillCasting.Attached(body)||target is not{Alive:true,Deleted:false}||target.Map!=body.Map
            ||body.ControlMaster is not PlayerMobile owner||!ClassCombat.IsHostile(owner,target)||ClassCombat.Friendly(body,target)){return 0;}
        if(at.Kind!=DateTimeKind.Utc){throw new ArgumentException("Shadow charge contact needs UTC");}
        OriginalNpcMelee.RequireAllocation(body,target);var rules=IronGolemContact.Require(body,owner,target);
        var values=ShadowSkillCasting.OwnValues(body,rank,id);
        int flags=NpcMeleeResult.Resolve(body,target,b=>Range(body,target,b),()=>ClassCombat.IsHostile(owner,target),
            n=>NpcEquipmentRandom.Limited(body,n),checked((int)values.Formula("toht")));
        var impact=NpcEquipmentImpact.Fills(flags)?OriginalNpcMelee.Impact(body,target,flags,sourceDamage:row.Number("SrcDam"))
            :NpcEquipmentImpact.Prepare(body,target,flags);
        // Source AllocCombat precedes progressive gain, and gain precedes
        // DrainItemDurability even if resistance/absorption prevents life loss.
        if((impact.Packet.ResultFlags&1)!=0){AssassinCharges.Gain(body,values,at);}
        _=IronGolemContact.Release(body,owner,target,new(rules,impact),b=>Range(body,target,b),at);
        return 1;
    }
}
