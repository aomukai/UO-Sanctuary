// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Server;
using Server.Logging;
using Server.Mobiles;
namespace Sanctuary.Integration;
public static class GoatmanBerserkStateChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(GoatmanBerserkStateChecks));
    private static string Manifest=>Path.Join(Core.BaseDirectory,"goatman-berserk-state-fixtures.json");
    private sealed record Saved(uint Timed,uint Temporary,int TimedDefense,int TemporaryDefense,GoatmanBerserkEffect Effect);
    private static void Require(bool b,string why){if(!b){throw new InvalidOperationException(why);}}
    private static void Stop(BaseCreature actor){actor.Frozen=actor.CantWalk=true;actor.Combatant=null;actor.FocusMob=null;actor.Warmode=false;actor.AIObject?.AITimer.Stop();}
    private static EncounterCreature Actor(int x)
    {
        var actor=new EncounterCreature{Name="NPC Berserk state [fixture]"};Stop(actor);
        for(int r=0;r<=8;r++)for(int dx=-r;dx<=r;dx++)for(int dy=-r;dy<=r;dy++)
        {
            var point=new Point3D(x+dx,1580+dy,Map.Trammel.GetAverageZ(x+dx,1580+dy));
            if(!Map.Trammel.CanSpawnMobile(point)){continue;}
            actor.MoveToWorld(point,Map.Trammel);Stop(actor);return actor;
        }
        actor.Delete();throw new InvalidOperationException("No legal NPC fixture point");
    }
    public static void Configure()
    {
        Register("goatman-berserk-state-check",Start);Register("goatman-berserk-state-reload-check",Reload);
        EventSink.WorldLoad+=()=>{if(File.Exists(Manifest)){var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));foreach(uint id in new[]{s.Timed,s.Temporary}){if(World.FindMobile((Serial)id) is BaseCreature actor){Stop(actor);}}}};
    }
    private static void Register(string n,Action action)=>ConsoleInputHandler.RegisterCommand(n,"NPC Berserk live defense and timed-state persistence",_=>Core.LoopContext.Post(()=>{try{action();Log.Information("{Name}: passed",n);}catch(Exception e){Log.Error(e,"{Name}: failed",n);}}));
    private static int[] N(JsonElement e)=>e.EnumerateArray().Select(x=>x.GetInt32()).ToArray();
    private static void Start()
    {
        Require(GoatmanBerserkEffects.Profiles!=null,"NPC state persistence not configured");
        var actor=Actor(6100);int basis=ClassStats.Defense(actor);Require(basis>0,"NPC ordinary defense fixture missing");int count=0;
        using(var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"goatman-npc-melee-source-probe.json"))))
        foreach(var row in doc.RootElement.GetProperty("states").EnumerateArray())
        {
            var p=N(row[0]);if(p[2]==0){continue;} // Managed state allocation;source failures remain in prior controls.
            GoatmanBerserkEffects.Clear(actor);var at=TimedEffects.Now;
            if(p[1]==2){GoatmanBerserkEffects.Begin(actor,7,at);}
            if(p[1]==4){GoatmanBerserkEffects.Impact(actor,7,at);}
            var state=GoatmanBerserkEffects.Begin(actor,p[0],at);var expected=N(row[2]);
            Require(state.Flags==expected[1]&&state.Rank==expected[3]&&state.UntilUtc==at.AddMilliseconds((expected[2]-1000)*40)&&ClassStats.Defense(actor)==0,"NPC source start/real defense differs");
            var deadline=state.UntilUtc.Value;var impactAt=p[4]!=0?deadline.AddMilliseconds(40):at.AddTicks(12345);
            var impact=GoatmanBerserkEffects.Impact(actor,p[5],impactAt);expected=N(row[4]);
            Require(impact.Flags==expected[1]&&impact.Rank==expected[3]&&impact.UntilUtc==(expected[2]==0?null:deadline),"NPC source impact flags/deadline differ");
            if(p[4]==0){Require(impact.UntilUtc==deadline,"Subframe impact extended or truncated timed state");}
            GoatmanBerserkEffects.ModeChanged(actor,4,4);Require(GoatmanBerserkEffects.At(actor,TimedEffects.Now)!=null,"Unchanged mode erased NPC state");
            GoatmanBerserkEffects.ModeChanged(actor,4,1);Require((ClassStats.Defense(actor)==0)==(impact.Flags==2),"NPC mode change must clear only temporary state");count++;
        }
        Require(count==108,"NPC source state bridge controls incomplete");GoatmanBerserkEffects.Clear(actor);
        // BeginFor applies start39's self-state even when the supplied hit misses.
        var atNow=TimedEffects.Now;var miss=GoatmanNpcMelee.BeginFor(actor,418,1,0,10000000,new(30,50,0),p=>p,_=>0,()=>99,atNow);
        Require(miss.Total256==0&&ClassStats.Defense(actor)==0,"Miss skipped NPC Berserk self-state");
        var old=GoatmanBerserkEffects.At(actor,atNow);GoatmanNpcMelee.RefreshForRelease(actor,418,20,atNow.AddMilliseconds(40));
        Require(GoatmanBerserkEffects.At(actor,atNow).UntilUtc==old.UntilUtc,"Release extended NPC Berserk timer");
        GoatmanBerserkEffects.Clear(actor);GoatmanNpcMelee.BeginFor(actor,416,1,0,10000000,new(30,50,0),p=>p,_=>0,()=>99,atNow);
        Require(ClassStats.Defense(actor)==basis,"NPC Stun applied Berserk defense override");
        var natural=GoatmanBerserkEffects.Begin(actor,1,TimedEffects.Now);
        Timer.StartTimer(TimeSpan.FromMilliseconds(3000),()=>
        {
            try
            {
                Require(TimedEffects.Now>=natural.UntilUtc&&GoatmanBerserkEffects.At(actor,TimedEffects.Now)==null&&ClassStats.Defense(actor)==basis,"Natural NPC expiry did not restore ordinary defense");
                SaveFixtures(actor,basis);Log.Information("goatman-berserk-state-result: passed");
            }
            catch(Exception e){Log.Error(e,"goatman-berserk-state-result: failed");}
        });
        Log.Information("NPC Berserk:108complete-source state bridges,real NPC defense/miss-start and mode controls;natural expiry/persistence staged");
    }
    private static void SaveFixtures(EncounterCreature actor,int basis)
    {
        var temporary=Actor(6102);var expired=Actor(6104);int temporaryDefense=ClassStats.Defense(temporary);var at=TimedEffects.Now;
        var state=GoatmanBerserkEffects.Begin(actor,20,at) with{UntilUtc=at.Add(FixtureSpace.Retention)};
        // Retention is an inspection-only save fixture after natural duration
        // passed;gameplay Begin still derives its ordinary source duration.
        var prior=GoatmanBerserkEffects.Snapshot().Where(s=>s.Actor!=actor.Serial.Value&&s.Actor!=expired.Serial.Value).ToArray();
        GoatmanBerserkEffects.Restore(prior.Append(state).Append(new(expired.Serial.Value,1,2,-100,at.AddMilliseconds(-1))).ToArray());
        var temp=GoatmanBerserkEffects.Impact(temporary,20,at);
        int RawCount()=>((IDictionary)typeof(GoatmanBerserkEffects).GetField("States",BindingFlags.NonPublic|BindingFlags.Static).GetValue(null)).Count;
        int before=RawCount();var bytes=Task.Run(()=>{var w=new BufferWriter(new byte[1024*1024],true);GoatmanBerserkEffects.Profiles.Serialize(w);return w.Buffer[..(int)w.Position];}).GetAwaiter().GetResult();
        var reader=new BufferReader(bytes);Require(reader.ReadInt()==0,"NPC state serializer version changed");var rows=JsonSerializer.Deserialize<GoatmanBerserkEffect[]>(reader.ReadString());
        Require(rows.Single(s=>s.Actor==actor.Serial.Value)==state&&rows.All(s=>s.Actor!=temporary.Serial.Value&&s.Actor!=expired.Serial.Value),"Worker serializer retained expired/in-flight NPC state");
        Require(RawCount()==before&&GoatmanBerserkEffects.At(temporary,at)==temp&&ClassStats.Defense(actor)==0&&ClassStats.Defense(expired)>0,"Worker serialization mutated NPC state/defense");
        foreach(var bad in new[]{state with{Flags=4},state with{Rank=0},state with{DefenseOverride=-99},state with{UntilUtc=null},state with{Actor=0}})
        {
            bool refused=false;try{GoatmanBerserkEffects.Restore(new[]{state,bad});}catch(InvalidDataException){refused=true;}
            Require(refused&&GoatmanBerserkEffects.At(actor,at)==state,"Malformed restore partially replaced NPC states");
        }
        bool duplicate=false;try{GoatmanBerserkEffects.Restore(new[]{state,state});}catch(InvalidDataException){duplicate=true;}
        Require(duplicate&&GoatmanBerserkEffects.At(actor,at)==state,"Duplicate restore was not atomic");
        expired.Delete();GoatmanBerserkEffects.Clear(expired);actor.Internalize();temporary.Internalize();File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(actor.Serial.Value,temporary.Serial.Value,basis,temporaryDefense,state)));
        Require(ClassStats.Defense(temporary)==0,"Temporary state cleared before save inspection");
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var timed=World.FindMobile((Serial)saved.Timed) as EncounterCreature;var temporary=World.FindMobile((Serial)saved.Temporary) as EncounterCreature;
        Require(timed!=null&&temporary!=null,"Saved NPC state fixture bodies missing");Stop(timed);Stop(temporary);
        Require(GoatmanBerserkEffects.At(timed,TimedEffects.Now)==saved.Effect&&ClassStats.Defense(timed)==0,"Timed NPC state/deadline/real defense did not persist");
        Require(GoatmanBerserkEffects.At(temporary,TimedEffects.Now)==null&&ClassStats.Defense(temporary)==saved.TemporaryDefense,"In-flight NPC temporary state survived restart");
        GoatmanBerserkEffects.ModeChanged(timed,4,1);Require(ClassStats.Defense(timed)==0,"Mode change cleared restored timed NPC state");
        var expired=saved.Effect with{UntilUtc=TimedEffects.Now.AddMilliseconds(-1)};GoatmanBerserkEffects.Restore(new[]{expired});Require(ClassStats.Defense(timed)==saved.TimedDefense,"Restored expired NPC state retained defense override");GoatmanBerserkEffects.Restore(new[]{saved.Effect});
        Log.Information("NPC Berserk:actual saved timed state/deadline restores zero defense;temporary state omitted,expired state ignored");
    }
}
