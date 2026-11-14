// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;
namespace Sanctuary;

// Approved attachment/reaction order; use the existing landed-weapon and
// post-mitigation, pre-health boundaries rather than a separate damage path.
internal static class HexPurgeEvents
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(HexPurgeEvents));
    internal static Func<ulong> InitialSeed=()=>SourceRandom.InitLow(unchecked((int)Random.Shared.NextInt64(1L<<32)));
    public static void Configure()
    {
        if(!HexPurgeReactionProposal.Approved){return;}
        WeaponLandedEvents.BeforeDamage+=Landed;SkillDamageEvents.BeforeDirectDamage+=Damage;
    }
    private static bool Apply(Mobile source,Mobile target)=>WeaponHexDelivery.Apply(source,target,static(owner,npc)=>
    {
        if(HexPurge.Buff(owner)is not{} registered){return false;}
        var values=SkillMechanics.Values(owner,CombatAbility.HexPurge,registered.Rank);var plan=HexPurge.Plan(values);
        var old=HexPurge.Debuff(npc);
        if(old!=null&&(old.Plan.Rank>plan.Rank||old.Plan.Rank==plan.Rank&&old.Creator!=owner.Serial.Value)){return false;}
        // Refresh keeps the original snapshot/stream; do not draw another birth
        // seed just because an equal-rank landed contact refreshes its lease.
        ulong seed=old is{Reaction:not null}&&old.Plan.Rank==plan.Rank?old.Reaction.Seed:InitialSeed();
        var capture=HexPurgeReactionProposal.Capture(plan,NamedElements.MasteryPercent(owner,"mag"),DruidFirestorm.Modifiers(owner),seed);
        return HexPurgeReactionProposal.AttachPrepared(owner,npc,registered,plan,capture,true,TimedEffects.Now);
    });
    internal static void Landed(WeaponLandedContact contact)=>Apply(contact.Source,contact.Target);
    internal static bool QualifiedHexTrigger(Mobile source,Mobile target,bool qualified)=>qualified&&Apply(source,target);
    internal static void Damage(SkillDamageContact contact)
    {
        var result=HexPurgeReactionProposal.OnDamagePrepared(contact,TimedEffects.Now);
        if(result.Fault!=null){Log.Warning("Purge reaction unsupported for {Target}: {Reason}",contact.Target.Serial.Value,result.Fault);}
    }
}
