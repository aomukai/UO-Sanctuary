// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Server;
using Server.Mobiles;
namespace Sanctuary;

// Narrow bridge from the existing golem item-event graph to the shared NPC
// missile host. This does not admit other spells or generated-NPC stat graphs.
internal static class IronGolemFireBall
{
    internal static bool Attached(IronGolemBody body,PlayerMobile owner)
        =>body?.Prepared is{Controller:not null} state&&state.Recipe.Owner==owner?.Serial.Value
            &&IronGolemItems.State(body)is{} binding&&binding.Item==state.Recipe.Item&&binding.Owner==state.Recipe.Owner
            &&World.FindItem((Serial)binding.Item)is{Deleted:false} item&&item.Parent==body;
    internal static string Binding(IronGolemBody body)
        =>Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(body.Prepared.Recipe))));
    internal static NpcItemSpellPacket Capture(IronGolemBody body,int skill,int rank)
    {
        if(skill!=47||!Attached(body,body.ControlMaster as PlayerMobile))
        {throw new NotSupportedException("Iron Golem item missile adapter supports Fire Ball only");}
        var item=NpcEquipmentCombat.Context(body)??throw new NotSupportedException("Fire Ball needs its active original item stats");
        return NpcItemSpellDamage.Calculate(skill,rank,body.Prepared.Recipe.OwnerLevel,item.Bonus("extra-fire"),
            item.Bonus("pierce-fire"),item.Bonus("pierce-cold"),item.Bonus("pierce-ltng"));
    }
    internal static bool Cast(SkeletonGuardian actor,NpcItemTarget target,int skill,int rank,int flag,DateTime at)
    {
        if(actor is not IronGolemBody||skill!=47||target==null||NpcOriginalItemProcs.Allocator is not{} allocate)
        {throw new NotSupportedException("Fire Ball proc needs its original Iron Golem,target and world allocator");}
        return NpcItemSpellCasts.CastOriginal(actor,target,skill,rank,flag,at,r=>allocate(actor,r,at));
    }
}
