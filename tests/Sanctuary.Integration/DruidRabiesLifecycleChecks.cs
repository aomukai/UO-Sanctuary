// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class DruidRabiesLifecycleChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(DruidRabiesLifecycleChecks));
    private sealed record Saved(uint OldCaster,uint NewCaster,uint Host,uint Other,RabiesInfectionSave State,TimedEffectState[] Poison,DateTime Clock,int Reloads);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"druid-rabies-lifecycle-fixtures.json");
    private static int _scene;
    public static void Configure()
    {Register("druid-rabies-lifecycle-check",Check);Register("druid-rabies-lifecycle-reload-check",Reload);Register("druid-rabies-lifecycle-snapshot-check",Snapshot);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Rabies separate marker/emitter lifecycle and version0 migration",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value);
    private static PlayerMobile Fresh()
    {
        var owner=new PlayerMobile{Body=0x190,Player=true,Name="RabiesLifecycleFixture"};owner.AddItem(new Backpack());ClassCreation.Profiles.Assign(owner,DiabloClass.Druid);ClassCreation.Profiles.AdvanceLevel(owner,99);
        FixtureSpace.Corridor(owner,6040,12,3500+32*_scene++);return owner;
    }
    private static BladeMissileTarget Target(PlayerMobile owner,int offset,bool immune=false)
    {
        var target=new BladeMissileTarget{Name="RabiesLifecycleFixtureTarget",NoKillAwards=true,Frozen=true,CantWalk=true};
        target.MoveToWorld(new(owner.X+offset,owner.Y,owner.Z),owner.Map);target.AIObject.AITimer.Stop();target.SetHits(50000);target.Hits=target.HitsMax;
        target.SetResistance(ResistanceType.Poison,immune?100:0);return target;
    }
    private static void Model()
    {
        using var document=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"druid-rabies-lifecycle-source-probe.json")));int count=0,callbacks=0;
        foreach(var row in document.RootElement.GetProperty("emitters").EnumerateArray())
        {
            int scene=row[1].GetInt32(),total=row[2].GetInt32(),age=0,steps=0;var births=new List<int>();bool retired=false;
            while(!retired)
            {
                steps++;var next=DruidRabiesEmitterClock.Step(total,age,scene!=4||steps<5,scene!=3||steps<5);
                if(next.Emit){births.Add(age);}age=next.Age;retired=next.Retire;callbacks++;
            }
            Require(steps==row[3].GetInt32()&&total-age==row[5].GetInt32()
                &&births.SequenceEqual(row[8].EnumerateArray().Select(x=>x.GetInt32())),"Complete source emitter clock mismatch");count++;
        }
        Require(count==1275,"Incomplete all-rank emitter matrix");Log.Information("Rabies lifecycle model: {Count} complete-source contexts / {Callbacks} callbacks pass",count,callbacks);
    }
    private static void MissingMarker()
    {
        var original=DruidRabiesInfections.Snapshot();var owner=Fresh();var host=Target(owner,1,true);var at=TimedEffects.Now.Add(FixtureSpace.Retention);
        try
        {
            var marker=DruidRabiesInfections.Mark(owner,host,1,100,at);var legacy=DruidRabiesInfections.Snapshot();
            DruidRabiesInfections.Restore(legacy with{Markers=null,Infections=[legacy.Infections[0] with{Emitting=false,NextUtc=marker.UntilUtc}]});
            Require(DruidRabiesInfections.Infection(host)==marker&&DruidRabiesInfections.Emitters(owner.Serial.Value).Length==0,"Version0 stopped-emitter migration lost marker or revived emitter");
            DruidRabiesInfections.Restore(legacy with{Markers=null});
            Require(Json(DruidRabiesInfections.Snapshot())==Json(legacy),"Version0 migration reset marker/emitter/rank/expiry/seed");
            try{DruidRabiesInfections.Restore(legacy with{Markers=[marker,marker]});throw new InvalidOperationException("Duplicate marker accepted");}catch(InvalidDataException){}
            Require(Json(DruidRabiesInfections.Snapshot())==Json(legacy),"Invalid marker restore partially changed the pool");
            // Loaded controllers skip overdue actions. Consume only the first
            // due frame, then verify the rest of the original age stream.
            DruidRabiesInfections.Tick(at.AddMilliseconds(40),true,owner.Serial.Value);
            DruidRabiesInfections.Tick(marker.UntilUtc,true,owner.Serial.Value);
            Require(DruidRabiesInfections.Infection(host)==null&&DruidRabiesInfections.Emitters(owner.Serial.Value).Single().Age==100,"Marker expiry erased its independent emitter");
            long before=DruidRabiesInfections.Snapshot().NextId;
            DruidRabiesInfections.Tick(marker.UntilUtc.AddMilliseconds(40),true,owner.Serial.Value);
            var tail=DruidRabiesInfections.Children(owner.Serial.Value).Single(x=>x.Id>before);
            Require(tail.InfectionUntilUtc==DateTime.UnixEpoch&&DruidRabiesInfections.Emitters(owner.Serial.Value).Single().Age==101,"Tail failed zeroed missing-marker deadline or negative current transition");
            Require(!DruidRabiesInfections.Contact(tail,owner,host,marker.UntilUtc.AddMilliseconds(40),out _)
                &&DruidRabiesInfections.Infection(host)==null,"Missing-marker child invented a valid infection deadline");
            DruidRabiesInfections.Tick(marker.UntilUtc.AddMilliseconds(80),true,owner.Serial.Value);
            Require(DruidRabiesInfections.Emitters(owner.Serial.Value).Length==0,"Negative-current emitter did not retire");
        }
        finally{DruidRabiesInfections.Restore(original);host.Delete();owner.Delete();}
    }
    private static TimedEffectState[] Poison(uint other)=>TimedEffects.Snapshot().Where(x=>x.Owner==other).ToArray();
    private static void Check()
    {
        Model();DruidRabiesInfectionChecks.Cases();MissingMarker();
        var owner=Fresh();var replacement=Fresh();replacement.MoveToWorld(new(owner.X,owner.Y+1,owner.Z),owner.Map);
        var host=Target(owner,1,true);var other=Target(owner,8);var at=TimedEffects.Now.Add(FixtureSpace.Retention);
        var old=DruidRabiesInfections.Mark(owner,host,3,120,at);
        DruidRabiesInfections.Tick(old.UntilUtc,true,owner.Serial.Value);
        Require(DruidRabiesInfections.Infection(host)==null&&DruidRabiesInfections.Emitters(owner.Serial.Value).Single().Age==120,"Old marker/emitter expiry coupling");
        var replaceAt=old.UntilUtc.AddMilliseconds(40);var marker=DruidRabiesInfections.Mark(replacement,host,1,100,replaceAt);
        Require(DruidRabiesInfections.Emitters(owner.Serial.Value).Length==1&&DruidRabiesInfections.Emitters(replacement.Serial.Value).Length==1,"Reinfection replaced the old emitter");
        long before=DruidRabiesInfections.Snapshot().NextId;
        DruidRabiesInfections.Tick(replaceAt,true,owner.Serial.Value);
        var tail=DruidRabiesInfections.Children(owner.Serial.Value).Single(x=>x.Id>before);
        Require(tail.Caster==owner.Serial.Value&&tail.Rank==3&&tail.Damage.Elements.PoisonMin==112&&tail.InfectionUntilUtc==marker.UntilUtc
            &&DruidRabiesInfections.Infection(host).Caster==replacement.Serial.Value,"Old emitter adopted new marker caster/rank or lost replacement deadline");
        other.MoveToWorld(new(owner.X+3,owner.Y,owner.Z),owner.Map);
        Require(DruidRabiesInfections.Contact(tail,owner,other,replaceAt,out _),"Old-emitter replacement-deadline contact");
        Require(DruidRabiesInfections.Infection(other) is{Rank:3} secondary&&secondary.Caster==owner.Serial.Value&&secondary.UntilUtc==marker.UntilUtc
            &&Poison(other.Serial.Value).Single().PoisonMs>0,"Replacement deadline failed original-caster spread/poison attribution");
        // Save while the old emitter has current=-1 and both new emitters are
        // still waiting for their first callback. Real reload must not replay.
        var state=DruidRabiesInfections.Snapshot();
        Require(state.Markers.Length==2&&state.Infections.Length==3&&state.Infections.Single(x=>x.Id==old.Id).Age==121,"Missing independent marker/old/new save identities");
        File.WriteAllText(Manifest,Json(new Saved(owner.Serial.Value,replacement.Serial.Value,host.Serial.Value,other.Serial.Value,state,Poison(other.Serial.Value),replaceAt.AddMilliseconds(40),0)));
        Log.Information("Rabies lifecycle: missing-marker zero deadline, overlapping replacement emitters, retained old caster/rank and actual secondary poison pass; saved version1, version0 migration retained");
    }
    private static void Snapshot()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));Require(Json(DruidRabiesInfections.Snapshot())==Json(saved.State)&&Json(Poison(saved.Other))==Json(saved.Poison),"Lifecycle changed before save");
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));
        Require(World.FindMobile((Serial)saved.OldCaster) is PlayerMobile{Alive:true}&&World.FindMobile((Serial)saved.NewCaster) is PlayerMobile{Alive:true}
            &&World.FindMobile((Serial)saved.Host) is BaseCreature{Alive:true}&&World.FindMobile((Serial)saved.Other) is BaseCreature{Alive:true},"Lifecycle saved actors missing");
        Require(Json(DruidRabiesInfections.Snapshot())==Json(saved.State)&&Json(Poison(saved.Other))==Json(saved.Poison),"Lifecycle save lost markers/emitters/paths/seeds/poison");
        DruidRabiesInfections.Tick(saved.Clock,true,saved.OldCaster);DruidRabiesInfections.Tick(saved.Clock,true,saved.NewCaster);
        var state=DruidRabiesInfections.Snapshot();
        Require(Json(state.Markers)==Json(saved.State.Markers)&&Json(state.Seeds)==Json(saved.State.Seeds)&&state.NextId==saved.State.NextId
            &&state.Contagions.All(x=>saved.State.Contagions.Any(y=>y.Id==x.Id))&&Json(Poison(saved.Other))==Json(saved.Poison),"Reload replayed emission/damage or changed deadline ownership");
        Require(state.Infections.Length==2,"Negative-current old emitter did not retire independently on resume");
        File.WriteAllText(Manifest,Json(saved with{State=state,Clock=saved.Clock.AddMilliseconds(40),Reloads=saved.Reloads+1}));
    }
}
