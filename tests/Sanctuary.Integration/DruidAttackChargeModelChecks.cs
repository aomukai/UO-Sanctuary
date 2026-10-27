// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Text.Json;
using Server;
using Server.Logging;
namespace Sanctuary.Integration;

public static class DruidAttackChargeModelChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(DruidAttackChargeModelChecks));
    public static void Configure()=>ConsoleInputHandler.RegisterCommand("druid-charge-model-check","Prepared Maul/Feral aura source differential",_=>Core.LoopContext.Post(()=>
    {try{Check();Log.Information("druid-charge-model-check: passed");}catch(Exception e){Log.Error(e,"druid-charge-model-check: failed");}}));
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static void Check()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"druid-charged-attacks-source-probe.json")));int count=0;
        foreach(var row in doc.RootElement.EnumerateArray())
        {
            bool maul=row[0].GetInt32()==0;var ability=maul?CombatAbility.Maul:CombatAbility.FeralRage;var definition=SkillMechanics.For(ability);
            int prior=row[2].GetInt32(),scene=row[3].GetInt32(),rank=row[12].GetInt32();
            var old=prior==0?null:DruidAttackChargeModel.Capture(ability,255,prior,DateTime.UnixEpoch.AddMilliseconds(400*40));
            var values=new SkillValues(definition,key=>key==definition.SourceKey?rank:0,_=>0,1);
            bool gain=row[4].GetInt32()==1&&row[5].GetInt32()==1&&row[6].GetInt32()==1;int allocations=0;
            var actual=gain?DruidAttackChargeModel.Gain(old,values,DateTime.UnixEpoch.AddMilliseconds(100*40),()=>{allocations++;return scene!=6;}):old;
            Require(allocations==(gain&&old==null?1:0),"Existing charge state allocated again");
            Require((actual?.Count??0)==row[9].GetInt32()&&(actual?.StoredRank??0)==row[11].GetInt32()
                &&(actual==null?0:(actual.UntilUtc-DateTime.UnixEpoch).TotalMilliseconds/40)==row[10].GetInt32(),"Charge count/live-rank/expiry mismatch");
            if(actual!=null)
            {
                DruidAttackChargeModel.Validate(actual);var stats=row[15];
                int[] channels=maul?[actual.DamagePercent,actual.StunFrames,actual.AttackRate,actual.OtherAnimationRate]
                    :[actual.VelocityPercent,actual.LifeDrainMinimum,actual.LifeDrainMaximum];
                int[] indices=maul?[0,2,1,6]:[3,4,5];
                for(int i=0;i<channels.Length;i++){Require(channels[i]==stats[indices[i]].GetInt32(),"Actual native formula/charge-level channel mismatch");}
            }
            count++;
        }
        Require(count==1120,"Incomplete source charge matrix");
        Log.Information("Druid charge model:1120native gain/cap/current-rank/expiry/channel comparisons; no world/stat/combat/handler acceptance claimed");
    }
}
