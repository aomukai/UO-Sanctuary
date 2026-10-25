// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class SentryActorChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(SentryActorChecks));
    private sealed record Body(uint Serial,SentryBodyState State,int Life256);
    private sealed record Saved(uint Owner,int OwnerMap,Point3D OwnerPosition,Body[] Bodies,AssassinTrapEntry[] Roster);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"sentry-actor-fixtures.json");
    public static void Configure(){Register("sentry-actor-check",Check);Register("sentry-actor-reload-check",Reload);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Saved prepared Sentry actor/controller; explicit target/do services, no projectile combat",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static void Differential()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"sentry-controller-source-probe.json")));
        SentryControlState c=null;int count=0,contexts=0;
        foreach(var row in doc.RootElement.EnumerateArray())
        {
            int V(int i)=>row[i].GetInt32();int frame=V(5);
            if(frame==1){c=AssassinSentryController.Start();contexts++;}
            bool hasTarget=V(1)!=1&&(V(1)!=3||frame>=40);int dispatched=0;
            c=AssassinSentryController.Advance(c,V(4)!=1||frame<70,V(4)!=2||frame<70,V(4)==3&&frame>=70,true,true,()=>V(0),
                ()=>hasTarget?new SentryTarget(1,V(1)==2?25:10):null,()=>V(2)==0||frame%2==0,()=>V(3)==0||frame%2==0,_=>dispatched++);
            Require(c.Frame==frame&&c.Mode==V(6)&&c.NextThink==V(7)&&c.Ai.RemainingShots==V(8)
                &&(c.Sequence?.Point??0)==V(9)&&(c.Sequence?.Remaining??0)==V(10)&&(c.Sequence?.Action??0)==V(11)
                &&c.ServerDo==(V(12)!=0)&&c.Thinks==V(13)&&c.Attempts==V(14)&&c.Dispatches==V(15),"Composed source/native frame differential "+count);
            AssassinSentryController.Validate(c);Require(dispatched<=1,"Duplicate sentry dispatch in same frame");count++;
        }
        Require(count==42240&&contexts==192,"Controller source fixture inventory");
        int queries=0;var dead=AssassinSentryController.Start() with{Ai=new(0),NextThink=1};
        dead=AssassinSentryController.Advance(dead,true,true,false,true,true,()=>throw new Exception("Exhausted formula"),
            ()=>{queries++;return null;},()=>throw new Exception("Exhausted chance"),()=>throw new Exception("Exhausted start"),_=>throw new Exception("Exhausted dispatch"));
        Require(dead.Mode==0&&queries==0,"Exhausted shot path queried target service");
    }
    private static AssassinSentryBody Create(PlayerMobile p,DateTime held)=>AssassinSentryBody.CreatePrepared(p,new(p.X+2,p.Y,p.Z),held,SourceRandom.InitLow(19));
    private static void Save(PlayerMobile p)
    {
        var roster=AssassinTrapRoster.For(p);var bodies=roster.Select(x=>(AssassinSentryBody)World.FindMobile((Serial)x.Actor)).ToArray();
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,p.Map.MapID,p.Location,bodies.Select(x=>new Body(x.Serial.Value,x.Prepared,SkillHealth.Balance256(x))).ToArray(),roster)));
    }
    private static void WithServices(PlayerMobile p,Action<Action> run)
    {
        var target=AssassinSentryActors.SelectTarget;var dispatch=AssassinSentryActors.Dispatch;int calls=0;
        // Explicit orchestration services. This records dispatches only, and
        // makes no assertion of actual allegiance, allocation, hits or damage.
        // An actor-local synthetic identity keeps these orchestration services
        // independent from owner-facet travel; real target/aim tests are separate.
        AssassinSentryActors.SelectTarget=body=>new(body.Serial.Value,10);
        AssassinSentryActors.Dispatch=(body,serial)=>{Require(serial==body.Serial.Value,"Retained sequence target changed");calls++;};
        try{run(()=>{Require(calls==1,"Expected exactly one source do dispatch");});}
        finally{AssassinSentryActors.SelectTarget=target;AssassinSentryActors.Dispatch=dispatch;}
    }
    private static void Check()
    {
        Differential();Lifecycle();var p=BladeBodyChecks.Fresh(6120);p.Name="SentryActorFixture";DateTime held=DateTime.UtcNow.AddDays(1);
        var facets=SummonDifficulties.Facets();
        try
        {
            int mana=NecromancerEffects.ManaBalance256(p);
            var blades=Enumerable.Range(0,2).Select(_=>BladeSentinelSummons.Create(p,new(p.X+8,p.Y,p.Z),held)).ToArray();
            var bodies=Enumerable.Range(0,5).Select(_=>Create(p,held)).ToArray();
            Require(blades.All(x=>x.Deleted)&&AssassinTrapRoster.For(p).Select(x=>x.Actor).SequenceEqual(bodies.Select(x=>x.Serial.Value)),"Mixed Blade/Sentry FIFO cap5");
            Require(bodies.All(x=>x.Summoned&&x.SummonMaster==p&&!x.BlocksFit&&!x.CanBeDamaged()&&!x.UsesNativeSummonTimer
                &&x.ControlSlots==0&&x.HitsMax==100&&x.Prepared.Creation.Level==75&&ClassStats.Level(x)==75
                &&ClassStats.AttackRating(x)==x.Prepared.Creation.Attack&&ClassStats.Defense(x)==x.Prepared.Creation.Defense),"Actual stationary actor base stats/owner/footprint/timer");
            Require(NecromancerEffects.ManaBalance256(p)==mana,"Prepared factory charged player input mana");
            var body=bodies[0];
            WithServices(p,one=>
            {
                for(int i=0;i<27;i++){body.AdvanceController(body.Prepared.NextFrameUtc,true);}
                Require(body.Prepared.Control is{Mode:14,Attempts:1,Dispatches:0,Ai.RemainingShots:4,Sequence.Action:1},"Actual prepared world actor first think/shot debit/sequence phase");
                var pending=body.Prepared;body.AdvanceController(body.Prepared.NextFrameUtc,true);one();
                Require(body.Prepared.Control is{Dispatches:1,Sequence.Point:768}&&body.Prepared.Seed==pending.Seed,"World actor dispatch phase repeated RNG");
                // A separate pending actor is retained for real world reload.
                var saved=bodies[1];for(int i=0;i<27;i++){saved.AdvanceController(saved.Prepared.NextFrameUtc,true);}
                var offline=bodies[2];for(int i=0;i<27;i++){offline.AdvanceController(offline.Prepared.NextFrameUtc,true);}
                var before=offline.Prepared;offline.AdvanceController(offline.Prepared.NextFrameUtc.AddMilliseconds(4000),false);
                Require(offline.Prepared.Control is{Mode:1,Sequence:null,Dispatches:0,Ai.RemainingShots:4}
                    &&offline.Prepared.Seed==before.Seed&&offline.Prepared.Control.Frame==before.Control.Frame+101,"Offline windup replayed attacks/RNG/refunded shot or froze clock");
            });
            SkillHealth.SetBalance256(body,12850);var origin=body.Location;var map=body.Map;var captured=body.Prepared.Creation;
            SummonDifficulties.SetFacets([new(Map.Felucca.MapID,MonsterDifficulty.Hell)]);
            p.MoveToWorld(new(p.X,p.Y,p.Z),Map.Felucca);SummonDifficulties.Sync(p);
            Require(body.Map==map&&body.Location==origin&&body.Prepared.Creation.Difficulty==MonsterDifficulty.Hell
                &&body.Prepared.Creation.Skills.SequenceEqual(captured.Skills)&&body.Prepared.Creation.Passives.SequenceEqual(captured.Passives)
                &&SkillHealth.Balance256(body)==12850,"Owner-facet rescale moved trap or lost fractional injury/birth context");
            var ownerPosition=p.Location;p.MoveToWorld(new(1496,1628,10),Map.Felucca);
            Require(Server.Spells.SpellHelper.IsTown(p.Location,p),"Cross-facet town fixture");
            var select=AssassinSentryActors.SelectTarget;AssassinSentryActors.SelectTarget=_=>null;
            try{var retained=bodies[4];retained.AdvanceController(retained.Prepared.NextFrameUtc.AddMilliseconds(24*40),true);
                Require(!retained.Deleted&&retained.Prepared.Control is{Mode:1,Thinks:1,Attempts:0},"Cross-facet owner-town transition removed retained trap");}
            finally{AssassinSentryActors.SelectTarget=select;}
            p.MoveToWorld(ownerPosition,Map.Felucca);
            Require(!AssassinTrapRoster.Fighting(p.Serial.Value),"Socket-null owner can fight through world adapter");
            // Preserve an independent copy for each process inspection; timers
            // cannot reach the deliberately held future frame in this suite.
            Save(p);Require(!AssassinCombat.Implemented(CombatAbility.ChargedBoltSentry),"Prepared body enabled unfinished learned skill");
            Log.Information("Sentry actor:42240 source/native frames, mixed cap5/oldest Blade replacement, saved stationary owner/stats, source phase dispatch via explicit services, offline no replay and owner-facet/old-facet fractional life pass");
        }
        finally{SummonDifficulties.SetFacets(facets);}
    }
    private static void Lifecycle()
    {
        var p=BladeBodyChecks.Fresh(6160);p.Name="SentryActorLifecycleFixture";
        try
        {
            var body=Create(p,DateTime.UtcNow.AddDays(1));var before=body.Prepared;
            body.AdvanceController(before.NextFrameUtc.AddDays(1),false);
            Require(body.Prepared.Control.Frame==2160001&&body.Prepared.Control is{Mode:1,Attempts:0,Dispatches:0,Ai.RemainingShots:-1}
                &&body.Prepared.Seed==before.Seed&&!body.Deleted,"Long offline interval replayed actions, invented expiry or froze elapsed clock");
            Require(AssassinSentryActors.SelectTarget!=null&&AssassinSentryActors.Dispatch!=null,"Sentry target or real projectile adapter is not bound");
            var selector=AssassinSentryActors.SelectTarget;AssassinSentryActors.SelectTarget=null;
            bool refused=false;try{body.AdvanceController(body.Prepared.NextFrameUtc,true);}catch(NotSupportedException e)
            {refused=e.Message.Contains("target-selection");body.SuspendController(e.Message);}
            finally{AssassinSentryActors.SelectTarget=selector;}
            Require(refused&&body.Prepared.ControllerFault!=null&&body.Prepared.Control.Attempts==0,"Unconnected world target service claimed success");
            body.AdvanceController(body.Prepared.NextFrameUtc.AddMilliseconds(400),true);
            Require(body.Prepared.Control is{Attempts:0,Dispatches:0},"Faulted controller replayed actions");
            var position=p.Location;var town=Create(p,DateTime.UtcNow.AddDays(1));
            p.MoveToWorld(new(1496,1628,10),Map.Trammel);
            Require(Server.Spells.SpellHelper.IsTown(p.Location,p),"Owner-town fixture");
            town.AdvanceController(town.Prepared.NextFrameUtc.AddMilliseconds(24*40),true);
            Require(town.Deleted,"Same-facet owner-town source exit was suppressed");
            p.MoveToWorld(position,Map.Trammel);
            p.Kill();AssassinTrapRoster.SweepOwners();Require(body.Deleted&&AssassinTrapRoster.For(p).Length==0,"Owner death retained registered sentry");
        }
        finally{p.Delete();}
    }
    private static void Reload()
    {
        Differential();var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);
        Require(AssassinTrapRoster.For(p).SequenceEqual(saved.Roster),"Saved mixed roster order changed");
        foreach(var old in saved.Bodies)
        {
            var body=World.FindMobile((Serial)old.Serial) as AssassinSentryBody;
            Require(body!=null&&!body.Deleted&&body.SummonMaster==p&&body.Map.MapID==old.State.Map&&body.Location==old.State.Position
                &&JsonSerializer.Serialize(body.Prepared)==JsonSerializer.Serialize(old.State)&&SkillHealth.Balance256(body)==old.Life256
                &&!body.UsesNativeSummonTimer&&!body.BlocksFit&&ClassStats.Level(body)==old.State.Creation.Level,"World actor reload lost actual source/clock/target/owner/stats/FP256 life");
        }
        p.MoveToWorld(saved.OwnerPosition,Map.Maps[saved.OwnerMap]);
        WithServices(p,one=>
        {
            // On first reload resume the saved action1; on the next reload run
            // through neutral/think until the following real dispatch boundary.
            var body=(AssassinSentryBody)World.FindMobile((Serial)saved.Bodies[1].Serial);int calls=body.Prepared.Control.Dispatches;
            for(int i=0;i<40&&body.Prepared.Control.Dispatches==calls;i++){body.AdvanceController(body.Prepared.NextFrameUtc,true);}
            one();Require(body.Prepared.Control.Dispatches==calls+1,"Reload missed/repeated pending dispatch");
        });
        var oldest=saved.Roster[0];var fresh=Create(p,DateTime.UtcNow.AddDays(1));var roster=AssassinTrapRoster.For(p);
        Require(World.FindMobile((Serial)oldest.Actor)==null&&roster.Length==5&&roster.Take(4).SequenceEqual(saved.Roster.Skip(1))
            &&roster[^1].Actor==fresh.Serial.Value&&roster[^1].Order>saved.Roster[^1].Order,"Reload cap5 replaced the wrong trap or lost FIFO order");
        Save(p);Require(!AssassinCombat.Implemented(CombatAbility.ChargedBoltSentry),"Reload enabled unfinished learned skill");
        Log.Information("Sentry actor reload: actual world entities retain shot/phase/target/RNG/deadline/facet/stats/injury and FIFO; exactly one continued dispatch via declared fixture service");
    }
}
