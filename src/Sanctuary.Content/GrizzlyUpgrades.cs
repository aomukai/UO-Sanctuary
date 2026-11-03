// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;
namespace Sanctuary;

internal static class GrizzlyUpgrades
{
    internal static int HardRank(Mobile actor)=>actor is DruidGrizzlyBody{ControlMaster:PlayerMobile{Deleted:false} owner}
        ?ClassCreation.Profiles.GetRank(owner.Serial,"druid.summongrizzly"):0;
    internal static int Crushing(Mobile actor)=>HardRank(actor)>=20?10:0;
    // Approved Sanctuary stun overrides the ordinary boss-immunity/unique-roll
    // policy only for this passive. Rules still supplies real boss/velocity data.
    internal static bool Stun(DruidGrizzlyBody actor,BaseCreature target,bool smite,Func<int,int> limited)
    {
        if(smite||HardRank(actor)<10||target is not{Alive:true,Deleted:false}
            ||!CombatStuns.Rules(target,out bool boss,out int velocity)||velocity==0){return false;}
        if(limited(100)>=(boss?10:50)){return false;}
        return CombatStuns.ApplyPreparedDuration(target,boss?25:75,TimedEffects.Now);
    }
}
