// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using Server;
using Server.Mobiles;
using Server.Spells;
namespace Sanctuary;

internal static class ShadowMindBlasts
{
    internal static ItemExecutionMetadata Row()
    {
        var r=ItemSkillExecution.Skill(273);
        if(r is not{Start:0,Do:51,InTown:false,UseManaOnDo:false,Lob:false,DecrementQuantity:false,Missile:-1}
            ||r.Number("TargetableOnly")!=0||r.Number("TargetCorpse")!=0||r.Number("TargetAlly")!=0||r.Number("LineOfSight")!=0
            ||r.Text("monanim")!="SC"||r.Number("UseAttackRate")!=1||ShadowSkillBook.Metadata(273).Mode!=7
            ||r.Number("aurafilter")!=33667||r.Number("ResultFlags")!=9||r.Number("HitFlags")!=0||r.Text("EType")!="stun"
            ||r.HasDelay("delay")||r.HasDelay("localdelay")||r.HasDelay("globaldelay")||r.HasDelay("perdelay"))
        {throw new NotSupportedException("Unsupported current Shadow Mind Blast execution fields");}
        return r;
    }
    internal static bool Valid(AssassinShadowBody body,Point3D point)=>ShadowSkillCasting.Attached(body)
        &&point.X>0&&point.Y>0&&point.X<body.Map.Width&&point.Y<body.Map.Height
        &&!SpellHelper.IsTown(body.Location,body.ControlMaster)&&!SpellHelper.IsTown(point,body.ControlMaster);
    internal static int Release(AssassinShadowBody body,BaseCreature selected,Point3D aim,int rank,DateTime at)
    {
        _=Row();PaladinConversion.Prune();
        var point=selected is{Alive:true,Deleted:false}&&selected.Map==body.Map?selected.Location:aim;
        if(!Valid(body,point)){return 0;}
        if(NecromancerSummons.Source(body.SourceMonsterId,"Crit")!=0)
        {throw new NotSupportedException("Mind Blast NPC intrinsic critical needs its roll adapter");}
        var owner=(PlayerMobile)body.ControlMaster;var values=ShadowSkillCasting.Values(body,rank,273);int radius=(int)values.Eval("aurarangecalc");
        var victims=new List<BaseCreature>();
        foreach(var t in body.Map.GetMobilesInRange<BaseCreature>(point,(int)Math.Ceiling(SkillGeometry.Tiles(radius))))
        {if(MindBlasts.Target(body,owner,t,point,radius)&&!ClassCombat.Friendly(body,t)){victims.Add(t);}}
        victims.Sort((a,b)=>a.Serial.CompareTo(b.Serial));
        // Qualify every native recipient before the shared area damage draw.
        foreach(var t in victims)
        {
            _=NpcElementSpellHit.Require(body,owner,t,at,"Shadow Mind Blast");
            if(!CombatStuns.Rules(t,out _,out _)){throw new NotSupportedException("Shadow Mind Blast needs explicit recipient stun metadata");}
        }
        int Roll(int n)=>NpcEquipmentRandom.Limited(body,n);
        var raw=MindBlasts.Damage(values,Roll);
        foreach(var target in victims)
        {
            bool convert=PaladinConversion.MindBlastEligible(body,owner,target)&&Roll(100)<=MindBlasts.Chance(values);
            int frames=convert?values.Param(3)+Roll(values.Param(4)):0;
            var hit=NpcDirectSpellHit.Commit(body,owner,target,raw,at,"Shadow Mind Blast",stunFrames:convert?0:values.DurationFrames,knockback:!convert,limited:Roll);
            _=NpcDirectSpellHit.Finish(hit);
            // Confirmed current success behavior supersedes the historical
            // no-damage branch. Allegiance changes after hostile HP/death.
            if(convert&&target is{Alive:true,Deleted:false}){_=PaladinConversion.ApplyMindBlast(body,target,frames,at);}
            target.FixedEffect(0x374A,10,15,0x481,0); // Existing identified Mind Blast impact placeholder.
        }
        body.PlaySound(0x1E5);Effects.SendLocationEffect(point,body.Map,0x373A,15,10,0x481,0); // Existing cast/center/audio placeholders.
        return 1;
    }
}
