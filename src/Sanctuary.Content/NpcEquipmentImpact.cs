// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server.Mobiles;
namespace Sanctuary;

internal sealed record NpcWeaponImpact(uint Actor,uint Target,PreparedMonsterDamagePacket Packet,PreparedPoison Poison,uint CreditOwner);

// Ordinary equipped NPC-to-NPC allocation. Preparation can consume defensive
// absorb pools through existing mitigation, but does not heal/subtract HP,
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
        var item=NpcEquipmentCombat.Context(actor)??throw new NotSupportedException("No original NPC impact equipment");
        // Source NPC drain fields have different units/health/event semantics
        // from player percentages. Refuse rather than silently dropping them.
        foreach(string code in new[]{"life-steal%","mana-steal%","stamdrain"})
        {if(item.Bonus(code)!=0){throw new NotSupportedException("NPC allocated drain lane remains unverified: "+code);}}
        var rolled=NpcEquipmentDamage.Roll(actor,target,limited,raw)??throw new NotSupportedException("NPC offensive roll binding was lost");
        var damage=rolled.Damage;
        var parts=SkillCombat.MitigatePrepared(actor,target,damage.Elements with{Physical=damage.Physical256/256.0*PaladinAuras.NativeWeaponMultiplier(actor,true)},out int absorbed);
        var poison=damage.Poison is{Rate256:0,Frames:0}?default:NpcEquipmentPoison.Mitigate(actor,target,rolled.Poison,damage.Poison);
        int coldFrames=rolled.ColdFrames<=0?0:ColdStates.LengthFrames(rolled.ColdFrames,NamedElements.Resistance(actor,target,"cold",target.ColdResistance),
            ItemDurationDefenses.CannotFreeze(target),ItemDurationDefenses.HalfFreeze(target));
        return new(actor.Serial.Value,target.Serial.Value,Packet(flags,SkillHealth.Balance256(target),damage,parts,coldFrames,poison,absorbed,rolled.PhysicalCritical),poison,rolled.Poison.CreditOwner);
    }
}
