// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;
namespace Sanctuary;

internal static class DruidFireClaws
{
    internal sealed record Pending(Serial Target,DruidPlayerForm Form,bool Hit,bool Blocked,bool BlockAnimation,
        PreparedWeaponImpact Impact,PlayerWeaponDrain Drain,double Roll,int Fire256,long EffectAt);
    internal static Func<double> HitRoll=Utility.RandomDouble;
    internal static Func<double> ElementRoll=Utility.RandomDouble;
    internal static Func<int,int> PhysicalRoll=Utility.Random;
    internal static Func<int,int> FireRoll=Utility.Random;
    internal static Random Effects=Random.Shared;
    internal static (int Min,int Max) FireRange(SkillValues values,int mastery)
        =>(AmazonMelee.EnhanceEndpoint(values.Damage256(false),mastery),AmazonMelee.EnhanceEndpoint(values.Damage256(true),mastery));
    internal static int Fire(PlayerMobile owner,SkillValues values)
    {
        var range=FireRange(values,NamedElements.MasteryPercent(owner,"fire"));int span=Math.Max(1,range.Max-range.Min);
        return checked(range.Min+Math.Clamp(FireRoll(span),0,span-1));
    }
    private static void Route(SkillValues values)
    {
        var row=SkillMechanics.For(CombatAbility.FireClaws);
        if(values.Ability!=CombatAbility.FireClaws||row.SourceId!=239||row.Number("srvstfunc")!=58||row.Number("srvdofunc")!=2
            ||row.Number("SrcDam")!=128||row.Text("EType")!="fire"||row.Text("anim")!="A1"||row.Text("seqtrans")!="A1"
            ||row.Number("restrict")!=2||row.Text("State1")!="wolf"||row.Text("State2")!="bear"||row.Text("srvoverlay")!="fire_hit"
            ||row.Text("EDmgSymPerCalc")!="(skill('Firestorm'.blvl)+skill('Molten Boulder'.blvl))*par8"||values.Mana256!=1024)
        {throw new NotSupportedException("Unmapped Fire Claws source route");}
    }
    internal static bool Begin(PlayerMobile owner,Serial selected,Point3D point,long now)
    {
        const CombatAbility ability=CombatAbility.FireClaws;
        if(!DruidCombat.Available(owner,ability)||!DruidFormAttacks.ValidTarget(owner,selected)){return false;}
        var values=SkillMechanics.Values(owner,ability);Route(values);
        // Table AttackNoMana1 supports ordinary Attack; exact current fallback
        // and the separate later free-empty-swing/control pass remain distinct.
        if(!NecromancerEffects.CanPay(owner,values.Mana256))
        {return ClassCombat.Perform(owner,CombatAbility.BasicAttack,World.FindMobile(selected),point,now);}
        var form=DruidPlayerForms.State(owner);
        if(!SkillCombat.Begin(owner,ability,selected,point,now,(_,_,id,_)=>DruidFormAttacks.ValidTarget(owner,id))){return false;}
        var session=ClassCombat.For(owner);var target=(BaseCreature)World.FindMobile(selected);
        using var melee=CombatTiming.Equipped(owner) is{} weapon?WeaponHands.UseMelee(owner,weapon):null;
        bool hit=ClassStats.CheckHit(owner,target,(int)values.Formula("toht"),HitRoll,projectile:false)==true;
        bool blocked=false,blockAnimation=false;if(hit&&SourceBlocking.TryRoll(target,true,out blocked,out blockAnimation)&&blocked){hit=false;}
        double roll=0;int fire=0;PreparedWeaponImpact impact=default;
        var drain=WeaponLeech.Capture(owner); // Existing Feral charge, never fire damage, supplies physical leech.
        if(hit)
        {
            roll=ElementRoll();var raw=GearEffects.Damage(owner,roll,target,(int)values.Eval("calc1"),PhysicalRoll);
            impact=GearEffects.PrepareWeaponImpact(owner,target,roll,Effects,raw,skillElementsFactory:()=>
            {fire=Fire(owner,values);return new(0,fire/256.0,0,0,0,ColdMs:0);});
        }
        if(!NecromancerEffects.Pay(owner,values.Mana256)){session.PendingAbility=CombatAbility.None;return false;}
        session.FireClawAttack=new(selected,form,hit,blocked,blockAnimation,impact,drain,roll,fire,session.EffectAt);return true;
    }
    internal static bool Release(PlayerMobile owner,long now)
    {
        var session=ClassCombat.For(owner);var pending=session.FireClawAttack;
        if(pending==null||now<pending.EffectAt){return false;}session.FireClawAttack=null;
        if(pending.Target!=session.PendingTarget||!ReferenceEquals(pending.Form,DruidPlayerForms.State(owner))
            ||!DruidCombat.Available(owner,CombatAbility.FireClaws)||!DruidFormAttacks.ValidTarget(owner,pending.Target)){return false;}
        var target=(BaseCreature)World.FindMobile(pending.Target);owner.DoHarmful(target,true);owner.PlaySound(0x238); // Identified contact/audio placeholder.
        using var melee=CombatTiming.Equipped(owner) is{} weapon?WeaponHands.UseMelee(owner,weapon):null;
        using var attempt=PaladinThorns.Attempt(owner,target,false);
        if(pending.Blocked){SourceBlocking.Present(target,pending.BlockAnimation);}if(!pending.Hit){return true;}
        target.FixedEffect(0x374A,10,5,0x489,0); // Identified fire_hit placeholder, preceding do2combat drain.
        GearEffects.Strike(owner,target,pending.Roll,Effects,preparedImpact:pending.Impact,sourceDrain:pending.Drain);
        // Fire Claws has no charge state: preserve existing Maul/Feral count and deadline.
        return true;
    }
    internal static void Cancel(PlayerMobile owner)
    {var session=ClassCombat.Existing(owner);if(session==null){return;}session.FireClawAttack=null;if(session.PendingAbility==CombatAbility.FireClaws){session.PendingAbility=CombatAbility.None;}}
    internal static void EquipmentChanged(PlayerMobile owner)
    {var session=ClassCombat.Existing(owner);if(session?.FireClawAttack!=null&&(CombatTiming.Equipped(owner)?.Serial??Serial.Zero)!=session.PendingWeapon){Cancel(owner);}}
}
