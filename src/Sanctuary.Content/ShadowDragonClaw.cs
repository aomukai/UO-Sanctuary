// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;
namespace Sanctuary;

// Own NPC start25/do46. Timing/hands stay with the monster sequence; damage
// and progressive release reuse the original full contact services.
internal static class ShadowDragonClaw
{
    internal static ItemExecutionMetadata Row()
    {
        var r=ItemSkillExecution.Skill(260);
        if(r is not{Start:25,Do:46,InTown:false,UseManaOnDo:false,Lob:false,DecrementQuantity:false,Missile:-1}
            ||r.Number("TargetableOnly")!=1||r.Number("TargetCorpse")!=0||r.Number("TargetAlly")!=0||r.Number("LineOfSight")!=0
            ||r.Text("monanim")!="xx"||r.Number("UseAttackRate")!=1||ShadowSkillBook.Metadata(260).Mode!=14||r.Number("SrcDam")!=128
            ||r.Number("weapsel")!=3||r.Text("EType")!="0"||r.Number("Param8")!=1||r.Number("prgchargesconsumed")!=1
            ||r.HasDelay("delay")||r.HasDelay("localdelay")||r.HasDelay("globaldelay")||r.HasDelay("perdelay"))
        {throw new NotSupportedException("Unsupported original Shadow Dragon Claw route");}
        return r;
    }
    internal static bool Continues(AssassinShadowBody body,BaseCreature target)
        =>ShadowSkillCasting.Attached(body)&&target is{Alive:true,Deleted:false}&&body.Map==target.Map
            &&body.ControlMaster is PlayerMobile owner&&ClassCombat.IsHostile(owner,target)&&!ClassCombat.Friendly(body,target)&&Range(body,target,0);
    private static bool Range(AssassinShadowBody body,BaseCreature target,int bonus)
        =>IronGolemWorld.Melee(body,new(body.Map.MapID,DruidRavenMotion.Center(body.X*2,body.Y*2),TimedEffects.Now),target,bonus);
    internal static void Progressive(AssassinShadowBody body)
    {
        if(ShadowActions.State(body)is not{Sequence:not null,Cast.Skill:260} action)
        {throw new NotSupportedException("Dragon progressive flag needs its actual NPC action context");}
        action.Cast.ServerDo=true; // Full sub6FCF77E0 sets this before callbacks/drain.
    }
    internal static int Release(AssassinShadowBody body,BaseCreature target,DateTime at)
    {
        _=Row();if(!Continues(body,target)){return 0;}
        if(at.Kind!=DateTimeKind.Utc){throw new ArgumentException("Shadow Dragon Claw needs UTC");}
        OriginalNpcMelee.RequireAllocation(body,target);var owner=(PlayerMobile)body.ControlMaster;var rules=IronGolemContact.Require(body,owner,target);
        var packet=ShadowFinishers.Prepare(body,owner,target,b=>Range(body,target,b),at,rules,260);
        _=IronGolemContact.Release(body,owner,target,packet,b=>Range(body,target,b),at);return 1;
    }
}
