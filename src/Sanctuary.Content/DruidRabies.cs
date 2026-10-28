// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;
namespace Sanctuary;

internal static class DruidRabies
{
    internal sealed record Pending(Serial Target,DruidPlayerForm Form,long EffectAt);
    internal static Func<double> HitRoll=Utility.RandomDouble;
    internal static Func<double> ElementRoll=Utility.RandomDouble;
    internal static Func<int,int> PhysicalRoll=Utility.Random;
    internal static Func<int,int> PoisonRoll=Utility.Random;
    internal static Random Effects=Random.Shared;
    private static void Route(SkillValues values)
    {
        var row=SkillMechanics.For(CombatAbility.Rabies);
        if(values.Ability!=CombatAbility.Rabies||row.SourceId!=238||row.Number("srvstfunc")!=57||row.Number("srvdofunc")!=121
            ||row.Number("SrcDam")!=0||row.Text("EType")!="pois"||row.Text("anim")!="S3"||row.Text("seqtrans")!="S3"
            ||row.Number("restrict")!=2||row.Text("State1")!="wolf"||row.Text("State2")!="0"
            ||row.Text("auratargetstate")!="rabies"||row.Text("srvmissilea")!="rabiesplague"||row.Number("ResultFlags")!=1||row.Number("HitFlags")!=2
            ||row.Text("EDmgSymPerCalc")!="(skill('Plague Poppy'.blvl))*par8"||values.Mana256!=2560)
        {throw new NotSupportedException("Unmapped Rabies source route");}
    }
    internal static bool Begin(PlayerMobile owner,Serial selected,Point3D point,long now)
    {
        const CombatAbility ability=CombatAbility.Rabies;
        if(!DruidCombat.Available(owner,ability)||!DruidFormAttacks.ValidTarget(owner,selected)){return false;}
        var values=SkillMechanics.Values(owner,ability);Route(values);
        if(!NecromancerEffects.CanPay(owner,values.Mana256))
        {return ClassCombat.Perform(owner,CombatAbility.BasicAttack,World.FindMobile(selected),point,now);}
        if(!SkillCombat.Begin(owner,ability,selected,point,now,(_,_,id,_)=>DruidFormAttacks.ValidTarget(owner,id))){return false;}
        var session=ClassCombat.For(owner);session.RabiesFault=null;
        var target=(BaseCreature)World.FindMobile(selected);
        using var melee=CombatTiming.Equipped(owner) is{} weapon?WeaponHands.UseMelee(owner,weapon):null;
        bool hit=ClassStats.CheckHit(owner,target,(int)values.Formula("toht"),HitRoll,projectile:false)==true;
        bool blocked=false,animation=false;if(hit&&SourceBlocking.TryRoll(target,true,out blocked,out animation)&&blocked){hit=false;}
        if(blocked){SourceBlocking.Present(target,animation);}
        // Complete older start57 refuses failed accuracy/block before payment.
        // Modern payment/failed-start presentation remain separately flagged.
        if(!hit){session.PendingAbility=CombatAbility.None;return false;}
        if(!NecromancerEffects.Pay(owner,values.Mana256))
        {session.PendingAbility=CombatAbility.None;session.RabiesFault="Reentrant start mana loss is unsupported";return false;}
        // Pinned modern10mana takes precedence over the older rank1payer's
        // minmana1quirk. Only the successful start gate is retained here.
        session.RabiesAttack=new(selected,DruidPlayerForms.State(owner),session.EffectAt);return true;
    }
    internal static bool Release(PlayerMobile owner,long now)
    {
        var session=ClassCombat.For(owner);var pending=session.RabiesAttack;
        if(pending==null||now<pending.EffectAt){return false;}session.RabiesAttack=null;
        if(pending.Target!=session.PendingTarget||!ReferenceEquals(pending.Form,DruidPlayerForms.State(owner))||!DruidCombat.Available(owner,CombatAbility.Rabies))
        {session.RabiesFault="Rabies form/skill/target context changed";return false;}
        if(!DruidFormAttacks.ValidTarget(owner,pending.Target))
        {
            // The older do121 can attach a marker after its range-gated drain
            // fails. Modern remote attachment is unverified: refuse this branch
            // explicitly instead of inventing a remote poison hit or refund.
            session.RabiesFault="Lost-contact Rabies attachment is unverified";return false;
        }
        var target=(BaseCreature)World.FindMobile(pending.Target);owner.DoHarmful(target,true);
        using var melee=CombatTiming.Equipped(owner) is{} weapon?WeaponHands.UseMelee(owner,weapon):null;
        // Source do121 allocates/drains a distinct ordinary transformed attack.
        // Its legacy extra-AR argument incorrectly uses Rabies rank; whether
        // modern D2R retains that path is unverified. Existing native form AR is
        // included once, without silently adding that old second bonus.
        bool hit=ClassStats.CheckHit(owner,target,0,HitRoll,projectile:false)==true;
        bool blocked=false,animation=false;if(hit&&SourceBlocking.TryRoll(target,true,out blocked,out animation)&&blocked){hit=false;}
        if(blocked){SourceBlocking.Present(target,animation);}
        if(hit)
        {
            using var attempt=PaladinThorns.Attempt(owner,target,false);
            double roll=ElementRoll();var raw=GearEffects.Damage(owner,roll,target,physicalRoll:PhysicalRoll);
            var packet=GearEffects.PrepareWeaponImpact(owner,target,roll,Effects,raw);
            GearEffects.Strike(owner,target,roll,Effects,preparedImpact:packet,sourceDrain:WeaponLeech.Capture(owner));
        }
        if(target is not{Alive:true,Deleted:false}||owner is not{Alive:true,Deleted:false}||!ReferenceEquals(pending.Form,DruidPlayerForms.State(owner)))
        {session.RabiesFault="Lethal/reentrant post-bite marker chronology is unverified";return true;}
        // Re-evaluate AFTER ordinary weapon events, as do121 does. Item poison
        // and physical critical/leech remain exclusively in that first packet.
        var values=SkillMechanics.Values(owner,CombatAbility.Rabies);Route(values);
        var at=TimedEffects.Now;
        DruidRabiesInfections.Mark(owner,target,values.Rank,values.DurationFrames,at);
        var poison=DruidRabiesPackets.Capture(owner,values.Rank).Elements;
        int rate=WeaponMissileDamage.RollValue(poison.PoisonMin,poison.PoisonMax,0,PoisonRoll);
        PoisonJavelins.Poison(owner,target,rate,values.DurationFrames,at);
        owner.PlaySound(0x238); // Identified bite/contact audio placeholder.
        return true;
    }
    internal static void Cancel(PlayerMobile owner)
    {var session=ClassCombat.Existing(owner);if(session==null){return;}session.RabiesAttack=null;if(session.PendingAbility==CombatAbility.Rabies){session.PendingAbility=CombatAbility.None;}}
    internal static void EquipmentChanged(PlayerMobile owner)
    {var session=ClassCombat.Existing(owner);if(session?.RabiesAttack!=null&&(CombatTiming.Equipped(owner)?.Serial??Serial.Zero)!=session.PendingWeapon){Cancel(owner);}}
}
