// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Mobiles;
namespace Sanctuary;

// Own transient source-frame conductor. Pending windups are not serialized
// or replayed; committed bombs retain their existing independent lifetime.
internal static class ShadowActions
{
    internal sealed record Pending(ShadowSkillCasting.Cast Cast,ShadowAnimationPlan Plan,Map Map,Point3D Position,
        string Binding,uint[] Items,int Frame,DateTime NextUtc)
    {internal ShadowClawSequenceState Sequence{get;init;}}
    private static readonly Dictionary<uint,Pending> Actions=new();
    internal static Pending State(AssassinShadowBody body)=>body==null?null:Actions.GetValueOrDefault(body.Serial.Value);
    public static void Initialize()=>Timer.StartTimer(TimeSpan.FromMilliseconds(40),TimeSpan.FromMilliseconds(40),()=>Tick(TimedEffects.Now));
    private static bool CanAct(AssassinShadowBody body)=>body is{Alive:true,Deleted:false,Frozen:false,Paralyzed:false,Spell:null}
        &&ShadowSkillCasting.Attached(body)&&SkillHealth.Balance256(body)>0&&CreatureHitRecovery.Remaining(body)==0
        &&CombatStuns.Remaining(body)==0&&AlliedOverlap.Allowed(body);
    internal static bool BeginFireBlast(AssassinShadowBody body,BaseCreature target,Point3D aim,DateTime at)
        =>BeginSkill(body,target,aim,at,251);
    internal static bool BeginSentry(AssassinShadowBody body,BaseCreature target,Point3D aim,DateTime at,int skill)
        =>ShadowSentries.Handles(skill)?BeginSkill(body,target,aim,at,skill):throw new NotSupportedException("Unqualified Shadow sentry");
    internal static bool BeginBuff(AssassinShadowBody body,BaseCreature target,DateTime at,int skill)
        =>ShadowDefensiveBuffs.Handles(skill)?BeginSkill(body,target,body.Location,at,skill)
            :throw new NotSupportedException("Unqualified Shadow self buff: "+skill);
    internal static bool BeginPsychicHammer(AssassinShadowBody body,BaseCreature target,DateTime at)
        =>BeginSkill(body,target,target?.Location??body.Location,at,253);
    internal static bool BeginMindBlast(AssassinShadowBody body,BaseCreature target,Point3D aim,DateTime at)
        =>BeginSkill(body,target,aim,at,273);
    internal static bool BeginBladeShield(AssassinShadowBody body,DateTime at)=>BeginSkill(body,null,body.Location,at,277);
    internal static bool BeginCharge(AssassinShadowBody body,BaseCreature target,DateTime at,int skill)
        =>ShadowChargeStrikes.Handles(skill)?BeginSkill(body,target,target?.Location??body.Location,at,skill)
            :throw new NotSupportedException("Unqualified Shadow charge action");
    private static bool BeginSkill(AssassinShadowBody body,BaseCreature target,Point3D aim,DateTime at,int skill)
    {
        if(at.Kind!=DateTimeKind.Utc||at>DateTime.MaxValue.AddSeconds(-30)){throw new ArgumentException("Shadow action needs its UTC entry clock");}
        if(!CanAct(body)||State(body)!=null){return false;}
        var cast=ShadowSkillCasting.Start(body,target,aim,skill,at);if(cast==null){return false;}
        try
        {
            bool s2=skill==251||ShadowSentries.Handles(skill);
            bool claws=ShadowChargeStrikes.Claws(skill);
            var plan=s2?ShadowAnimation.S2(body):claws?ShadowAnimation.Claws(body):ShadowChargeStrikes.Handles(skill)?ShadowAnimation.Charge(body)
                :skill is 253 or 273 or 277?ShadowAnimation.Cast(body):ShadowAnimation.Buff(body);
            int marker=s2?2:1;
            int releases=plan.Events.Count(e=>e.Type==SourceAnimationEvents.ModeChange&&e.Kind==marker);
            if((claws?releases is <1 or >2:releases!=1)
                ||plan.Events.Count(e=>e.Type==SourceAnimationEvents.EndAnimation)!=1)
            {throw new NotSupportedException("Shadow S2 needs one actual release and end event");}
            Actions.Add(body.Serial.Value,new(cast,plan,body.Map,body.Location,ShadowLoadout.Binding(body).Identity,
                body.EquipmentNumbers.ActiveItems,0,at.AddMilliseconds(40)){Sequence=claws?ShadowClawSequence.Initial():null});return true;
        }
        catch{ShadowSkillCasting.Complete(cast);throw;}
    }
    private static bool MeleeRange(AssassinShadowBody body,BaseCreature target,int bonus)
    {
        var point=DruidRavenMotion.Center(body.X*2,body.Y*2);
        return IronGolemWorld.Melee(body,new(body.Map.MapID,point,TimedEffects.Now),target,bonus);
    }
    internal static bool BeginMelee(AssassinShadowBody body,BaseCreature target,DateTime at)
    {
        if(at.Kind!=DateTimeKind.Utc||at>DateTime.MaxValue.AddSeconds(-30)){throw new ArgumentException("Shadow action needs UTC");}
        if(!CanAct(body)||State(body)!=null||target is not{Alive:true,Deleted:false}||target.Map!=body.Map
            ||body.ControlMaster is not PlayerMobile owner||!ClassCombat.IsHostile(owner,target)||ClassCombat.Friendly(body,target)
            ||!MeleeRange(body,target,0)||body.OwnedSkillRank(0)<=0){return false;}
        OriginalNpcMelee.RequireAllocation(body,target);body.ChangeSkills(book=>book.Use(0));
        var cast=new ShadowSkillCasting.Cast(body,target,target.Location,0);
        try
        {
            var plan=ShadowAnimation.Melee(body);
            if(plan.Events.Count(e=>e.Type==1&&e.Kind==1)!=1||plan.Events.Count(e=>e.Type==2)!=1)
            {throw new NotSupportedException("Shadow A1 needs one melee action and end event");}
            Actions.Add(body.Serial.Value,new(cast,plan,body.Map,body.Location,ShadowLoadout.Binding(body).Identity,
                body.EquipmentNumbers.ActiveItems,0,at.AddMilliseconds(40)));return true;
        }
        catch{ShadowSkillCasting.Complete(cast);throw;}
    }
    internal static void Cancel(AssassinShadowBody body)
    {if(body!=null&&Actions.Remove(body.Serial.Value,out var action)){ShadowSkillCasting.Complete(action.Cast);}}
    internal static void Tick(DateTime at,bool? actions=null,uint? onlyActor=null)
    {
        if(at.Kind!=DateTimeKind.Utc){throw new ArgumentException("Shadow action clock needs UTC");}
        foreach(uint actor in Actions.Keys.ToArray())
        {
            if(onlyActor.HasValue&&actor!=onlyActor.Value){continue;}
            while(Actions.TryGetValue(actor,out var action)&&action.NextUtc<=at)
            {
                var body=action.Cast.Body;var owner=body.ControlMaster as PlayerMobile;
                if(!CanAct(body)||!(actions??owner?.NetState is{Running:true})||body.Map!=action.Map||body.Location!=action.Position
                    ||body.OwnedSkills.Used!=action.Cast.Skill||ShadowLoadout.Binding(body).Identity!=action.Binding
                    ||!body.EquipmentNumbers.ActiveItems.SequenceEqual(action.Items))
                {Cancel(body);break;}
                int frame=action.Frame+1;bool end=false;
                if(action.Sequence is{} sequence)
                {
                    if(ShadowClawSequence.Complete(sequence)){Cancel(body);break;}
                    if(ShadowClawSequence.Releases(sequence))
                    {
                        _=ShadowSkillCasting.ReleaseSequence(action.Cast,sequence.Clock.Point,action.NextUtc);
                        if(!Actions.TryGetValue(actor,out var current)||!ReferenceEquals(current,action)||action.Cast.Released){break;}
                        if(action.Cast.ServerDo!=(((sequence.Clock.Point>>8)&1)!=0))
                        {Cancel(body);throw new NotSupportedException("Shadow do35 lost its raw NPC sequence context");}
                    }
                    Actions[actor]=action with{Frame=frame,NextUtc=action.NextUtc.AddMilliseconds(40),
                        Sequence=ShadowClawSequence.Advance(sequence,action.Plan.Speed)};
                    continue;
                }
                foreach(var e in action.Plan.Events.Where(e=>e.Frame==frame))
                {
                    if(e.Type==SourceAnimationEvents.ModeChange&&(e.Kind==2&&(action.Cast.Skill==251||ShadowSentries.Handles(action.Cast.Skill))||e.Kind==1&&(action.Cast.Skill is 253 or 273 or 277||ShadowDefensiveBuffs.Handles(action.Cast.Skill)||ShadowChargeStrikes.Handles(action.Cast.Skill))))
                    {_=ShadowSkillCasting.Release(action.Cast,action.NextUtc);}
                    else if(e.Type==SourceAnimationEvents.ModeChange&&e.Kind==1&&action.Cast.Skill==0)
                    {
                        action.Cast.Released=true;
                        if(action.Cast.Target is{Alive:true,Deleted:false} target&&target.Map==body.Map&&ClassCombat.IsHostile(owner,target)&&!ClassCombat.Friendly(body,target))
                        {
                            var contact=OriginalNpcMelee.Prepare(body,owner,target,b=>MeleeRange(body,target,b),action.NextUtc);
                            _=IronGolemContact.Release(body,owner,target,contact,b=>MeleeRange(body,target,b),action.NextUtc);
                        }
                    }
                    else if(e.Type==SourceAnimationEvents.EndAnimation){end=true;}
                    else{Cancel(body);throw new NotSupportedException("Unqualified Shadow action event");}
                }
                if(end){Cancel(body);break;}
                Actions[actor]=action with{Frame=frame,NextUtc=action.NextUtc.AddMilliseconds(40)};
            }
        }
    }
}
