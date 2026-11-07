// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
namespace Sanctuary;

internal sealed record ValkyrieInheritedPassive(int Skill,int Rank,int Stat,int Value);

// Original ordinary-melee allocation. Captured innate skills are independent
// of owner gear; the NPC's own complete item graph and live Command supply
// bonuses. Derived passive values form a read-only allocation view, not new
// saved stat lists, AI/defense admission or execution of item-granted skills.
internal static class ValkyrieEquipmentDamage
{
    private static readonly Dictionary<int,int> Stats=new(){{9,337},{13,338},{18,339},{29,340}};
    internal static ValkyrieInheritedPassive[] Passives(AmazonValkyrieBody body)
    {
        _=ValkyrieEquipmentWeapons.Frame(body);var graph=body.EquipmentNumbers;var skills=body.BirthStats.Skills;
        var definitions=skills.Select(s=>(Captured:s,Definition:SkillMechanics.Source(s.Name))).ToArray();
        if(definitions.Any(s=>!Stats.ContainsKey(s.Definition.SourceId)))
        {throw new NotSupportedException("Unknown captured Valkyrie inherited skill");}
        var ids=definitions.Select(s=>s.Definition.SourceId).ToHashSet();
        if(graph.Stats.Any(s=>s.Value!=0&&s.Stat is 97 or 107&&!ids.Contains((ushort)s.Packed)))
        {throw new NotSupportedException("Valkyrie item-granted skill materialization is unfinished;original skill layers are retained");}
        var result=new List<ValkyrieInheritedPassive>();
        foreach(var (captured,d) in definitions)
        {
            int stat=Stats[d.SourceId];
            if(!d.Passive||d.Text("passivestat1")!=EquipmentStatValues.Definition(stat).Name||d.Text("passivestat2")!="0"
                ||d.Text("passiveitype")!="0"||d.Text("EType")!="0"||d.Text("etype")!="0")
            {throw new NotSupportedException("Valkyrie inherited passive requires another source metadata consumer");}
            int rank=NpcInnateSkillRank.Read(captured.Value,0,checked(graph.Read(127)+BattleCommand.Bonus(body)),graph.Read(97,d.SourceId),graph.Read(107,d.SourceId));
            int value=rank==0?0:checked((int)new SkillValues(d,key=>key.Equals(d.SourceKey,StringComparison.OrdinalIgnoreCase)?rank:0,_=>0,body.BirthStats.Level).Eval("passivecalc1"));
            result.Add(new(d.SourceId,rank,stat,value));
        }
        return result.ToArray();
    }
    internal static NpcFillPacket Allocate(AmazonValkyrieBody body,GeneratedNpcFillTarget target,NpcFillPacket initial=null,int sourceDamage=128)
        =>OriginalNpcMelee.Allocate(body,target,initial,sourceDamage);
}
