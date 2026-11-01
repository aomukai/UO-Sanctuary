// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;
namespace Sanctuary;

internal static class FireGolemUpgrades
{
    internal static int HardRank(Mobile actor)=>actor is NecromancerMinion{Kind:CombatAbility.FireGolem,ControlMaster:PlayerMobile{Deleted:false} owner}
        ?ClassCreation.Profiles.GetRank(owner.Serial,"necromancer.firegolem"):0;
    internal static bool CannotFreeze(Mobile actor)=>HardRank(actor)>=10;
    // An ordinary100resistance floor,not a separate invulnerability flag.
    // Existing Conviction/Lower Resist and immunity-break rules still apply.
    internal static int Resistance(NecromancerMinion actor,ResistanceType type,int original)
        =>type==ResistanceType.Cold&&HardRank(actor)>=20?Math.Max(100,original):original;
    internal static SkillValues HolyFire(NecromancerMinion actor)=>new(SkillMechanics.Source("Holy Fire"),
        name=>name=="Holy Fire"?actor.Rank:0,_=>0,actor.CombatProfile.Level);
    internal static void Pulse(NecromancerMinion actor,PlayerMobile owner,DateTime at,Func<int,int> roll)
    {
        if(actor.Kind!=CombatAbility.FireGolem||!actor.Alive||actor.Deleted||actor.ControlMaster!=owner||owner is not{Alive:true,Deleted:false}
            ||owner.NetState==null||actor.Map==null||actor.Map==Map.Internal||actor.Map!=owner.Map||!PersistentMinions.Active(owner,actor))
        {return;}
        PaladinElements.Pulse(actor,owner,CombatAbility.HolyFire,HolyFire(actor),at,roll);
    }
}
