// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;

namespace Sanctuary;

public static class WeaponBlocks
{
    internal static Func<int,int> Roll=Utility.Random;
    public static int Chance(PlayerMobile owner)
    {
        if(owner is not{Alive:true,Deleted:false} || ClassCreation.Profiles?.TryGet(owner.Serial,out var profile)!=true
            || profile.Class!=DiabloClass.Assassin || WeaponHands.Pair(owner)==null
            || SkillMechanics.Rank(owner,CombatAbility.WeaponBlock)<=0){return 0;}
        return (int)SkillMechanics.Values(owner,CombatAbility.WeaponBlock).Eval("passivecalc1");
    }
    internal static bool Attempt(PlayerMobile owner,long now,bool reaction=true)
    {
        // The source dispatcher returns through moving Evade before Weapon
        // Block. Accepted movement leases also preserve the running deviation.
        if(ClassMovement.IsMoving(owner,now)){return false;}
        int chance=Chance(owner);
        if(chance<=0 || Roll(100)>=chance){return false;}
        // Shared current block reaction preserves a pending attack/cast. Final
        // animation/audio and complete source soft-hit/frame throttle remain
        // separately tracked; callers may suppress a soft-hit reaction.
        if(reaction){HitRecovery.Block(owner,now);}
        return true;
    }
}

// Shared defense stage: ordinary weapons arrive after their shield attempt;
// traced missile/area producers arrive only at their existing defense stage.
// Collision, return-fire and child creation stay with their original producer.
public static class ClassAvoidance
{
    internal static bool Weapon(PlayerMobile owner,bool projectile,long now)
        =>WeaponBlocks.Attempt(owner,now) || AmazonAvoidance.Weapon(owner,projectile,now);
    // Copied area flags do not retain the old WeaponBlock reaction bit.
    // A traced missile supplies its own GetHit/KnockBack context explicitly.
    internal static bool Missile(Mobile owner,string missile)=>Missile(owner,
        NecromancerSummons.Source(missile,"GetHit")!=0 || NecromancerSummons.Source(missile,"KnockBack")!=0);
    internal static bool Missile(Mobile owner,bool reaction=false)=>owner is PlayerMobile player
        && (WeaponBlocks.Attempt(player,Core.TickCount,reaction) || AmazonAvoidance.Missile(player));
}
