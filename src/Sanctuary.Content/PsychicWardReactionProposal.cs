// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;
namespace Sanctuary;

// Approved Sanctuary incoming rule,not recovered event37 semantics.
// Each damage packet owns its ticket;there is no mutable global hit cache.
internal static class PsychicWardReactionProposal
{
    internal static bool Approved=>true;
    internal static void RequireApproval()
    {if(!Approved){throw new NotSupportedException("Psychic Ward incoming-event proposal awaits approval");}}
    internal sealed class Ticket(PlayerMobile owner,BaseCreature attacker,PsychicWardState registered)
    {
        private bool _consumed;
        internal bool Deliver(Mobile source,Mobile target,bool ranged,DateTime at)
        {
            if(_consumed){return false;}_consumed=true;
            if(source!=attacker||target!=owner||ranged||at.Kind!=DateTimeKind.Utc
                ||attacker is not{Alive:true,Deleted:false}||attacker.Map!=owner.Map){return false;}
            return PsychicWard.ReactPrepared(owner,registered,attacker,true,at);
        }
    }
    internal static Ticket Capture(Mobile attacker,Mobile defender,int rawPhysical256,bool melee,bool successfulContact)
    {
        if(!melee||!successfulContact||rawPhysical256<=0||attacker is not BaseCreature{Alive:true,Deleted:false} npc
            ||defender is not PlayerMobile{Alive:true,Deleted:false} owner||attacker.Map!=owner.Map
            ||!ClassCombat.IsHostile(owner,attacker)||PsychicWard.State(owner) is not{} registered){return null;}
        return new(owner,npc,registered);
    }
}
