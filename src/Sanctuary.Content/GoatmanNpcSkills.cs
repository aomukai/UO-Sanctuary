// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
namespace Sanctuary;

// Internal monster rows retain the parent talent identity but do not enter the
// playable240-skill registry. Their formulas are not player Barbarian copies.
internal static class GoatmanNpcSkills
{
    private static readonly Lazy<IReadOnlyDictionary<int,SkillDefinition>> Rows=new(()=>
    {
        var rows=BalanceCatalog.Read<SkillDefinition>("warlock-npc-skills");
        if(rows.Length!=4||!rows.Select(r=>r.SourceId).SequenceEqual(new[]{416,417,418,419})
            ||rows.Any(r=>r.Ability!=CombatAbility.SummonGoatman||r.CharacterClass!=DiabloClass.Warlock||r.Left||r.Right))
        {throw new InvalidDataException("Invalid Goatman NPC source rows");}
        if(rows[0].SourceKey!="Goatman Stun"||rows[0].Number("srvstfunc")!=32||rows[0].Number("srvdofunc")!=2
            ||rows[0].Text("calc1")!="0"||rows[0].Text("EType")!="stun"||rows[0].Number("HitClass")!=96
            ||rows[0].Text("ELenSymPerCalc")!="0"||rows[2].SourceKey!="Goatman Berserk"||rows[2].Number("srvstfunc")!=39
            ||rows[2].Number("srvdofunc")!=2||rows[2].Text("calc1")!="ln12"||rows[2].Parameter(1)!=105||rows[2].Parameter(2)!=1
            ||rows[2].Text("EType")!="mag"||rows[2].Number("calc4")!=100||rows[2].Number("aurastatcalc2")!=-100
            ||rows[1].SourceKey!="Goatman Frenzy"||rows[1].Number("srvstfunc")!=67
            ||rows[3].SourceKey!="Goatman Cleave"||rows[3].Number("srvstfunc")!=68||rows[3].Number("srvdofunc")!=164)
        {throw new InvalidDataException("Unsupported current Goatman NPC routes");}
        return rows.ToDictionary(r=>r.SourceId);
    });
    internal static SkillDefinition Source(int id)=>Rows.Value.TryGetValue(id,out var row)?row:throw new NotSupportedException("Unknown Goatman NPC skill");
    internal static SkillValues Values(int id,int rank,int actorLevel=1,Func<string,int> npcLearned=null)
    {
        if(rank is <1 or >255||actorLevel is <1 or >99){throw new ArgumentOutOfRangeException();}
        var row=Source(id);
        // No owner's talents are inherited. The optional learned supplier is
        // explicitly the NPC's own context;known ordinary Goatman has none.
        return new(row,key=>key==row.SourceKey?rank:0,npcLearned??(_=>0),actorLevel);
    }
}
