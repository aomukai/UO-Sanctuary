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
        int cold=0,freeze=0;
        if(damage.Cold>0)
        {
            if(flight.Damage.FreezeColdLength){(cold,freeze)=DragonTails.ColdLengths(owner,target,0,flight.Damage.Elements.ColdFrames);}
            else{cold=ColdStates.LengthFrames(flight.Damage.Elements.ColdFrames,NamedElements.Resistance(owner,target,"cold",target.ColdResistance),ItemDurationDefenses.CannotFreeze(target),ItemDurationDefenses.HalfFreeze(target));}
        }
        var contact=new PlayerWeaponContactPrepared(SourceMeleeResult.Successful|SourceMeleeResult.GetHit,impact,
            new(flight.Damage.Elements.LifeDrain,flight.Damage.Elements.ManaDrain),cold,0,WeaponMissiles.ItemEffects,HitClass:hitClass,FreezeFrames:freeze,HitFlags:hitFlags,SuppressWear:true,
            UninterruptibleItemCasts:()=>BarbarianWhirlwind.Locked(owner));
        PlayerWeaponContact.ReleaseMissile(owner,target,contact,flight.NextUtc,true);
    }
}
