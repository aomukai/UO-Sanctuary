// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server.Mobiles;
namespace Sanctuary;

// Registered item stat136/function16 and approved Grizzly hard20 share the
// existing FP256 CB commitment, without a second damage or recovery event.
internal static class NpcCrushing
{
    internal static bool Potential(BaseCreature actor)=>actor is DruidGrizzlyBody;
    internal static int Chance(BaseCreature actor)=>Math.Clamp(checked(GrizzlyUpgrades.Crushing(actor)+Math.Max(0,NpcEquipmentCombat.EventBonus(actor,"crush"))),0,100);
    internal static int Apply(BaseCreature actor,BaseCreature target,bool ranged=false,Func<int,int> limited=null)
    {
        if(target is not{Alive:true,Deleted:false}||!target.CanBeDamaged()){return 0;}
        int chance=Chance(actor);if(chance<=0){return 0;}
        if((limited?.Invoke(100)??NpcEquipmentRandom.Limited(actor,100))>=chance){return 0;}
        return NamedCombat.ApplyCrushing(target,ranged);
    }
}
