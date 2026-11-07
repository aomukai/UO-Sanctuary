// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Logging;

namespace Sanctuary.Integration;

// Immutable data and supplied-rate planning only. There is no paid cast or
// saved Flight action here; those require the separate landing/contact policy.
public static class DragonFlightSequenceChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(DragonFlightSequenceChecks));
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    public static void Configure()
    {
        foreach(var name in new[]{"dragon-flight-sequence-check","dragon-flight-sequence-reload-check"})
        {
            ConsoleInputHandler.RegisterCommand(name,"Dragon Flight current SQ21 and original scheduler controls",_=>Core.LoopContext.Post(()=>
            {try{Check();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
        }
    }
    private static void Check()
    {
        string[] codes=["HTH","1HT","2HT","1HS","2HS","BOW","XBW","STF","1JS","1JT","1SS","1ST","HT1","HT2"];
        using var source=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"dragon-flight-source-cases.json")));
        int count=0;
        foreach(var code in codes)
        {
            var profile=PlayerWeaponSequences.Profile(21,code);
            Require(profile.Sequence==21&&profile.Points==23&&profile.Frames==23&&profile.RawAuxiliary==256,"Current SQ21 descriptor");
            for(int i=0;i<23;i++)
            {
                Require(profile.Rows[i].SequenceEqual(new[]{0,i<10?10:12,i<10?i:i-10,0,i is 9 or 14?1:0}),"Current SQ21 pose/action row");
            }
            profile.Rows[9][4]=0;profile.Rows[14][1]=0;
            Require(PlayerWeaponSequences.Profile(21,code).Rows[9][4]==1&&PlayerWeaponSequences.Profile(21,code).Rows[14][1]==12,"Mutable SQ21 profile escaped");
            foreach(var row in source.RootElement.GetProperty("sequence").EnumerateArray())
            {
                var plan=PlayerWeaponSequences.Schedule(21,code,row[0][0].GetInt32(),0,100);
                var events=row[2].EnumerateArray().ToArray();
                Require(plan.Events.Length==events.Length,"SQ21 source event count");
                for(int i=0;i<events.Length;i++)
                {
                    var expected=events[i];var actual=plan.Events[i];
                    Require(actual==new SourceAnimationEvent(expected[0].GetInt32(),100+expected[1].GetInt32(),expected[2].GetInt32(),expected[3].GetInt32()),"Complete original SQ21 scheduler differential");
                }
                if(row[0][0].GetInt32()>0)
                {
                    Require(plan.Events.Where(e=>e.Type==SourceAnimationEvents.ModeChange).Select(e=>e.Param).SequenceEqual(new[]{0,1}),"SQ21 teleport then kick ordinal");
                }
                count++;
            }
        }
        Require(count==53830&&AssassinCombat.Implemented(CombatAbility.DragonFlight),"Approved Flight lost sequence supplier");
        try{PlayerWeaponSequences.Profile(21,"???");throw new InvalidOperationException("Unknown Flight weapon class accepted");}catch(NotSupportedException){}
        File.WriteAllText(Path.Join(Core.BaseDirectory,"dragon-flight-sequence-fixtures.json"),JsonSerializer.Serialize(new{Profiles=14,Comparisons=count,LearnedCast=false}));
        Log.Information("Dragon Flight:14 current SQ21 profiles/53830 complete scheduler comparisons; no learned cast or Flight save acceptance");
    }
}
