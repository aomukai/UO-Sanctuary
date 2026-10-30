// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;
namespace Sanctuary;

internal sealed record NpcPoisonSnapshot(uint Actor,uint CreditOwner,MissileSourceStats Stats,int Mastery,int Pierce);
internal sealed record NpcPoisonDelivery(uint Actor,uint CreditOwner,PreparedPoison Poison);

internal static class NpcEquipmentPoison
{
    internal static NpcPoisonSnapshot Capture(SkeletonGuardian actor)
    {
        if(NpcEquipmentCombat.Context(actor) is not{} item||actor.ControlMaster is not PlayerMobile owner)
        {throw new NotSupportedException("No owned original NPC poison equipment");}
        return new(actor.Serial.Value,owner.Serial.Value,item.Poison(),item.Bonus("extra-pois"),Math.Max(0,item.Bonus("pierce-pois")));
    }
    internal static WeaponPoisonRoll Roll(NpcPoisonSnapshot snapshot,Func<int,int> limited)
        =>WeaponPoisonPackets.Melee(snapshot.Stats,snapshot.Mastery,128,limited);
    internal static NpcPoisonDelivery Prepare(SkeletonGuardian actor,Mobile target,NpcPoisonSnapshot snapshot,WeaponPoisonRoll rolled)
    {
        if(rolled.Rate256<=0||rolled.Frames<=0||target is not BaseCreature creature){return null;}
        if(actor.Serial.Value!=snapshot.Actor){throw new InvalidOperationException("NPC poison actor changed");}
        // Read the recipient's resistance/curse/aura pools, then apply only
        // this NPC item's pierce once. Player-owner equipment is not inherited.
        int resistance=NamedElements.Resistance(actor,target,"pois",creature.PoisonResistance,pierceOverride:0);
        var poison=PoisonPackets.Mitigate(rolled.Rate256,rolled.Frames,false,resistance,0,snapshot.Pierce,0,0);
        return poison.Rate256>0&&poison.Frames>0?new(snapshot.Actor,snapshot.CreditOwner,poison):null;
    }
    internal static bool Commit(NpcPoisonDelivery delivery,Mobile target,DateTime at)
    {
        if(delivery==null||World.FindMobile((Serial)delivery.CreditOwner) is not PlayerMobile{Deleted:false} owner
            ||target is not{Alive:true,Deleted:false}||ClassCombat.Friendly(owner,target)){return false;}
        // As with Creeper mats, the real player's durable credit survives pet
        // removal. Rate/mastery/pierce came from the actual NPC, not this owner.
        if(owner.Alive){owner.DoHarmful(target,true);}
        return delivery.Poison.Commit(owner,target,at);
    }
}
