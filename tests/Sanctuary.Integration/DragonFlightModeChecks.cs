// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class DragonFlightModeChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(DragonFlightModeChecks));
    private sealed record Saved(uint Owner,int Mana,ActionTiming Ordinary,ActionTiming Tail);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"dragon-flight-mode-fixtures.json");
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    public static void Configure(){Register("dragon-flight-mode-check",Check);Register("dragon-flight-mode-reload-check",Reload);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Prepared Flight ordinary-human fixed mode clock",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Model()
    {
        using var source=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"dragon-flight-mode-cases.json")));
        string[] codes=["HTH","1HT","2HT","1HS","2HS","BOW","XBW","STF","1JS","1JT","1SS","1ST","HT1","HT2"];
        int comparisons=0;
        foreach(var row in source.RootElement.EnumerateArray())
        {
            if(row[0][0].GetInt32()!=0||row[0][1].GetInt32()!=7||row[0][2].GetInt32()!=1){continue;}
            Require(row[1][0].GetInt32()==DragonFlightPreparation.Advance,"Original Flight selected clock");
            foreach(var code in codes)
            {
                var plan=DragonFlightPreparation.Plan(code,100);var expected=row[2].EnumerateArray().ToArray();
                Require(plan.Events.Length==expected.Length,"Flight mode-entry event count");
                for(int i=0;i<expected.Length;i++)
                {
                    var e=expected[i];Require(plan.Events[i]==new SourceAnimationEvent(e[0].GetInt32(),100+e[1].GetInt32(),e[2].GetInt32(),e[3].GetInt32()),"Complete mode-entry/rate-dispatch/scheduler differential");
                }
                Require(DragonFlightPreparation.Clock(code)==(9,14,23),"Flight ordinals collapsed");comparisons++;
            }
        }
        Require(comparisons==1680,"Incomplete Flight mode contexts");
        int variations=0;
        foreach(var family in Enum.GetValues<AttackFamily>())foreach(int modifier in new[]{-30,0,20})
        foreach(int ias in new[]{0,20,10000})foreach(int fcr in new[]{0,200})foreach(int slow in new[]{0,50,85})foreach(int skillRate in new[]{0,20,300})
        {
            Require(CombatTiming.Calculate(DiabloClass.Assassin,CombatAbility.DragonFlight,new(family,modifier),ias,fcr,slow,skillRate)==new ActionTiming(23,9),"Flight incorrectly inherited IAS/FCR/WSM/attack-slow/skill-rate");variations++;
        }
        Require(variations==1296&&AssassinCombat.Implemented(CombatAbility.DragonFlight),"Approved Flight lost prepared clock");
        Log.Information("Dragon Flight mode:1680 full source mode/rate/event comparisons,1296 native modifier controls;fixed256/actions9,14/end23;no learned cast");
    }
    private static void Owner(PlayerMobile owner,int mana,ActionTiming ordinary,ActionTiming tail)
    {
        Require(CombatTiming.For(owner,CombatAbility.DragonFlight)==new ActionTiming(23,9),"Owner Flight clock");
        Require(NecromancerEffects.ManaBalance256(owner)==mana&&CombatTiming.For(owner,CombatAbility.BasicAttack)==ordinary
            &&CombatTiming.For(owner,CombatAbility.DragonTail)==tail,"Prepared Flight changed owner resources or other clocks");
    }
    private static void Check()
    {
        Model();var owner=BladeBodyChecks.Fresh(6820,10,3500);owner.Name="DragonFlightClockFixture";
        int mana=NecromancerEffects.ManaBalance256(owner);var ordinary=CombatTiming.For(owner,CombatAbility.BasicAttack);var tail=CombatTiming.For(owner,CombatAbility.DragonTail);
        Owner(owner,mana,ordinary,tail);File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(owner.Serial.Value,mana,ordinary,tail)));
    }
    private static void Reload()
    {
        Model();var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));
        Owner((PlayerMobile)World.FindMobile((Serial)saved.Owner),saved.Mana,saved.Ordinary,saved.Tail);
    }
}
