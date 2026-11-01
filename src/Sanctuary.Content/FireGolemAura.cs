// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;
namespace Sanctuary;

public partial class FireGolemBody
{
    internal SkillValues HolyFireValues=>FireGolemUpgrades.HolyFire(this);
    protected override void PulseFireAura(PlayerMobile owner)=>PulseHolyFire(owner,TimedEffects.Now);
    internal void PulseHolyFire(PlayerMobile owner,DateTime now)
        =>FireGolemUpgrades.Pulse(this,owner,now,n=>NpcEquipmentRandom.Limited(this,n));
}
