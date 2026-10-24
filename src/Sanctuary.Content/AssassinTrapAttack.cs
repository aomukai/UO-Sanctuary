// SPDX-License-Identifier: GPL-3.0-or-later
using System;

namespace Sanctuary;

// Nonplayer hit-success component. Defense already includes its missile-specific
// channel; the caller supplies actual inherited actor stats, never player-only
// ignore/fractional defense or species AR. Does not change player combat rolls.
public static class AssassinTrapAttack
{
    internal static int Chance(int level,int attack,int dexterity,int attackPercent,int targetLevel,int defense,int missileAttack=0)
    {
        if(level is <1 or >255 || targetLevel is <1 or >255){throw new ArgumentOutOfRangeException();}
        int rating=checked(missileAttack+5*dexterity+attack);
        rating=checked(rating+(int)((long)rating*attackPercent/100));
        if(defense<0){rating=checked(rating-defense);defense=0;}
        if(rating<0){defense=checked(defense-rating);rating=0;}
        defense=Math.Max(0,defense);int divisor=checked(rating+defense);
        int factor=divisor!=0?checked(100*rating)/divisor:100;
        return Math.Clamp(checked(2*level*factor)/(targetLevel+level),5,95);
    }
    internal static bool Hit(int chance,uint sourceRoll)
    {
        if(chance is <5 or >95){throw new ArgumentOutOfRangeException(nameof(chance));}
        return sourceRoll%100<chance;
    }
}
