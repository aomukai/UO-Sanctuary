// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using Server;
using Server.Mobiles;
using Server.Spells;
namespace Sanctuary;

internal static class ShadowDragonTail
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(ShadowDragonTail));
    private static readonly HashSet<string> Gaps=new();
    internal static ItemExecutionMetadata Row()
    {
        var r=ShadowKickPackets.Row(270);
        if(r.InTown||r.UseManaOnDo||r.Lob||r.DecrementQuantity||r.Missile!=-1||r.Number("TargetableOnly")!=0
            ||r.Number("LineOfSight")!=0||r.Number("UseAttackRate")!=1||ShadowSkillBook.Metadata(270).Mode!=4||r.Number("Param4")!=-40
            ||r.Number("periodic")!=0||r.Number("passive")!=0||r.Number("aura")!=0
            ||r.HasDelay("delay")||r.HasDelay("localdelay")||r.HasDelay("globaldelay")||r.HasDelay("perdelay"))
        {throw new NotSupportedException("Unqualified original Shadow Tail action row");}
        return r;
    }
    internal static int Start(ShadowSkillCasting.Cast cast,int rank)
    {
        _=Row();if(cast.Target is not{Alive:true,Deleted:false}||cast.Target.Map!=cast.Body.Map){return 0;}
        var packet=ShadowKickContacts.Allocate(cast,270,false,cast.StartedUtc);
        if(!NpcEquipmentImpact.Fills(packet.Impact.Packet.ResultFlags)){packet.Cancel();return 0;}
        _=DragonKickPackets.TailFire256(packet.Impact.Packet.Physical256,ShadowSkillCasting.OwnValues(cast.Body,rank,270),OriginalNpcEquipment.Graph(cast.Body).Read(329));
        cast.Tail=packet;return 1;
    }
    internal static int Burst(AssassinShadowBody body,Point3D center,Map map,int physical,int rank,DateTime at)
    {
        if(!ShadowSkillCasting.Attached(body)||body.Map!=map||body.ControlMaster is not PlayerMobile owner||SpellHelper.IsTown(center,owner)){return 0;}
        var graph=OriginalNpcEquipment.Graph(body);var values=ShadowSkillCasting.OwnValues(body,rank,270);
        int fire=DragonKickPackets.TailFire256(physical,values,graph.Read(329)),radius=checked((int)values.Eval("aurarangecalc")),pierce=graph.Read(333);
        if(radius is <1 or >64){throw new NotSupportedException("Unqualified own Tail area radius");}
        int hits=0;
        foreach(var target in SkillCombat.NearbySnapshot<BaseCreature>(map,center,(radius+1)/2))
        {
            if(target is not{Alive:true,Deleted:false}||!FistsFireRelease.InRadius(center,target,radius)||!ClassCombat.IsHostile(owner,target)
                ||ClassCombat.Friendly(body,target)||SpellHelper.IsTown(target.Location,target)){continue;}
            // Unqualified recipients are named,not silently assigned invented
            // source identity. Continue the other admitted area recipients.
            try
            {
                var metadata=NpcElementSpellHit.Require(body,owner,target,at,"Shadow Tail");
                if((metadata.Modes&(1<<PreparedMonsterReturn.Knockback))!=0&&SourceKnockbacks.Rule(target)is not{Knockback:true,GetHit:true,Distance:5})
                {throw new NotSupportedException("Shadow Tail recipient knockback trajectory is unqualified");}
            }
            catch(NotSupportedException e)
            {if(Gaps.Add(target.GetType().Name+":"+e.Message)){Log.Warning("Shadow Tail area recipient unsupported: {Target}: {Reason}",target.GetType().Name,e.Message);}continue;}
            _=NpcElementSpellHit.Deliver(body,owner,target,"fire",fire,0,at,"Shadow Tail",pierce:e=>e=="fire"?pierce:0,knockback:true);hits++;
        }
        Effects.SendLocationEffect(center,map,0x36BD,10,10,0x489,0); // Shared identified Tail burst placeholder.
        return hits;
    }
    internal static int Release(ShadowSkillCasting.Cast cast,int rank,DateTime at)
    {
        var packet=cast.Tail;cast.Tail=null;
        if(packet==null||cast.Target is not{Deleted:false} target||target.Map!=cast.Map){packet?.Cancel();return 0;}
        var body=cast.Body;var owner=(PlayerMobile)body.ControlMaster;var center=target.Location;var map=target.Map;
        // Full do50 copies stored mitigated physical before any progressive
        // callback can drain/mutate the parent. Fire follows those contacts.
        int physical=packet.Impact.Packet.Physical256;
        if(target.Alive)
        {
            if(ShadowDragonClaw.Continues(body,target))
            {
                cast.ServerDo=true;var states=ShadowFinisherDamage.States(body,target,at);var effects=ShadowFinisherDamage.Effects(body,states);
                ShadowFinishers.Emit(body,target,packet,states,effects.Fire,effects.Ice,effects.Thunder,at,Row().Number("prgchargesconsumed"));
            }
            _=IronGolemContact.Release(body,owner,target,packet,b=>ShadowKickContacts.Range(body,target,b),at);
        }
        else{packet.Cancel();}
        if(body is{Alive:true,Deleted:false}&&SkillHealth.Balance256(body)>0&&ShadowSkillCasting.Attached(body))
        {
            if(!target.Deleted&&target.Map==map){center=target.Location;}
            _=Burst(body,center,map,physical,rank,at);
        }
        return 1;
    }
}
