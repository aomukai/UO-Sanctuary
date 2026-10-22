// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;
using Server.Spells;

namespace Sanctuary;

public static class PaladinCharge
{
    internal enum Phase{Moving,Impact,Recovery}
    internal sealed class Flight
    {
        internal Phase Phase;
        internal Serial Target,Weapon;
        internal Point3D Point,Position;
        internal Map Map;
        internal long NextFrame,Until;
        internal int Velocity256;
        internal long Distance16;
    }
    internal static Func<int,int> Roll=Utility.Random;
    internal static Func<double> HitRoll=Utility.RandomDouble;
    internal static Func<double> ElementRoll=Utility.RandomDouble;
    internal static int Velocity256(int run,int percent,int bonus)
        =>(int)Math.Clamp((long)run*256*(bonus+Math.Max(50,percent))/100,0,int.MaxValue);
    internal static int VelocityPercent(PlayerMobile owner)=>100-EquipmentBalance.RunPenalty(owner)
        +PaladinVigor.VelocityPercent(owner)+FireGround.Speed(owner)-GearEffects.ChillPercent(owner)
        -TimedEffects.Slow(owner,TimedEffects.Now)-NecromancerEffects.Slow(owner);
    internal static bool Moving(PlayerMobile owner)=>ClassCombat.For(owner).Charge is{Phase:Phase.Moving};
    internal static bool Locked(PlayerMobile owner,long now)=>owner is{Alive:true,Deleted:false} && ClassCombat.Enabled(owner)
        && ClassCombat.For(owner).Charge!=null;
    private static bool PointAllowed(PlayerMobile owner,Point3D point)=>owner.Map!=null && owner.Map!=Map.Internal
        && point.X>=0 && point.Y>=0 && point.X<owner.Map.Width && point.Y<owner.Map.Height
        && SpellHelper.CheckTown(point,owner);
    public static bool ValidTarget(PlayerMobile owner,Serial selected,Point3D point)
    {
        if(!PaladinVengeance.WeaponAllowed(owner) || !PointAllowed(owner,owner.Location) || !PointAllowed(owner,point)
            || !owner.InRange(point,(int)SkillGeometry.Tiles(100))){return false;}
        return selected==Serial.Zero || ClassCombat.IsHostile(owner,World.FindMobile(selected));
    }
    public static bool Begin(PlayerMobile owner,Serial selected,Point3D point,long now)
    {
        var session=ClassCombat.For(owner);
        if(!PaladinCombat.Available(owner,CombatAbility.Charge) || !owner.Alive || !AlliedOverlap.Allowed(owner) || owner.Frozen || owner.Paralyzed
            || owner.Spell!=null || HitRecovery.Active(owner,now) || session.PendingAbility!=CombatAbility.None || session.Charge!=null
            || now-session.NextAction<0 || !ValidTarget(owner,selected,point)){return false;}
        var values=SkillMechanics.Values(owner,CombatAbility.Charge);
        if(!NecromancerEffects.CanPay(owner,values.Mana256)){return false;}
        // St31 redirects an already adjacent target to ordinary attack, without Charge ED/AR.
        // Its mana caller still names Charge. Keep the modern table's nine mana, not the
        // reconstruction's anomalous rank-one MinMana-only branch.
        if(selected!=Serial.Zero && PaladinVengeance.ValidTarget(owner,selected))
        {
            if(!ClassCombat.Perform(owner,CombatAbility.BasicAttack,World.FindMobile(selected),point,now)){return false;}
            return NecromancerEffects.Pay(owner,values.Mana256);
        }
        if(!NecromancerEffects.Pay(owner,values.Mana256)){return false;}
        // The pinned Paladin charstats RunVelocity is9. Initial velocity is snapshotted by St31.
        session.Charge=new(){Target=selected,Point=point,Position=owner.Location,Map=owner.Map,
            Weapon=CombatTiming.Equipped(owner)?.Serial??Serial.Zero,NextFrame=now+40,
            Velocity256=Velocity256(9,VelocityPercent(owner),(int)values.Eval("calc2"))};
        session.PendingAbility=CombatAbility.Charge;session.PendingTarget=selected;session.PendingPoint=point;
        session.NextAction=now+40;session.Timing=new(1,1);session.Animation=ClassCombat.AnimationFor(owner,CombatAbility.Charge,CombatTiming.Equipped(owner));
        owner.RevealingAction();owner.PlaySound(0x238);ClassCombat.SendState(owner,session);return true;
    }
    private static Mobile Nearby(PlayerMobile owner,Serial previous)
    {
        var candidates=SkillCombat.Targets(owner,owner.Location,SkillGeometry.Tiles(3));
        uint serial=PaladinZeal.NextSerial(candidates.ConvertAll(x=>x.Serial.Value),previous.Value);
        var target=World.FindMobile((Serial)serial);
        return PaladinVengeance.ValidTarget(owner,target?.Serial??Serial.Zero)?target:null;
    }
    private static void Arrive(PlayerMobile owner,Flight flight,Mobile target,long now)
    {
        if(target==null){End(owner,now+40);return;}
        flight.Target=target.Serial;flight.Phase=Phase.Impact;flight.NextFrame=now+40;
        var session=ClassCombat.For(owner);session.NextAction=now+9*40;session.Timing=new(9,1);
        ClassCombat.SendAnimation(owner,session.Animation,session.Timing.DurationMs);owner.Animate(session.Animation,5,1,true,false,0);
    }
    private static void PathEnded(PlayerMobile owner,Flight flight,long now)=>Arrive(owner,flight,Nearby(owner,flight.Target),now);
    private static void End(PlayerMobile owner,long until)
    {
        var session=ClassCombat.For(owner);session.Charge.Phase=Phase.Recovery;session.Charge.Until=until;session.NextAction=until;
    }
    internal static void Cancel(PlayerMobile owner)
    {
        var session=ClassCombat.For(owner);if(session.Charge==null){return;}session.Charge=null;
        if(session.PendingAbility==CombatAbility.Charge){session.PendingAbility=CombatAbility.None;}
        owner.NetState?.ResetMovementState();
    }
    internal static bool Resolve(PlayerMobile owner,long now)
    {
        var session=ClassCombat.For(owner);var flight=session.Charge;
        if(flight==null){return false;}
        if(!owner.Alive || owner.Deleted || !ClassCombat.Enabled(owner) || !PaladinCombat.Available(owner,CombatAbility.Charge)
            || owner.Frozen || owner.Paralyzed || owner.Spell!=null || owner.Map!=flight.Map || owner.Location!=flight.Position
            || (CombatTiming.Equipped(owner)?.Serial??Serial.Zero)!=flight.Weapon)
        {Cancel(owner);return false;}
        if(flight.Phase==Phase.Recovery){if(now-flight.Until>=0){Cancel(owner);}return false;}
        if(now-flight.NextFrame<0){return false;}
        flight.NextFrame=now+40;session.NextAction=now+40;
        if(flight.Phase==Phase.Impact)
        {
            var target=World.FindMobile(flight.Target);
            if(!ClassCombat.IsHostile(owner,target)){target=Nearby(owner,flight.Target);}
            End(owner,now+8*40); // Do67 rewinds again to point7 before committing its one impact.
            if(!AlliedOverlap.Allowed(owner) || !PaladinVengeance.ValidTarget(owner,target?.Serial??Serial.Zero)){return false;}
            owner.Direction=owner.GetDirectionTo(target);owner.DoHarmful(target,true);
            using var attempt=PaladinThorns.Attempt(owner,target,false);
            var values=SkillMechanics.Values(owner,CombatAbility.Charge);
            if(ClassStats.CheckHit(owner,target,(int)values.Formula("toht"),HitRoll)==true)
            {
                GearEffects.Strike(owner,target,ElementRoll(),skillPhysicalPercent:(int)values.Eval("calc1"),physicalRoll:Roll,skillKnockback:true);
                // Native source-mode knockback is separate from stun and item knockback.
                // Unmapped/special sand-leaper cases remain explicit controller gates.
                if(target is BaseCreature creature){SourceKnockbacks.Begin(owner,creature);}
            }
            return true;
        }
        var aimed=World.FindMobile(flight.Target);
        if(ClassCombat.IsHostile(owner,aimed))
        {
            flight.Point=aimed.Location;
            if(PaladinVengeance.ValidTarget(owner,aimed.Serial)){Arrive(owner,flight,aimed,now);return true;}
        }
        if(owner.X==flight.Point.X && owner.Y==flight.Point.Y){PathEnded(owner,flight,now);return true;}
        // Source path velocity is FP16 per frame; the established two-subtile projection
        // converts it to a swept native tile-distance budget. No missile75% scaling.
        flight.Distance16+=(long)flight.Velocity256*8;
        while(flight.Phase==Phase.Moving)
        {
            var direction=owner.GetDirectionTo(flight.Point);
            bool diagonal=(direction&Direction.Mask) is Direction.Right or Direction.Down or Direction.Left or Direction.Up;
            int cost=diagonal?92682:65536;
            if(flight.Distance16<cost){break;}
            if(!ChargeMotion.TryStep(owner,direction,p=>PointAllowed(owner,p))){PathEnded(owner,flight,now);break;}
            flight.Distance16-=cost;flight.Position=owner.Location;
            aimed=World.FindMobile(flight.Target);
            if(ClassCombat.IsHostile(owner,aimed) && PaladinVengeance.ValidTarget(owner,aimed.Serial)){Arrive(owner,flight,aimed,now);break;}
            if(owner.X==flight.Point.X && owner.Y==flight.Point.Y){PathEnded(owner,flight,now);break;}
        }
        ClassCombat.SendState(owner,session);return true;
    }
}
