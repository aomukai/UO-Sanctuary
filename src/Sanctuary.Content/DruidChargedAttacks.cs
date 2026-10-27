// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;
namespace Sanctuary;

internal static class DruidChargedAttacks
{
    internal sealed record Pending(CombatAbility Ability,Serial Target,DruidPlayerForm Form,bool Hit,bool Blocked,bool BlockAnimation,PreparedWeaponImpact Impact,
        PlayerWeaponDrain Drain,double Roll,long EffectAt);
    internal static Func<double> HitRoll=Utility.RandomDouble;
    internal static Func<double> ElementRoll=Utility.RandomDouble;
    internal static Func<int,int> PhysicalRoll=Utility.Random;
    internal static Random Effects=Random.Shared;
    // Both current start56/do120 rows share this contact/charge transaction.
    internal static bool Handles(CombatAbility ability)=>ability is CombatAbility.Maul or CombatAbility.FeralRage;
    internal static bool ValidTarget(PlayerMobile owner,CombatAbility ability,Serial selected)
        =>DruidFormAttacks.ValidTarget(owner,selected)&&(ability!=CombatAbility.Maul||World.FindMobile(selected) is BaseCreature creature&&CombatStuns.Rules(creature,out _,out _));
    internal static bool Begin(PlayerMobile owner,CombatAbility ability,Serial selected,Point3D point,long now)
    {
        if(!Handles(ability)||!DruidCombat.Available(owner,ability)||!ValidTarget(owner,ability,selected)){return false;}
        var values=SkillMechanics.Values(owner,ability);
        // Current AttackNoMana1 and the pinned older player dispatcher support
        // ordinary Attack at low mana; exact modern outcome remains unverified.
        // No new charge or early control-pass acquisition rule is introduced.
        if(!NecromancerEffects.CanPay(owner,values.Mana256))
        {return ClassCombat.Perform(owner,CombatAbility.BasicAttack,World.FindMobile(selected),point,now);}
        var form=DruidPlayerForms.State(owner);
        if(!SkillCombat.Begin(owner,ability,selected,point,now,(_,_,id,_)=>ValidTarget(owner,ability,id))){return false;}
        var session=ClassCombat.For(owner);var target=(BaseCreature)World.FindMobile(selected);
        using var melee=CombatTiming.Equipped(owner) is{} weapon?WeaponHands.UseMelee(owner,weapon):null;
        // Source start56 allocates the hit and existing charge damage/stun before
        // the action event. Immunity does not turn a successful contact into a miss.
        bool hit=ClassStats.CheckHit(owner,target,(int)values.Formula("toht"),HitRoll,projectile:false)==true;
        bool blocked=false,blockAnimation=false;
        if(hit&&SourceBlocking.TryRoll(target,true,out blocked,out blockAnimation)&&blocked){hit=false;}
        double roll=0;PreparedWeaponImpact packet=default;
        if(hit)
        {roll=ElementRoll();packet=GearEffects.PrepareWeaponImpact(owner,target,roll,Effects,GearEffects.Damage(owner,roll,target,(int)values.Eval("calc1"),PhysicalRoll));}
        var drain=WeaponLeech.Capture(owner);
        if(!NecromancerEffects.Pay(owner,values.Mana256)){session.PendingAbility=CombatAbility.None;return false;}
        session.DruidChargeAttack=new(ability,selected,form,hit,blocked,blockAnimation,packet,drain,roll,session.EffectAt);return true;
    }
    internal static bool Release(PlayerMobile owner,CombatAbility ability,long now)
    {
        var session=ClassCombat.For(owner);var pending=session.DruidChargeAttack;
        if(pending==null||now<pending.EffectAt){return false;}session.DruidChargeAttack=null;
        if(pending.Ability!=ability||pending.Target!=session.PendingTarget||!ReferenceEquals(pending.Form,DruidPlayerForms.State(owner))
            ||!DruidCombat.Available(owner,ability)||!ValidTarget(owner,ability,pending.Target)){return false;}
        var target=World.FindMobile(pending.Target);owner.DoHarmful(target,true);owner.PlaySound(0x238); // Identified contact/audio placeholder.
        using var melee=CombatTiming.Equipped(owner) is{} weapon?WeaponHands.UseMelee(owner,weapon):null;
        using var attempt=PaladinThorns.Attempt(owner,target,false);
        if(pending.Blocked){SourceBlocking.Present((BaseCreature)target,pending.BlockAnimation);}
        if(!pending.Hit){return true;}
        GearEffects.Strike(owner,target,pending.Roll,Effects,preparedImpact:pending.Impact,sourceDrain:pending.Drain);
        // do120 drains combat/durability before aura gain. Reentrant death or
        // form removal cannot resurrect a stale group's state after that commit.
        if(ReferenceEquals(pending.Form,DruidPlayerForms.State(owner)))
        {DruidAttackCharges.Gain(owner,SkillMechanics.Values(owner,ability));}
        return true;
    }
    internal static void Cancel(PlayerMobile owner)
    {var session=ClassCombat.Existing(owner);if(session==null){return;}session.DruidChargeAttack=null;if(Handles(session.PendingAbility)){session.PendingAbility=CombatAbility.None;}}
    internal static void EquipmentChanged(PlayerMobile owner)
    {var session=ClassCombat.Existing(owner);if(session?.DruidChargeAttack!=null&&(CombatTiming.Equipped(owner)?.Serial??Serial.Zero)!=session.PendingWeapon){Cancel(owner);}}
}
