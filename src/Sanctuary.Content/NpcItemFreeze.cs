// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server.Mobiles;
namespace Sanctuary;

// Complete function14 arithmetic reuses the existing item freeze formula.
// Its nested missile event has no successful-hit flag or direct damage.
internal static class NpcItemFreeze
{
    internal static int Bonus(BaseCreature source)=>NpcEquipmentCombat.EventBonus(source,"freeze");
    internal static bool Apply(BaseCreature source,BaseCreature target,bool ranged,DateTime at,Func<uint> raw=null)
    {
        if(source==null||target==null){return false;}
        int bonus=Bonus(source);if(bonus<=0){return false;}
        NpcFreezeStatus.Require(source,target);
        uint draw=(raw??(()=>NpcEquipmentRandom.Raw(source)))();
        int frames=NamedControl.FreezeFrames(ClassStats.Level(source),ClassStats.Level(target),bonus,ranged,(int)(draw%100));
        if(frames<=0){return false;}
        _=NpcFreezeStatus.Execute(source,target,frames,at);return true;
    }
}
