// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Server;
using Server.Logging;
namespace Sanctuary.Integration;

public static class DruidFormRateChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(DruidFormRateChecks));
    public static void Configure()=>ConsoleInputHandler.RegisterCommand("druid-form-rate-check","Prepared modern normalized form arithmetic,no live timing caller",_=>Core.LoopContext.Post(()=>
    {try{Check();Log.Information("druid-form-rate-check: passed");}catch(Exception e){Log.Error(e,"druid-form-rate-check: failed");}}));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static void Check()
    {
        using var data=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"modern-form-progress-source-probe.json")));
        int count=0;var partitions=new Dictionary<string,DruidFormRateResult>();
        foreach(var row in data.RootElement.EnumerateArray())
        {
            int N(int i)=>row[i].GetInt32();var expected=row[7];int E(int i)=>expected[i].GetInt32();
            bool wolf=N(0)==2;int rank=N(5);
            var skill=new SkillValues(SkillMechanics.For(CombatAbility.Werewolf),key=>key=="Wearwolf"?rank:0,_=>0,99);
            int skillRate=wolf?DruidFormModel.Capture(skill).AttackRate:0;
            var actual=DruidFormRates.Calculate(new(wolf?CombatAbility.Werewolf:CombatAbility.Werebear,E(0),E(1),N(2),N(3),N(4),skillRate,N(6)));
            Require(actual.ItemEias==E(5)&&actual.Eias==E(3)&&actual.LimitedEias==E(6)&&actual.CycleFrames==E(7)&&actual.Progress256==E(8),"Pinned modern rate/progression model mismatch");
            string key=$"{N(0)}/{row[1].GetString()}/{N(2)}/{N(3)+N(4)}/{rank}/{N(6)}";
            if(partitions.TryGetValue(key,out var prior)){Require(prior==actual,"Weapon/off-weapon IAS partition changed result");}
            else{partitions.Add(key,actual);}count++;
        }
        Require(count==14400&&partitions.Count==4800,"Incomplete modern form model contexts");
        var input=new DruidFormRateInput(CombatAbility.Werewolf,16,256,0,0,0,20,0);
        foreach(var invalid in new[]{input with{Form=CombatAbility.Raven},input with{WeaponIas=-1},input with{GearIas=-1},input with{SkillAttackRate=-1},input with{Slowing=-1}})
        {bool refused=false;try{DruidFormRates.Calculate(invalid);}catch(ArgumentOutOfRangeException){refused=true;}Require(refused,"Unsupported input silently accepted");}
        foreach(var invalid in new[]{input with{HumanFrames=15},input with{HumanSpeed=224}})
        {bool refused=false;try{DruidFormRates.Calculate(invalid);}catch(NotSupportedException){refused=true;}Require(refused,"Unmapped animation metadata accepted");}
        Require(DruidFormRates.Calculate(input with{SkillAttackRate=int.MaxValue,WeaponIas=int.MaxValue,GearIas=int.MaxValue}).LimitedEias==150
            &&DruidFormRates.Calculate(input with{Slowing=int.MaxValue}).LimitedEias==-85,"Clamps/large supplied stat arithmetic");
        Log.Information("Druid form rates:14400native arithmetic comparisons match pinned3.3author model with current8human A1lengths;4800IAS totals retain partition invariance. No live animation,event,impact or current-executable acceptance claimed");
    }
}
