// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Linq;
using Server;
using Server.Mobiles;

namespace Sanctuary;

public static class DragonFlights
{
    internal sealed record Flight(Serial Weapon,Serial Secondary,Serial Boots,Serial Target,Map Map,Point3D Position,
        long TeleportAt,long KickAt,long Until,bool Landed=false,bool Kicked=false);
    internal static Func<double> HitRoll=Utility.RandomDouble;
    internal static Func<double> ElementRoll=Utility.RandomDouble;
    internal static Func<int,int> PhysicalRoll=Utility.Random;
    internal static Func<int,int> PoisonRoll=Utility.Random;
    internal static Random Effects=Random.Shared;
    private static Serial Primary(PlayerMobile owner)=>(WeaponHands.Pair(owner)?.Primary??CombatTiming.Equipped(owner))?.Serial??Serial.Zero;
    private static Serial Boots(PlayerMobile owner)=>owner.Items.FirstOrDefault(x=>ClassGear.Slot(x)==GearSlot.Feet)?.Serial??Serial.Zero;
    private static bool Hands(PlayerMobile owner,Flight flight)=>Primary(owner)==flight.Weapon
        &&(WeaponHands.Pair(owner)?.Secondary.Serial??Serial.Zero)==flight.Secondary&&Boots(owner)==flight.Boots;
    internal static void Cancel(PlayerMobile owner)
    {
        if(ClassCombat.Existing(owner) is not{} s){return;}s.Flight=null;
        if(s.PendingAbility==CombatAbility.DragonFlight){s.PendingAbility=CombatAbility.None;}
    }
    internal static void EquipmentChanged(PlayerMobile owner)
    {if(ClassCombat.Existing(owner)?.Flight is{} flight&&!Hands(owner,flight)){Cancel(owner);}}
    public static void Configure()
    {
        void Offline(Mobile m){if(m is PlayerMobile p){Cancel(p);}}
        EventSink.Disconnected+=Offline;EventSink.Logout+=Offline;
    }
    public static void Initialize()=>ClassMapHooks.LocationChanged+=m=>
    {
        if(m is PlayerMobile p&&ClassCombat.Existing(p)?.Flight is{} flight&&(p.Map!=flight.Map||p.Location!=flight.Position)){Cancel(p);}
    };
    private static bool Valid(PlayerMobile owner,Flight flight)=>owner is{Alive:true,Deleted:false,Frozen:false,Paralyzed:false,Spell:null}
        &&SkillHealth.Balance256(owner)>0&&AssassinCombat.Available(owner,CombatAbility.DragonFlight)&&AlliedOverlap.Allowed(owner)
        &&DruidPlayerForms.State(owner)==null&&!HitRecovery.Active(owner,Core.TickCount)
        &&owner.Map==flight.Map&&owner.Location==flight.Position&&Hands(owner,flight);
    private static void Require(PlayerMobile owner,BaseCreature target)
    {
        var pair=WeaponHands.Pair(owner);using var hand=pair is{} hands?WeaponHands.Use(owner,hands.Primary):null;
        var weapon=CombatTiming.Equipped(owner);using var melee=weapon!=null?WeaponHands.UseMelee(owner,weapon):null;
        PlayerWeaponContact.RequireOrdinary(owner,target,true);
        _=DragonKickDamage.Capture(owner,SkillMechanics.Values(owner,CombatAbility.DragonFlight).Linear(1,2));
    }
    public static bool Begin(PlayerMobile owner,Serial selected,Point3D point,long now)
    {
        if(!AssassinCombat.Available(owner,CombatAbility.DragonFlight)||DruidPlayerForms.State(owner)!=null
            ||World.FindMobile(selected) is not BaseCreature target||!DragonFlightLanding.StartTarget(owner,target)){return false;}
        try
        {
            var pair=WeaponHands.Pair(owner);using var hand=pair is{} hands?WeaponHands.Use(owner,hands.Primary):null;
            var values=SkillMechanics.Values(owner,CombatAbility.DragonFlight);
            if(!NecromancerEffects.CanPay(owner,values.Mana256)){return ClassCombat.Perform(owner,CombatAbility.BasicAttack,target,point,now);}
            Require(owner,target);var clock=DragonFlightPreparation.Clock(PaladinZeal.AnimationCode(owner));
            if(!SkillCombat.Begin(owner,CombatAbility.DragonFlight,selected,point,now,(_,_,id,_)=>World.FindMobile(id) is BaseCreature t&&DragonFlightLanding.StartTarget(owner,t),timing:new(clock.End,clock.Teleport))){return false;}
            var s=ClassCombat.For(owner);s.Flight=new(Primary(owner),WeaponHands.Pair(owner)?.Secondary.Serial??Serial.Zero,Boots(owner),selected,owner.Map,owner.Location,
                now+clock.Teleport*40L,now+clock.Kick*40L,now+clock.End*40L);return true;
        }
        catch(NotSupportedException e){Cancel(owner);owner.SendMessage(0x22,e.Message);return false;}
    }
    internal static bool Resolve(PlayerMobile owner,long now)
    {
        var s=ClassCombat.For(owner);var flight=s.Flight;bool processed=false;if(flight==null){return false;}
        if(s.PendingAbility!=CombatAbility.DragonFlight||!Valid(owner,flight)){Cancel(owner);return false;}
        if(!flight.Landed)
        {
            if(now<flight.TeleportAt){return false;}
            if(World.FindMobile(flight.Target) is not BaseCreature target){Cancel(owner);return false;}
            Flight claimed=null;
            bool moved=DragonFlightLanding.Transfer(owner,target,out var destination,point=>
            {
                if(!ReferenceEquals(s.Flight,flight)||!Valid(owner,flight)||!NecromancerEffects.Pay(owner,SkillMechanics.Values(owner,CombatAbility.DragonFlight).Mana256)){return false;}
                // Claim before moving;the exact own destination is valid for the
                // location hook. External movement or arrival callbacks cancel.
                claimed=flight with{Landed=true,Position=point};s.Flight=claimed;return true;
            });
            if(!moved){Cancel(owner);return false;}
            if(!ReferenceEquals(s.Flight,claimed)){return true;}
            if(!Valid(owner,claimed)){Cancel(owner);return true;}
            s.ActionOrigin=destination;s.EffectAt=claimed.KickAt;
            flight=claimed;processed=true; // Preserve ordered catch-up of the two distinct events.
        }
        if(flight.Kicked)
        {if(now>=flight.Until){Cancel(owner);return true;}return processed;}
        if(now<flight.KickAt){if(processed){ClassCombat.SendState(owner,s);}return processed;}
        var current=flight with{Kicked=true};s.Flight=current;
        if(World.FindMobile(flight.Target) is not BaseCreature recipient||!DragonTails.ValidTarget(owner,flight.Target)){Cancel(owner);return false;}
        try
        {
            Require(owner,recipient);var pair=WeaponHands.Pair(owner);using var hand=pair is{} hands?WeaponHands.Use(owner,hands.Primary):null;
            var weapon=CombatTiming.Equipped(owner);using var melee=weapon!=null?WeaponHands.UseMelee(owner,weapon):null;
            owner.Direction=owner.GetDirectionTo(recipient);owner.PlaySound(0x238); // Identified kick/contact placeholder.
            var kick=DragonTails.PrepareKick(owner,recipient,flight.KickAt,CombatAbility.DragonFlight);
            if((kick.Flags&SourceMeleeResult.Successful)!=0&&PlayerMeleeGeometry.Within(owner,recipient,0))
            {DragonTails.Progressive(owner,recipient,recipient.Location,recipient.Map,CombatAbility.DragonFlight);}
            if(!recipient.Deleted){PlayerWeaponContact.Release(owner,recipient,kick.Contact,TimedEffects.Now,true);}
            if(ReferenceEquals(s.Flight,current)&&(!Valid(owner,current)||now>=current.Until)){Cancel(owner);}
            ClassCombat.SendState(owner,s);return true;
        }
        catch(NotSupportedException e){Cancel(owner);owner.SendMessage(0x22,e.Message);return false;}
    }
}
