// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Text.Json;
using Server;
using Server.Logging;

namespace Sanctuary.Integration;

public static class WeaponSequenceRateChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(WeaponSequenceRateChecks));
    public static void Configure()=>ConsoleInputHandler.RegisterCommand("weapon-sequence-rate-check","Compiled attack-rate and original modern skill-layer comparisons",_=>Core.LoopContext.Post(()=>
    {try{Check();Log.Information("weapon-sequence-rate-check: passed");}catch(Exception ex){Log.Error(ex,"weapon-sequence-rate-check: failed");}}));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static void Check()
    {
        using var fixture=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"weapon-sequence-rate-probe.json")));int rates=0,modern=0;
        foreach(var row in fixture.RootElement.GetProperty("rates").EnumerateArray())
        {
            int ias=row[0].GetInt32(),stat=row[1].GetInt32(),mode=row[2].GetInt32(),baseSpeed=row[3].GetInt32();
            int actual=CombatTiming.FrameAdvance(new(AttackFamily.Thrust,0),ias,0,stat-100,mode==1?-30:0,baseSpeed);
            Require(actual==row[4].GetInt32() && actual==row[5].GetInt32(),"Compiled engine rate/sequence/clamp/truncation: "+row);rates++;
        }
        foreach(var row in fixture.RootElement.GetProperty("sias").EnumerateArray())
        {
            var ability=row[0].GetString() switch{"JAB"=>CombatAbility.Jab,"IMPALE"=>CombatAbility.Impale,"DOUBLE_THROW"=>CombatAbility.DoubleThrow,_=>CombatAbility.BasicAttack};
            int native=row[1].GetInt32()==1?CombatTiming.SequenceRateAdjustment(ability):0;
            int expected=row[5].GetInt32()+row[2].GetInt32()+row[3].GetInt32()*50+row[4].GetInt32()*50;
            Require(native==expected,"Original current-family player/mercenary skill-rate layer: "+row);modern++;
        }
        Require(rates==288 && modern==96,"Incomplete rate comparison");
        Require(CombatTiming.Calculate(DiabloClass.Amazon,CombatAbility.Jab,new(AttackFamily.Thrust,0))==new ActionTiming(26,5)
            && CombatTiming.Calculate(DiabloClass.Amazon,CombatAbility.Jab,new(AttackFamily.TwoHandedThrust,0))==new ActionTiming(31,5)
            && CombatTiming.Calculate(DiabloClass.Barbarian,CombatAbility.DoubleThrow,new(AttackFamily.Throw,0))==new ActionTiming(18,8),"Real sequence dispatch skipped adjustment");
        Require(CombatTiming.Calculate(DiabloClass.Amazon,CombatAbility.PowerStrike,new(AttackFamily.Thrust,0))==new ActionTiming(13,7)
            && CombatTiming.Calculate(DiabloClass.Amazon,CombatAbility.ChargedStrike,new(AttackFamily.TwoHandedThrust,0))==new ActionTiming(16,9),"Ordinary A1 acquired sequence penalty");
        Require(CombatTiming.SequenceRateAdjustment(CombatAbility.Impale)==0 && !AmazonCombat.Implemented(CombatAbility.Impale),"Impale speed preparation enabled unfinished handler");
        Log.Information("Weapon sequence rate:288 native engine rows and96 original modern skill-layer rows passed; Impale metadata preparation only");
    }
}
