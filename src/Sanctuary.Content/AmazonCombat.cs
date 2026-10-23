// SPDX-License-Identifier: GPL-3.0-or-later
using Server;
using Server.Mobiles;

namespace Sanctuary;

public static class AmazonCombat
{
    public static bool Implemented(CombatAbility ability) => ability is CombatAbility.CriticalStrike or CombatAbility.Penetrate or CombatAbility.Pierce or CombatAbility.InnerSight;
    public static bool Handles(CombatAbility ability)=>ability==CombatAbility.InnerSight;
    public static bool Available(PlayerMobile owner,CombatAbility ability)=>Handles(ability)
        && ClassCreation.Profiles?.TryGet(owner.Serial,out var profile)==true && profile.Class==DiabloClass.Amazon
        && SkillMechanics.Rank(owner,ability) is >0 and <=255;
    public static bool Begin(PlayerMobile owner,CombatAbility ability,Serial selected,Point3D point,long now)=>Handles(ability) && AmazonInnerSight.Begin(owner,selected,point,now);
    public static bool Release(PlayerMobile owner,CombatAbility ability)=>Handles(ability) && AmazonInnerSight.Release(owner);
    public static int CriticalChance(PlayerMobile owner) => Passive(owner,CombatAbility.CriticalStrike);
    public static int AttackPercent(PlayerMobile owner) => Passive(owner,CombatAbility.Penetrate);
    public static int PiercePercent(PlayerMobile owner) => Passive(owner,CombatAbility.Pierce);
    private static int Passive(PlayerMobile owner,CombatAbility ability)
    {
        if(owner==null || ClassCreation.Profiles?.TryGet(owner.Serial,out var profile)!=true || profile.Class!=DiabloClass.Amazon
            || SkillMechanics.Rank(owner,ability)<=0){return 0;}
        return (int)SkillMechanics.Values(owner,ability).Eval("passivecalc1");
    }
}
