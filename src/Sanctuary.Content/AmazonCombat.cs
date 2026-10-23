// SPDX-License-Identifier: GPL-3.0-or-later
using Server;
using Server.Mobiles;

namespace Sanctuary;

public static class AmazonCombat
{
    public static bool Implemented(CombatAbility ability) => ability is CombatAbility.Fend or CombatAbility.Impale or CombatAbility.ChargedStrike or CombatAbility.PowerStrike or CombatAbility.Jab or CombatAbility.CriticalStrike or CombatAbility.Penetrate or CombatAbility.Pierce or CombatAbility.InnerSight;
    public static bool Handles(CombatAbility ability)=>ability is CombatAbility.Fend or CombatAbility.Impale or CombatAbility.InnerSight or CombatAbility.Jab or CombatAbility.ChargedStrike or CombatAbility.PowerStrike;
    public static bool Available(PlayerMobile owner,CombatAbility ability)=>Handles(ability)
        && ClassCreation.Profiles?.TryGet(owner.Serial,out var profile)==true && profile.Class==DiabloClass.Amazon
        && SkillMechanics.Rank(owner,ability) is >0 and <=255 && (ability is not(CombatAbility.Fend or CombatAbility.Impale or CombatAbility.Jab or CombatAbility.ChargedStrike or CombatAbility.PowerStrike) || AmazonJab.WeaponAllowed(owner));
    public static bool Begin(PlayerMobile owner,CombatAbility ability,Serial selected,Point3D point,long now)=>ability==CombatAbility.Fend?AmazonFend.Begin(owner,selected,point,now):ability==CombatAbility.Impale?AmazonImpale.Begin(owner,selected,point,now):AmazonMelee.Handles(ability)?AmazonMelee.Begin(owner,ability,selected,point,now):ability==CombatAbility.Jab?AmazonJab.Begin(owner,selected,point,now):ability==CombatAbility.InnerSight && AmazonInnerSight.Begin(owner,selected,point,now);
    public static bool Release(PlayerMobile owner,CombatAbility ability,long? now=null)=>ability==CombatAbility.Fend?AmazonFend.Resolve(owner,now??Core.TickCount):ability==CombatAbility.Impale?AmazonImpale.Resolve(owner,now??Core.TickCount):AmazonMelee.Handles(ability)?AmazonMelee.Release(owner,ability,now??Core.TickCount):ability==CombatAbility.InnerSight && AmazonInnerSight.Release(owner);
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
