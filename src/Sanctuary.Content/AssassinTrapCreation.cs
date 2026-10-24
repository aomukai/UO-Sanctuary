// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;

namespace Sanctuary;

public sealed record AssassinTrapCreationStats(int Rank,int Level,int BaseStatsLevel,int Life256,int Defense,int Attack,
    int DurationFrames,SummonStat[] Passives,SummonStat[] Skills,int AttackPercent=0,bool MissileArmed=false);

// Creation component only: actual spawn stats and accrued owner stats are explicit
// inputs. Actor movement, damage ownership, pet limits and persistence are separate
// consumers. This component does not enable a learned Blade Sentinel handler.
public static class AssassinTrapCreation
{
    internal static AssassinTrapCreationStats Prepare(SkillValues values,int casterLevel,int difficulty,bool expansion,
        SummonSpawnStats spawn,Func<string,int> accrued)
    {
        ArgumentNullException.ThrowIfNull(values);ArgumentNullException.ThrowIfNull(accrued);
        if(values.Ability!=CombatAbility.BladeSentinel){throw new ArgumentException("Expected Blade Sentinel creation values",nameof(values));}
        // Both source paths call the same full SetSummonBaseStats helper.
        var basis=ValkyrieCreation.Base(casterLevel,values.Rank,difficulty,expansion,spawn);
        var definition=SkillMechanics.For(values.Ability);var passives=new List<SummonStat>();var skills=new List<SummonStat>();
        for(int i=1;i<=14;i++)
        {
            string name=definition.Text("passivestat"+i);if(name=="0"){continue;}
            string field="passivecalc"+i;
            // The exact pinned unclosed function compiles to zero without a
            // callback in Fog. Keep the general formula interpreter strict.
            // Modern cold-sunder inheritance remains explicitly uncertain.
            int amount=i==6 && definition.Text(field)=="stat('item_pierce_cold_immunity'.accr"?0
                :checked((int)values.StatEval(field,accrued));
            passives.Add(new(name,amount));
        }
        for(int i=1;i<=5;i++)
        {
            string name=definition.Text("sumskill"+i);if(name=="0"){continue;}
            int rank=checked((int)values.Eval("sumsk"+i+"calc"));if(rank>0){skills.Add(new(name,rank));}
        }
        int attack=checked(basis.Attack+passives.Where(x=>x.Name=="tohit").Sum(x=>x.Value));
        // The passive helper refills life (calc1 is absent/zero); do44 then
        // overwrites level with caster level without recomputing base AC/TH.
        return new(values.Rank,casterLevel,basis.Level,spawn.Life256,basis.Defense,attack,
            checked((int)values.Eval("calc4")),passives.ToArray(),skills.ToArray());
    }
    internal static AssassinTrapCreationStats Arm(AssassinTrapCreationStats snapshot,int ownerAttack,
        int ownerItemAttackPercent,int ownerWeaponMastery)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if(snapshot.MissileArmed){throw new InvalidOperationException("Blade missile attack inheritance already applied");}
        return snapshot with{Attack=checked(snapshot.Attack+ownerAttack),
            AttackPercent=checked(snapshot.AttackPercent+ownerItemAttackPercent+ownerWeaponMastery),MissileArmed=true};
    }
}
