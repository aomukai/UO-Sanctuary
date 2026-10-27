// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
namespace Sanctuary;

// Internal monster skills have distinct pinned rows and no new player
// talent/hotkey identities. Never borrow the summon talent's formulas.
internal static class DruidNpcSkills
{
    private static SkillDefinition _rage,_bearSmite;
    internal static void Load()
    {
        var rows=BalanceCatalog.Read<SkillDefinition>("druid-npc-skills");
        if(rows.Length!=2 || rows[0] is not{SourceId:314,Ability:CombatAbility.SummonDireWolf,CharacterClass:DiabloClass.Druid,SourceKey:"fenris rage"} rage
            || rage.Number("srvdofunc")!=137 || rage.Text("aurastate")!="fenris_rage" || rage.Text("aurastat1")!="damagepercent"
            || rage.Text("aurastat2")!="0" || rage.Number("TargetCorpse")!=1)
        {throw new InvalidDataException("Invalid Fenris NPC314 source row");}
        var smite=rows[1];
        if(smite is not{SourceId:326,Ability:CombatAbility.SummonGrizzly,CharacterClass:DiabloClass.Druid,SourceKey:"BearSmite"}
            ||smite.Number("srvstfunc")!=32||smite.Number("srvdofunc")!=2||smite.Number("ResultFlags")!=8
            ||smite.Number("SrcDam")!=128||smite.Number("HitShift")!=8||smite.Text("EType")!="0"
            ||smite.Parameter(1)!=15||smite.Parameter(2)!=5||smite.Parameter(3)!=15||smite.Parameter(4)!=15
            ||smite.Text("calc1")!="ln34"||smite.Text("calc2").Trim('"')!="max(250,ln12)")
        {throw new InvalidDataException("Invalid Grizzly NPC326 source row");}
        _rage=rage;_bearSmite=smite;
    }
    internal static SkillValues Rage(int rank=1,int level=1)
        =>new(_rage??throw new InvalidOperationException("Fenris NPC314 not loaded"),key=>key=="fenris rage"?rank:0,_=>0,level);
    internal static SkillValues BearSmite(int rank=1,int level=1)
    {
        if(rank is <1 or >255||level is <1 or >99){throw new ArgumentOutOfRangeException();}
        return new(_bearSmite??throw new InvalidOperationException("Grizzly NPC326 not loaded"),key=>key=="BearSmite"?rank:0,_=>0,level);
    }
}
