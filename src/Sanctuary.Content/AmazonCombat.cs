// SPDX-License-Identifier: GPL-3.0-or-later
using Server.Mobiles;

namespace Sanctuary;

public static class AmazonCombat
{
    public static bool Implemented(CombatAbility ability) => ability is CombatAbility.CriticalStrike or CombatAbility.Penetrate;
    public static int CriticalChance(PlayerMobile owner) => Passive(owner,CombatAbility.CriticalStrike);
    public static int AttackPercent(PlayerMobile owner) => Passive(owner,CombatAbility.Penetrate);
    private static int Passive(PlayerMobile owner,CombatAbility ability)
    {
        if(owner==null || ClassCreation.Profiles?.TryGet(owner.Serial,out var profile)!=true || profile.Class!=DiabloClass.Amazon
            || SkillMechanics.Rank(owner,ability)<=0){return 0;}
        return (int)SkillMechanics.Values(owner,ability).Eval("passivecalc1");
    }
}
