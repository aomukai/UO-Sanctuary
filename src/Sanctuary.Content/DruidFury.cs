// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Mobiles;
namespace Sanctuary;

// Current wolf A1,shared start37/do13 and the pinned original-author rollback
// model. Exact current outer dispatch and native event/seed adapters remain
// documented; an interruptible Fury does not inherit Fend's recovery lock.
internal static class DruidFury
{
    internal sealed record Series(Serial Weapon,Serial Target,DruidPlayerForm Form,Map Map,Point3D Origin,
        int Remaining,int Released,int StartFrame,int Advance,int EventFrames,long EffectAt,long Until);
    internal static Func<double> HitRoll=Utility.RandomDouble;
    internal static Func<double> ElementRoll=Utility.RandomDouble;
    internal static Func<int,int> PhysicalRoll=Utility.Random;
    internal static Random Effects=Random.Shared;
    internal static ActionTiming Timing(int advance,int start=0)
    {
        // Derived from the existing normalized wolf model:39..640scaled
        // progress over supported human A1length16..23 gives22..520.
        if(advance is <22 or >520||start is <0 or >2){throw new NotSupportedException("Unmapped Fury clock");}
        return new(AmazonFend.Ceiling(13-start,advance)-1,AmazonFend.Ceiling(7-start,advance));
    }
    internal static ActionTiming Timing(PlayerMobile owner)=>Timing(DruidFormAttacks.Progress(owner));
    internal static (int[] Events,int End) Schedule(int advance,int count)
    {
        if(count is <2 or >5){throw new ArgumentOutOfRangeException(nameof(count));}
        int start=0,at=0,end=0;int[] events=new int[count];
        for(int i=0;i<count;i++)
        {
            var timing=Timing(advance,start);end=at+timing.Frames;at+=timing.EffectFrame;events[i]=at;
            start=AmazonFend.Rollback(start,advance,timing.EffectFrame,70);
        }
        return(events,end);
    }
    private static void Route(SkillValues values)
    {
        var row=SkillMechanics.For(CombatAbility.Fury);
        if(values.Ability!=CombatAbility.Fury||values.Rank is <1 or >255||row.SourceId!=248||row.Number("srvstfunc")!=37||row.Number("srvdofunc")!=13
            ||row.Text("anim")!="A1"||row.Text("seqtrans")!="A1"||row.Number("SrcDam")!=128||row.Number("Param2")!=70
            ||row.Number("interrupt")!=1||row.Number("restrict")!=2||row.Text("State1")!="wolf"||values.Mana256!=1024
            ||values.Eval("calc1")!=Math.Min(values.Rank+1,5)||values.Eval("calc2")!=100+17*(values.Rank-1))
        {throw new NotSupportedException("Unmapped Fury route");}
    }
    internal static bool ValidTarget(PlayerMobile owner,Serial selected)=>DruidFormAttacks.ValidTarget(owner,selected)
        &&World.FindMobile(selected) is BaseCreature target&&SourceBlocking.TryRate(target,out _,out _);
    private static List<Mobile> Candidates(PlayerMobile owner)
    {
        var result=new List<Mobile>();if(owner.Map==null||owner.Map==Map.Internal||!DruidFormAttacks.Supported(owner)){return result;}
        foreach(var target in owner.Map.GetMobilesInRange<Mobile>(owner.Location,DruidFormAttacks.Reach(owner)))
        {if(ValidTarget(owner,target.Serial)){result.Add(target);}}
        return result;
    }
    internal static Mobile Target(PlayerMobile owner,Serial selected,bool cycle=false)
    {
        if(!cycle&&ValidTarget(owner,selected)){return World.FindMobile(selected);}
        var candidates=Candidates(owner);uint id=PaladinZeal.NextSerial(candidates.Select(x=>x.Serial.Value),selected.Value);
        return candidates.FirstOrDefault(x=>x.Serial.Value==id);
    }
    private static bool Valid(PlayerMobile owner,Series series)=>series!=null&&owner is{Alive:true,Deleted:false,Frozen:false,Paralyzed:false,Spell:null}
        &&ClassCombat.Enabled(owner)&&AlliedOverlap.Allowed(owner)&&DruidCombat.Available(owner,CombatAbility.Fury)&&DruidFormAttacks.Supported(owner)
        &&ReferenceEquals(series.Form,DruidPlayerForms.State(owner))&&owner.Map==series.Map&&owner.Location==series.Origin
        &&(CombatTiming.Equipped(owner)?.Serial??Serial.Zero)==series.Weapon;
    internal static bool Begin(PlayerMobile owner,Serial selected,Point3D point,long now)
    {
        if(!DruidCombat.Available(owner,CombatAbility.Fury)||!DruidFormAttacks.Supported(owner)){return false;}
        var target=Target(owner,selected);if(target==null){return false;}
        var values=SkillMechanics.Values(owner,CombatAbility.Fury);Route(values);
        // Approved Sanctuary ordinary-Attack fallback for insufficient melee mana.
        if(!NecromancerEffects.CanPay(owner,values.Mana256)){return ClassCombat.Perform(owner,CombatAbility.BasicAttack,target,target.Location,now);}
        var timing=Timing(owner);
        if(!SkillCombat.Begin(owner,CombatAbility.Fury,target.Serial,target.Location,now,(_,_,id,_)=>ValidTarget(owner,id),timing:timing)){return false;}
        var action=ClassCombat.For(owner);
        // The pinned current4mana overrides the older generic rank1minimum
        // payer quirk. A successful start pays once; later do13events do not.
        if(!NecromancerEffects.Pay(owner,values.Mana256)){action.PendingAbility=CombatAbility.None;return false;}
        action.Fury=new(action.PendingWeapon,target.Serial,DruidPlayerForms.State(owner),owner.Map,owner.Location,
            (int)values.Eval("calc1"),0,0,DruidFormAttacks.Progress(owner),timing.EffectFrame,action.EffectAt,action.NextAction);return true;
    }
    internal static bool Resolve(PlayerMobile owner,long now)
    {
        var action=ClassCombat.For(owner);var series=action.Fury;
        if(action.PendingAbility!=CombatAbility.Fury||!Valid(owner,series)||HitRecovery.Active(owner,now)){Cancel(owner);return false;}
        if(series.Remaining<=0){if(now>=series.Until){Cancel(owner);ClassCombat.SendState(owner,action);return true;}return false;}
        if(now<series.EffectAt){return false;}
        var target=Target(owner,series.Target);
        if(target!=null)
        {
            using var melee=CombatTiming.Equipped(owner) is{} weapon?WeaponHands.UseMelee(owner,weapon):null;
            owner.Direction=owner.GetDirectionTo(target);owner.DoHarmful(target,true);owner.PlaySound(0x238); // Identified melee sound placeholder.
            var values=SkillMechanics.Values(owner,CombatAbility.Fury);Route(values);
            bool hit=ClassStats.CheckHit(owner,target,(int)values.Formula("toht"),HitRoll,projectile:false)==true;
            bool blocked=false,animation=false;
            if(hit&&(!SourceBlocking.TryRoll((BaseCreature)target,true,out blocked,out animation)||blocked)){hit=false;}
            if(blocked){SourceBlocking.Present((BaseCreature)target,animation);}
            if(!ReferenceEquals(action.Fury,series)||action.PendingAbility!=CombatAbility.Fury){return true;}
            if(!Valid(owner,series)){Cancel(owner);return true;}
            if(hit&&ValidTarget(owner,target.Serial))
            {
                using var attempt=PaladinThorns.Attempt(owner,target,false);
                GearEffects.Strike(owner,target,ElementRoll(),Effects,skillPhysicalPercent:(int)values.Eval("calc2"),physicalRoll:PhysicalRoll);
            }
        }
        // Health/item callbacks may deliberately stop,replace or unshift the
        // action. Never resurrect the old series after a reentrant callback.
        if(!ReferenceEquals(action.Fury,series)||action.PendingAbility!=CombatAbility.Fury){return true;}
        if(!Valid(owner,series)){Cancel(owner);ClassCombat.SendState(owner,action);return true;}
        series=series with{Remaining=target==null?0:series.Remaining-1,Released=series.Released+1};
        if(target!=null&&series.Remaining>0&&Target(owner,target.Serial,true) is{} next)
        {
            int start=AmazonFend.Rollback(series.StartFrame,series.Advance,series.EventFrames,70),advance=DruidFormAttacks.Progress(owner);var timing=Timing(advance,start);
            // Reuse the existing native multi-hit policy:one actual attempt per
            // update,then schedule from this dispatch;no overdue damage burst.
            series=series with{Target=next.Serial,StartFrame=start,Advance=advance,EventFrames=timing.EffectFrame,EffectAt=now+timing.EffectMs,Until=now+timing.DurationMs};
            action.PendingTarget=next.Serial;action.Timing=timing;action.EffectAt=series.EffectAt;action.NextAction=series.Until;
            ClassCombat.SendAnimation(owner,action.Animation,timing.DurationMs);owner.Animate(action.Animation,5,1,true,false,0);
        }
        else{series=series with{Remaining=0};}
        action.Fury=series;ClassCombat.SendState(owner,action);return target!=null;
    }
    internal static void Cancel(PlayerMobile owner)
    {var action=ClassCombat.Existing(owner);if(action==null){return;}action.Fury=null;if(action.PendingAbility==CombatAbility.Fury){action.PendingAbility=CombatAbility.None;}}
    internal static void EquipmentChanged(PlayerMobile owner)
    {var action=ClassCombat.Existing(owner);if(action?.Fury is{} series&&(CombatTiming.Equipped(owner)?.Serial??Serial.Zero)!=series.Weapon){Cancel(owner);}}
}
