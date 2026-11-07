// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using Server;
using Server.Mobiles;
using Server.Spells;
namespace Sanctuary;

internal static class ShadowBladeShields
{
    internal static ItemExecutionMetadata Row()
    {
        var r=ItemSkillExecution.Skill(277);
        if(r is not{Start:28,Do:54,InTown:true,UseManaOnDo:false,Lob:false,DecrementQuantity:false,Missile:-1}
            ||r.Number("TargetableOnly")!=0||r.Number("TargetCorpse")!=0||r.Number("TargetAlly")!=0||r.Number("LineOfSight")!=0
            ||r.Text("monanim")!="SC"||r.Number("UseAttackRate")!=1||ShadowSkillBook.Metadata(277).Mode!=7||r.Number("periodic")!=1
            ||r.Number("aurafilter")!=33667||r.Number("ResultFlags")!=32||r.Number("HitFlags")!=0||r.Number("HitClass")!=3||r.Number("SrcDam")!=96
            ||r.Text("aurastate")!="bladeshield"||r.Text("perdelay")!="par3"||r.Number("Param3")!=25
            ||ItemSkillExecution.MissileId(r.Text("srvmissilea"))<=0
            ||r.HasDelay("delay")||r.HasDelay("localdelay")||r.HasDelay("globaldelay"))
        {throw new NotSupportedException("Unsupported current original Shadow Blade Shield fields");}
        return r;
    }
    internal static int Start(AssassinShadowBody body,int rank,DateTime at)
    {
        _=Row();var values=ShadowSkillCasting.Values(body,rank,277);
        if(values.Eval("auralencalc")<=0){return 0;}
        _=BladeShields.StartShadow(body,values,at);return 1;
    }
    internal static int Release(AssassinShadowBody body,DateTime at)
        =>BladeShields.State(body)is{} state&&BladeShields.Activate(body,state.Id,at)?1:0;
    internal static int Pulse(AssassinShadowBody body,int rank,DateTime at)
    {
        _=Row();if(!ShadowSkillCasting.Attached(body)||SpellHelper.IsTown(body.Location,body.ControlMaster)){return 0;}
        if(NecromancerSummons.Source(body.SourceMonsterId,"Crit")!=0)
        {throw new NotSupportedException("Blade Shield intrinsic NPC skill critical needs its roll context");}
        var owner=(PlayerMobile)body.ControlMaster;var values=ShadowSkillCasting.Values(body,rank,277);int radius=(int)values.Eval("aurarangecalc");
        var targets=new List<(BaseCreature Target,IronGolemContact.Rules Rules)>();
        foreach(var t in body.Map.GetMobilesInRange<BaseCreature>(body.Location,(int)Math.Ceiling(SkillGeometry.Tiles(radius))))
        {
            if(!MindBlasts.Target(body,owner,t,body.Location,radius)||ClassCombat.Friendly(body,t)){continue;}
            OriginalNpcMelee.RequireAllocation(body,t);var rules=IronGolemContact.Require(body,owner,t);
            if(!SourceBlocking.TryRate(t,out _,out _)){throw new NotSupportedException("Blade Shield target block supplier is unavailable");}
            targets.Add((t,rules));
        }
        targets.Sort((a,b)=>a.Target.Serial.CompareTo(b.Target.Serial));
        int skill=BladeShields.RollSkill(values,n=>NpcEquipmentRandom.Limited(body,n)),hits=0;
        foreach(var (target,rules) in targets)
        {
            if(!ShadowSkillCasting.Attached(body)){break;}
            bool Range(int _)=>AmazonInnerSight.InRadius(target.X-body.X,target.Y-body.Y,radius)&&Math.Abs(target.Z-body.Z)<16;
            int flags=NpcMeleeResult.Resolve(body,target,Range,()=>ClassCombat.IsHostile(owner,target),n=>NpcEquipmentRandom.Limited(body,n))|32;
            if((flags&1)==0){continue;}
            var impact=OriginalNpcMelee.Impact(body,target,flags,new(Physical:skill,HitClass:3,HitFlags:2),96);
            var prepared=new IronGolemContact.Prepared(rules,impact);
            if(IronGolemContact.Release(body,owner,target,prepared,Range,at).Commit!=null){hits++;}
            // The shared DrainItemDurability contact owns ordered events,
            // poison/leech/status/return. Its Kind1 source has no player wear.
        }
        return hits;
    }
}
