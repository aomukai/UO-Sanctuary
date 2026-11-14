// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Items;
using Server.Mobiles;
namespace Sanctuary;

internal static class SigilDeathEvents
{
    public static void Configure(){if(SigilDeathProposal.Approved){SigilActors.Pulse+=Pulse;}}
    // Configure installs the shared corpse capture/reward observer first.
    // Multicast delivery then queues our mark before native cleanup returns.
    public static void Initialize(){if(SigilDeathProposal.Approved){ClassLootHooks.CreatureDied+=Death;}}
    internal static void Pulse(SigilActor actor,PlayerMobile owner,DateTime at){if(actor.Ability==CombatAbility.SigilDeath){SigilDeathProposal.PulsePrepared(actor,owner,at);}}
    internal static void Death(BaseCreature actor,Container corpse)=>SigilDeathProposal.QueuePrepared(actor,corpse,TimedEffects.Now);
}
