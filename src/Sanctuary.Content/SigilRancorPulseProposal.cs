// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Linq;
using Server;
using Server.Mobiles;
namespace Sanctuary;

internal readonly record struct RancorPulseResult(bool Claimed,int Recipients,int Successes);

// Approved Sanctuary adaptation for protected do61; see the bound approval.
// Reuse the existing Sigil driver and saved once-per-frame claim.
internal static class SigilRancorPulseProposal
{
    internal static bool Approved=>true;
    public static void Configure(){if(Approved){SigilActors.Pulse+=Pulse;}}
    internal static void Pulse(SigilActor actor,PlayerMobile owner,DateTime at)=>PulsePrepared(actor,owner,at);
    private static bool Recipient(PlayerMobile owner,Item marker,BaseCreature target,int radius)=>
        target is{Alive:true,Deleted:false}&&target.Map==marker.Map&&target.CanBeDamaged()
        &&ClassCombat.IsHostile(owner,target)&&SourceAuraTargets.Eligible(target)&&NecromancerEffects.AiCurse(target)
        &&!Server.Spells.SpellHelper.IsTown(target.Location,target)&&AmazonInnerSight.InRadius(target.X-marker.X,target.Y-marker.Y,radius)
        &&Math.Abs(target.Z-marker.Z)<16;
    internal static RancorPulseResult PulsePrepared(SigilActor actor,PlayerMobile owner,DateTime at,Func<int> initialSeed=null)
    {
        if(actor.Ability!=CombatAbility.SigilRancor||at.Kind!=DateTimeKind.Utc||owner is not{Alive:true,Deleted:false}
            ||actor.Owner!=owner.Serial.Value||World.FindItem((Serial)actor.Marker)is not{Deleted:false} marker
            ||owner.Map!=marker.Map||marker.Map.MapID!=actor.Map||marker.Location!=actor.Origin||MinionSafeAreas.Contains(owner)) {return default;}
        var values=SigilRancorPrimary.Capture(SigilActors.Values(actor));
        if(values.RawDurationFrames!=12||values.AiChance!=75){throw new NotSupportedException("Rancor candidate requires the pinned chance and duration");}
        if(!SigilActors.ClaimRancorPulse(actor)){return default;}
        var targets=SkillCombat.NearbySnapshot<BaseCreature>(marker.Map,marker.Location,(int)Math.Ceiling(SkillGeometry.Tiles(values.RawRadius)));
        targets.Sort((a,b)=>a.Serial.CompareTo(b.Serial));int accepted=0,confused=0;
        foreach(var target in targets)
        {
            if(!Recipient(owner,marker,target,values.RawRadius)){continue;}
            var state=NecromancerEffects.Profiles.Find(target)??new();var old=SigilRancorPrimary.State(target,at);
            if(old!=null&&old.Rank>values.Rank){continue;}
            ulong seed=state.RancorMind?.Seed??SourceRandom.InitLow(initialSeed?.Invoke()??unchecked((int)Random.Shared.NextInt64(1L<<32)));
            uint roll=SourceRandom.Limited(ref seed,100);var until=at.AddMilliseconds(values.RawDurationFrames*40L);
            var primary=old!=null&&old.Rank==values.Rank?old with{UntilUtc=until}:
                new RancorPrimaryState(owner.Serial.Value,values.Rank,values.DamagePercent,values.AttackRate,until);
            var mind=state.RancorMind??new MindAiState(seed,actor.Map,DateTime.UnixEpoch,0);
            bool success=roll<(uint)values.AiChance;
            mind=success?mind with{Seed=seed,Map=actor.Map,UntilUtc=until,Mode=3}:mind with{Seed=seed};
            NecromancerEffects.Profiles.Store(target,state with{RancorPrimary=primary,RancorMind=mind});
            if(success){target.ForceReacquire();confused++;}
            accepted++;
        }
        return new(true,accepted,confused);
    }
    internal static bool ExpirePrepared(Mobile actor,DateTime at)
    {
        if(at.Kind!=DateTimeKind.Utc){throw new ArgumentException("Rancor expiry requires UTC",nameof(at));}
        var state=NecromancerEffects.Profiles.Find(actor);if(state==null){return false;}
        bool primary=state.RancorPrimary is{} p&&p.UntilUtc<=at;
        bool mind=state.RancorMind is{} m&&m.UntilUtc>DateTime.UnixEpoch&&m.UntilUtc<=at;
        if(!primary&&!mind){return false;}
        bool ordinary=state.Mind is{Mode:2 or 3} o&&o.UntilUtc>at||SigilRancorMind.Suppressed(actor,state,at)
            ||PaladinConversion.Active(actor)||actor is BaseCreature{Controlled:true} or BaseCreature{Summoned:true};
        var next=state with{RancorPrimary=primary?null:state.RancorPrimary,
            RancorMind=(primary||mind)&&state.RancorMind is{} old?old with{UntilUtc=DateTime.UnixEpoch,Mode=0,Focus=0}:state.RancorMind};
        NecromancerEffects.Profiles.Store(actor,next);
        bool ownedAi=state.RancorMind is{} lease&&lease.UntilUtc>DateTime.UnixEpoch;
        if(ownedAi&&!ordinary&&actor is BaseCreature npc)
        {
            npc.Combatant=null;npc.FocusMob=null;npc.ForceReacquire();
            if(npc.AIObject is{} ai){ai.ResetApproachState();ai.ClearMoveIntent();ai.Action=ActionType.Wander;}
        }
        return true;
    }
}
