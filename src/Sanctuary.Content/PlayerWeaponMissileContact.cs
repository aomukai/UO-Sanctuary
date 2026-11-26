// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;
namespace Sanctuary;

// Actor packet allocation shared by individually qualified weapon missiles.
internal static class PlayerWeaponMissileContact
{
    internal static PreparedMonsterDamageResult Release(PlayerMobile owner,BaseCreature target,WeaponFlight flight,GearDamage damage,int rate256,int hitClass,uint hitFlags=32)
    {
        var parts=SkillCombat.MitigatePrepared(owner,target,damage with{Poison=damage.ImpactPoison},out int absorbed);
        bool reset=flight.Damage.LightningJavelinPercent is >0;
        var poison=reset?new PreparedPoison(0,0):PoisonJavelins.PreparePoison(owner,target,rate256,flight.Damage.Elements.PoisonImpactFrames);
        var impact=new PreparedWeaponImpact(damage,parts,absorbed) with{LifeDamage256=checked(NamedElements.To256(parts.Total)+poison.Rate256),AllocatedPoison=poison};
        int cold=0,freeze=0;
        if(damage.Cold>0)
        {
            if(flight.Damage.FreezeColdLength){(cold,freeze)=DragonTails.ColdLengths(owner,target,0,flight.Damage.Elements.ColdFrames);}
            else{cold=ColdStates.LengthFrames(flight.Damage.Elements.ColdFrames,NamedElements.Resistance(owner,target,"cold",target.ColdResistance),ItemDurationDefenses.CannotFreeze(target),ItemDurationDefenses.HalfFreeze(target));}
        }
        var contact=new PlayerWeaponContactPrepared(SourceMeleeResult.Successful|SourceMeleeResult.GetHit,impact,
            reset?new(0,0):new(flight.Damage.Elements.LifeDrain,flight.Damage.Elements.ManaDrain),cold,0,WeaponMissiles.ItemEffects,HitClass:hitClass,FreezeFrames:freeze,HitFlags:hitFlags,SuppressWear:true,
            UninterruptibleItemCasts:()=>BarbarianWhirlwind.Locked(owner));
        return PlayerWeaponContact.ReleaseMissile(owner,target,contact,flight.NextUtc,true);
    }
    // Element-only HitFlags2 area packets share HP/status/credit/return,not weapon events.
    internal static bool TryElemental(PlayerMobile owner,BaseCreature target,int damage256,bool freeze,int rawFrames,DateTime at,out string reason)
        =>TryElementalCore(owner,target,damage256,freeze,rawFrames,at,5,2,0,0,SkillDamageKind.Direct,out reason);
    internal static bool TryFireFrame(PlayerMobile owner,BaseCreature target,int damage256,int damageRate,bool soft,DateTime at,out string reason)
        =>TryElementalCore(owner,target,damage256,false,0,at,1|(soft?16384:0),0,32,damageRate,SkillDamageKind.Periodic,out reason);
    internal static bool TryLightning(PlayerMobile owner,BaseCreature target,int damage256,DateTime at,out string reason,int hitClass=64)
    {
        if(!ElementalAdmission(owner,target,out bool contact,out reason)){return false;}
        if(!contact){return true;}
        var point = target.Location;
        Effects.SendLocationEffect(point, owner.Map, SpellEffectGraphics.ChargedBoltContact, SpellEffectGraphics.LightningFrames);
        SpellAreaSounds.Pulse(owner.Serial.Value, SpellEffectSounds.ChargedBoltContact, point, owner.Map, TimedEffects.Now, 80);
        CommitElemental(owner,target,new(0,0,0,damage256/256.0,0),false,0,at,5,0,hitClass,0,SkillDamageKind.Direct);
        return true;
    }
    internal static bool TryPoison(PlayerMobile owner,BaseCreature target,int rate256,int frames,DateTime at,out string reason,int hitClass=6)
    {
        if(!ElementalAdmission(owner,target,out bool contact,out reason)){return false;}
        if(!contact){return true;}
        var poison=PoisonJavelins.PreparePoison(owner,target,rate256,frames);
        var impact=new PreparedWeaponImpact(default,default,0)with{LifeDamage256=poison.Rate256,AllocatedPoison=poison};
        var packet=new PlayerWeaponContactPrepared(SourceMeleeResult.Successful,impact,new(0,0),0,0,WeaponMissiles.ItemEffects,
            HitClass:hitClass,HitFlags:0,SuppressWear:true);
        PlayerWeaponContact.ReleaseMissile(owner,target,packet,at,true);return true;
    }
    internal static bool TryLightningStrike(PlayerMobile owner,BaseCreature target,GearDamage damage,DateTime at,out string reason)
    {
        if(!ElementalAdmission(owner,target,out bool contact,out reason)){return false;}
        if(contact)
        {
            SorceressVisuals.Impact(target,CombatAbility.LightningStrike);
            CommitElemental(owner,target,damage,false,0,at,5,0,64,0,SkillDamageKind.Direct);
        }
        return true;
    }
    private static bool ElementalAdmission(PlayerMobile owner,BaseCreature target,out bool contact,out string reason)
    {
        contact=false;reason=null;
        if(owner is not{Alive:true,Deleted:false}){reason="Elemental missile caster is no longer active";return false;}
        if(target is not{Alive:true,Deleted:false}||!target.CanBeDamaged()||target.Map!=owner.Map||ClassCombat.Friendly(owner,target)){return true;}
        try
        {
            PlayerWeaponContact.Require(owner,target,true,weaponSetup:false);
            if(PlayerWeaponContact.Metadata(target,true)is{Killable:false}){throw new NotSupportedException("Elemental missile recipient is not source-killable");}
        }
        catch(NotSupportedException e){reason=e.Message;return false;}
        contact=!ClassAvoidance.Missile(target);return true;
    }
    private static bool TryElementalCore(PlayerMobile owner,BaseCreature target,int damage256,bool freeze,int rawFrames,DateTime at,int flags,uint hitFlags,int hitClass,int damageRate,SkillDamageKind kind,out string reason)
    {
        if(!ElementalAdmission(owner,target,out bool contact,out reason)){return false;}
        if(!contact){return true;}
        var damage=freeze?new GearDamage(0,0,damage256/256.0,0,0):new GearDamage(0,damage256/256.0,0,0,0);
        CommitElemental(owner,target,damage,freeze,rawFrames,at,flags,hitFlags,hitClass,damageRate,kind);return true;
    }
    private static void CommitElemental(PlayerMobile owner,BaseCreature target,GearDamage damage,bool freeze,int rawFrames,DateTime at,int flags,uint hitFlags,int hitClass,int damageRate,SkillDamageKind kind)
    {
        var parts=SkillCombat.MitigatePrepared(owner,target,damage,out int absorbed,damageRate);int cold=0,frozen=0;
        if(freeze){(cold,frozen)=DragonTails.ColdLengths(owner,target,0,rawFrames);}
        // A potion's immediate poison component has zero status duration; it
        // belongs in the typed packet but must not replace an existing clock.
        var impact=new PreparedWeaponImpact(damage,parts,absorbed)with{LifeDamage256=NamedElements.To256(parts.Total)};
        var packet=new PlayerWeaponContactPrepared(flags,impact,new(0,0),cold,0,WeaponMissiles.ItemEffects,
            HitClass:hitClass,FreezeFrames:frozen,HitFlags:hitFlags,SuppressWear:true,DamageKind:kind);
        PlayerWeaponContact.ReleaseMissile(owner,target,packet,at,true);
    }

}
