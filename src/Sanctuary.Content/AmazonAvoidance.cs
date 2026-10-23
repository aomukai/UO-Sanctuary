// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;

namespace Sanctuary;

public static class AmazonAvoidance
{
    internal static Func<int,int> Roll=Utility.Random;
    public static int DodgeChance(PlayerMobile player)=>Chance(player,CombatAbility.Dodge);
    public static int AvoidChance(PlayerMobile player)=>Chance(player,CombatAbility.Avoid);
    public static int EvadeChance(PlayerMobile player)=>Chance(player,CombatAbility.Evade);
    private static int Chance(PlayerMobile player,CombatAbility ability)
    {
        if(player is not{Alive:true,Deleted:false} || ClassCreation.Profiles?.TryGet(player.Serial,out var profile)!=true
            || profile.Class!=DiabloClass.Amazon || SkillMechanics.Rank(player,ability)<=0){return 0;}
        return (int)SkillMechanics.Values(player,ability).Eval("passivecalc1");
    }
    internal static bool Dodge(PlayerMobile player,bool projectile,long now)=>!projectile && Attempt(player,CombatAbility.Dodge,now);
    // Ordinary weapon hit checks have already attempted shield blocking.
    internal static bool Weapon(PlayerMobile player,bool projectile,long now)=>Attempt(player,projectile?CombatAbility.Avoid:CombatAbility.Dodge,now);
    // Call once at a traced missile/area damage stage. Collision history,
    // return-fire and child/explosion creation are independent earlier events.
    // This has no shield request; ordinary physical weapons use Weapon instead.
    internal static bool Missile(Mobile defender)=>defender is PlayerMobile player && Attempt(player,CombatAbility.Avoid,Core.TickCount);
    private static bool Attempt(PlayerMobile player,CombatAbility ability,long now)
    {
        // Moving source modes use Evade instead. Sanctuary's accepted running
        // step rule takes precedence over source running avoidance.
        if(ClassMovement.IsRunning(player,now)){return false;}
        if(ClassMovement.IsMoving(player,now)){ability=CombatAbility.Evade;}
        int chance=Chance(player,ability);
        if(chance<=0 || Roll(100)>=chance){return false;}
        if(ability==CombatAbility.Evade)
        {
            // Source Evade presentation requests sound and returns without an
            // S1 mode change. Identified native block sound, final audio pending.
            player.PlaySound(0x1A4);return true;
        }
        // Identified native marker pending the source S1 dodge animation/sound.
        // Current Param8=2 and released2.4 permit other actions; no recovery lock
        // or cancellation of an already paid attack is introduced here.
        player.FixedEffect(0x375A,5,5,0x481,0);
        return true;
    }
}
