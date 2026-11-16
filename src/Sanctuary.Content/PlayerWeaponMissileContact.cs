// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;
namespace Sanctuary;

// Actor packet allocation shared by individually qualified weapon missiles.
internal static class PlayerWeaponMissileContact
{
    internal static void Release(PlayerMobile owner,BaseCreature target,WeaponFlight flight,GearDamage damage,int rate256,int hitClass,uint hitFlags=32)
    {
        var parts=SkillCombat.MitigatePrepared(owner,target,damage with{Poison=0},out int absorbed);
        var poison=PoisonJavelins.PreparePoison(owner,target,rate256,flight.Damage.Elements.PoisonImpactFrames);
        var impact=new PreparedWeaponImpact(damage,parts,absorbed) with{LifeDamage256=checked(NamedElements.To256(parts.Total)+poison.Rate256),AllocatedPoison=poison};
        int cold=damage.Cold>0?ColdStates.LengthFrames(flight.Damage.Elements.ColdFrames,NamedElements.Resistance(owner,target,"cold",target.ColdResistance),ItemDurationDefenses.CannotFreeze(target),ItemDurationDefenses.HalfFreeze(target)):0;
        var contact=new PlayerWeaponContactPrepared(SourceMeleeResult.Successful|SourceMeleeResult.GetHit,impact,
            new(flight.Damage.Elements.LifeDrain,flight.Damage.Elements.ManaDrain),cold,0,WeaponMissiles.ItemEffects,HitClass:hitClass,HitFlags:hitFlags,SuppressWear:true,
            UninterruptibleItemCasts:()=>BarbarianWhirlwind.Locked(owner));
        PlayerWeaponContact.ReleaseMissile(owner,target,contact,flight.NextUtc,true);
    }
}
