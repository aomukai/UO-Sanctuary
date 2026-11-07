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
        string Binding,uint[] Items,int Frame,DateTime NextUtc);
    private static readonly Dictionary<uint,Pending> Actions=new();
    internal static Pending State(AssassinShadowBody body)=>body==null?null:Actions.GetValueOrDefault(body.Serial.Value);
    public static void Initialize()=>Timer.StartTimer(TimeSpan.FromMilliseconds(40),TimeSpan.FromMilliseconds(40),()=>Tick(TimedEffects.Now));
    private static bool CanAct(AssassinShadowBody body)=>body is{Alive:true,Deleted:false,Frozen:false,Paralyzed:false,Spell:null}
        &&ShadowSkillCasting.Attached(body)&&SkillHealth.Balance256(body)>0&&CreatureHitRecovery.Remaining(body)==0
        &&CombatStuns.Remaining(body)==0&&AlliedOverlap.Allowed(body);
    internal static bool BeginFireBlast(AssassinShadowBody body,BaseCreature target,Point3D aim,DateTime at)
    {
        if(at.Kind!=DateTimeKind.Utc||at>DateTime.MaxValue.AddSeconds(-30)){throw new ArgumentException("Shadow action needs its UTC entry clock");}
        if(!CanAct(body)||State(body)!=null){return false;}
        var cast=ShadowSkillCasting.Start(body,target,aim);if(cast==null){return false;}
        try
        {
            var plan=ShadowAnimation.FireBlast(body);
            if(plan.Events.Count(e=>e.Type==SourceAnimationEvents.ModeChange&&e.Kind==2)!=1
                ||plan.Events.Count(e=>e.Type==SourceAnimationEvents.EndAnimation)!=1)
            {throw new NotSupportedException("Shadow S2 needs one actual release and end event");}
            Actions.Add(body.Serial.Value,new(cast,plan,body.Map,body.Location,ShadowLoadout.Binding(body).Identity,
                body.EquipmentNumbers.ActiveItems,0,at.AddMilliseconds(40)));return true;
        }
        catch{ShadowSkillCasting.Complete(cast);throw;}
    }
    internal static void Cancel(AssassinShadowBody body)
    {if(body!=null&&Actions.Remove(body.Serial.Value,out var action)){ShadowSkillCasting.Complete(action.Cast);}}
    internal static void Tick(DateTime at,bool? actions=null)
    {
        if(at.Kind!=DateTimeKind.Utc){throw new ArgumentException("Shadow action clock needs UTC");}
        foreach(uint actor in Actions.Keys.ToArray())
        {
            while(Actions.TryGetValue(actor,out var action)&&action.NextUtc<=at)
            {
                var body=action.Cast.Body;var owner=body.ControlMaster as PlayerMobile;
                if(!CanAct(body)||!(actions??owner?.NetState is{Running:true})||body.Map!=action.Map||body.Location!=action.Position
                    ||body.OwnedSkills.Used!=251||ShadowLoadout.Binding(body).Identity!=action.Binding
                    ||!body.EquipmentNumbers.ActiveItems.SequenceEqual(action.Items))
                {Cancel(body);break;}
                int frame=action.Frame+1;bool end=false;
                foreach(var e in action.Plan.Events.Where(e=>e.Frame==frame))
                {
                    if(e.Type==SourceAnimationEvents.ModeChange&&e.Kind==2){_=ShadowSkillCasting.Release(action.Cast,action.NextUtc);}
                    else if(e.Type==SourceAnimationEvents.EndAnimation){end=true;}
                    else{Cancel(body);throw new NotSupportedException("Unqualified Shadow action event");}
                }
                if(end){Cancel(body);break;}
                Actions[actor]=action with{Frame=frame,NextUtc=action.NextUtc.AddMilliseconds(40)};
            }
        }
    }
}
