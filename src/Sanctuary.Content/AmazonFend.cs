// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Mobiles;

namespace Sanctuary;

public static class AmazonFend
{
    internal sealed record Series(Serial Weapon,Serial Target,Map Map,Point3D Origin,int Remaining,int Released,bool Basic,
        int StartFrame,int Advance,int EventFrames,long EffectAt,long Until);
    internal static Func<double> HitRoll=Utility.RandomDouble;
    internal static Func<double> ElementRoll=Utility.RandomDouble;
    internal static Func<int,int> PhysicalRoll=Utility.Random;
    internal static Random Effects=Random.Shared;
    internal static Random WearRoll=Random.Shared;
    internal static int Ceiling(int frames,int advance)=>(frames*256+Math.Max(1,advance)-1)/Math.Max(1,advance);
    internal static ActionTiming Timing(string code,int advance,int start=2)
    {
        int action=code=="2HT"?11:9,end=code=="2HT"?18:15;
        // Current-family original calculator: final length is the full final
        // cycle from its rollback frame, NOT an extra post-hit duration.
        return new(Math.Max(1,Ceiling(end-start,advance)-1),Math.Max(1,Ceiling(action-start,advance)));
    }
    internal static ActionTiming Timing(PlayerMobile owner)=>Timing(AmazonJab.Code(owner),CombatTiming.WeaponSequenceAdvance(owner));
    internal static int Rollback(int start,int advance,int elapsed,int factor)=>
        (int)((start*256L+advance*(long)elapsed)/256)*(100-factor)/100;
    internal static (int[] Events,int End) Schedule(string code,int advance,int count,int factor=30)
    {
        count=Math.Clamp(count,1,12);int start=2,at=0,end=0;int[] events=new int[count];
        for(int i=0;i<count;i++)
        {
            var timing=Timing(code,advance,start);end=at+timing.Frames;at+=timing.EffectFrame;events[i]=at;
            start=Rollback(start,advance,timing.EffectFrame,factor);
        }
        return(events,end);
    }
    internal static int Count(int nearby,int limit)=>Math.Min(nearby,limit);
    internal static List<Mobile> Candidates(PlayerMobile owner)
    {
        var result=new List<Mobile>();if(owner.Map==null || owner.Map==Map.Internal || !AmazonJab.WeaponAllowed(owner)){return result;}
        // Source filter0x2A783 includes a separate melee test (0x20000),
        // hostile/valid/town/LOS gates. Reuse the shared native melee policy.
        foreach(var target in owner.Map.GetMobilesInRange<Mobile>(owner.Location,AmazonJab.Range(owner)))
        {if(AmazonJab.ValidTarget(owner,target.Serial)){result.Add(target);}}
        return result;
    }
    internal static Mobile Target(PlayerMobile owner,Serial selected,bool cycle=false)
    {
        if(!cycle && AmazonJab.ValidTarget(owner,selected)){return World.FindMobile(selected);}
        var candidates=Candidates(owner);uint id=PaladinZeal.NextSerial(candidates.Select(x=>x.Serial.Value),selected.Value);
        return candidates.FirstOrDefault(x=>x.Serial.Value==id);
    }
    private static bool Valid(PlayerMobile owner,Series series)=>series!=null && owner is{Alive:true,Deleted:false,Frozen:false,Paralyzed:false,Spell:null}
        && AlliedOverlap.Allowed(owner) && AmazonCombat.Available(owner,CombatAbility.Fend) && owner.Map==series.Map && owner.Location==series.Origin
        && CombatTiming.Equipped(owner)?.Serial==series.Weapon;
    public static bool Locked(PlayerMobile owner,long now)=>ClassCombat.Existing(owner)?.Fend is{Basic:false} series && now<series.Until && Valid(owner,series);
    internal static void Cancel(PlayerMobile owner)
    {
        var session=ClassCombat.Existing(owner);if(session==null){return;}session.Fend=null;
        if(session.PendingAbility==CombatAbility.Fend){session.PendingAbility=CombatAbility.None;}
    }
    public static bool Begin(PlayerMobile owner,Serial selected,Point3D point,long now)
    {
        // Empty-ground client auto-acquisition/air swings remain an explicit
        // acceptance gap; the supported input has an eligible selected target.
        if(!AmazonCombat.Available(owner,CombatAbility.Fend) || !AmazonJab.ValidTarget(owner,selected)){return false;}
        var values=SkillMechanics.Values(owner,CombatAbility.Fend);bool basic=!NecromancerEffects.CanPay(owner,values.Mana256);
        var weapon=CombatTiming.Equipped(owner);int advance=CombatTiming.WeaponSequenceAdvance(owner);ActionTiming timing;
        using(WeaponHands.UseMelee(owner,weapon)){timing=basic?CombatTiming.For(owner,CombatAbility.BasicAttack):Timing(owner);}
        if(!SkillCombat.Begin(owner,CombatAbility.Fend,selected,point,now,(_,_,id,_)=>AmazonJab.ValidTarget(owner,id),requireMana:!basic,timing:timing)){return false;}
        var session=ClassCombat.For(owner);int remaining=basic?1:Count(Candidates(owner).Count,(int)values.Eval("calc1"));
        if(!basic && !NecromancerEffects.Pay(owner,values.Mana256)){session.PendingAbility=CombatAbility.None;return false;}
        session.Fend=new(weapon.Serial,selected,owner.Map,owner.Location,remaining,0,basic,2,advance,timing.EffectFrame,session.EffectAt,session.NextAction);return true;
    }
    internal static bool Resolve(PlayerMobile owner,long now)
    {
        var session=ClassCombat.For(owner);var series=session.Fend;
        if(session.PendingAbility!=CombatAbility.Fend || !Valid(owner,series) || series.Basic && HitRecovery.Active(owner,now)){Cancel(owner);return false;}
        if(series.Remaining<=0)
        {if(now>=series.Until){Cancel(owner);ClassCombat.SendState(owner,session);return true;}return false;}
        if(now<series.EffectAt){return false;}
        // One actual do-event per update. Delayed native ticks do not invent a
        // burst of several victim hits; a rollback schedules the next event.
        var target=series.Basic?(AmazonJab.ValidTarget(owner,series.Target)?World.FindMobile(series.Target):null):Target(owner,series.Target);
        if(target!=null)
        {
            using var mode=WeaponHands.UseMelee(owner,CombatTiming.Equipped(owner));
            owner.Direction=owner.GetDirectionTo(target);owner.DoHarmful(target,true);owner.PlaySound(0x238); // Identified native melee placeholder.
            using var attempted=PaladinThorns.Attempt(owner,target,false);
            var values=SkillMechanics.Values(owner,CombatAbility.Fend);
            if(ClassStats.CheckHit(owner,target,series.Basic?0:(int)values.Formula("toht"),HitRoll)==true)
            {
                var weapon=CombatTiming.Equipped(owner);
                GearEffects.Strike(owner,target,ElementRoll(),Effects,wearWeapon:false,skillPhysicalPercent:series.Basic?0:(int)values.Eval("calc2"),physicalRoll:PhysicalRoll);
                AmazonJab.Wear(weapon,WearRoll);
            }
        }
        series=series with{Remaining=series.Remaining-1,Released=series.Released+1};
        if(!Valid(owner,series)){Cancel(owner);ClassCombat.SendState(owner,session);return true;}
        if(target!=null && !series.Basic && series.Remaining>0 && Target(owner,target.Serial,true) is{} next)
        {
            var values=SkillMechanics.Values(owner,CombatAbility.Fend);
            int start=Rollback(series.StartFrame,series.Advance,series.EventFrames,(int)values.Param(2));
            int advance=CombatTiming.WeaponSequenceAdvance(owner);var timing=Timing(AmazonJab.Code(owner),advance,start);
            series=series with{Target=next.Serial,StartFrame=start,Advance=advance,EventFrames=timing.EffectFrame,EffectAt=now+timing.EffectMs,Until=now+timing.DurationMs};
            session.Timing=timing;session.EffectAt=series.EffectAt;session.NextAction=series.Until;
            ClassCombat.SendAnimation(owner,session.Animation,timing.DurationMs);owner.Animate(session.Animation,5,1,true,false,0);
        }
        else{series=series with{Remaining=0};}
        session.Fend=series;if(now>=series.Until){Cancel(owner);}ClassCombat.SendState(owner,session);return true;
    }
}
