// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Logging;
namespace Sanctuary.Integration;

public static class LeapAttackSequenceChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(LeapAttackSequenceChecks));
    private static void Require(bool b,string why){if(!b){throw new InvalidOperationException(why);}}
    public static void Configure()=>ConsoleInputHandler.RegisterCommand("leap-attack-sequence-check","Current SQ14 and source absolute phase resets",_=>Core.LoopContext.Post(()=>
    {try{Check();Log.Information("leap-attack-sequence-check: passed");}catch(Exception e){Log.Error(e,"leap-attack-sequence-check: failed");}}));
    private static SourceAnimationEvent[] Events(JsonElement data)=>data.EnumerateArray().Select(e=>new SourceAnimationEvent(e[0].GetInt32(),e[1].GetInt32(),e[2].GetInt32(),e[3].GetInt32())).ToArray();
    private static void Check()
    {
        string[] codes=["HTH","1HT","2HT","1HS","2HS","STF","1JS","1JT","1SS","1ST","HT1","HT2"];
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"leap-attack-sequence-cases.json")));
        int initial=0,absolute=0;
        foreach(var row in doc.RootElement.GetProperty("initial").EnumerateArray())
        {
            int p=row[0][0].GetInt32(),speed=row[0][1].GetInt32(),frame=row[0][2].GetInt32(),fraction=row[0][3].GetInt32();
            var plan=PlayerWeaponSequences.Schedule(14,codes[p],speed,0,frame);
            Require(plan.Events.SequenceEqual(Events(row[1]))&&plan.Epoch256==row[5].GetInt32(),"Initial SQ14 complete source scheduler mismatch");
            var events=plan.Events.Where(e=>e.Type==1).ToArray();
            Require(events.Select(e=>e.Param).SequenceEqual(row[2].EnumerateArray().Select(e=>e.GetInt32()))
                &&row[3].EnumerateArray().All(e=>e.GetInt32()==fraction)
                &&row[4].GetInt32()==(events.Length>0?(events[^1].Param<<8)|fraction:fraction),"Initial callback ordinal/fraction mismatch");
            initial++;
        }
        foreach(var row in doc.RootElement.GetProperty("absolute").EnumerateArray())
        {
            int p=row[0][0].GetInt32(),speed=row[0][1].GetInt32(),frame=row[0][2].GetInt32(),point=row[0][3].GetInt32();
            var profile=PlayerWeaponSequences.Profile(14,codes[p]);
            var state=new SourceAnimationState(12345,speed,profile.Frames*256,profile.Rows.Select(r=>r[4]).ToArray(),
                [new(1,frame+1,1,99),new(2,frame+2,0),new(3,frame+3,0,17)]);
            var plan=SourceAnimationEvents.AtPoint(state,frame,point);
            Require(plan.Events.SequenceEqual(Events(row[1]))&&plan.Epoch256==row[2].GetInt32()&&row[3].GetInt32()==123,
                "Absolute point reset confused relative rewind,ordinal or unrelated events");
            Require(state.Epoch256==12345&&state.Events.Length==3,"Absolute reset mutated its input plan");absolute++;
        }
        foreach(string code in new[]{"BOW","XBW"})
        {
            var row=PlayerWeaponSequences.Profile(14,code);Require(row.Points==0&&row.Frames==0&&row.RawAuxiliary==256&&row.Rows.Length==0,"Missing ranged descriptor was fabricated");
            bool refused=false;try{_=PlayerWeaponSequences.Schedule(14,code,256,0,100);}catch(NotSupportedException){refused=true;}Require(refused,"Empty ranged profile scheduled actions");
        }
        foreach(string code in codes)
        {
            var row=PlayerWeaponSequences.Profile(14,code);row.Rows[5][4]=0;
            Require(PlayerWeaponSequences.Profile(14,code).Rows[5][4]==1,"SQ14 caller changed shared data");
        }
        Require(initial==1620&&absolute==1080&&!BarbarianCombat.Implemented(CombatAbility.LeapAttack),"Incomplete SQ14 matrix or learned route silently enabled");
        Log.Information("Leap Attack SQ14:1620initial and1080absolute phase-reset source comparisons,12active/2empty profiles,three action markers,ordinal/fraction/unrelated-event retention and immutable rows pass;actual flight/rate/landing wave and learned route remain separate");
    }
}
