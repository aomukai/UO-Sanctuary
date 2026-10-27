// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
namespace Sanctuary;

internal readonly record struct BarbsAura(int Rank,int Radius,int FlatDamage);
internal static class DruidBarbsStats
{
    private static SkillDefinition _aura;
    public static void Configure()
    {
        var row=BalanceCatalog.Read<SkillDefinition>("druid-spirit-auras").SingleOrDefault(r=>r.SourceId==296);
        if(row is not{SourceKey:"Barbs Aura",Ability:CombatAbility.SpiritOfBarbs}
            || row.Number("srvdofunc")!=65 || row.Number("aurafilter")!=65795
            || row.Text("aurastat1")!="item_attackertakesdamage" || row.Text("aurastatcalc1")!="edmn"
            || row.Text("aurastat2")!="0" || row.Text("aurastate")!="barbscontrol" || row.Text("auratargetstate")!="barbs")
        {throw new InvalidDataException("Unsupported Barbs aura route");}
        _aura=row;
    }
    internal static BarbsAura Aura(int rank)
    {
        if(rank is <1 or >255){throw new ArgumentOutOfRangeException(nameof(rank));}
        var values=new SkillValues(_aura,_=>rank,_=>0,1);
        if(values.Mana256!=0){throw new NotSupportedException("Unexpected Barbs NPC aura cost");}
        return new(rank,checked((int)values.Eval("aurarangecalc")),checked((int)values.Eval("aurastatcalc1")));
    }
}
