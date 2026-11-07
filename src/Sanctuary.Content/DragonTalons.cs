// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Linq;
using Server;
using Server.Mobiles;
using Server.Spells;

namespace Sanctuary;

// Separately approved fixed-target/per-contact finisher policy. Prepared kick,
// typed contact,charges,source knockback and transient action services are shared.
public static class DragonTalons
{
    internal sealed record Series(Serial Weapon,Serial Secondary,Serial Boots,Serial Target,Map Map,Point3D Origin,
        int Rank,int Total,int Released,long EffectAt,long Until,DragonTails.Prepared Kick);
    internal static Func<double> HitRoll=Utility.RandomDouble;
    internal static Func<double> ElementRoll=Utility.RandomDouble;
    internal static Func<int,int> PhysicalRoll=Utility.Random;
    internal static Func<int,int> PoisonRoll=Utility.Random;
    internal static Random Effects=Random.Shared;
    internal static Random KnockbackRoll=Random.Shared;
    public static void Configure()
    {
        void Offline(Mobile mobile){if(mobile is PlayerMobile owner){Cancel(owner);}}
        EventSink.Disconnected+=Offline;EventSink.Logout+=Offline;
    }
    public static void Initialize()=>ClassMapHooks.LocationChanged+=mobile=>
    {if(mobile is PlayerMobile owner&&ClassCombat.Existing(owner)?.Talon is{} s&&(owner.Map!=s.Map||owner.Location!=s.Origin)){Cancel(owner);}};
    private static Serial Boots(PlayerMobile owner)=>owner.Items.FirstOrDefault(item=>ClassGear.Slot(item)==GearSlot.Feet)?.Serial??Serial.Zero;
    private static Serial Primary(PlayerMobile owner)=>(WeaponHands.Pair(owner)?.Primary??CombatTiming.Equipped(owner))?.Serial??Serial.Zero;
    private static bool Hands(PlayerMobile owner,Series series)=>Primary(owner)==series.Weapon
        &&(WeaponHands.Pair(owner)?.Secondary.Serial??Serial.Zero)==series.Secondary&&Boots(owner)==series.Boots;
    internal static void EquipmentChanged(PlayerMobile owner)
    {if(ClassCombat.Existing(owner)?.Talon is{} series&&!Hands(owner,series)){Cancel(owner);}}
    internal static void Cancel(PlayerMobile owner)
    {
        if(ClassCombat.Existing(owner) is not{} session){return;}session.Talon=null;
        if(session.PendingAbility==CombatAbility.DragonTalon){session.PendingAbility=CombatAbility.None;}
    }
    private static bool Eligible(PlayerMobile owner,Series series)=>owner is{Alive:true,Deleted:false,Frozen:false,Paralyzed:false,Spell:null}
        &&ClassCombat.Enabled(owner)&&AssassinCombat.Available(owner,CombatAbility.DragonTalon)&&AlliedOverlap.Allowed(owner)
        &&!HitRecovery.Active(owner,Core.TickCount)&&owner.Map==series.Map&&owner.Location==series.Origin&&Hands(owner,series);
    internal static ActionTiming Timing(PlayerMobile owner)
    {var plan=DragonTalonTiming.Plan(owner);return new(plan.EndFrame,plan.Contacts[0]);}
    internal static void RequireRecipient(PlayerMobile owner,BaseCreature target)
    {
        PlayerWeaponContact.RequireOrdinary(owner,target,true);
        var meta=PlayerWeaponContact.Metadata(target,true);
        if(meta==null){throw new NotSupportedException("Dragon Talon needs an admitted NPC knockback/recovery profile;unclassified native recipient remains unsupported");}
        if((meta.Modes&(1<<PreparedMonsterReturn.Knockback))!=0&&SourceKnockbacks.Rule(target) is not{Knockback:true,GetHit:true,Distance:5})
        {throw new NotSupportedException("Dragon Talon recipient knockback trajectory is not adapted");}
    }
    internal static int KnockbackChance(SkillValues values,bool unique,bool boss,bool player=false)
        =>unique||boss||player?checked((int)values.Eval(player?"calc4":boss?"calc3":"calc2")):100;
    private static bool Knockback(PlayerMobile owner,BaseCreature target,int ordinal,int total)
    {
        if(total==1){return true;}if(ordinal!=total-1){return false;}
        bool unique=target is EncounterCreature{Grade:EncounterGrade.Unique or EncounterGrade.SuperUnique};
        int chance=KnockbackChance(SkillMechanics.Values(owner,CombatAbility.DragonTalon),unique,CreatureProfiles.For(target).Traits.Boss==true);
        return chance>0&&KnockbackRoll.Next(100)<chance;
    }
    public static bool Begin(PlayerMobile owner,Serial selected,Point3D point,long now)
    {
        if(!AssassinCombat.Available(owner,CombatAbility.DragonTalon)||!DragonTails.ValidTarget(owner,selected)
            ||World.FindMobile(selected) is not BaseCreature target){return false;}
        try
        {
            var pair=WeaponHands.Pair(owner);using var primary=pair is{} hands?WeaponHands.Use(owner,hands.Primary):null;
            RequireRecipient(owner,target);_=DragonKickDamage.Capture(owner,SkillMechanics.Values(owner,CombatAbility.DragonTalon).Linear(1,2));
            var values=SkillMechanics.Values(owner,CombatAbility.DragonTalon);
            if(!NecromancerEffects.CanPay(owner,values.Mana256)){return ClassCombat.Perform(owner,CombatAbility.BasicAttack,target,point,now);}
            var plan=DragonTalonTiming.Plan(owner);var timing=new ActionTiming(plan.EndFrame,plan.Contacts[0]);
            if(!SkillCombat.Begin(owner,CombatAbility.DragonTalon,selected,point,now,(_,_,id,_)=>DragonTails.ValidTarget(owner,id),timing:timing)){return false;}
            var session=ClassCombat.For(owner);var kick=DragonTails.PrepareKick(owner,target,session.EffectAt,CombatAbility.DragonTalon,Knockback(owner,target,0,plan.Contacts.Length));
            if(!NecromancerEffects.Pay(owner,values.Mana256)){Cancel(owner);return false;}
            session.Talon=new(Primary(owner),WeaponHands.Pair(owner)?.Secondary.Serial??Serial.Zero,
                Boots(owner),selected,owner.Map,owner.Location,plan.Rank,plan.Contacts.Length,0,session.EffectAt,session.NextAction,kick);
            return true;
        }
        catch(NotSupportedException e){Cancel(owner);owner.SendMessage(0x22,e.Message);return false;}
    }
    internal static bool Resolve(PlayerMobile owner,long now)
    {
        var session=ClassCombat.For(owner);var series=session.Talon;
        if(series==null){return false;}
        if(session.PendingAbility!=CombatAbility.DragonTalon||!Eligible(owner,series)){Cancel(owner);return false;}
        if(series.Released>=series.Total)
        {if(now>=series.Until){Cancel(owner);return true;}return false;}
        if(now<series.EffectAt){return false;}
        if(!DragonTails.ValidTarget(owner,series.Target)||World.FindMobile(series.Target) is not BaseCreature target){Cancel(owner);return false;}
        // Reserve before progressive/item/death callbacks; never resurrect a
        // cancelled series or replay this ordinal after a reentrant callback.
        var current=series with{Released=series.Released+1,Kick=null};session.Talon=current;
        try
        {
            RequireRecipient(owner,target);owner.Direction=owner.GetDirectionTo(target);owner.PlaySound(0x238); // Identified native kick/contact placeholder.
            var pair=WeaponHands.Pair(owner);using var primary=pair is{} hands?WeaponHands.Use(owner,hands.Primary):null;
            var weapon=CombatTiming.Equipped(owner);using var melee=weapon!=null?WeaponHands.UseMelee(owner,weapon):null;
            if((series.Kick.Flags&SourceMeleeResult.Successful)!=0&&PlayerMeleeGeometry.Within(owner,target,0))
            {DragonTails.Progressive(owner,target,target.Location,target.Map,CombatAbility.DragonTalon);}
            PlayerWeaponContact.Release(owner,target,series.Kick.Contact,TimedEffects.Now,true);
            if(!ReferenceEquals(session.Talon,current)){return true;}
            if(!Eligible(owner,current)||!DragonTails.ValidTarget(owner,current.Target)){Cancel(owner);return true;}
            if(current.Released<current.Total)
            {
                // The next packet sees remaining charges and current gear/rank;
                // the series count remains the rank captured at entry.
                int advance=DragonTalonTiming.Advance(owner),step=AmazonFend.Ceiling(4,advance),last=AmazonFend.Ceiling(13,advance)-1;
                long effect=now+step*40L,until=now+(current.Released==current.Total-1?last:step)*40L;
                var kick=DragonTails.PrepareKick(owner,target,effect,CombatAbility.DragonTalon,Knockback(owner,target,current.Released,current.Total));
                if(!ReferenceEquals(session.Talon,current)){return true;}
                session.Talon=current with{Kick=kick,EffectAt=effect,Until=until};session.EffectAt=effect;session.NextAction=until;
                ClassCombat.SendAnimation(owner,session.Animation,(int)Math.Max(40,until-now));owner.Animate(session.Animation,5,1,true,false,0);
            }
        }
        catch(NotSupportedException e){Cancel(owner);owner.SendMessage(0x22,e.Message);return false;}
        ClassCombat.SendState(owner,session);return true;
    }
}
