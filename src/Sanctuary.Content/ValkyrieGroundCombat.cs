// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Linq;
using Server.Mobiles;
namespace Sanctuary;

// Ordinary monster A1 allocates at its action event, not during windup.
// Supplied source rate bases remain explicit until live startup is qualified.
internal static class ValkyrieGroundCombat
{
    private static int Require(AmazonValkyrieBody body)
    {
        if(body?.Prepared?.GroundMotion?.AttackRate is not{} rate
            ||body.Prepared.EquipmentContactProfile!=ValkyrieEquipmentContact.OriginalProfile)
        {throw new NotSupportedException("Original equipped ground actor needs its full item-event/contact binding;unarmed or legacy Iron fallback is forbidden");}
        _=ValkyrieGroundMotion.Mode(body);_=ValkyrieEquipmentContact.Registry(body);return rate;
    }
    internal static (int Speed,int Action,int End) Attack(AmazonValkyrieBody body)
    {
        int rate=checked(Require(body)+SourceAttackRate.WolfRate(body)-100);
        var plan=ValkyrieEquipmentAnimation.Plan(body,4,rate,0);
        var actions=plan.Events.Where(e=>e.Type==1&&e.Kind==1).ToArray();
        var ends=plan.Events.Where(e=>e.Type==2).ToArray();
        if(actions.Length!=1||ends.Length!=1||actions[0].Frame<1||ends[0].Frame<=actions[0].Frame)
        {throw new NotSupportedException("Original Valkyrie A1 needs one action marker followed by end animation");}
        return(plan.Speed,actions[0].Frame,ends[0].Frame);
    }
    internal static bool Release(AmazonValkyrieBody body,PlayerMobile owner,IronGolemWorldState state,BaseCreature target,DateTime at)
    {
        _=Require(body);
        if(state is not{Mode:4}||state.ActionAt!=state.Frame||state.Target!=target?.Serial.Value)
        {throw new NotSupportedException("Original Valkyrie contact requires its owned current A1 action event");}
        var contact=ValkyrieEquipmentContact.Prepare(body,owner,target,bonus=>IronGolemWorld.Melee(body,state,target,bonus),at);
        var result=IronGolemContact.Release(body,owner,target,contact,bonus=>IronGolemWorld.Melee(body,state,target,bonus),at);
        return(result.Packet.ResultFlags&SourceMeleeResult.Successful)!=0;
    }
}
