// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;

namespace Sanctuary;

public static class AmazonAvoidance
{
    internal static Func<int,int> Roll=Utility.Random;
    public static int DodgeChance(PlayerMobile player)
    {
        if(player is not{Alive:true,Deleted:false} || ClassCreation.Profiles?.TryGet(player.Serial,out var profile)!=true
            || profile.Class!=DiabloClass.Amazon || SkillMechanics.Rank(player,CombatAbility.Dodge)<=0){return 0;}
        return (int)SkillMechanics.Values(player,CombatAbility.Dodge).Eval("passivecalc1");
    }
    internal static bool Dodge(PlayerMobile player,bool projectile,long now)
    {
        // Moving source modes use Evade instead. Sanctuary's accepted running
        // step rule takes precedence over source running avoidance.
        if(projectile || ClassMovement.IsMoving(player,now)){return false;}
        int chance=DodgeChance(player);
        if(chance<=0 || Roll(100)>=chance){return false;}
        // Identified native marker pending the source S1 dodge animation/sound.
        // Current Param8=2 and released2.4 permit other actions; no recovery lock
        // or cancellation of an already paid attack is introduced here.
        player.FixedEffect(0x375A,5,5,0x481,0);
        return true;
    }
}
