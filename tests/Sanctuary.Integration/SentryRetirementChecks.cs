// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary.Integration;

public static class SentryRetirementChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(SentryRetirementChecks));
    private sealed record Saved(uint Owner,uint Natural,uint Replacement,uint Target,int Map,Point3D Origin,
        SentryBodyState NaturalState,SentryBodyState ReplacementState,SentryBoltState Bolt,int Health,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"sentry-retirement-fixtures.json");
    private static void Require(bool yes,string why){if(!yes){throw new InvalidOperationException(why);}}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Natural Charged Bolt Sentry death clock and original issued bolts",_=>Core.LoopContext.Post(()=>{try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    public static void Configure()
    {
        Register("sentry-retirement-check",()=>FixtureNetwork.Connected("sentry-retirement-result",Check));
        Register("sentry-retirement-reload-check",()=>FixtureNetwork.Connected("sentry-retirement-reload-result",Reload));
    }
    private static void Differential()
    {
        var rows=JsonSerializer.Deserialize<int[][]>(File.ReadAllText(Path.Join(Core.BaseDirectory,"sentry-death-clock-source-probe.json")));
        Require(rows.Length==90,"Missing composed death clock contexts");
        foreach(var row in rows){Require(AssassinSentryRetirement.EndDelay(row[0],row[1])==row[4]&&row[5]==12&&row[6]==1,"Death animation scheduler differs from source");}
        var at=TimedEffects.Now;var state=AssassinSentryRetirement.Begin(at,"replacement");AssassinSentryRetirement.Validate(state);
        foreach(var bad in new[]{state with{CompletesUtc=state.CompletesUtc.AddMilliseconds(40)},state with{EnteredUtc=DateTime.SpecifyKind(at,DateTimeKind.Local)},state with{Cause="guessed"},state with{Profile=null}})
        {bool refused=false;try{AssassinSentryRetirement.Validate(bad);}catch(InvalidDataException){refused=true;}Require(refused,"Malformed retirement clock accepted");}
    }
    private static AssassinSentryBody Actor(PlayerMobile p,DateTime at)=>AssassinSentryBody.CreatePrepared(p,new(p.X+2,p.Y,p.Z),at,SourceRandom.InitLow(19));
    private static BladeMissileTarget Target(PlayerMobile p,Point3D at)
    {var t=new BladeMissileTarget{NoKillAwards=true,CantWalk=true,Frozen=true};t.MoveToWorld(at,p.Map);t.AIObject?.AITimer.Stop();t.SetHits(50000);t.Hits=t.HitsMax;t.SetResistance(ResistanceType.Energy,0);return t;}
    private static long Bolt(AssassinSentryBody body,Mobile target)
    {
        long id=AssassinSentryBolts.Create(body,new(0,new(target.X*2,target.Y*2),ChargedBolts.SeedSubtile(target.X*2,0),77,77));
        Hold(id,TimedEffects.Now.Add(FixtureSpace.Retention));return id;
    }
    private static void Hold(long id,DateTime at)
    {var state=AssassinSentryBolts.Snapshot();AssassinSentryBolts.Restore(state with{Bolts=state.Bolts.Select(x=>x.Id==id?x with{NextUtc=at}:x).ToArray()});}
    private static void Save(PlayerMobile p,AssassinSentryBody natural,AssassinSentryBody replacement,BladeMissileTarget target,long bolt,int generation,Point3D origin,int map)
    {
        Hold(bolt,TimedEffects.Now.Add(FixtureSpace.Retention));p.Internalize();
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,natural.Serial.Value,replacement.Serial.Value,target.Serial.Value,map,origin,
            natural.Prepared,replacement.Prepared,AssassinSentryBolts.State(bolt),SkillHealth.Balance256(target),generation)));
    }
    private static void Check(NetState network)
    {
        Differential();var p=BladeBodyChecks.Fresh(reach:20);LimitedCorpseChecks.Pool(p);
        FixtureNetwork.Authenticate(network,p,"sentry-retire-");var origin=p.Location;var natural=Actor(p,TimedEffects.Now);
        var target=Target(p,new(natural.X+8,natural.Y,natural.Z));long issued=Bolt(natural,target);string creation=JsonSerializer.Serialize(natural.Prepared.Creation);
        int beforeDeath=0;bool entered=false,damagedDuringDeath=false;long end=Core.TickCount+15000;
        void Observe()
        {
            try
            {
                Require(Core.TickCount<end&&!natural.Deleted&&natural.Prepared.ControllerFault==null,"Natural retirement failed or timed out");
                if(natural.Prepared.Retirement is{} death)
                {
                    if(!entered)
                    {
                        entered=true;beforeDeath=SkillHealth.Balance256(target);Hold(issued,TimedEffects.Now.AddMilliseconds(40));
                        Require(!natural.Alive&&!natural.Hidden&&natural.ReleasedProjectileOwner&&!natural.RetainedCorpse
                            &&natural.Prepared.Control is{Dispatches:5,Ai.RemainingShots:0}&&AssassinTrapRoster.For(p).All(x=>x.Actor!=natural.Serial.Value),"Exhaustion retained live AI/slot or entered corpse pool early");
                        Require(death.CompletesUtc-death.EnteredUtc==TimeSpan.FromMilliseconds(920),"Natural source death duration");
                        var stable=natural.Prepared;natural.BeginRetirement(TimedEffects.Now,"replacement");Require(natural.Prepared==stable,"Repeated retirement restarted the death clock");
                        bool refused=false;try{AssassinSentryBolts.Dispatch(natural,target.Serial.Value);}catch(InvalidOperationException){refused=true;}Require(refused,"Dying source launched a new volley");
                    }
                    damagedDuringDeath|=SkillHealth.Balance256(target)<beforeDeath;
                }
                if(!natural.RetainedCorpse){Timer.StartTimer(TimeSpan.FromMilliseconds(10),Observe);return;}
                Require(entered&&damagedDuringDeath&&!natural.Alive&&natural.Hidden&&!AssassinSentryActors.Actors().Contains(natural)
                    &&JsonSerializer.Serialize(natural.Prepared.Creation)==creation,"Natural death lost issued damage/stats/identity or restarted AI");
                var replacement=Actor(p,TimedEffects.Now.AddHours(1));long held=Bolt(replacement,target);
                for(int i=0;i<5;i++){Actor(p,TimedEffects.Now.AddHours(1));}
                Require(!replacement.Alive&&!replacement.Deleted&&replacement.Prepared.Retirement is{Cause:"replacement"}
                    &&AssassinTrapRoster.For(p).Length==5&&AssassinSentryBolts.State(held).Actor==replacement.Serial.Value,"Sixth trap deleted original source or lost cap5");
                Save(p,natural,replacement,target,held,0,origin,p.Map.MapID);Log.Information("sentry-retirement-result: passed");
            }
            catch(Exception e){Log.Error(e,"sentry-retirement-result: failed");}
        }
        Observe();
    }
    private static SentryBodyState Completed(SentryBodyState state)=>state.Retirement is{} death
        ?state with{Retirement=null,Corpse=new(death.CompletesUtc)}:state;
    private static void Reload(NetState network)
    {
        Differential();var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);
        var natural=(AssassinSentryBody)World.FindMobile((Serial)saved.Natural);var replacement=(AssassinSentryBody)World.FindMobile((Serial)saved.Replacement);var target=(BladeMissileTarget)World.FindMobile((Serial)saved.Target);
        Require(saved.Generation<2&&natural is{Alive:false,Deleted:false,RetainedCorpse:true}&&replacement is{Alive:false,Deleted:false,RetainedCorpse:true}
            &&JsonSerializer.Serialize(natural.Prepared)==JsonSerializer.Serialize(Completed(saved.NaturalState))
            &&JsonSerializer.Serialize(replacement.Prepared)==JsonSerializer.Serialize(Completed(saved.ReplacementState)),"Offline pending death did not complete at original deadline/identity");
        Require(JsonSerializer.Serialize(AssassinSentryBolts.State(saved.Bolt.Id))==JsonSerializer.Serialize(saved.Bolt)
            &&SkillHealth.Balance256(target)==saved.Health&&!AssassinSentryActors.Actors().Contains(replacement),"Reload lost held flight/wounds or restarted dead AI");
        p.MoveToWorld(saved.Origin,Map.Maps[saved.Map]);FixtureNetwork.Authenticate(network,p,"sentry-retire-resume-");Hold(saved.Bolt.Id,TimedEffects.Now.AddMilliseconds(40));long end=Core.TickCount+3000;
        void Observe()
        {
            try
            {
                var bolt=AssassinSentryBolts.State(saved.Bolt.Id);Require(bolt!=null&&bolt.Fault==null,"Original retired-owner flight vanished/faulted");
                if(bolt.Frame<saved.Bolt.Frame+2){Require(Core.TickCount<end,"Original retired-owner flight failed to resume");Timer.StartTimer(TimeSpan.FromMilliseconds(10),Observe);return;}
                Require(bolt.Actor==replacement.Serial.Value&&bolt.Owner==p.Serial.Value&&replacement.RetainedCorpse,"Resume changed original NPC/player ownership");
                Save(p,natural,replacement,target,bolt.Id,saved.Generation+1,saved.Origin,saved.Map);Log.Information("sentry-retirement-reload-result: passed");
            }
            catch(Exception e){Log.Error(e,"sentry-retirement-reload-result: failed");}
        }
        Observe();
    }
}
