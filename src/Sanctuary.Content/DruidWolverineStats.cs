// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
namespace Sanctuary;

internal readonly record struct WolverineAura(int Rank,int Radius,int AttackPercent,int DamagePercent);
internal static class DruidWolverineStats
{
    private static SkillDefinition _aura;
    public static void Configure()
    {
        var row=BalanceCatalog.Read<SkillDefinition>("druid-spirit-auras").SingleOrDefault(r=>r.SourceId==297);
        if(row is not{SourceKey:"Wolverine Aura",Ability:CombatAbility.HeartOfWolverine}
            || row.Number("srvdofunc")!=65 || row.Number("aurafilter")!=65795
            || row.Text("aurastat1")!="item_tohit_percent" || row.Text("aurastat2")!="damagepercent"
            || row.Text("aurastate")!="wolverinecontrol" || row.Text("auratargetstate")!="wolverine")
        {throw new InvalidDataException("Unsupported Wolverine aura route");}
        _aura=row;
    }
    internal static WolverineAura Aura(int rank)
    {
        if(rank is <1 or >255){throw new ArgumentOutOfRangeException(nameof(rank));}
        var values=new SkillValues(_aura,_=>rank,_=>0,1);
        if(values.Mana256!=0){throw new NotSupportedException("Unexpected Wolverine NPC aura cost");}
        return new(rank,checked((int)values.Eval("aurarangecalc")),checked((int)values.Eval("aurastatcalc1")),checked((int)values.Eval("aurastatcalc2")));
    }
}
