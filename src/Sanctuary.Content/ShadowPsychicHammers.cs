// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;
using Server.Spells;
namespace Sanctuary;

internal static class ShadowPsychicHammers
{
    internal static ItemExecutionMetadata Row()
    {
        var r=ItemSkillExecution.Skill(253);
        if(r is not{Start:22,Do:33,InTown:false,UseManaOnDo:false,Lob:false,DecrementQuantity:false,Missile:-1}
            ||r.Number("TargetableOnly")!=1||r.Number("TargetCorpse")!=0||r.Number("TargetAlly")!=0||r.Number("LineOfSight")!=4
            ||r.Text("monanim")!="SC"||r.Number("UseAttackRate")!=1||ShadowSkillBook.Metadata(253).Mode!=7
            ||r.HasDelay("delay")||r.HasDelay("localdelay")||r.HasDelay("globaldelay")||r.HasDelay("perdelay"))
        {throw new NotSupportedException("Unsupported current Shadow Psychic Hammer execution fields");}
        return r;
    }
    internal static bool Valid(AssassinShadowBody body,BaseCreature target)=>ShadowSkillCasting.Attached(body)
        &&target is{Alive:true,Deleted:false}&&target.Map==body.Map&&target.CanBeDamaged()
        &&body.ControlMaster is PlayerMobile owner&&ClassCombat.IsHostile(owner,target)&&!ClassCombat.Friendly(body,target)
        &&!SpellHelper.IsTown(body.Location,owner)&&!SpellHelper.IsTown(target.Location,target);
    internal static int Release(AssassinShadowBody body,BaseCreature target,int rank,DateTime at)
    {
        _=Row();if(!Valid(body,target)){return 0;}
        if(NecromancerSummons.Source(body.SourceMonsterId,"Crit")!=0)
        {throw new NotSupportedException("Psychic Hammer NPC intrinsic critical needs its roll adapter");}
        var owner=(PlayerMobile)body.ControlMaster;_=NpcElementSpellHit.Require(body,owner,target,at,"Shadow Psychic Hammer");
        var graph=OriginalNpcEquipment.Graph(body);var values=ShadowSkillCasting.Values(body,rank,253);
        int chance=PsychicHammers.Chance(owner,target,values);
        var raw=PsychicHammers.Damage(values,graph.Read(357),n=>NpcEquipmentRandom.Limited(body,n));
        var pending=NpcDirectSpellHit.Commit(body,owner,target,raw,at,"Shadow Psychic Hammer",e=>e=="mag"?graph.Read(358):0);
        // Source do33 draws after damage,including lethal damage;the typed
        // return phase owns actual knockback/death with the real NPC emitter.
        bool knock=chance>0&&NpcEquipmentRandom.Limited(body,100)<chance;
        NpcDirectSpellHit.Finish(pending,knock?8:0);
        body.PlaySound(0x1E5);target.FixedEffect(0x374A,10,15,0x481,0);target.PlaySound(0x1F1); // Existing identified Hammer presentation placeholders.
        return 1;
    }
}
