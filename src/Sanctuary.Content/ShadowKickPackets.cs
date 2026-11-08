// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Linq;
using Server;
using Server.Mobiles;
namespace Sanctuary;

internal sealed record ShadowKickPacket(KickDamageInput? Input,NpcFillPacket Filled,NpcWeaponImpact Impact);

// Own NPC Talon allocation only. Hit/block selection, cached packet release,
// progressive emissions/consumption and action timing remain caller stages.
internal static class ShadowKickPackets
{
    internal static ItemExecutionMetadata Row(int skill=255)
    {
        if(skill is not(255 or 270)){throw new NotSupportedException("Unqualified original Shadow kick skill");}
        var r=ItemSkillExecution.Skill(skill);
        if(r.Start!=(skill==255?24:27)||r.Do!=(skill==255?42:50)||r.Number("Kick")!=1||r.Number("weapsel")!=4||r.Text("monanim")!="A1"
            ||r.Text("EType")!=(skill==255?"0":"fire")||r.Number("SrcDam")!=0||r.Number("Param8")!=1||r.Number("prgchargesconsumed")!=1
            ||skill==270&&(r.Number("EMin")!=0||r.Number("EMax")!=0||Enumerable.Range(1,5).Any(i=>r.Number("EMinLev"+i)!=0||r.Number("EMaxLev"+i)!=0)))
        {throw new NotSupportedException("Unqualified original Shadow kick packet row");}
        return r;
    }
    internal static ShadowKickPacket Prepare(AssassinShadowBody body,BaseCreature target,int flags,DateTime? at=null,int skill=255)
    {
        var row=Row(skill);int rank=body.OwnedSkillRank(skill);
        if(rank is<1 or>255||(flags&~0xffff)!=0||!ShadowSkillCasting.Attached(body)||target is not{Alive:true,Deleted:false}
            ||body.Map!=target.Map||body.ControlMaster is not PlayerMobile owner||!ClassCombat.IsHostile(owner,target)||ClassCombat.Friendly(body,target))
        {throw new NotSupportedException("Own NPC kick needs its living original world context and skill rank");}
        OriginalNpcMelee.RequireAllocation(body,target);
        if(!NpcEquipmentImpact.Fills(flags))
        {
            var miss=NpcEquipmentImpact.Prepare(body,target,flags);
            int hitClass=skill==255?1:0;
            return new(null,new(ResultFlags:flags,HitClass:hitClass),miss with{Packet=miss.Packet with{HitClass=hitClass}});
        }
        var states=ShadowFinisherDamage.States(body,target,at);
        int enhanced=checked((skill==255?row.Number("Param1")+(rank-1)*row.Number("Param2"):0)+
            AssassinCharges.TigerPercent(states.FirstOrDefault(s=>s.Ability==CombatAbility.TigerStrike),body.OwnedSkillRank(254)));
        var input=ShadowKickDamage.Capture(body,enhanced);
        int physical=ShadowKickDamage.Roll256(body,input);
        var binding=ShadowLoadout.Binding(body);var frame=OriginalNpcEquipment.Graph(body).Weapons(binding.Primary,binding.Secondary,true,true,4,0,0);
        NpcFillPacket filled;
        try
        {
            frame.SuppressDamage();
            // a5=1 supplies the source reset/restore hand context. This kick
            // is a melee packet, not an inferred projectile attack.
            filled=OriginalNpcMelee.Allocate(body,OriginalNpcMelee.TargetFacts(target),
                new(Physical:physical,AdditionalPercent:enhanced,HitFlags:3,ResultFlags:flags),128,frame,true);
            filled=ShadowFinisherDamage.Inline(body,states,filled,out _,out _,out _);
        }
        finally{frame.Restore();}
        // The caller writes class1 after the full kick builder, overriding
        // any earlier intrinsic NPC critical hit-class adjustment.
        if(skill==255){filled=filled with{HitClass=1};}
        return new(input,filled,OriginalNpcMelee.FromFill(body,target,filled));
    }
}
