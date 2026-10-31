// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using Server;
using Server.Mobiles;

namespace Sanctuary;

// Sanctuary T29 extension, not a D2 poison stat. A zero-rate exposure keeps
// elemental ticks eligible without setting the poison life-mask or status.
public sealed record SpellPoisonExposure(uint Source,int RawRate256,int Rate256,int Resistance,
    int RemainingMs,int ElapsedMs,int FireMastery,int ColdMastery,int LightningMastery,int PoisonMastery);

internal static class SpellPoisonTicks
{
    internal static PreparedPoison Prepare(PlayerMobile source,Mobile target,int rawRate256,int frames)
    {
        int resistance=NamedElements.Resistance(source,target,"pois",target.PoisonResistance);
        return PoisonPackets.Mitigate(rawRate256,frames,false,resistance,ItemDurationDefenses.PoisonLength(target),0,0,0);
    }
    internal static bool Apply(PlayerMobile source,Mobile target,int rawRate256,int frames,FirestormModifiers mods,DateTime at)
    {
        if(source is not{Alive:true,Deleted:false}||target is not{Alive:true,Deleted:false,Blessed:false,IsDeadBondedPet:false}
            ||ClassCombat.Friendly(source,target)||rawRate256<=0||frames is <=0 or >int.MaxValue/40||mods==null){return false;}
        // The player poison floor versus lethal elemental additions needs a
        // separately verified composition. Do not silently choose one here.
        if(target is PlayerMobile){throw new NotSupportedException("Pure poison spell bonus ticks against players are not verified");}
        int resistance=NamedElements.Resistance(source,target,"pois",target.PoisonResistance);
        var prepared=Prepare(source,target,rawRate256,frames);
        if(prepared.Frames<=0){return false;}
        TimedEffects.Settle(target,at);if(!target.Alive||target.Deleted){return false;}
        if(prepared.Rate256>0)
        {
            if(!TimedEffects.PoisonFrames(source,target,prepared.Rate256,prepared.Frames,at)){return false;}
        }
        else if(TimedEffects.TryActor(target,out var existing)&&existing.PoisonMs>0){return false;}
        var actor=TimedEffects.Actor(target,at);
        actor.SpellPoison=new(source.Serial.Value,rawRate256,prepared.Rate256,resistance,prepared.Frames*40,0,
            mods.FireMastery,mods.ColdMastery,mods.LightningMastery,mods.PoisonMastery);
        return true;
    }

    // Called by the shared effect clock before its poison health payment.
    // The scheduler caps these steps at40ms, so at most one frame is due.
    internal static int Advance(Mobile target,TimedActor actor,int elapsed)
    {
        if(actor.SpellPoison is not{} exposure){return 0;}
        int step=Math.Min(elapsed,exposure.RemainingMs),phase=exposure.ElapsedMs+step;
        var next=exposure with{RemainingMs=exposure.RemainingMs-step,ElapsedMs=phase%40};
        actor.SpellPoison=next.RemainingMs>0?next:null;
        if(phase<40){return 0;}
        if(phase>=80){throw new InvalidOperationException("Spell poison clock skipped a frame");}
        if(World.FindMobile((Serial)exposure.Source) is not PlayerMobile source){return 0;}
        int potion=CombatPotions.BonusAt(source,"magical-penetration",TimedEffects.DamageTime);
        if(potion<=0){return 0;}
        int Extra(int mastery)=>(int)Math.Clamp(decimal.Truncate((long)exposure.RawRate256*potion/100)*Math.Max(0,100L+mastery)/100,0,int.MaxValue);
        var packet=new GearDamage(0,Extra(exposure.FireMastery)/256.0,Extra(exposure.ColdMastery)/256.0,
            Extra(exposure.LightningMastery)/256.0,0);
        var parts=SkillCombat.MitigatePrepared(source,target,packet,out int healing);
        if(healing>0){SkillHealth.Heal(target,healing);}
        // Combine original and added poison before its resistance percentage,
        // retaining the original poison's captured rate/length defenses.
        int combined=(int)Math.Min(int.MaxValue,(long)exposure.RawRate256+Extra(exposure.PoisonMastery));
        int poison=PoisonPackets.Mitigate(combined,1,false,exposure.Resistance,0,0,0,0).Rate256;
        return (int)Math.Min(int.MaxValue,(long)NamedElements.To256(parts.Total)+Math.Max(0,poison-exposure.Rate256));
    }

    internal static void Validate(SpellPoisonExposure s,TimedEffectState row)
    {
        if(s==null){return;}
        if(!((Serial)s.Source).IsMobile||s.RawRate256<=0||s.Rate256<0||s.Resistance is <-100 or >200
            ||s.RemainingMs<=0||s.ElapsedMs is <0 or >39
            ||s.Rate256!=PoisonPackets.Mitigate(s.RawRate256,1,false,s.Resistance,0,0,0,0).Rate256
            ||s.Rate256>0&&(row.PoisonClock==null||row.PoisonClock.Rate256!=s.Rate256||row.PoisonMs!=s.RemainingMs||row.PoisonClock.ElapsedMs!=s.ElapsedMs)
            ||s.Rate256==0&&row.PoisonMs>0)
        {throw new InvalidDataException("Invalid spell poison exposure");}
    }
}
