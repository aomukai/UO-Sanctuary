// SPDX-License-Identifier: GPL-3.0-or-later
using Server;
using Server.Items;
using Server.Mobiles;

namespace Sanctuary;

public static class AssassinCombat
{
    public static bool Implemented(CombatAbility ability) => ability is CombatAbility.ClawMastery or CombatAbility.WeaponBlock or CombatAbility.BurstOfSpeed or CombatAbility.Fade or CombatAbility.PsychicHammer or CombatAbility.CloakOfShadows or CombatAbility.Venom;
    public static bool Handles(CombatAbility ability)=>ability is CombatAbility.BurstOfSpeed or CombatAbility.Fade or CombatAbility.PsychicHammer or CombatAbility.CloakOfShadows or CombatAbility.Venom;
    public static bool Available(PlayerMobile owner,CombatAbility ability)=>owner!=null && Handles(ability)
        && ClassCreation.Profiles.TryGet(owner.Serial,out var profile) && profile.Class==DiabloClass.Assassin
        && SkillMechanics.Rank(owner,ability) is >0 and <=255;
    private static bool Valid(PlayerMobile owner)=>owner.Map!=null && owner.Map!=Map.Internal;
    public static bool Begin(PlayerMobile owner,CombatAbility ability,Serial selected,Point3D point,long now)
        =>ability==CombatAbility.Venom?Venoms.Begin(owner,selected,point,now):ability==CombatAbility.CloakOfShadows?CloakShadows.Begin(owner,selected,point,now):ability==CombatAbility.PsychicHammer?PsychicHammers.Begin(owner,selected,point,now):SkillCombat.Begin(owner,ability,selected,point,now,(p,_,_,_)=>Valid(p));
    public static bool Release(PlayerMobile owner,CombatAbility ability,Serial selected=default)
    {
        if(ability==CombatAbility.Venom){return Venoms.Release(owner);}
        if(ability==CombatAbility.CloakOfShadows){return CloakShadows.Release(owner);}
        if(ability==CombatAbility.PsychicHammer){return PsychicHammers.Release(owner,selected);}
        if(!owner.Alive || !Available(owner,ability) || !Valid(owner)){return false;}
        var values=SkillMechanics.Values(owner,ability);if(!NecromancerEffects.Pay(owner,values.Mana256)){return false;}
        AssassinBuffs.Cast(owner,values);return true;
    }

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
