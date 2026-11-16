// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;
namespace Sanctuary;

// Form skill controllers own accuracy,shield selection,critical,element rolls
// and payment at their distinct start/action phases. Retain that packet rather
// than allocating it again in the shared contact consumer.
internal static class DruidWeaponContact
{
    internal static Func<int,int> PoisonRoll=Utility.Random;
    internal static GearDamage Damage(PlayerMobile source,BaseCreature target,double roll,int? physicalPercent=null,Func<int,int> physicalRoll=null)
        =>GearEffects.Damage(source,roll,target,physicalPercent,physicalRoll,PoisonRoll,()=>
        {
            var venom=Venoms.State(source);
            return WeaponPoisonPackets.Melee(WeaponPoisonPackets.AddVenom(BowLaunch.RawPoison(source,CombatTiming.Equipped(source)),venom),
                NamedElements.MasteryPercent(source,"pois"),128,PoisonRoll,overrideFrames:venom?.Frames??0);
        });
    internal static bool RollHit(PlayerMobile source,BaseCreature target,int attackPercent,Func<double> roll)
    {
        bool hit=ClassStats.CheckHit(source,target,attackPercent,roll,projectile:false)==true;
        if(hit){WeaponStartEffects.SuccessfulHitRoll(source,target);}return hit;
    }
    internal static PlayerWeaponContactPrepared Capture(PlayerMobile source,BaseCreature target,bool hit,bool blocked,
        PreparedWeaponImpact impact,PlayerWeaponDrain drain,Random effects)
    {
        PlayerWeaponContact.Require(source,target,true);
        int flags=hit?SourceMeleeResult.Successful|SourceMeleeResult.GetHit:blocked?SourceMeleeResult.Block:0;
        int cold=0;var damage=impact.Damage;
        if(hit&&impact.AllocatedPoison==null&&damage.SourcePoison is{} rawPoison)
        {
            var poisonPacket=PoisonJavelins.PreparePoison(source,target,rawPoison.Rate256,rawPoison.Frames);
            impact=impact with{LifeDamage256=checked((impact.LifeDamage256??NamedElements.To256(impact.Parts.Total))+poisonPacket.Rate256),AllocatedPoison=poisonPacket};
        }
        if(hit&&damage.Cold>0)
        {
            if(damage.ColdMs%40!=0){throw new NotSupportedException("Captured form cold duration has no source-frame projection");}
            cold=ColdStates.LengthFrames(damage.ColdMs/40,NamedElements.Resistance(source,target,"cold",target.ColdResistance),
                ItemDurationDefenses.CannotFreeze(target),ItemDurationDefenses.HalfFreeze(target));
        }
        // Explicit aggregate-only compatibility callers retain their old
        // transaction. All six learned producers now carry source rate/length;
        // never infer either from a rounded aggregate total.
        Action<PlayerMobile,BaseCreature,DateTime> poison=null;
        if(hit&&damage.Poison>0&&impact.AllocatedPoison==null)
        {
            poison=(owner,recipient,at)=>GearEffects.ApplyPoison(owner,recipient,damage.Poison,TimedEffects.TickAt(at),damage.PoisonMs);
        }
        return new(flags,impact,drain,cold,CreatureHitRecovery.PlayerHitClass(source),effects,
            MeleeRange:DruidFormAttacks.Reach(source),CapturedPoison:poison);
    }
    internal static MonsterContactResult Release(PlayerMobile source,BaseCreature target,PlayerWeaponContactPrepared captured,DateTime at)
        =>PlayerWeaponContact.Release(source,target,captured,at,true);
}
