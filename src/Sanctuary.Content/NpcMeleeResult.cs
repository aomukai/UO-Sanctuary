// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;
namespace Sanctuary;

// Shared Kind1 hit/block result binding. Packet allocation and contact remain
// the existing ordered services; caller supplies live geometry and hostility.
internal static class NpcMeleeResult
{
    private sealed class Services(BaseCreature source,BaseCreature target,Func<int,bool> range,Func<bool> hostile,Func<int,int> limited,bool guaranteed):SourceMeleeResultServices
    {
        internal override int? AttackerKind=>1;
        internal override int? DefenderKind=>1;
        internal override bool Hostile()=>hostile();
        internal override bool DefenderRunning()=>throw new InvalidOperationException("NPC result cannot query player running mode");
        internal override int MeleeReach()=>0;
        internal override bool InRange(int bonus)=>range(bonus);
        internal override bool HitSuccessful(int attackPercent,bool missile)
        {
            if(missile){throw new NotSupportedException("Ordinary melee cannot use a missile hit check");}
            if(guaranteed){return true;}
            return ClassStats.CheckHit(source,target,attackPercent,()=>limited(100)/100.0,notifyMelee:false,npcDefense:false)
                ??throw new NotSupportedException("Missing ordinary NPC hit supplier");
        }
        internal override int BlockOrDodge(bool avoid,bool block)
        {
            if(!NpcBlockOrDodge.Try(target,avoid,block,out int flags,out _))
            {throw new NotSupportedException("Ordinary NPC recipient block supplier is not mapped");}
            return flags;
        }
        internal override bool Uninterruptible()=>MonsterUninterruptibles.Active(target);
    }
    internal static int Resolve(BaseCreature source,BaseCreature target,Func<int,bool> range,Func<bool> hostile,Func<int,int> limited,int attackPercent=0,bool guaranteed=false)
    {
        ArgumentNullException.ThrowIfNull(range);ArgumentNullException.ThrowIfNull(hostile);ArgumentNullException.ThrowIfNull(limited);
        // Preserve existing component appearance/block admission before actor hit RNG.
        if(!SourceBlocking.TryRate(target,out _,out _)){throw new NotSupportedException("Ordinary NPC recipient block supplier is not mapped");}
        return SourceMeleeResult.Resolve(new Services(source,target,range,hostile,limited,guaranteed),attackPercent);
    }
}
