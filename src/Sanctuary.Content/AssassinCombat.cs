// SPDX-License-Identifier: GPL-3.0-or-later
using Server.Items;
using Server.Mobiles;

namespace Sanctuary;

public static class AssassinCombat
{
    public static bool Implemented(CombatAbility ability) => ability is CombatAbility.ClawMastery;

    // Claw Mastery populates the same item-type-layer melee mastery stats as
    // Barbarian masteries. It never grants mastery to another weapon family.
    internal static int Mastery(PlayerMobile owner,BaseWeapon weapon,int stat)
    {
        if(stat is <1 or >3 || owner==null || weapon==null
            || ClassCreation.Profiles?.TryGet(owner.Serial,out var profile)!=true || profile.Class!=DiabloClass.Assassin
            || !ClassGear.Active(owner,weapon) || ClassGear.Base(weapon) is not{} basis
            || CombatTiming.Weapon(weapon).Family is AttackFamily.Bow or AttackFamily.Crossbow or AttackFamily.Throw)
        {return 0;}
        var skill=SkillMechanics.For(CombatAbility.ClawMastery);
        if(!ClassItemTypes.Is(basis.Type,skill.Text("passiveitype")) || SkillMechanics.Rank(owner,skill.Ability)<=0){return 0;}
        return (int)SkillMechanics.Values(owner,skill.Ability).Eval("passivecalc"+stat);
    }
}
