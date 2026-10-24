// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Spells;

namespace Sanctuary;

public static class AmazonStrafe
{
    internal sealed record Series(Serial Weapon,Serial Target,Map Map,Point3D Origin,long Cast,int Remaining,int Released,
        int StartFrame,int Advance,int EventFrames,long EffectAt,long Until,bool Finished=false);
    internal static ActionTiming Timing(AttackFamily family,int advance,int start=0)=>AmazonBow.Timing(family,advance,start);
    internal static (int[] Events,int End) Schedule(AttackFamily family,int advance,int count)
    {
        int start=0,at=0,end=0;int[] events=new int[Math.Clamp(count,1,10)];
        for(int i=0;i<events.Length;i++)
        {var timing=Timing(family,advance,start);end=at+timing.Frames;at+=timing.EffectFrame;events[i]=at;start=AmazonFend.Rollback(start,advance,timing.EffectFrame,50);}
        return(events,end);
    }
    internal static int Count(SkillValues values,int nearby,bool ground)
    {int cap=(int)values.Eval("calc1"),minimum=Math.Min((int)values.Eval("calc3"),cap);return Math.Min(cap,Math.Max(minimum,ground?0:nearby));}
    internal static List<Mobile> Candidates(PlayerMobile owner)
    {
        var result=new List<Mobile>();if(owner.Map==null || owner.Map==Map.Internal){return result;}
        double radius=SkillGeometry.Tiles(SkillMechanics.Values(owner,CombatAbility.Strafe).Eval("aurarangecalc"));
        foreach(var target in owner.Map.GetMobilesInRange<Mobile>(owner.Location,(int)Math.Ceiling(radius)))
        {
            double dx=target.X-owner.X,dy=target.Y-owner.Y;
            if(dx*dx+dy*dy<=radius*radius && Math.Abs(target.Z-owner.Z)<16 && ClassCombat.IsHostile(owner,target)
                && owner.InLOS(target) && !SpellHelper.IsTown(target.Location,owner)){result.Add(target);}
        }
        return result;
    }
    internal static Mobile Target(PlayerMobile owner,Serial selected,bool cycle=false)
    {
        // do12 uses an existing cached server identity without another aura
        // eligibility query. A removed entity invokes the cyclic selector.
        if(!cycle && World.FindMobile(selected) is{Deleted:false} cached && cached.Map==owner.Map){return cached;}
        var candidates=Candidates(owner);uint id=PaladinZeal.NextSerial(candidates.Select(x=>x.Serial.Value),selected.Value);
        return candidates.FirstOrDefault(x=>x.Serial.Value==id);
    }
    private static bool Valid(PlayerMobile owner,Series series)=>series!=null && owner is{Alive:true,Deleted:false,Frozen:false,Paralyzed:false,Spell:null}
        && AlliedOverlap.Allowed(owner) && AmazonCombat.Available(owner,CombatAbility.Strafe) && owner.Map==series.Map && owner.Location==series.Origin
        && CombatTiming.Equipped(owner)?.Serial==series.Weapon;
    public static bool Locked(PlayerMobile owner,long now)=>ClassCombat.Existing(owner)?.Strafe is{} series && now<series.Until && Valid(owner,series);
    internal static void Cancel(PlayerMobile owner)
    {var session=ClassCombat.Existing(owner);if(session==null){return;}session.Strafe=null;if(session.PendingAbility==CombatAbility.Strafe){session.PendingAbility=CombatAbility.None;}}
    public static bool Begin(PlayerMobile owner,Serial selected,Point3D point,long now)
    {
        if(!AmazonCombat.Available(owner,CombatAbility.Strafe) || !BowAmmunition.CanRelease(owner,CombatTiming.Equipped(owner))){return false;}
        bool ground=selected==Serial.Zero;
        if(!ground && !ClassCombat.IsHostile(owner,World.FindMobile(selected))){return false;}
        var values=SkillMechanics.Values(owner,CombatAbility.Strafe);var candidates=Candidates(owner);
        var target=ground?Target(owner,(Serial)uint.MaxValue,true):World.FindMobile(selected);
        var timing=Timing(CombatTiming.Weapon(CombatTiming.Equipped(owner)).Family,CombatTiming.WeaponSequenceAdvance(owner));
        if(!SkillCombat.Begin(owner,CombatAbility.Strafe,selected,point,now,(p,_,_,_)=>p.Map!=null && p.Map!=Map.Internal && !SpellHelper.IsTown(p.Location,p),timing:timing)){return false;}
        var session=ClassCombat.For(owner);
        // Complete start8/generic wrapper: quantity precedes one start debit.
        // Later do-events have neither a quantity gate nor another mana debit.
        if(!BowAmmunition.Consume(owner,CombatTiming.Equipped(owner)) || !NecromancerEffects.Pay(owner,values.Mana256))
        {session.PendingAbility=CombatAbility.None;return false;}
        session.PendingLeft=session.HeldAbility==CombatAbility.Strafe && session.HeldAttack;
        session.Strafe=new(CombatTiming.Equipped(owner).Serial,target?.Serial??(Serial)uint.MaxValue,owner.Map,owner.Location,WeaponMissiles.NewCast(),
            Count(values,candidates.Count,ground),0,0,CombatTiming.WeaponSequenceAdvance(owner),timing.EffectFrame,session.EffectAt,session.NextAction);
        return true;
    }
    internal static bool Resolve(PlayerMobile owner,long now)
    {
        var session=ClassCombat.For(owner);var series=session.Strafe;
        if(session.PendingAbility!=CombatAbility.Strafe || !Valid(owner,series)){Cancel(owner);return false;}
        if(series.Finished || series.Remaining<=0)
        {if(now>=series.Until){Cancel(owner);ClassCombat.SendState(owner,session);return true;}return false;}
        if(now<series.EffectAt){return false;}
        var target=Target(owner,series.Target);
        if(target==null)
        {session.Strafe=series with{Finished=true};if(now>=series.Until){Cancel(owner);}ClassCombat.SendState(owner,session);return true;}
        owner.Direction=owner.GetDirectionTo(target);
        var values=SkillMechanics.Values(owner,CombatAbility.Strafe);string missile=BowAmmunition.AmmoType(owner,CombatTiming.Equipped(owner))==typeof(Arrow)?"strafearrow":"strafebolt";
        var damage=BowLaunch.Capture(owner,CombatAbility.Strafe,NamedCombat.ProjectileCritical(owner,AmazonBow.LaunchRandom),preparedValues:values);
        int dx=(target.X-owner.X)*2,dy=(target.Y-owner.Y)*2;
        WeaponMissiles.Launch(owner,CombatAbility.Strafe,new FanRay(dx,dy,0x21,NecromancerSummons.Source(missile,"Activate")),damage,series.Cast,
            NamedProjectiles.PierceBudget(WeaponMissiles.CurrentPierce(owner),AmazonBow.LaunchRandom),NecromancerSummons.Source(missile,"Vel"),NecromancerSummons.Source(missile,"Range"),
            NecromancerSummons.Source(missile,"NextDelay"),missile,values.Rank);
        owner.PlaySound(0x238); // Identified ordinary bow sound placeholder.
        series=series with{Remaining=series.Remaining-1,Released=series.Released+1};
        if(!Valid(owner,series)){Cancel(owner);return true;}
        if(series.Remaining>0 && Target(owner,target.Serial,true) is{} next)
        {
            int start=AmazonFend.Rollback(series.StartFrame,series.Advance,series.EventFrames,(int)values.Param(6));
            int advance=CombatTiming.WeaponSequenceAdvance(owner);var timing=Timing(CombatTiming.Weapon(CombatTiming.Equipped(owner)).Family,advance,start);
            series=series with{Target=next.Serial,StartFrame=start,Advance=advance,EventFrames=timing.EffectFrame,EffectAt=now+timing.EffectMs,Until=now+timing.DurationMs};
            session.Timing=timing;session.EffectAt=series.EffectAt;session.NextAction=series.Until;
            ClassCombat.SendAnimation(owner,session.Animation,timing.DurationMs);owner.Animate(session.Animation,5,1,true,false,0);
        }
        else{series=series with{Finished=true};}
        session.Strafe=series;if(now>=series.Until){Cancel(owner);}ClassCombat.SendState(owner,session);return true;
    }
}
