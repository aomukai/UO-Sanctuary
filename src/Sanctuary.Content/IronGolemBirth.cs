// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
namespace Sanctuary;

internal sealed record IronGolemBirthStats(int Rank,int Level,int BaseDefenseAddition,int BaseAttackAddition,
    int MaximumLifeBaseWrite256,int CurrentLifeWrite256,int RequestedEquipmentLevel,
    SummonStat[] Passives,SummonStat[] Auras,SummonStat[] Resistances);

// do57 equips the original BEFORE the shared base/passive/resistance helpers.
// Supplied stats must therefore be the post-equip getters. In particular, the
// MAXHP result below is a BASE WRITE, not the final accrued maximum. Armor/AR
// additions and passive writes are likewise distinct from accrued getters:
// attached item operations remain present when the real actor applies them.
// This component does not enable casting, register item events or run pet AI.
internal static class IronGolemBirth
{
    private static void Route(SkillValues values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var row=SkillMechanics.For(CombatAbility.IronGolem);
        if(values.Ability!=CombatAbility.IronGolem || values.Rank is <1 or >255 || row.SourceId!=90
            || row.Number("srvstfunc")!=20 || row.Number("srvdofunc")!=57 || row.Text("summon")!="IronGolem"
            || row.Text("pettype")!="golem" || row.Text("summode")!="S1" || values.Eval("petmax")!=1
            || row.Text("aurastate")!="thorns" || row.Text("aurastat1")!="item_attackertakesdamage"
            || row.Text("aurastat2")!="fade")
        {throw new NotSupportedException("Unsupported Iron Golem creation route");}
        for(int i=1;i<=5;i++)
        {if(row.Text("sumskill"+i)!="0"){throw new NotSupportedException("Unmapped Iron Golem summon skill");}}
        for(int i=1;i<=3;i++)
        {if(row.Text("auraevent"+i)!="0"){throw new NotSupportedException("Unmapped Iron Golem aura event");}}
    }

    internal static IronGolemBirthStats Prepare(SkillValues values,MonsterDifficulty difficulty,bool expansion,
        SummonSpawnStats equipped,Func<string,int> accrued,int summonResistance,Func<string,int> equippedStat)
    {
        Route(values);ArgumentNullException.ThrowIfNull(accrued);ArgumentNullException.ThrowIfNull(equippedStat);
        int ownerLevel=checked((int)values.Formula("ulvl"));
        // Shared base helper computes/caps the default pet level and adds the
        // correct monlvl columns; reuse its already verified implementation.
        var basis=ValkyrieCreation.Base(ownerLevel,values.Rank,(int)difficulty,expansion,equipped);
        var row=SkillMechanics.For(CombatAbility.IronGolem);
        var passives=new List<SummonStat>();var auras=new List<SummonStat>();var resistances=new List<SummonStat>();
        // All ten pinned slots are retained. The old five-slot executable
        // proof does not certify modern sunder delivery; do not discard them.
        for(int i=1;i<=10;i++)
        {
            string name=row.Text("passivestat"+i);if(name=="0"){continue;}
            int amount=checked((int)values.StatEval("passivecalc"+i,accrued));
            passives.Add(new(name,amount));
        }
        for(int i=1;i<=6;i++)
        {
            string name=row.Text("aurastat"+i);if(name=="0"){continue;}
            int amount=checked((int)values.Eval("aurastatcalc"+i));
            if(amount!=0){auras.Add(new(name,amount));}
        }
        int life=checked(equipped.Life256+SourcePercentage.Calculate(equipped.Life256,checked((int)values.Eval("calc1")),100));
        // The resistance helper runs last, so it observes original-item
        // absorption. Nonpositive absorb admits the bonus; poison always does.
        if(summonResistance!=0)
        {
            foreach(var (resist,absorb) in new[]{("fireresist","item_absorbfire_percent"),
                ("lightresist","item_absorblight_percent"),("coldresist","item_absorbcold_percent")})
            {if(equippedStat(absorb)<=0){resistances.Add(new(resist,summonResistance));}}
            resistances.Add(new("poisonresist",summonResistance));
        }
        return new(values.Rank,basis.Level,checked(basis.Defense-equipped.Defense),checked(basis.Attack-equipped.Attack),
            life,life,Math.Clamp(checked(3*values.Rank),1,ownerLevel),
            passives.ToArray(),auras.ToArray(),resistances.ToArray());
    }
}
