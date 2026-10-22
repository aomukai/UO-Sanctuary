// SPDX-License-Identifier: GPL-3.0-or-later
using Server;
using Server.Mobiles;

namespace Sanctuary;

public static class BarbarianCombat
{
    public static bool Implemented(CombatAbility ability) => ability is CombatAbility.IncreasedStamina or CombatAbility.IronSkin
        or CombatAbility.IncreasedSpeed or CombatAbility.NaturalResistance or CombatAbility.SwordMastery or CombatAbility.AxeMastery
        or CombatAbility.MaceMastery or CombatAbility.PolearmMastery or CombatAbility.SpearMastery or CombatAbility.Bash or CombatAbility.Stun or CombatAbility.Concentrate;
    public static bool Handles(CombatAbility ability)=>Implemented(ability) && ClassAbilities.Find(ability) is{Passive:false};
    public static bool Available(PlayerMobile owner,CombatAbility ability)=>Handles(ability)
        && ClassCreation.Profiles?.TryGet(owner.Serial,out var profile)==true && profile.Class==DiabloClass.Barbarian
        && SkillMechanics.Rank(owner,ability)>0;
    public static bool Begin(PlayerMobile owner,CombatAbility ability,Serial selected,Point3D point,long now)=>BarbarianMelee.Begin(owner,ability,selected,point,now);
    public static bool Release(PlayerMobile owner,CombatAbility ability,Serial selected,long now)=>BarbarianMelee.Release(owner,ability,selected,now);
    public static int Passive(PlayerMobile owner,CombatAbility ability)
    {
        if(!Implemented(ability) || ClassCreation.Profiles?.TryGet(owner.Serial,out var profile)!=true || profile.Class!=DiabloClass.Barbarian){return 0;}
        int rank=SkillMechanics.Rank(owner,ability);if(rank<=0){return 0;}
        var row=SkillMechanics.For(ability);
        return (int)new SkillValues(row,key=>key==row.SourceKey?rank:0,_=>0,profile.Level).Eval("passivecalc1");
    }
    public static int Resistance(PlayerMobile owner,ResistanceType type)=>type is ResistanceType.Fire or ResistanceType.Cold or ResistanceType.Energy or ResistanceType.Poison
        ?Passive(owner,CombatAbility.NaturalResistance):0;
    public static void Refresh(PlayerMobile owner)
    {
        ClassGear.Refresh(owner);owner.CheckStatTimers();StatNetwork.Send(owner);
    }
}
