// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;
namespace Sanctuary;

internal static class DruidHunger
{
    internal sealed record Pending(Serial Target,DruidPlayerForm Form,long EffectAt);
    internal static Func<double> HitRoll=Utility.RandomDouble;
    internal static Func<double> ElementRoll=Utility.RandomDouble;
    internal static Func<int,int> PhysicalRoll=Utility.Random;
    internal static Random Effects=Random.Shared;
    internal static int ReducePhysical(int physical256)
    {
        if(physical256<0){throw new ArgumentOutOfRangeException(nameof(physical256));}
        return checked(physical256+SourcePercentage.Calculate(physical256,-75,100));
    }
    private static void Route(SkillValues values)
    {
        var row=SkillMechanics.For(CombatAbility.Hunger);
        if(values.Ability!=CombatAbility.Hunger||row.SourceId!=242||row.Number("srvstfunc")!=0||row.Number("srvdofunc")!=122
            ||row.Number("SrcDam")!=128||row.Text("EType")!="0"||row.Text("anim")!="S3"||row.Text("seqtrans")!="S3"
            ||row.Number("restrict")!=2||row.Text("State1")!="wolf"||row.Text("State2")!="bear"
            ||row.Text("calc1")!="par5"||row.Number("Param5")!=-75||row.Text("calc2")!="dm12"||row.Text("calc3")!="dm34"||values.Mana256!=768)
        {throw new NotSupportedException("Unmapped Hunger source route");}
    }
    internal static bool Begin(PlayerMobile owner,Serial selected,Point3D point,long now)
    {
        const CombatAbility ability=CombatAbility.Hunger;
        if(!DruidCombat.Available(owner,ability)||!DruidFormAttacks.ValidTarget(owner,selected)){return false;}
        var values=SkillMechanics.Values(owner,ability);Route(values);
        if(!NecromancerEffects.CanPay(owner,values.Mana256))
        {return ClassCombat.Perform(owner,CombatAbility.BasicAttack,World.FindMobile(selected),point,now);}
        var form=DruidPlayerForms.State(owner);
        if(!SkillCombat.Begin(owner,ability,selected,point,now,(_,_,id,_)=>DruidFormAttacks.ValidTarget(owner,id))){return false;}
        // No start routine. Do122 captures the contact/weapon packet at the
        // S3action, not now, unlike start58 Fire Claws/start56 charge attacks.
        var session=ClassCombat.For(owner);session.HungerAttack=new(selected,form,session.EffectAt);return true;
    }
    internal static bool Release(PlayerMobile owner,long now)
    {
        var session=ClassCombat.For(owner);var pending=session.HungerAttack;
        if(pending==null||now<pending.EffectAt){return false;}session.HungerAttack=null;
        if(pending.Target!=session.PendingTarget||!ReferenceEquals(pending.Form,DruidPlayerForms.State(owner))
            ||!DruidCombat.Available(owner,CombatAbility.Hunger)||!DruidFormAttacks.ValidTarget(owner,pending.Target)){return false;}
        var values=SkillMechanics.Values(owner,CombatAbility.Hunger);Route(values);
        // Lost mana during windup is a distinct unverified modern branch.
        // Refuse it rather than silently copying the old ignored-debit quirk.
        if(!NecromancerEffects.CanPay(owner,values.Mana256)){return false;}
        var target=(BaseCreature)World.FindMobile(pending.Target);owner.DoHarmful(target,true);
        using var melee=CombatTiming.Equipped(owner) is{} weapon?WeaponHands.UseMelee(owner,weapon):null;
        bool hit=ClassStats.CheckHit(owner,target,(int)values.Formula("toht"),HitRoll,projectile:false)==true;
        bool blocked=false,blockAnimation=false;if(hit&&SourceBlocking.TryRoll(target,true,out blocked,out blockAnimation)&&blocked){hit=false;}
        if(blocked){SourceBlocking.Present(target,blockAnimation);}
        if(hit)
        {
            using var attempt=PaladinThorns.Attempt(owner,target,false);
            double roll=ElementRoll();var raw=GearEffects.Damage(owner,roll,target,physicalRoll:PhysicalRoll);
            var packet=GearEffects.PrepareWeaponImpact(owner,target,roll,Effects,raw,physicalAfterCritical:ReducePhysical);
            var drain=WeaponLeech.Capture(owner,new((int)values.Eval("calc2"),(int)values.Eval("calc3")));
            GearEffects.Strike(owner,target,roll,Effects,preparedImpact:packet,sourceDrain:drain);
            owner.PlaySound(0x238); // Identified bite/contact audio placeholder; leech effect uses the shared placeholder.
        }
        // No-start handler consumes mana after do122, so a full mana pool
        // cannot bank this bite's leech against its own payment.
        NecromancerEffects.Pay(owner,values.Mana256);
        return true;
    }
    internal static void Cancel(PlayerMobile owner)
    {var session=ClassCombat.Existing(owner);if(session==null){return;}session.HungerAttack=null;if(session.PendingAbility==CombatAbility.Hunger){session.PendingAbility=CombatAbility.None;}}
    internal static void EquipmentChanged(PlayerMobile owner)
    {var session=ClassCombat.Existing(owner);if(session?.HungerAttack!=null&&(CombatTiming.Equipped(owner)?.Serial??Serial.Zero)!=session.PendingWeapon){Cancel(owner);}}
}
