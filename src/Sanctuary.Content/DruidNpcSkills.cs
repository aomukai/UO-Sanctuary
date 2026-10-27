// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
namespace Sanctuary;

// Internal monster skills have their own pinned rows. NPC314 is not another
// player talent or hotkey and must not borrow player237's formulas.
internal static class DruidNpcSkills
{
    private static SkillDefinition _rage;
    internal static void Load()
    {
        var rows=BalanceCatalog.Read<SkillDefinition>("druid-npc-skills");
        if(rows.Length!=1 || rows[0] is not{SourceId:314,Ability:CombatAbility.SummonDireWolf,CharacterClass:DiabloClass.Druid,SourceKey:"fenris rage"} rage
            || rage.Number("srvdofunc")!=137 || rage.Text("aurastate")!="fenris_rage" || rage.Text("aurastat1")!="damagepercent"
            || rage.Text("aurastat2")!="0" || rage.Number("TargetCorpse")!=1)
        {throw new InvalidDataException("Invalid Fenris NPC314 source row");}
        _rage=rage;
    }
    internal static SkillValues Rage(int rank=1,int level=1)
        =>new(_rage??throw new InvalidOperationException("Fenris NPC314 not loaded"),key=>key=="fenris rage"?rank:0,_=>0,level);
}
