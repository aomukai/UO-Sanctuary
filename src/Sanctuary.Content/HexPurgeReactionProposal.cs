// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using Server;
using Server.Mobiles;
namespace Sanctuary;

internal sealed record HexPurgeReaction(int RemainingCharges,int MagicMastery,FirestormModifiers Modifiers,ulong Seed);
internal readonly record struct HexPurgeBurst(bool Triggered,int Recipients,int Magic256,int RemainingCharges,string Fault=null);

// Explicit Sanctuary candidate, not recovered callback36/39 behavior. No
// automatic subscription, weapon hook or learned route is installed here.
internal static class HexPurgeReactionProposal
{
    internal static bool Approved=>false;
    internal static HexPurgeReaction Capture(HexPurgePlan plan,int mastery,FirestormModifiers modifiers,ulong seed)
    {var next=new HexPurgeReaction(plan.InitialCharges,mastery,modifiers,seed);Validate(plan,next);return next;}
    internal static void Validate(HexPurgePlan plan,HexPurgeReaction reaction)
    {
        static bool Mastery(int n)=>n is >=-100 and <=1000000;
        if(reaction==null||reaction.RemainingCharges<1||reaction.RemainingCharges>plan.InitialCharges||reaction.Seed==0
            ||!Mastery(reaction.MagicMastery)||reaction.Modifiers is not{} m||m.Potion is <0 or >1000000
            ||!Mastery(m.FireMastery)||!Mastery(m.ColdMastery)||!Mastery(m.LightningMastery)||!Mastery(m.PoisonMastery))
        {throw new InvalidDataException("Invalid Purge captured reaction");}
    }
    // The caller explicitly qualifies a landed weapon event or hextrigger.
    // Same-owner/equal-rank refresh keeps the old snapshot and spent charges.
    internal static bool AttachPrepared(PlayerMobile owner,BaseCreature target,HexPurgeBuff registration,HexPurgePlan plan,
        HexPurgeReaction capture,bool eligible,DateTime at)
    {
        if(!eligible||at.Kind!=DateTimeKind.Utc||registration==null||registration!=HexPurge.Buff(owner)
            ||registration.Rank!=plan?.Rank||registration.UntilUtc<=at||!WeaponHexes.Equipped(owner)
            ||owner.Map==null||owner.Map==Map.Internal||owner.Map!=target?.Map||!ClassCombat.IsHostile(owner,target)
            ||!SourceAuraTargets.Eligible(target)){return false;}
        Validate(plan,capture);if(capture.RemainingCharges!=plan.InitialCharges){throw new InvalidDataException("New Purge attachment has spent charges");}
        var old=HexPurge.Debuff(target);
        if(old!=null&&old.UntilUtc>at)
        {
            if(old.Plan.Rank>plan.Rank||old.Plan.Rank==plan.Rank&&old.Creator!=owner.Serial.Value){return false;}
            if(old.Plan.Rank==plan.Rank&&old.Reaction!=null)
            {HexPurge.StoreReactionPrepared(old with{UntilUtc=at.AddMilliseconds(old.Plan.DebuffFrames*40L)});return true;}
        }
        var next=new HexPurgeTarget(target.Serial.Value,owner.Serial.Value,at.AddMilliseconds(plan.DebuffFrames*40L),plan){Reaction=capture};
        // Validate the complete record before changing either Hex group.
        HexPurge.StoreReactionPrepared(next);
        WeaponHexGroups.ReplaceTarget(target,CombatAbility.HexPurge);
        target.FixedEffect(0x375A,10,15,0x48E,0); // Identified curse attachment placeholder.
        return true;
    }
    private static bool Active(PlayerMobile owner,BaseCreature target,HexPurgeTarget state,DateTime at,bool? actions)
        =>at.Kind==DateTimeKind.Utc&&state.UntilUtc>at&&state.Reaction!=null&&owner is{Alive:true,Deleted:false}
            &&ClassCreation.Profiles.TryGet(owner.Serial,out var profile)&&profile.Class==DiabloClass.Warlock
            &&target is{Alive:true,Deleted:false}&&owner.Map!=null&&owner.Map!=Map.Internal&&owner.Map==target.Map
            &&(actions??owner.NetState is{Running:true})&&ClassCombat.IsHostile(owner,target);
    internal static bool ManagedPrepared(Mobile target)=>target!=null&&HexPurge.Debuff(target)?.Reaction!=null;
    internal static int Enhanced(int raw,int mastery)=>(int)Math.Clamp((long)raw+(long)raw*mastery/100,0,int.MaxValue);
    private static bool Recipient(PlayerMobile owner,BaseCreature target,Map map,Point3D center,int radius)
        =>target is{Alive:true,Deleted:false}&&SourceDamageAreas.Recipient(owner,target,map.MapID,center.X*2,center.Y*2,center.Z,radius,true,center);
    internal static HexPurgeBurst OnDamagePrepared(SkillDamageContact contact,DateTime at,bool? actions=null)
    {
        if(contact.Damage256<=0||contact.Target is not BaseCreature target||HexPurge.Debuff(target) is not{} state
            ||World.FindMobile((Serial)state.Creator)is not PlayerMobile owner||!Active(owner,target,state,at,actions)
            ||!CombatItemEvents.Allowed(contact.Source,target)){return default;}
        var reaction=state.Reaction;Validate(state.Plan,reaction);ulong seed=reaction.Seed;
        int chance=Math.Clamp(state.Plan.ChanceRaw,0,100);uint roll=SourceRandom.Limited(ref seed,100);
        if(roll>=chance)
        {HexPurge.AdvanceReactionPrepared(state,state with{Reaction=reaction with{Seed=seed}});return new(false,0,0,reaction.RemainingCharges);}
        int magic=SourceSpellPacketRoll.Range(ref seed,Enhanced(state.Plan.Minimum256,reaction.MagicMastery),Enhanced(state.Plan.Maximum256,reaction.MagicMastery));
        var map=target.Map;var center=target.Location;var recipients=new List<BaseCreature>();
        try
        {
            foreach(var recipient in SkillCombat.NearbySnapshot<BaseCreature>(map,center,(int)Math.Ceiling(SkillGeometry.Tiles(state.Plan.RadiusRaw))))
            {
                if(!Recipient(owner,recipient,map,center,state.Plan.RadiusRaw)){continue;}
                TaintedFireBallMissiles.RequireOrdinaryRecipient(owner,recipient);recipients.Add(recipient);
            }
        }
        catch(NotSupportedException ex){return new(false,0,0,reaction.RemainingCharges,ex.Message);}
        int remaining=reaction.RemainingCharges-1;
        if(!HexPurge.AdvanceReactionPrepared(state,remaining==0?null:state with{Reaction=reaction with{RemainingCharges=remaining,Seed=seed}})){return default;}
        // Claim the charge before any nested health/death callback. One shared
        // roll per burst; its visual missile717 is not a second damage actor.
        var raw=DruidMoltenBoulder.Potion(reaction.Modifiers,new GearDamage(0,0,0,0,0,Magic:magic/256.0));int applied=0;
        foreach(var recipient in recipients)
        {
            if(!Recipient(owner,recipient,map,center,state.Plan.RadiusRaw)){continue;}
            using(CombatItemEvents.Suppress(owner,recipient))
            {
                owner.DoHarmful(recipient,true);var parts=SkillCombat.MitigatePrepared(owner,recipient,raw);
                SkillHealth.Damage(owner,recipient,NamedElements.To256(parts.Total));
                if(!recipient.Deleted){recipient.FixedEffect(0x375A,5,5,0x48E,0);} // Identified explosion placeholder.
            }
            applied++;
        }
        return new(true,applied,magic,remaining);
    }
}
