// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server.Mobiles;
namespace Sanctuary;

// Form skill controllers own accuracy,shield selection,critical,element rolls
// and payment at their distinct start/action phases. Retain that packet rather
// than allocating it again in the shared contact consumer.
internal static class DruidWeaponContact
{
    internal static PlayerWeaponContactPrepared Capture(PlayerMobile source,BaseCreature target,bool hit,bool blocked,
        PreparedWeaponImpact impact,PlayerWeaponDrain drain,Random effects)
    {
        PlayerWeaponContact.Require(source,target,true);
        int flags=hit?SourceMeleeResult.Successful|SourceMeleeResult.GetHit:blocked?SourceMeleeResult.Block:0;
        int cold=0;var damage=impact.Damage;
        if(hit&&damage.Cold>0)
        {
            if(damage.ColdMs%40!=0){throw new NotSupportedException("Captured form cold duration has no source-frame projection");}
            cold=ColdStates.LengthFrames(damage.ColdMs/40,NamedElements.Resistance(source,target,"cold",target.ColdResistance),
                ItemDurationDefenses.CannotFreeze(target),ItemDurationDefenses.HalfFreeze(target));
        }
        // These pre-existing aggregate producers do not have AllocCombat's
        // initial poison-rate lane. Keep their captured poison transaction,
        // without fabricating an extra direct poison hit or recovering a new
        // rate from a rounded total. New packet producers use AllocatedPoison.
        Action<PlayerMobile,BaseCreature,DateTime> poison=null;
        if(hit&&damage.Poison>0&&impact.AllocatedPoison==null)
        {
            if(damage.SourcePoison is{} packet){poison=(owner,recipient,at)=>packet.Commit(owner,recipient,at);}
            else{poison=(owner,recipient,at)=>GearEffects.ApplyPoison(owner,recipient,damage.Poison,TimedEffects.TickAt(at),damage.PoisonMs);}
        }
        return new(flags,impact,drain,cold,CreatureHitRecovery.PlayerHitClass(source),effects,
            MeleeRange:DruidFormAttacks.Reach(source),CapturedPoison:poison);
    }
    internal static MonsterContactResult Release(PlayerMobile source,BaseCreature target,PlayerWeaponContactPrepared captured,DateTime at)
        =>PlayerWeaponContact.Release(source,target,captured,at,true);
}
