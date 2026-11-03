// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;
namespace Sanctuary;

// Approved Sanctuary additions; effective birth rank remains independent.
internal static class SpiritWolfUpgrades
{
    internal static int HardRank(Mobile actor)=>actor is DruidSpiritWolfBody{ControlMaster:PlayerMobile{Deleted:false} owner}
        ?ClassCreation.Profiles.GetRank(owner.Serial,"druid.summonspiritwolf"):0;
    internal static int OpenWounds(Mobile actor)=>HardRank(actor)>=10?100:0;
    // Same breakable resistance floor as the approved Fire Golem upgrade.
    internal static int Resistance(DruidSpiritWolfBody actor,ResistanceType type,int original)
        =>type==ResistanceType.Cold&&HardRank(actor)>=20?Math.Max(100,original):original;
}
