// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Sanctuary;

internal sealed record ShadowEquipmentProfile(string Ability,SummonEquipRow[] Rows);
// Prepared requests only. No generated items, raw NPC stat graph, AI, paid
// summon or saved shadow actor is installed by this supplier.
internal static class ShadowEquipment
{
    private static readonly Lazy<ShadowEquipmentProfile[]> Profiles=new(()=>
    {
        var rows=BalanceCatalog.Read<ShadowEquipmentProfile>("shadow-equipment");
        if(rows.Length!=2||rows[0].Ability!="ShadowWarrior"||rows[1].Ability!="ShadowMaster"
            ||rows[0].Rows.Length!=4||rows[1].Rows.Length!=19||rows.Any(p=>p.Rows.Where((r,i)=>i>0&&r.Rank>p.Rows[i-1].Rank).Any()))
        {throw new InvalidDataException("Unsupported pinned Shadow equipment profiles");}
        return rows;
    });
    internal static SummonEquipRow[] Rows(CombatAbility ability)
    {
        int index=ability switch{CombatAbility.ShadowWarrior=>0,CombatAbility.ShadowMaster=>1,_=>throw new ArgumentOutOfRangeException(nameof(ability))};
        return Profiles.Value[index].Rows.Select(r=>r with{Choices=(SummonEquipChoice[])r.Choices.Clone()}).ToArray();
    }
    internal static int ItemLevel(CombatAbility ability,int rank)
    {
        if(ability is not(CombatAbility.ShadowWarrior or CombatAbility.ShadowMaster)||rank is <1 or >255){throw new ArgumentOutOfRangeException();}
        var skill=SkillMechanics.For(ability);return Math.Clamp(checked(skill.Number("Param5")+(rank-1)*skill.Number("Param6")),1,99);
    }
    internal static void Select(CombatAbility ability,int rank,ref ulong petSeed,Func<SummonEquipmentRequest,bool> create,
        Func<int,string> ownerBase,IReadOnlyCollection<int> occupied=null)
        =>SummonEquipment.Select(Rows(ability),rank,ItemLevel(ability,rank),ref petSeed,create,occupied,ownerBase);
}
