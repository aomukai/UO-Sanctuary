// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Items;
using Server.Mobiles;

namespace Sanctuary;

// Shared two-contact claw sequence. Builders gain their own charge only;
// Dragon Claw supplies its separately approved finisher packets and dual rate.
public static class AssassinClaws
{
    internal sealed record Series(CombatAbility Ability,Serial Primary,Serial Secondary,Serial Target,Map Map,Point3D Origin,
        long Started,long Until,int Released,bool Basic,int[] Events);
    internal static bool Handles(CombatAbility ability)=>ability is CombatAbility.DragonClaw or CombatAbility.FistsOfFire or CombatAbility.ClawsOfThunder or CombatAbility.BladesOfIce;
    internal static (int End,int[] Events) Schedule(bool dual,int advance)
    {
        if(advance<=0){throw new NotSupportedException("Claw sequence requires a positive source frame advance");}
        int Frame(int point)=>(point*256+advance-1)/advance;
        return(Frame(dual?16:12),dual?new[]{Frame(6),Frame(10)}:new[]{Frame(6)});
    }
    internal static int Advance(int primaryWsm,int ias,int slow,int skillRate)
        =>CombatTiming.FrameAdvance(new(AttackFamily.Thrust,primaryWsm),ias,slow,skillRate,-30,256);
    internal static int Advance(PlayerMobile owner)
    {
        var pair=WeaponHands.Pair(owner);var primary=pair?.Primary??CombatTiming.Equipped(owner);
        // Current-family original Fists/Thunder calculators use primary WSM/IAS and
        // sequence base256. Old dual mean/A1 base208 is a separate control.
        // At sequence entry ordinal0, ensure a
        // caller's temporary damage-hand scope cannot select secondary IAS.
        using var hand=pair is{} weapons?WeaponHands.Use(owner,weapons.Primary):null;
        return Advance(CombatTiming.Weapon(primary).Modifier,
            CombatTiming.Ias(owner),GearEffects.AttackSlowTotal(owner),CombatTiming.SkillAttackRate(owner));
    }
    internal static ActionTiming Timing(PlayerMobile owner,CombatAbility ability=CombatAbility.FistsOfFire)
    {if(ability==CombatAbility.DragonClaw){var plan=DragonClawPreparation.Plan(owner);return new(plan.EndFrame,plan.Contacts[0]);}var s=Schedule(WeaponHands.Pair(owner)!=null,Advance(owner));return new(s.End,s.Events[0]);}
    internal static bool WeaponAllowed(PlayerMobile owner)=>WeaponHands.Claw(CombatTiming.Equipped(owner))
        && ClassGear.Active(owner,CombatTiming.Equipped(owner)) && (WeaponHands.Pair(owner) is not{} pair || ClassGear.Active(owner,pair.Secondary));
    internal static bool ValidTarget(PlayerMobile owner,Serial selected,CombatAbility ability=CombatAbility.FistsOfFire)
        =>ability==CombatAbility.DragonClaw?DragonClawPreparation.WeaponAllowed(owner)&&DragonTails.ValidTarget(owner,selected):WeaponAllowed(owner)&&TigerStrikes.ValidTarget(owner,selected);
    private static bool Hands(PlayerMobile owner,Series s)
    {
        var pair=WeaponHands.Pair(owner);
        return WeaponAllowed(owner) && ((pair?.Primary??CombatTiming.Equipped(owner))?.Serial??Serial.Zero)==s.Primary
            && (pair?.Secondary.Serial??Serial.Zero)==s.Secondary;
    }
    private static bool Valid(PlayerMobile owner,Series s)=>s!=null && owner is{Alive:true,Deleted:false,Frozen:false,Paralyzed:false,Spell:null}
        && AssassinCombat.Available(owner,s.Ability) && AlliedOverlap.Allowed(owner) && !HitRecovery.Active(owner,Core.TickCount)
        && owner.Map==s.Map && owner.Location==s.Origin && Hands(owner,s);
    internal static void Cancel(PlayerMobile owner)
    {
        var session=ClassCombat.Existing(owner);if(session==null){return;}session.ClawAttack=null;
        if(Handles(session.PendingAbility)){session.PendingAbility=CombatAbility.None;}
    }
    internal static void EquipmentChanged(PlayerMobile owner)
    {if(ClassCombat.Existing(owner)?.ClawAttack is{} s && !Hands(owner,s)){Cancel(owner);}}
    public static bool Begin(PlayerMobile owner,CombatAbility ability,Serial selected,Point3D point,long now)
    {
        if(!Handles(ability) || !AssassinCombat.Available(owner,ability) || !ValidTarget(owner,selected,ability)){return false;}
        var values=SkillMechanics.Values(owner,ability);bool basic=!NecromancerEffects.CanPay(owner,values.Mana256);
        var pair=WeaponHands.Pair(owner);using var primary=ability==CombatAbility.DragonClaw&&pair is{} hands?WeaponHands.Use(owner,hands.Primary):null;
        if(ability==CombatAbility.DragonClaw)
        {
            if(basic){return ClassCombat.Perform(owner,CombatAbility.BasicAttack,World.FindMobile(selected),point,now);}
            if(World.FindMobile(selected) is not BaseCreature recipient){owner.SendMessage(0x22,"Dragon Claw player recipients are not adapted");return false;}
            try{DragonClaws.Require(owner,recipient);}catch(NotSupportedException e){owner.SendMessage(0x22,e.Message);return false;}
        }
        var clock=ability==CombatAbility.DragonClaw?DragonClawPreparation.Plan(owner):null;
        var timing=basic?TigerStrikes.Timing(owner):clock!=null?new ActionTiming(clock.EndFrame,clock.Contacts[0]):Timing(owner);
        if(!SkillCombat.Begin(owner,ability,selected,point,now,(_,_,id,_)=>ValidTarget(owner,id,ability),requireMana:!basic,timing:timing)){return false;}
        var session=ClassCombat.For(owner);
        if(!basic && !NecromancerEffects.Pay(owner,values.Mana256)){session.PendingAbility=CombatAbility.None;return false;}
        int[] events=basic?new[]{timing.EffectMs}:Array.ConvertAll(clock?.Contacts??Schedule(pair!=null,Advance(owner)).Events,x=>x*40);
        session.ClawAttack=new(ability,CombatTiming.Equipped(owner).Serial,pair?.Secondary.Serial??Serial.Zero,selected,owner.Map,owner.Location,now,session.NextAction,0,basic,events);
        return true;
    }
    internal static bool Resolve(PlayerMobile owner,long now)
    {
        var session=ClassCombat.For(owner);var s=session.ClawAttack;if(s==null){return false;}
        if(session.PendingAbility!=s.Ability || !Valid(owner,s)){Cancel(owner);return false;}
        bool processed=false;
        while(s.Released<s.Events.Length && now>=s.Started+s.Events[s.Released])
        {
            if(s.Ability==CombatAbility.DragonClaw&&!ValidTarget(owner,s.Target,s.Ability)){Cancel(owner);return processed;}
            int ordinal=s.Released;s=s with{Released=ordinal+1};session.ClawAttack=s;processed=true;
            // Contact ordinals0/1 select primary/secondary; visual points6/10
            // are both even and must never be used as the hand selector.
            var weapon=World.FindItem(ordinal==1?s.Secondary:s.Primary) as BaseWeapon;
            using var hand=WeaponHands.Pair(owner)!=null?WeaponHands.Use(owner,weapon):null;
            if(ValidTarget(owner,s.Target,s.Ability) && World.FindMobile(s.Target) is{} target)
            {
                if(s.Ability==CombatAbility.DragonClaw)
                {
                    try{DragonClaws.Strike(owner,(BaseCreature)target);}
                    catch(NotSupportedException e){Cancel(owner);owner.SendMessage(0x22,e.Message);return processed;}
                }
                else
                {
                    owner.DoHarmful(target,true);owner.PlaySound(0x238); // Identified claw pose/contact/audio placeholder.
                    using var attempt=PaladinThorns.Attempt(owner,target,false);
                    if(s.Basic){TigerStrikes.BasicStrike(owner,target);CombatDamage.Finish(target,owner);}
                    else{TigerStrikes.ChargeContact(owner,target,SkillMechanics.Values(owner,s.Ability));}
                }
            }
            // Validate actual equipped-pair identity independently of the
            // temporary damage hand; callbacks may cancel the paid action.
            if(session.ClawAttack!=s){return processed;}
            if(!Valid(owner,s)||s.Ability==CombatAbility.DragonClaw&&!ValidTarget(owner,s.Target,s.Ability)){Cancel(owner);return processed;}
        }
        if(!Valid(owner,s)){Cancel(owner);return processed;}
        if(s.Released<s.Events.Length){session.EffectAt=s.Started+s.Events[s.Released];}
        else if(now>=s.Until){Cancel(owner);}
        ClassCombat.SendState(owner,session);return processed;
    }
}
