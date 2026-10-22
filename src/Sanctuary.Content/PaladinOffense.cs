// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;

namespace Sanctuary;

public static class PaladinOffense
{
    internal static Func<int> RollPercent=static()=>Utility.Random(100);
    public static int AttackRate(Mobile target)=>PaladinAuras.Effect(target,CombatAbility.Fanaticism)?.Percent ?? 0;
    public static int HammerBonus(Mobile target)=>(PaladinAuras.Effect(target,CombatAbility.Concentration)?.Percent ?? 0)
        *SkillMechanics.For(CombatAbility.BlessedHammer).Parameter(1)/8;
    public static int CreatureCycle(Mobile target,int milliseconds)
    {
        int rate=Math.Clamp(100+AttackRate(target)-GearEffects.AttackSlow(target),15,175);
        if(rate==100){return milliseconds;}
        int advance=256*rate/100,frames=(milliseconds+39)/40;
        return Math.Max(40,(frames*256+advance-1)/advance*40);
    }
    public static bool PreventRecovery(PlayerMobile actor,long now)
    {
        var aura=PaladinAuras.Effect(actor,CombatAbility.Concentration);
        if(aura is not{UninterruptChance:>0}){return false;}
        var action=ClassCombat.For(actor);
        // The player-mode reference does not prevent recovery from neutral, death or an expired action.
        // Non-class UO spells/movement mode changes need separate native mode evidence.
        if(now>=action.NextAction || action.Animation is 0 or 20 || action.ActionMap!=actor.Map || action.ActionOrigin!=actor.Location){return false;}
        return RollPercent()<aura.UninterruptChance;
    }
}
