// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;
namespace Sanctuary;

internal sealed record NpcSpectralPayload(uint Actor,uint Owner,SpectralHitRoll Roll,int PoisonPierce);

internal static class NpcSpectralHit
{
    internal static NpcSpectralPayload Capture(SkeletonGuardian actor,Func<int,int> limited)
    {
        if(IronGolemUpgrades.HardRank(actor)<20||actor.ControlMaster is not PlayerMobile{Deleted:false} owner){return null;}
        // Like the encounter's raw Basis,ignore Extra Strong/auras/item-local
        // weapon ED when choosing this independent affix amount.
        var basis=actor.SourceWeaponBase;
        var roll=SpectralHit.Roll(basis.Min,basis.Max,(low,high)=>checked(low+limited(checked(high-low+1))),limited);
        return new(actor.Serial.Value,owner.Serial.Value,roll,NamedElements.PoisonPierce(actor));
    }
    internal static void After(BaseCreature source,Mobile target,NpcSpectralPayload payload,DateTime at)
    {
        if(payload==null||target is not{Alive:true,Deleted:false}||SkillHealth.Balance256(target)<=0){return;}
        if(source.Serial.Value!=payload.Actor){throw new InvalidOperationException("Spectral Hit creator changed");}
        long tick=TimedEffects.TickAt(at);
        // The existing wild affix has no cold caster argument. Preserve its
        // shared survivor-only resistance/CBF/half-length/refresh consumer.
        if(payload.Roll.ColdMs>0){GearEffects.ApplyChill(target,tick,payload.Roll.ColdMs);}
        if(payload.Roll.PoisonTotal>0&&World.FindMobile((Serial)payload.Owner) is PlayerMobile{Deleted:false} owner
            &&!ClassCombat.Friendly(owner,target))
        {
            if(owner.Alive){owner.DoHarmful(target,true);}
            // Calculate using the actual NPC and captured NPC pierce. Durable
            // owner credit follows existing NPC poison,even after pet death.
            GearEffects.ApplyCreditedPoison(source,owner,target,payload.Roll.PoisonTotal,tick,1600,payload.PoisonPierce,false);
        }
    }
    internal static int OrdinaryColdFrames(NpcWeaponRoll rolled)=>rolled.Spectral is{Roll.Channel:3} spectral
        &&rolled.Damage.Elements.Cold<=spectral.Roll.Amount?0:rolled.ColdFrames;
}
