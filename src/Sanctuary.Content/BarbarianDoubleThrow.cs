// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Spells;

namespace Sanctuary;

public static class BarbarianDoubleThrow
{
    internal sealed record Series(Serial Primary,Serial Secondary,Map Map,Point3D Origin,long SecondAt,int Released);
    internal static Func<double> HitRoll=Utility.RandomDouble;
    internal static Func<double> ElementRoll=Utility.RandomDouble;
    internal static Func<int,int> PhysicalRoll=Utility.Random;
    internal static Random Effects=Random.Shared;
    private static bool Throwable(BaseWeapon weapon)=>ClassGear.Base(weapon) is{} basis && ClassItemTypes.Is(basis.Type,"thro")
        && NamedAmmunition.Maximum(weapon)>0;
    internal static WeaponPair? Pair(PlayerMobile owner)=>WeaponHands.Pair(owner) is{} pair && Throwable(pair.Primary) && Throwable(pair.Secondary)?pair:null;
    private static bool Aim(PlayerMobile owner,Serial selected,Point3D point)
    {
        if(selected!=Serial.Zero)
        {if(World.FindMobile(selected) is not{} target || !ClassCombat.IsHostile(owner,target)){return false;}point=target.Location;}
        return owner.Map is not null && owner.Map!=Map.Internal && point.X>=0 && point.Y>=0 && point.X<owner.Map.Width && point.Y<owner.Map.Height
            && Math.Abs(point.Z-owner.Z)<16 && owner.InRange(point,CombatTiming.Range(owner,CombatAbility.DoubleThrow))
            && owner.InLOS(new Point3D(point.X,point.Y,point.Z+14)) && SpellHelper.CheckTown(owner.Location,owner);
    }
    public static bool Begin(PlayerMobile owner,Serial selected,Point3D point,long now)
    {
        if(!BarbarianCombat.Available(owner,CombatAbility.DoubleThrow) || Pair(owner) is not{} pair || !NamedAmmunition.CanAttack(pair.Primary)
            || !SkillCombat.Begin(owner,CombatAbility.DoubleThrow,selected,point,now,(_,_,id,p)=>Aim(owner,id,p))){return false;}
        var session=ClassCombat.For(owner);
        session.DoubleThrow=new(pair.Primary.Serial,pair.Secondary.Serial,owner.Map,owner.Location,
            now+CombatTiming.WeaponSequencePoint(owner,9)*40,0);
        return true;
    }
    internal static void Cancel(PlayerMobile owner)
    {
        var session=ClassCombat.Existing(owner);if(session==null){return;}
        session.DoubleThrow=null;if(session.PendingAbility==CombatAbility.DoubleThrow){session.PendingAbility=CombatAbility.None;}
    }
    internal static bool Resolve(PlayerMobile owner,long now)
    {
        var session=ClassCombat.For(owner);var series=session.DoubleThrow;
        if(series==null){return false;}
        if(session.PendingAbility!=CombatAbility.DoubleThrow || owner is not{Alive:true,Deleted:false,Frozen:false,Paralyzed:false,Spell:null}
            || !BarbarianCombat.Available(owner,CombatAbility.DoubleThrow) || !AlliedOverlap.Allowed(owner) || HitRecovery.Active(owner,now)
            || owner.Map!=series.Map || owner.Location!=series.Origin || Pair(owner) is not{} pair
            || pair.Primary.Serial!=series.Primary || pair.Secondary.Serial!=series.Secondary)
        {Cancel(owner);return false;}
        if(now-session.EffectAt<0){return false;}
        // SQ15 points5/9 dispatch event ordinals0/1; the server selector
        // uses that ordinal parity, yielding primary then secondary.
        var weapon=series.Released==0?pair.Primary:pair.Secondary;
        using(WeaponHands.Use(owner,weapon))
        {
            var values=SkillMechanics.Values(owner,CombatAbility.DoubleThrow);
            var selected=World.FindMobile(session.PendingTarget);
            var aim=selected is{Deleted:false} && selected.Map==owner.Map?selected.Location:session.PendingPoint;
            var release=BarbarianThrowing.Release(owner,TimedEffects.Now,allowUntrained:true);
            if(release is{} thrown)
            {
                int range=CombatTiming.Range(owner,CombatAbility.DoubleThrow);
                var end=NamedProjectiles.Endpoint(owner,aim,range);
                var target=ClassCombat.FindRayTarget(owner,end,range);
                owner.PlaySound(0x238); // Identified native throwing sound/art placeholders.
                owner.MovingEffect(target??(IEntity)new Entity(Serial.Zero,end,owner.Map),0xF42,5,0,false,false);
                int attack=(int)values.Formula("toht"),damage=(int)values.Eval("calc1");
                if(target!=null)
                {
                    var collision=target.Location;owner.DoHarmful(target,true);
                    if(ClassStats.CheckHit(owner,target,attack,HitRoll)==true)
                    {GearEffects.Strike(owner,target,ElementRoll(),Effects,false,skillPhysicalPercent:damage,physicalRoll:PhysicalRoll,projectileCritical:thrown.Critical);}
                    CombatDamage.Finish(target,owner);
                    NamedProjectiles.Continue(owner,target,aim,thrown.Pierces,Effects,thrown.Critical,damage,attack,collision);
                }
            }
            // No srv-start: the legacy wrapper consumes after each successful
            // do74, even if quantity/allocation failed, and ignores debit failure.
            // Exact modern low-mana outcome remains a separate reference gap.
            NecromancerEffects.Pay(owner,values.Mana256);
        }
        if(series.Released==0)
        {session.DoubleThrow=series with{Released=1};session.EffectAt=series.SecondAt;}
        else{Cancel(owner);}
        ClassCombat.SendState(owner,session);return true;
    }
}
