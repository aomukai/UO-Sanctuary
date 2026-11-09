// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using Server;
using Server.Mobiles;
namespace Sanctuary;

// Prepared independent AI lease. Eligibility, expiry and initial stream are
// explicit inputs, not inferred current do61 chance/pulse behavior. Selection
// itself reuses SourceMind, including its native acquisition clock and RNG.
internal static class SigilRancorMind
{
    internal static void Validate(MindAiState mind)
    {
        SourceMind.Validate(mind);
        if(mind.Mode is not(0 or 3)||mind.Seed==0){throw new InvalidDataException("Invalid Rancor mind lease");}
    }
    internal static bool Active(Mobile actor,NecromancerState state,DateTime at)
    {
        if(actor is not BaseCreature{Alive:true,Deleted:false,Controlled:false,Summoned:false}
            ||state?.RancorMind is not{Mode:0 or 3} mind||mind.UntilUtc<=at||mind.Map!=actor.Map?.MapID
            ||SigilRancorPrimary.State(actor,at)==null||PaladinConversion.Active(actor)) {return false;}
        // Existing curse and item AI retain priority. A non-AI curse such as
        // Weaken remains independent and does not erase this prepared lease.
        return !Suppressed(actor,state,at);
    }
    internal static bool Suppressed(Mobile actor,NecromancerState state,DateTime? at=null)
    {
        var time=at??TimedEffects.Now;var curse=state.Curse is{} c&&c.UntilUtc>time?c:null;
        return curse?.Ability is CombatAbility.Confuse or CombatAbility.Attract or CombatAbility.DimVision
            or CombatAbility.Terror or CombatAbility.GrimWard or CombatAbility.Taunt or CombatAbility.CloakOfShadows
            ||state.ItemTerror!=null||NamedControl.State(actor)is{} fear&&fear.FearUntil>time;
    }
    internal static bool ApplyPrepared(PlayerMobile owner,BaseCreature target,DateTime until,ulong seed,bool eligible)
    {
        if(!eligible||owner is not{Alive:true,Deleted:false}||target is not{Alive:true,Deleted:false}
            ||owner.Map==null||owner.Map==Map.Internal||owner.Map!=target.Map||!NecromancerEffects.AiCurse(target)||PaladinConversion.Active(target)
            ||SigilRancorPrimary.State(target) is not{} primary||primary.Creator!=owner.Serial.Value||until<=TimedEffects.Now){return false;}
        var next=new MindAiState(seed,target.Map.MapID,until);Validate(next);
        var old=NecromancerEffects.Profiles.Find(target);
        if(old.RancorMind is{} active&&active.UntilUtc>TimedEffects.Now)
        {throw new NotSupportedException("Rancor AI refresh/chance policy remains unverified");}
        NecromancerEffects.Profiles.Store(target,old with{RancorMind=next});target.ForceReacquire();return true;
    }
    internal static void Clear(BaseCreature target)
    {
        if(NecromancerEffects.Profiles.Find(target)is{} state)
        {NecromancerEffects.Profiles.Store(target,state with{RancorMind=null});target.ForceReacquire();}
    }
}
