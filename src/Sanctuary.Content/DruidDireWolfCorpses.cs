// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Items;
using Server.Mobiles;
namespace Sanctuary;

internal readonly record struct DireWolfConsumeResult(bool Consumed,bool RageApplied);

internal static class DruidDireWolfCorpses
{
    // NPC314 release has no new range test. The AI and animation event own
    // acquisition/reach; retain the shared world/access safeguards here.
    // Rage allocation is an explicit service until the actor stat-list/mode
    // consumer is connected. Its failure must not restore an eaten corpse.
    internal static DireWolfConsumeResult Consume(DruidDireWolfBody actor,Corpse corpse,Func<bool> applyRage)
    {
        ArgumentNullException.ThrowIfNull(applyRage);
        if(actor is not{Alive:true,Deleted:false,Prepared:{} state}
            || actor.ControlMaster is not PlayerMobile owner || state.Owner!=owner.Serial.Value
            || !CorpseSkillRules.DireWolfUsableAt(owner,corpse,actor,double.PositiveInfinity)
            || !CorpseSkillRules.Reserve(corpse))
        {return default;}
        // Same main-loop transaction as other corpse skills: reserve first,
        // preserve contained loot, consume before attempting the self buff.
        NecromancerCombat.Consume(corpse);
        return new(true,applyRage());
    }
}
