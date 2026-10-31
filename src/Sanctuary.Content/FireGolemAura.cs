// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;
namespace Sanctuary;

public partial class FireGolemBody
{
    internal SkillValues HolyFireValues=>new(SkillMechanics.Source("Holy Fire"),
        name=>name=="Holy Fire"?Birth.HolyFireRank:0,_=>0,CombatProfile.Level);
    protected override void PulseFireAura(PlayerMobile owner)=>PulseHolyFire(owner,TimedEffects.Now);
    internal void PulseHolyFire(PlayerMobile owner,DateTime now)
    {
        if(Kind!=CombatAbility.FireGolem||!Alive||Deleted||ControlMaster!=owner||owner is not{Alive:true,Deleted:false}
            ||owner.NetState==null||Map==null||Map==Map.Internal||Map!=owner.Map||!PersistentMinions.Active(owner,this))
        {return;}
        // Do66 applies a nonplayer emitter's own weapon stats before returning
        // for town; it has no player mana gate. Preserve the existing native
        // first-think/cadence adapter separately from this pulse implementation.
        PaladinElements.Pulse(this,owner,CombatAbility.HolyFire,HolyFireValues,now,n=>NpcEquipmentRandom.Limited(this,n));
    }
}
