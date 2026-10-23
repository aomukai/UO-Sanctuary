// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Spells;

namespace Sanctuary;

public static class AmazonJab
{
    internal sealed record Series(Serial Weapon,Serial Target,Map Map,Point3D Origin,long Started,long Until,int Released,bool Basic,int[] Events);
    internal static Func<double> HitRoll=Utility.RandomDouble;
    internal static Func<double> ElementRoll=Utility.RandomDouble;
    internal static Func<int,int> PhysicalRoll=Utility.Random;
    internal static Random Effects=Random.Shared;
    internal static Random WearRoll=Random.Shared;
    internal static string Code(PlayerMobile owner)=>PaladinZeal.AnimationCode(owner);
    internal static bool WeaponAllowed(PlayerMobile owner)=>CombatTiming.Equipped(owner) is{} weapon && ClassGear.State(weapon) is{Identified:true}
        && ClassGear.Base(weapon) is{} basis && ClassItemTypes.Is(basis.Type,"spea") && Code(owner) is "1HT" or "2HT" && NamedAmmunition.CanAttack(weapon);
    internal static int[] Points(string code)=>code=="1HT"?[3,9,15]:code=="2HT"?[3,10,17]:[];
    internal static int Range(PlayerMobile owner)=>CombatTiming.Weapon(CombatTiming.Equipped(owner)).Family==AttackFamily.Throw?1:CombatTiming.Range(owner,CombatAbility.BasicAttack);
    internal static (int End,int[] Events) Schedule(string code,int advance)
    {
        advance=Math.Max(1,advance);int end=code=="2HT"?21:18;
        return((end*256+advance-1)/advance,Array.ConvertAll(Points(code),p=>(p*256+advance-1)/advance));
    }
    internal static ActionTiming Timing(PlayerMobile owner)
    {
        var schedule=Schedule(Code(owner),CombatTiming.WeaponSequenceAdvance(owner,CombatAbility.Jab));
        return schedule.Events.Length==0?new(1,1):new(schedule.End,schedule.Events[0]);
    }
    internal static bool ValidTarget(PlayerMobile owner,Serial selected)
    {
        var target=World.FindMobile(selected);
        return owner.Map is not null && owner.Map!=Map.Internal && WeaponAllowed(owner) && ClassCombat.IsHostile(owner,target)
            && owner.InRange(target,Range(owner)) && owner.InLOS(target) && Math.Abs(owner.Z-target.Z)<16
            && SpellHelper.CheckTown(owner.Location,owner) && SpellHelper.CheckTown(target.Location,owner);
    }
    private static bool Valid(PlayerMobile owner,Series series)=>series!=null && owner is{Alive:true,Deleted:false,Frozen:false,Paralyzed:false,Spell:null}
        && AmazonCombat.Available(owner,CombatAbility.Jab) && AlliedOverlap.Allowed(owner) && owner.Map==series.Map && owner.Location==series.Origin
        && CombatTiming.Equipped(owner)?.Serial==series.Weapon;
    public static bool Locked(PlayerMobile owner,long now)=>ClassCombat.Existing(owner)?.Jab is{Basic:false} series && now<series.Until && Valid(owner,series);
    internal static void Cancel(PlayerMobile owner)
    {
        var session=ClassCombat.Existing(owner);if(session==null){return;}session.Jab=null;
        if(session.PendingAbility==CombatAbility.Jab){session.PendingAbility=CombatAbility.None;}
    }
    public static bool Begin(PlayerMobile owner,Serial selected,Point3D point,long now)
    {
        if(!AmazonCombat.Available(owner,CombatAbility.Jab) || !ValidTarget(owner,selected)){return false;}
        var values=SkillMechanics.Values(owner,CombatAbility.Jab);bool basic=!NecromancerEffects.CanPay(owner,values.Mana256);
        var weapon=CombatTiming.Equipped(owner);var session=ClassCombat.For(owner);
        if(basic)
        {
            // Pinned AttackNoMana: ordinary melee, with no Jab AR/ED or series.
            // Keep the Jab binding and held input for the next affordable cycle.
            if(!owner.Alive || owner.Frozen || owner.Paralyzed || owner.Spell!=null || !AlliedOverlap.Allowed(owner) || HitRecovery.Active(owner,now)
                || session.PendingAbility!=CombatAbility.None || now<session.NextAction){return false;}
            using(WeaponHands.UseMelee(owner,weapon)){session.Timing=CombatTiming.For(owner,CombatAbility.BasicAttack);}
            session.PendingAbility=CombatAbility.Jab;session.PendingTarget=selected;session.PendingPoint=point;session.PendingWeapon=weapon.Serial;
            session.ActionOrigin=owner.Location;session.ActionMap=owner.Map;session.NextAction=now+session.Timing.DurationMs;session.EffectAt=now+session.Timing.EffectMs;
            session.Animation=ClassCombat.AnimationFor(owner,CombatAbility.Jab,weapon);owner.Direction=owner.GetDirectionTo(World.FindMobile(selected));
            owner.RevealingAction();ClassCombat.SendAnimation(owner,session.Animation,session.Timing.DurationMs);owner.Animate(session.Animation,5,1,true,false,0);
        }
        else
        {
            if(!SkillCombat.Begin(owner,CombatAbility.Jab,selected,point,now,(_,_,id,_)=>ValidTarget(owner,id))){return false;}
            if(!NecromancerEffects.Pay(owner,values.Mana256)){session.PendingAbility=CombatAbility.None;return false;}
        }
        var events=basic?new[]{session.Timing.EffectMs}:Array.ConvertAll(Schedule(Code(owner),CombatTiming.WeaponSequenceAdvance(owner,CombatAbility.Jab)).Events,p=>p*40);
        session.Jab=new(weapon.Serial,selected,owner.Map,owner.Location,now,session.NextAction,0,basic,events);
        ClassCombat.SendState(owner,session);return true;
    }
    // Expansion ordinary weapon wear: successful melee hit,4% weapon/10% throwable;
    // a depleted throwable unit consumes one stack member and resets durability.
    internal static void Wear(BaseWeapon weapon,Random random)
    {
        if(weapon==null || weapon.Deleted || NamedGear.Indestructible(weapon) || weapon.HitPoints<=0 || weapon.MaxHitPoints<=0){return;}
        bool stack=NamedAmmunition.Maximum(weapon)>0;
        if(random.Next(100)>=(stack?10:4)){return;}
        if(weapon.HitPoints>1){weapon.HitPoints--;return;}
        if(stack && NamedAmmunition.Remaining(weapon)>1 && NamedAmmunition.Consume(weapon,TimedEffects.Now)){weapon.HitPoints=weapon.MaxHitPoints;}
        else{if(stack){NamedAmmunition.Consume(weapon,TimedEffects.Now);}weapon.HitPoints=0;}
    }
    internal static bool Resolve(PlayerMobile owner,long now)
    {
        var session=ClassCombat.For(owner);var series=session.Jab;if(series==null){return false;}
        if(session.PendingAbility!=CombatAbility.Jab || !Valid(owner,series) || series.Basic && HitRecovery.Active(owner,now)){Cancel(owner);return false;}
        bool processed=false;
        // Independent scheduled do-events: resolve every due event once, retaining
        // the original target. A dead/out-of-range target never redirects to a neighbour.
        while(series.Released<series.Events.Length && now>=series.Started+series.Events[series.Released])
        {
            series=series with{Released=series.Released+1};session.Jab=series;processed=true;
            if(ValidTarget(owner,series.Target) && World.FindMobile(series.Target) is{} target)
            {
                using var mode=WeaponHands.UseMelee(owner,CombatTiming.Equipped(owner));
                owner.DoHarmful(target,true);owner.PlaySound(0x238); // Identified native weapon sound/animation placeholder.
                using var attempt=PaladinThorns.Attempt(owner,target,false);
                var values=SkillMechanics.Values(owner,CombatAbility.Jab);
                if(ClassStats.CheckHit(owner,target,series.Basic?0:(int)values.Formula("toht"),HitRoll)==true)
                {var weapon=CombatTiming.Equipped(owner);GearEffects.Strike(owner,target,ElementRoll(),Effects,wearWeapon:false,skillPhysicalPercent:series.Basic?0:(int)values.Eval("calc1"),physicalRoll:PhysicalRoll);Wear(weapon,WearRoll);}
            }
            if(!Valid(owner,series)){Cancel(owner);return processed;}
        }
        if(series.Released<series.Events.Length){session.EffectAt=series.Started+series.Events[series.Released];}
        else if(now>=series.Until){Cancel(owner);}
        ClassCombat.SendState(owner,session);return processed;
    }
}
