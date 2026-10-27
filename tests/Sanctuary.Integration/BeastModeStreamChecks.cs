// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Text.Json;
using Server;
using Server.Logging;
namespace Sanctuary.Integration;

public static class BeastModeStreamChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(BeastModeStreamChecks));
    public static void Configure()=>ConsoleInputHandler.RegisterCommand("beast-mode-stream-check","Header-backed raw mode-list writer for all three Druid beasts",_=>Core.LoopContext.Post(()=>
    {try{Check();Log.Information("beast-mode-stream-check: passed");}catch(Exception e){Log.Error(e,"beast-mode-stream-check: failed");}}));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static void Check()
    {
        using var data=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"beast-mode-stream-source-probe.json")));
        int compared=0;var counts=new int[3,16];
        foreach(var row in data.RootElement.EnumerateArray())
        {
            int N(int i)=>row[i].GetInt32();if(N(5)!=0){continue;}
            var ability=N(0) switch{0=>CombatAbility.SummonSpiritWolf,1=>CombatAbility.SummonDireWolf,2=>CombatAbility.SummonGrizzly,_=>throw new InvalidOperationException("Unknown source species")};
            var values=new SkillValues(SkillMechanics.For(ability),_=>N(1),_=>0,99);
            var actual=DruidBeastAttackStats.Capture(values,(MonsterDifficulty)N(2),N(4));
            Require(actual==new BeastAttackStats(N(1),N(6),N(7),N(8)),"Complete raw mode writer composition: "+row.GetRawText());
            counts[N(0),N(4)]++;compared++;
        }
        Require(compared==73728,"Incomplete beast mode-stream capture");
        for(int species=0;species<3;species++)for(int mode=0;mode<16;mode++)
        {Require(counts[species,mode]==1536,"Missing rank/difficulty/game contexts for species/mode");}
        foreach(var ability in new[]{CombatAbility.SummonSpiritWolf,CombatAbility.SummonDireWolf,CombatAbility.SummonGrizzly})
        {
            var values=new SkillValues(SkillMechanics.For(ability),_=>28,_=>0,99);
            var a1=DruidBeastAttackStats.Capture(values,MonsterDifficulty.Normal,4);
            var block=DruidBeastAttackStats.Capture(values,MonsterDifficulty.Normal,6);
            Require(DruidBeastAttackStats.Capture(values,MonsterDifficulty.Normal,3)==a1
                &&DruidBeastAttackStats.Capture(values,MonsterDifficulty.Normal,12)==a1
                &&DruidBeastAttackStats.Capture(values,MonsterDifficulty.Normal,14)==a1
                &&DruidBeastAttackStats.Capture(values,MonsterDifficulty.Normal,7)==block
                &&DruidBeastAttackStats.Capture(values,MonsterDifficulty.Normal,8)==block,
                "GH3/dead12/sequence14 and block6/cast7/S1lane classification");
            int literal=ability==CombatAbility.SummonSpiritWolf?50:ability==CombatAbility.SummonDireWolf?150:300;
            Require(a1.Minimum==block.Minimum&&a1.Maximum==block.Maximum&&a1.Attack-block.Attack==literal,"Copied birth/passives leaked into raw mode lane");
            foreach(int mode in new[]{-1,16,int.MaxValue})
            {bool refused=false;try{DruidBeastAttackStats.Capture(values,MonsterDifficulty.Normal,mode);}catch(NotSupportedException){refused=true;}Require(refused,"Unmapped mode accepted");}
        }
        bool rejected=false;try{DruidBeastAttackStats.Capture(null,MonsterDifficulty.Normal,4);}catch(ArgumentNullException){rejected=true;}
        Require(rejected,"Missing skill values accepted");
        Require(DruidCombat.Implemented(CombatAbility.SummonGrizzly),"Learned Grizzly route missing");
        Log.Information("Beast raw mode stream:73728native complete writer comparisons for3species/all16header modes,256ranks/3difficulties/2game contexts pass; GH3,block6,dead12,sequence14 remain distinct. Raw stats do not execute attacks or BearSmite sequence");
    }
}
