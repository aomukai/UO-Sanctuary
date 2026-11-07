// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using Server;
using Server.Mobiles;
using Server.Spells;
namespace Sanctuary;

internal static class ShadowCloaks
{
    internal static ItemExecutionMetadata Row()
    {
        var r=ItemSkillExecution.Skill(264);
        if(r is not{Start:0,Do:47,InTown:false,UseManaOnDo:false,Lob:false,DecrementQuantity:false,Missile:-1}
            ||r.Number("TargetableOnly")!=0||r.Number("TargetCorpse")!=0||r.Number("TargetAlly")!=0||r.Number("LineOfSight")!=0
            ||r.Text("monanim")!="SC"||r.Number("UseAttackRate")!=1||ShadowSkillBook.Metadata(264).Mode!=7
            ||r.Number("aurafilter")!=57347||r.Text("aurastate")!="cloak_of_shadows"||r.Text("auratargetstate")!="cloaked"
            ||r.Text("passivestat1")!="item_armor_percent"||r.Text("aurastat1")!="skill_armor_percent"
            ||r.HasDelay("delay")||r.HasDelay("localdelay")||r.HasDelay("globaldelay")||r.HasDelay("perdelay"))
        {throw new NotSupportedException("Unsupported current Shadow Cloak execution fields");}
        return r;
    }
    internal static bool Valid(AssassinShadowBody body)=>ShadowSkillCasting.Attached(body)
        &&!SpellHelper.IsTown(body.Location,body.ControlMaster)&&CloakShadows.RecastAllowed(body);
    internal static int Release(AssassinShadowBody body,int rank,DateTime at)
    {
        _=Row();if(!Valid(body)){return 0;}
        var values=ShadowSkillCasting.Values(body,rank,264);
        if(!GeneratedNpcLiveResources.ApplyShadowBuff(body,264,values,at)){return 0;}
        var owner=(PlayerMobile)body.ControlMaster;int radius=(int)values.Eval("aurarangecalc");var targets=new List<BaseCreature>();
        foreach(var target in body.Map.GetMobilesInRange<BaseCreature>(body.Location,(int)Math.Ceiling(SkillGeometry.Tiles(radius))))
        {if(CloakShadows.Target(body,owner,target,radius)&&!ClassCombat.Friendly(body,target)){targets.Add(target);}}
        targets.Sort((a,b)=>a.Serial.CompareTo(b.Serial));foreach(var target in targets){_=CloakShadows.Apply(body,target,values,at);}
        body.FixedEffect(0x373A,10,15,0x455,0);body.PlaySound(0x1E5); // Existing identified Cloak cast/darkness/audio placeholders.
        return 1;
    }
}
