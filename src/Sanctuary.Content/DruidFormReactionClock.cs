// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server.Mobiles;
namespace Sanctuary;

internal readonly record struct DruidFormReaction(int Advance256,int SourceEndFrame)
{
    // Existing native hit/block convention, distinct from the proved source
    // ENDANIM timestamp. Exact modern reentry remains separately unverified.
    internal int NativeFrames=>Math.Max(1,SourceEndFrame-1);
}

internal static class DruidFormReactionClock
{
    internal static DruidFormReaction Calculate(CombatAbility form,bool block,int bonus,bool holyShield=false)
    {
        if(form is not(CombatAbility.Werewolf or CombatAbility.Werebear)){throw new ArgumentOutOfRangeException(nameof(form));}
        int frames=form==CombatAbility.Werewolf&&!block?4:5;
        int speed=form==CombatAbility.Werewolf?256:block?200:184;
        bonus=Math.Clamp(bonus,0,10000);
        int effective=120*bonus/(120+bonus);
        int advance=Math.Max(1,speed*((block&&holyShield?100:50)+effective)/100);
        return new(advance,Math.Max(2,(frames*256+advance-1)/advance));
    }
    internal static DruidFormReaction? Current(PlayerMobile owner,bool block)
    {
        if(DruidPlayerForms.State(owner) is not{} state){return null;}
        int bonus=block?ClassGear.Bonus(owner,"block2")+ClassGear.Bonus(owner,"block3"):HitRecovery.Fhr(owner);
        return Calculate(state.Form.Ability,block,bonus,block&&HolyShields.State(owner)!=null);
    }
}
