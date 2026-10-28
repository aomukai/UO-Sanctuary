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
public static class GoatmanStunRecipientChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(GoatmanStunRecipientChecks));
    private static string Manifest=>Path.Join(Core.BaseDirectory,"goatman-stun-recipient-fixtures.json");
    private sealed record Saved(uint Target,CombatStunState State);
    private static void Require(bool b,string why){if(!b){throw new InvalidOperationException(why);}}
    private static int[] N(JsonElement e)=>e.EnumerateArray().Select(x=>x.GetInt32()).ToArray();
    private static void Stop(BaseCreature body){body.Frozen=body.CantWalk=true;body.Combatant=null;body.FocusMob=null;body.AIObject?.AITimer.Stop();}
    public static void Configure()
    {
        Register("goatman-stun-recipient-check",Start);Register("goatman-stun-recipient-reload-check",Reload);
        EventSink.WorldLoad+=()=>{if(File.Exists(Manifest)){var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));if(World.FindMobile((Serial)s.Target) is BaseCreature body){Stop(body);}}};
    }
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Prepared NPC Stun recipient phase",_=>Core.LoopContext.Post(()=>{try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Start()
    {
        var body=new EncounterCreature{Name="NPC Stun recipient [fixture]"};Stop(body);body.SetHits(10000);body.Hits=body.HitsMax;
        var before=CombatStuns.Snapshot();int selections=0,commits=0,lethal=0,zero=0,shorter=0;
        using(var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"goatman-npc-release-source-probe.json"))))
        foreach(var row in doc.RootElement.EnumerateArray())
        {
            var input=N(row[0]);var output=N(row[1]);var stun=N(row[2]);
            int id=input[0],rank=input[1],flags=input[2],hp=input[3],gate=input[4],unique=input[5],boss=input[6],velocity=input[7],hire=input[8],roll=input[9],existing=input[10],elapsed=input[11];
            bool reaches=(flags&1)!=0&&gate==0&&hp>0;
            int frames=reaches&&id==416?GoatmanNpcSkills.Values(id,rank).DurationFrames:0;
            var calls=new List<string>();
            int duration=PreparedMonsterStun.Resolve(frames,unique!=0,b=>{Require(b==100,"Wrong unique roll bound");calls.Add("roll");return roll;},
                ()=>{calls.Add("boss");return boss!=0;},()=>{calls.Add("velocity");return velocity;},()=>{calls.Add("hireling");return hire!=0;});
            int expected=stun[2]+stun[3]>0?stun[1]-1000-elapsed:0;
            Require(duration==expected,"Complete source recipient duration differs at "+selections);
            var expectedCalls=new List<string>();
            if(frames>0)
            {
                if(unique!=0){expectedCalls.Add("roll");}
                if(unique==0||roll>=90)
                {
                    expectedCalls.Add("boss");
                    if(boss==0){expectedCalls.Add("velocity");if(velocity!=0){expectedCalls.Add("hireling");}}
                }
            }
            Require(calls.SequenceEqual(expectedCalls),"Recipient lazy service order differs");selections++;
            var at=TimedEffects.Now;var prior=existing!=0?new CombatStunState(body.Serial.Value,at.AddMilliseconds((1000-elapsed)*40)):null;
            CombatStuns.Restore(prior==null?before:before.Append(prior).ToArray());
            if(duration>0&&output[2]==0){lethal++;continue;} // Source transient lethal state requires a separate death bridge.
            int health=SkillHealth.Balance256(body);bool applied=PreparedMonsterStun.CommitSurvivor(body,duration,at);
            Require(applied==(duration>0)&&SkillHealth.Balance256(body)==health,"Stun phase changed health or rejected a surviving recipient");
            var state=CombatStuns.Snapshot().SingleOrDefault(s=>s.Target==body.Serial.Value);
            Require(state==(duration>0?new CombatStunState(body.Serial.Value,at.AddMilliseconds(duration*40)):prior),"Native exact deadline/replacement differs");
            if(duration>0){commits++;if(output[8]==0){zero++;}if(prior!=null&&state.UntilUtc<prior.UntilUtc){shorter++;}}
        }
        Require((selections,commits,lethal,zero,shorter)==(3840,160,24,72,72),"Recipient source coverage incomplete");
        CombatStuns.Restore(before);var clock=TimedEffects.Now;
        Require(PreparedMonsterStun.CommitSurvivor(body,30,clock),"Natural recipient start failed");
        Timer.StartTimer(TimeSpan.FromMilliseconds(1500),()=>
        {
            try
            {
                Require(CombatStuns.Remaining(body)==0,"Natural 30-frame recipient stun did not expire");
                var at=TimedEffects.Now;Require(PreparedMonsterStun.CommitSurvivor(body,30,at),"Save recipient start failed");
                var state=new CombatStunState(body.Serial.Value,at.Add(FixtureSpace.Retention));
                CombatStuns.Restore(CombatStuns.Snapshot().Where(s=>s.Target!=body.Serial.Value).Append(state).ToArray());
                body.Internalize();File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(body.Serial.Value,state)));
                var dead=new EncounterCreature();dead.Delete();bool refused=false;
                try{PreparedMonsterStun.CommitSurvivor(dead,30,at);}catch(NotSupportedException){refused=true;}
                Require(refused,"Unsupported lethal/deleted recipient was silently accepted");
                Log.Information("goatman-stun-recipient-result: passed");
            }
            catch(Exception e){Log.Error(e,"goatman-stun-recipient-result: failed");}
        });
        Log.Information("NPC Stun recipient:3840source selections/160native surviving commits/72zero-damage/72shorter refreshes;24lethal source states remain unsupported");
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var body=World.FindMobile((Serial)saved.Target) as EncounterCreature;
        Require(body is{Alive:true,Deleted:false},"Saved Stun recipient missing");Stop(body);
        Require(CombatStuns.Snapshot().Single(s=>s.Target==saved.Target)==saved.State&&CombatStuns.Remaining(body)>0,"Recipient exact stun deadline did not survive reload");
        var at=TimedEffects.Now;Require(PreparedMonsterStun.CommitSurvivor(body,1,at),"Shorter reloaded stun failed");
        Require(CombatStuns.Snapshot().Single(s=>s.Target==saved.Target).UntilUtc==at.AddMilliseconds(40),"Reloaded recipient kept the longer expiry");
        CombatStuns.Restore(CombatStuns.Snapshot().Where(s=>s.Target!=saved.Target).Append(saved.State).ToArray());
        Log.Information("NPC Stun recipient:actual saved body/exact deadline and shorter replacement verified");
    }
}
