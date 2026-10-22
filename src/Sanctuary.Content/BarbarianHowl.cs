// SPDX-License-Identifier: GPL-3.0-or-later
using Server;
using Server.Mobiles;

namespace Sanctuary;

public static class BarbarianHowl
{
    public static bool Begin(PlayerMobile owner,Serial selected,Point3D point,long now)=>SkillCombat.Begin(owner,CombatAbility.Howl,selected,point,now,(p,_,_,_)=>BarbarianShout.ValidCast(p));
    public static bool Release(PlayerMobile owner)
    {
        if(!BarbarianCombat.Available(owner,CombatAbility.Howl) || !BarbarianShout.ValidCast(owner)){return false;}
        var values=SkillMechanics.Values(owner,CombatAbility.Howl);if(!NecromancerEffects.CanPay(owner,values.Mana256)){return false;}
        BarbarianShout.Cast(owner,values,TimedEffects.Now,CombatAbility.Howl);NecromancerEffects.Pay(owner,values.Mana256);return true;
    }
    internal static bool LevelAllowed(int storedRank,int casterLevel,int targetLevel)=>storedRank+SkillMechanics.For(CombatAbility.Howl).Parameter(2)+casterLevel>targetLevel;
    internal static bool Hit(PlayerMobile owner,Mobile target,int rank,System.DateTime at)
    {
        if(target is not BaseCreature creature || !ClassCombat.IsHostile(owner,target) || !LevelAllowed(rank,ClassStats.Level(owner),ClassStats.Level(target))){return false;}
        int duration=SkillMechanics.For(CombatAbility.Howl).Parameter(5)+(rank-1)*SkillMechanics.For(CombatAbility.Howl).Parameter(6);
        return SourceTerror.Apply(owner,creature,rank,duration,at);
    }
}
