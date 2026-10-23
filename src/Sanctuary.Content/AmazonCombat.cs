// SPDX-License-Identifier: GPL-3.0-or-later
using Server.Mobiles;

namespace Sanctuary;

public static class AmazonCombat
{
    public static bool Implemented(CombatAbility ability) => ability is CombatAbility.CriticalStrike;
    public static int CriticalChance(PlayerMobile owner)
    {
        if(owner==null || ClassCreation.Profiles?.TryGet(owner.Serial,out var profile)!=true || profile.Class!=DiabloClass.Amazon
            || SkillMechanics.Rank(owner,CombatAbility.CriticalStrike)<=0){return 0;}
        return (int)SkillMechanics.Values(owner,CombatAbility.CriticalStrike).Eval("passivecalc1");
    }
}
