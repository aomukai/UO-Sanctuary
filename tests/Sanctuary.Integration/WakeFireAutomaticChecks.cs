// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Logging;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary.Integration;

public static class WakeFireAutomaticChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(WakeFireAutomaticChecks));
    private sealed record Saved(uint Owner,uint Body,uint Target,SentryBodyState Actor,WakeFireSave Pool,int Health,int Mana,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"wake-fire-automatic-fixtures.json");
    public static void Configure()
    {
        Register("wake-fire-automatic-check",Check);Register("wake-fire-automatic-reload-check",Reload);
        Register("wake-fire-automatic-live-check",()=>FixtureNetwork.Connected("wake-fire-automatic-live-result",Live));
    }
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Automatic prepared Wake firing/E9 death/original issued fire; learned casting stays gated",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static PlayerMobile Fresh()
    {
        var p=BladeBodyChecks.Fresh(7060,32);p.Name="WakeAutomaticFixture";
        while(ClassCreation.Profiles.GetRank(p.Serial,"assassin.wakeoffire")<3){Require(ClassCreation.Profiles.TryTrain(p.Serial,"assassin.wakeoffire",out _),"Train Wake");}
        return p;
    }
    private static AssassinSentryBody Actor(PlayerMobile p,DateTime at)=>AssassinSentryBody.CreateEmerging(p,new(p.X+2,p.Y,p.Z),at,SourceRandom.InitLow(91),CombatAbility.WakeOfFire);
    private static BladeMissileTarget Target(AssassinSentryBody b,int dx=2,int dy=1)
    {
        var t=new BladeMissileTarget{NoKillAwards=true,Frozen=true,CantWalk=true};t.MoveToWorld(new(b.X+dx,b.Y+dy,b.Z),b.Map);t.AIObject?.AITimer.Stop();t.SetHits(100000);t.Hits=t.HitsMax;t.SetResistance(ResistanceType.Fire,0);return t;
    }
    private static void Frames(AssassinSentryBody b,int count)
    {for(int i=0;i<count;i++){b.AdvanceController(b.Prepared.NextFrameUtc,true);}}
    internal static WakeFireSave Scope(AssassinSentryBody b)
    {
        var all=AssassinWakeFireMissiles.Snapshot();var flights=all.Flights.Where(x=>x.Actor==b.Serial.Value).ToArray();var groups=flights.Select(x=>x.Execution).ToHashSet();
        return new(flights,all.Leases.Where(x=>groups.Contains(x.Execution)).ToArray(),all.NextId);
    }
    private static void Tick(AssassinSentryBody b,int count,bool actions=true)
    {
        for(int i=0;i<count;i++)
        {var flights=Scope(b).Flights;if(flights.Length==0){return;}AssassinWakeFireMissiles.TickFrame(flights.Min(x=>x.NextUtc),actions);}
    }
    internal static void Rearm(AssassinSentryBody b,DateTime at)
    {
        var all=AssassinWakeFireMissiles.Snapshot();AssassinWakeFireMissiles.Restore(all with{Flights=all.Flights.Select(x=>x.Actor==b.Serial.Value?x with{NextUtc=at}:x).ToArray()});
    }
    private static void KillCredit(AssassinSentryBody b,PlayerMobile p)
    {
        var before=AssassinWakeFireMissiles.Snapshot();var source=Scope(b).Flights.First(x=>x.Wave);var victim=Target(b,7,0);victim.SetHits(1);victim.Hits=1;
        try
        {
            long id=before.NextId+1;var flight=source with{Id=id,Frame=0,X=victim.X-.05,Y=victim.Y,Vx=.1,Vy=0,LastCollision=0,
                Minimum256=2560,Maximum256=2560,Steps=new([new(victim.X*2,victim.Y*2)]),NextUtc=b.Prepared.Corpse.CompletedUtc.AddHours(1)};
            AssassinWakeFireMissiles.Restore(before with{Flights=before.Flights.Append(flight).ToArray(),NextId=id});
            AssassinWakeFireMissiles.TickFrame(flight.NextUtc,true);
            Require(!victim.Alive&&victim.LastKiller==b&&ClassLoot.Killer(victim)==p,"Completed original Wake source lost NPC/player lethal credit");
            SkillHealth.Profiles.Remainders[victim.Serial]=(206,victim.Hits);SkillHealth.Profiles.Prune();
            Require(!SkillHealth.Profiles.Remainders.ContainsKey(victim.Serial),"Ordinary dead receiver retained a health remainder");
        }
        finally{AssassinWakeFireMissiles.Restore(before);victim.Delete();}
    }
    private static void Check()
    {
        Require(AssassinSentryActors.DispatchWake==AssassinWakeFireMissiles.Dispatch,"Real default Wake factory is not bound");
        var p=Fresh();DateTime at=DateTime.UtcNow.AddDays(2);var b=Actor(p,at);var target=Target(b);int mana=NecromancerEffects.ManaBalance256(p);
        Frames(b,25);Require(b.Prepared.WakeControl is{Mode:9,Dispatches:0,Ai.RemainingShots:4},"Automatic Wake windup");
        Frames(b,1);var maker=Scope(b).Flights.Single();
        Require(b.Prepared.WakeControl.Dispatches==1&&!maker.Wave&&maker.PerpendicularX==-2&&maker.PerpendicularY==4,"Real direct S2 maker/target-relative perpendicular");
        int hp=SkillHealth.Balance256(target);b.BeginRetirement(b.Prepared.NextFrameUtc,"replacement");
        var death=b.Prepared.Retirement;Require(!b.Alive&&death.Profile==AssassinSentryRetirement.InfernoProfile&&death.CompletesUtc-death.EnteredUtc==TimeSpan.FromMilliseconds(1000),"Wake E9 source death profile");
        Tick(b,12);Require(Scope(b).Flights.Any(x=>x.Wave)&&SkillHealth.Balance256(target)<hp,"Original dying maker did not emit real damaging waves");
        b.AdvanceController(death.CompletesUtc,false);Require(b.RetainedCorpse&&!b.Deleted,"Wake same NPC did not join completed corpse pool");
        SkillHealth.SetBalance256(b,12850);SkillHealth.Profiles.Prune();Require(SkillHealth.Balance256(b)==12850,"Completed registered sentry lost fractional wounds");
        bool refused=false;try{AssassinWakeFireMissiles.Create(b,new((b.X+4)*2,b.Y*2));}catch(InvalidOperationException){refused=true;}Require(refused,"Dead Wake actor started a new shot");
        Rearm(b,death.CompletesUtc.AddMilliseconds(40));long sequence=AssassinWakeFireMissiles.Snapshot().NextId;
        Tick(b,8);Require(AssassinWakeFireMissiles.Snapshot().NextId>sequence,"Retained dead maker lost child allocation ownership");KillCredit(b,p);
        Tick(b,600,false);Require(Scope(b).Flights.Length==0&&NecromancerEffects.ManaBalance256(p)==mana,"Retired offline fire froze/paid owner mana");target.Delete();
        var saved=Actor(p,at.AddDays(1));var receiver=Target(saved);Frames(saved,26);Tick(saved,12);
        var original=Scope(saved);Require(original.Flights.Any(x=>!x.Wave)&&original.Flights.Any(x=>x.Wave)&&original.Leases.Any(x=>x.Target==receiver.Serial.Value),"Missing original automatic maker/waves");
        for(int i=0;i<5;i++){Actor(p,at.AddDays(1));}
        Require(!saved.Alive&&saved.Prepared.Retirement is{Cause:"replacement"}&&AssassinTrapRoster.For(p).Length==5
            &&!AssassinTrapRoster.For(p).Any(x=>x.Actor==saved.Serial.Value)&&Scope(saved).Flights.Select(x=>x.Id).SequenceEqual(original.Flights.Select(x=>x.Id)),"Sixth trap erased original issued fire or kept a live slot");
        WakeFireMissileChecks.PrepareSave();
        SkillHealth.SetBalance256(saved,12850);SkillHealth.Profiles.Prune();Require(SkillHealth.Balance256(saved)==12850,"Pending sentry lost fractional wounds");receiver.MoveToWorld(new(receiver.X,receiver.Y+8,receiver.Z),receiver.Map);p.Internalize();
        Write(new(p.Serial.Value,saved.Serial.Value,receiver.Serial.Value,saved.Prepared,Scope(saved),SkillHealth.Balance256(receiver),mana,0));
        Require(AssassinCombat.Implemented(CombatAbility.WakeOfFire),"Approved Wake learned route is missing");
    }
    private static void Write(Saved s)=>File.WriteAllText(Manifest,JsonSerializer.Serialize(s));
    private static void Reload()
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);var b=(AssassinSentryBody)World.FindMobile((Serial)s.Body);var target=(BladeMissileTarget)World.FindMobile((Serial)s.Target);
        // Cap replacement used the real clock. Its original short death
        // interval naturally finishes before the disposable-world save/reload.
        var expected=s.Actor.Retirement is{} death?s.Actor with{Retirement=null,Corpse=new(death.CompletesUtc)}:s.Actor;
        var current=Scope(b);Require(s.Generation<2&&JsonSerializer.Serialize(b.Prepared)==JsonSerializer.Serialize(expected)
            &&JsonSerializer.Serialize(current.Flights)==JsonSerializer.Serialize(s.Pool.Flights)&&JsonSerializer.Serialize(current.Leases)==JsonSerializer.Serialize(s.Pool.Leases)
            &&SkillHealth.Balance256(b)==12850&&SkillHealth.Balance256(target)==s.Health&&NecromancerEffects.ManaBalance256(p)==s.Mana,$"Wake automatic reload mismatch actor={JsonSerializer.Serialize(b.Prepared)==JsonSerializer.Serialize(expected)} flights={JsonSerializer.Serialize(current.Flights)==JsonSerializer.Serialize(s.Pool.Flights)} leases={JsonSerializer.Serialize(current.Leases)}/{JsonSerializer.Serialize(s.Pool.Leases)} actorLife={SkillHealth.Balance256(b)} targetLife={SkillHealth.Balance256(target)}/{s.Health} mana={NecromancerEffects.ManaBalance256(p)}/{s.Mana}; actualActor={JsonSerializer.Serialize(b.Prepared)} expectedActor={JsonSerializer.Serialize(expected)}");
        DateTime at=b.Prepared.Retirement?.CompletesUtc??b.Prepared.Corpse.CompletedUtc;
        b.AdvanceController(at,false);Require(b.RetainedCorpse&&b.Hidden,"Reload lost original E9 completion");Rearm(b,current.Flights.Min(x=>x.NextUtc).AddHours(1));
        var first=Scope(b).Flights;Tick(b,1);Require(first.All(x=>AssassinWakeFireMissiles.State(x.Id) is{} n&&n.Frame==x.Frame+1),"Original dead-owner fire did not resume once");
        Write(s with{Actor=b.Prepared,Pool=Scope(b),Health=SkillHealth.Balance256(target),Generation=s.Generation+1});
    }
    private static void Live(NetState network)
    {
        var p=Fresh();FixtureNetwork.Authenticate(network,p,"wake-natural-");var b=Actor(p,TimedEffects.Now);var t=Target(b);int hp=SkillHealth.Balance256(t);
        Timer.StartTimer(TimeSpan.FromMilliseconds(6200),()=>Core.LoopContext.Post(()=>
        {
            try
            {
                Require(b.RetainedCorpse&&!b.Deleted&&b.Prepared.WakeControl is{Mode:0,Dispatches:5,Attempts:5,Ai.RemainingShots:0}
                    &&b.Prepared.ControllerFault==null&&SkillHealth.Balance256(t)<hp,"Connected natural five shots/real damage/E9 completion failed");
                Log.Information("wake-fire-automatic-live-result: passed");
            }
            catch(Exception e){Log.Error(e,"wake-fire-automatic-live-result: failed");}
            finally{t.Delete();b.Delete();p.Delete();}
        }));
    }
}
