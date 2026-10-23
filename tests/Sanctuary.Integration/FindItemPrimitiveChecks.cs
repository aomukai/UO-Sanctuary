// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class FindItemPrimitiveChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(FindItemPrimitiveChecks));
    private sealed class Draw(int value):Random
    {
        public readonly List<int> Bounds=[];
        public override int Next(int maxValue){Bounds.Add(maxValue);return Math.Clamp(value,0,maxValue-1);}
    }
    public static void Configure()=>ConsoleInputHandler.RegisterCommand("find-item-primitives-check","Source NoDrop and independent historical curve checks",_=>Core.LoopContext.Post(()=>
    {try{Check();Log.Information("find-item-primitives-check: passed");}catch(Exception e){Log.Error(e,"find-item-primitives-check: failed");}}));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static string Root=>Environment.GetEnvironmentVariable("SANCTUARY_PROJECT_ROOT");
    private static void Check()
    {
        using var loot=JsonDocument.Parse(File.ReadAllText(Path.Join(Root,".local/class-skills/find-item-loot-source-probe.json")));
        var rows=loot.RootElement.GetProperty("tables").Deserialize<GoldTreasureClass[]>(new JsonSerializerOptions{PropertyNameCaseInsensitive=true});
        foreach(var item in loot.RootElement.GetProperty("cases").EnumerateArray())
        {
            var x=item.EnumerateArray().Select(v=>v.GetInt32()).ToArray();var random=new Draw(x[2]);int result=CreatureGold.Roll(rows[x[0]],random,x[1]!=0);
            Require(Math.Max(-1,result)==x[3] && random.Bounds.SequenceEqual(new[]{x[4]}),"4464 compiled NoDrop roll/bound vectors");
        }
        var byName=rows.ToDictionary(x=>x.Name);
        foreach(var start in rows)foreach(bool high in new[]{false,true})
        {
            var row=start;var expectedBounds=new List<int>();string leaf=null;
            for(int step=0;step<64;step++)
            {
                expectedBounds.Add(row.Entries.Sum(x=>x.Weight));var entry=high?row.Entries.Last():row.Entries.First();
                if(entry.Nested==null){leaf=entry.Item;break;}row=byName[entry.Nested];
            }
            Require(leaf!=null,"Cyclic fixture graph");var random=new Draw(high?int.MaxValue:0);
            Require(CreatureGold.SelectTreasureClass(start.Name,random,true)==leaf && random.Bounds.SequenceEqual(expectedBounds),"Nested NoDrop bypass lost a level or changed entry weights");
            foreach(int seed in new[]{0,173}){Require(CreatureGold.SelectTreasureClass(start.Name,new Random(seed))==CreatureGold.SelectTreasureClass(start.Name,new Random(seed),false),"Ordinary default changed");}
        }
        using var primary=JsonDocument.Parse(File.ReadAllText(Path.Join(Root,"tools/reference/diminishing-reference-conflict.json")));
        foreach(var curve in primary.RootElement.GetProperty("publishedCurves").EnumerateArray())
        {
            var row=SkillMechanics.Source(curve.GetProperty("skill").GetString());int rank=0;
            foreach(var expected in curve.GetProperty("values").EnumerateArray())
            {
                rank++;var values=new SkillValues(row,key=>key==row.SourceKey?rank:0,_=>0,99);
                Require(values.Param(1)==curve.GetProperty("parameter1").GetInt32() && values.Param(2)==curve.GetProperty("parameter2").GetInt32() && values.Eval("passivecalc1")==expected.GetInt32(),"60 independent Blizzard published ranks/current parameter pairs");
            }
        }
        var barb=ClassSkillChecks.Player(DiabloClass.Barbarian);var necro=ClassSkillChecks.Player();
        try
        {
            foreach(var (owner,ability) in new[]{(barb,CombatAbility.NaturalResistance),(necro,CombatAbility.SummonResist)})
            {while(ClassCreation.Profiles.GetRank(owner.Serial,SkillMechanics.For(ability).TalentId)<3){Require(ClassCreation.Profiles.TryTrain(owner.Serial,SkillMechanics.For(ability).TalentId,out _),"Curve consumer rank3");}}
            Require(BarbarianCombat.Passive(barb,CombatAbility.NaturalResistance)==28 && barb.ColdResistance==28,"Native Natural Resistance consumer disagrees with primary historical rank3");
            Require(NecromancerSummons.Resistance(necro)==39,"Native Summon Resist consumer disagrees with primary historical rank3");
        }
        finally{barb.Delete();necro.Delete();}
    }
}
