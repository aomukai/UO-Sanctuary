// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server.Mobiles;
namespace Sanctuary;

internal sealed record NpcWeaponImpact(uint Actor,uint Target,PreparedMonsterDamagePacket Packet,PreparedPoison Poison,uint CreditOwner,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] NpcSpectralPayload Spectral=null);

// Ordinary equipped NPC-to-NPC allocation. Preparation can consume defensive
// absorb pools and mutate base armor through existing setup, but does not heal/subtract HP,
// apply cold/poison states or dispatch contact/return/death events.
internal static class NpcEquipmentImpact
{
    private const int NoFill=128|256|512|32768;
    internal static bool Fills(int flags)=>(flags&1)!=0&&(flags&NoFill)==0;
    internal static PreparedMonsterDamagePacket Packet(int flags,int targetLife256,NpcCriticalPacket rolled,
        GearDamage parts,int coldFrames,PreparedPoison poison,int absorbed256,bool physicalCritical=false)
    {
        if((flags&~0xffff)!=0||targetLife256<0||coldFrames<0||poison.Rate256<0||poison.Frames<0||absorbed256<0)
        {throw new ArgumentOutOfRangeException();}
        if(!Fills(flags)){return new(0,0,0,0,flags);}
        ArgumentNullException.ThrowIfNull(rolled);
        int physical=NamedElements.To256(parts.Physical),fire=NamedElements.To256(parts.Fire),lightning=NamedElements.To256(parts.Lightning),
            magic=NamedElements.To256(parts.Magic),cold=NamedElements.To256(parts.Cold);
        if(parts.Physical<0||parts.Fire<0||parts.Lightning<0||parts.Magic<0||parts.Cold<0||parts.Poison!=0)
        {throw new NotSupportedException("NPC impact needs nonnegative channels and a separate source poison rate");}
        // CalculateTotalDamage includes the mitigated poison RATE once, even
        // when zero length prevents a later poison state. Periodic frames are
        // not deducted from this total and are scheduled separately on release.
        int total=checked(physical+fire+lightning+magic+cold+poison.Rate256);
        if(physicalCritical){flags|=8192;}
        if((total&~255)>(targetLife256&~255)){flags|=2;}
        return new(physical,total,0,coldFrames,flags,rolled.HitClass,false,rolled.Overlay,magic,fire,lightning,cold,poison.Rate256,32,absorbed256);
    }
    internal static NpcWeaponImpact Prepare(SkeletonGuardian actor,BaseCreature target,int flags,Func<int,int> limited=null,Func<uint> raw=null)
    {
        if(actor is not{Alive:true,Deleted:false}||target is not{Alive:true,Deleted:false}||(flags&~0xffff)!=0)
        {throw new ArgumentException("NPC impact allocation requires existing living actors and source result flags");}
        if(!Fills(flags)){return new(actor.Serial.Value,target.Serial.Value,Packet(flags,0,null,default,0,default,0),default,0);}
        var item=NpcEquipmentCombat.Context(actor);
        if(item==null&&actor is not ISourceUnarmedNpc){throw new NotSupportedException("No original NPC impact equipment");}
        // Ordinary current item minima do not create a positive NPC drain
        // field. Positive maxima/skill-supplied fields remain separate work.
        item?.RequireOrdinaryNpcDrainInputs();
        NpcArmorOnHit.Apply(actor,target);
        var rolled=NpcEquipmentDamage.Roll(actor,target,limited,raw)??throw new NotSupportedException("NPC offensive roll binding was lost");
        return FromRoll(actor,target,flags,rolled,physicalMultiplier:PaladinAuras.NativeWeaponMultiplier(actor,true),armorPrepared:true);
    }
    internal static NpcWeaponImpact FromRoll(BaseCreature actor,BaseCreature target,int flags,NpcWeaponRoll rolled,
        Func<string,int?> pierceOverride=null,double physicalMultiplier=1,bool armorPrepared=false)
    {
        if(actor is not{Alive:true,Deleted:false}||target is not{Alive:true,Deleted:false}||rolled==null||rolled.Actor!=actor.Serial.Value||rolled.Target!=target.Serial.Value
            ||!Fills(flags)||(flags&~0xffff)!=0||physicalMultiplier<0||!double.IsFinite(physicalMultiplier))
        {throw new ArgumentException("NPC mitigated allocation requires a matching successful offensive roll");}
        if(!armorPrepared&&NpcArmorOnHit.Delta(actor)!=0)
        {throw new NotSupportedException("Rolled NPC packet needs its earlier source-ordered armor setup");}
        var damage=rolled.Damage;
        var parts=SkillCombat.MitigatePrepared(actor,target,damage.Elements with{Physical=damage.Physical256/256.0*physicalMultiplier},out int absorbed,pierceOverride:pierceOverride);
        var poison=damage.Poison is{Rate256:0,Frames:0}?default:NpcEquipmentPoison.Mitigate(actor,target,rolled.Poison,damage.Poison);
        int coldFrames=rolled.ColdFrames<=0?0:ColdStates.LengthFrames(rolled.ColdFrames,NamedElements.Resistance(actor,target,"cold",target.ColdResistance,pierceOverride?.Invoke("cold")),
            ItemDurationDefenses.CannotFreeze(target),ItemDurationDefenses.HalfFreeze(target));
        return new(actor.Serial.Value,target.Serial.Value,Packet(flags,SkillHealth.Balance256(target),damage,parts,coldFrames,poison,absorbed,rolled.PhysicalCritical),poison,rolled.Poison.CreditOwner,rolled.Spectral);
    }
    // Full ordinary fill allocation reaches the existing mitigation/poison and
    // prepared HP/event receiver. Caller facts remain explicit; do not infer
    // owner pierce,source mode or unimplemented status consumers.
    internal static NpcWeaponImpact FromFill(BaseCreature actor,BaseCreature target,NpcFillPacket fill,NpcPoisonSnapshot poisonSource,
        Func<string,int?> pierceOverride=null,double physicalMultiplier=1)
    {
        ArgumentNullException.ThrowIfNull(fill);ArgumentNullException.ThrowIfNull(poisonSource);
        if(actor is not{Alive:true,Deleted:false}||target is not{Alive:true,Deleted:false}||actor is BaseHire
            ||poisonSource.Actor!=actor.Serial.Value||(fill.ResultFlags&~0xffff)!=0||!Fills(fill.ResultFlags)||physicalMultiplier<0||!double.IsFinite(physicalMultiplier))
        {throw new NotSupportedException("Full ordinary impact requires qualified non-hireling NPC and matching source poison context");}
        if(fill.FreezeFrames!=0||fill.BurnFrames!=0||(fill.HitFlags&(256|512|1024))!=0)
        {throw new NotSupportedException("Full ordinary freeze/burn and bypass-resistance allocation needs its recipient adapter");}
        if(fill.Physical<0||fill.Fire<0||fill.Lightning<0||fill.Cold<0||fill.Magic<0||fill.Poison<0||fill.PoisonFrames<0||fill.ColdFrames<0||fill.StunFrames<0
            ||fill.LifeDrain<0||fill.ManaDrain<0||fill.StaminaDrain<0)
        {throw new NotSupportedException("Full ordinary impact requires nonnegative allocated channels/lengths/drains");}
        var parts=SkillCombat.MitigatePrepared(actor,target,new(fill.Physical/256.0*physicalMultiplier,fill.Fire/256.0,fill.Cold/256.0,fill.Lightning/256.0,0,Magic:fill.Magic/256.0),out int absorb,pierceOverride:pierceOverride);
        var poison=NpcEquipmentPoison.Mitigate(actor,target,poisonSource,new(fill.Poison,fill.PoisonFrames));
        int cold=ColdStates.LengthFrames(fill.ColdFrames,NamedElements.Resistance(actor,target,"cold",target.ColdResistance,pierceOverride?.Invoke("cold")),ItemDurationDefenses.CannotFreeze(target),ItemDurationDefenses.HalfFreeze(target));
        int physical=NamedElements.To256(parts.Physical),fire=NamedElements.To256(parts.Fire),lightning=NamedElements.To256(parts.Lightning),magic=NamedElements.To256(parts.Magic),ice=NamedElements.To256(parts.Cold);
        int total=checked(physical+fire+lightning+magic+ice+poison.Rate256+fill.LifeDrain),flags=fill.ResultFlags;
        if((total&~255)>(SkillHealth.Balance256(target)&~255)){flags|=2;}
        var packet=new PreparedMonsterDamagePacket(physical,total,fill.StunFrames,cold,flags,fill.HitClass,false,fill.Overlay,magic,fire,lightning,ice,poison.Rate256,fill.HitFlags,absorb,fill.LifeDrain,fill.ManaDrain,fill.StaminaDrain);
        return new(actor.Serial.Value,target.Serial.Value,packet,poison,poisonSource.CreditOwner);
    }

}
