// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server.Items;
using Server.Mobiles;

namespace Sanctuary;

public static class BarbarianMasteries
{
    internal static readonly CombatAbility[] Melee=[CombatAbility.SwordMastery,CombatAbility.AxeMastery,CombatAbility.MaceMastery,CombatAbility.PolearmMastery,CombatAbility.SpearMastery];
    public static int Bonus(PlayerMobile owner,BaseWeapon weapon,int stat)
    {
        if(stat is <1 or >3){return 0;}
        if(CombatTiming.Weapon(weapon).Family==AttackFamily.Throw){return Throwing(owner,weapon,stat);}
        if(stat is <1 or >3 || owner==null || ClassCreation.Profiles?.TryGet(owner.Serial,out var profile)!=true || profile.Class!=DiabloClass.Barbarian
            || weapon==null || !ClassGear.Active(owner,weapon) || ClassGear.Base(weapon) is not{} basis
            || CombatTiming.Weapon(weapon).Family is AttackFamily.Bow or AttackFamily.Crossbow or AttackFamily.Throw
            || ClassGear.Definition(weapon)?.Category=="Throwing"){return 0;}
        int maximum=0;
        foreach(var ability in Melee)
        {
            int rank=SkillMechanics.Rank(owner,ability);if(rank<=0){continue;}
            var row=SkillMechanics.For(ability);if(!ClassItemTypes.Is(basis.Type,row.Text("passiveitype"))){continue;}
            var values=new SkillValues(row,key=>key==row.SourceKey?rank:0,_=>0,profile.Level);
            maximum=Math.Max(maximum,(int)values.Eval("passivecalc"+stat));
        }
        return maximum; // Original item-type-layer selector takes the strongest eligible value, never a sum.
    }
    internal static int Throwing(PlayerMobile owner,BaseWeapon weapon,int stat)
    {
        if(stat is <1 or >6 || owner==null || weapon==null || !ClassGear.Active(owner,weapon)
            || CombatTiming.Weapon(weapon).Family!=AttackFamily.Throw || ClassGear.Base(weapon) is not{} basis
            || !ClassItemTypes.Is(basis.Type,"thro") || ClassCreation.Profiles?.TryGet(owner.Serial,out var profile)!=true
            || profile.Class!=DiabloClass.Barbarian){return 0;}
        int rank=SkillMechanics.Rank(owner,CombatAbility.ThrowingMastery);if(rank<=0){return 0;}
        return (int)SkillMechanics.Values(owner,CombatAbility.ThrowingMastery).Eval("passivecalc"+stat);
    }
}
