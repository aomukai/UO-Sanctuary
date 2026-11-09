// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;
namespace Sanctuary;
internal static class WeaponHexDelivery
{
    internal static bool Apply(Mobile source,Mobile target,Func<PlayerMobile,BaseCreature,bool> receive)
    {
        if(!WeaponHexGroups.Approved||source is not PlayerMobile owner||target is not BaseCreature{Alive:true,Deleted:false} npc
            ||source.Map==null||source.Map==Map.Internal||source.Map!=target.Map||!WeaponHexes.Equipped(owner)){return false;}
        return receive(owner,npc);
    }
}
