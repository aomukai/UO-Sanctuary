// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class CreeperGroundChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(CreeperGroundChecks));
    private sealed record Saved(uint Owner,uint RemovedActor,CreeperGround Ground,int Map,Point3D Point);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"creeper-ground-fixtures.json");private static int _scene;
    private static void Require(bool b,string why){if(!b){throw new InvalidOperationException(why);}}
    public static void Configure()
    {Register("creeper-ground-check",Check);Register("creeper-ground-reload-check",Reload);}
    private static void Register(string name,Action check)=>ConsoleInputHandler.RegisterCommand(name,"Independent Creeper ground field acceptance",_=>Core.LoopContext.Post(()=>
    {try{check();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static PlayerMobile Fresh()
    {
        var p=new PlayerMobile{Body=0x190,Player=true,Name="CreeperGroundFixture"};p.AddItem(new Backpack());ClassCreation.Profiles.Assign(p,DiabloClass.Druid);
        ClassCreation.Profiles.AdvanceLevel(p,99);Require(ClassCreation.Profiles.TryTrain(p.Serial,"druid.poisoncreeper",out _),"Train Creeper");
        int y=3400+64*_scene++,floor=Enumerable.Range(0x500,0x100).First(i=>TileData.ItemTable[i].Surface&&!TileData.ItemTable[i].Impassable&&TileData.ItemTable[i].CalcHeight==0);
        for(int x=7070;x<=7130;x++)for(int row=y-20;row<=y+20;row++){new Static(floor).MoveToWorld(new(x,row,100),Map.Trammel);}
        p.MoveToWorld(new(7100,y,100),Map.Trammel);return p;
    }
    private static void End(PlayerMobile p){DruidCreeperGround.Cancel(p);PersistentMinions.Dismiss(p);p.Delete();}
    private static GraveyardZombie Enemy(Point3D point,Map map)
    {var e=new GraveyardZombie{CantWalk=true,NoKillAwards=true};e.MoveToWorld(point,map);e.AIObject.AITimer.Stop();return e;}
    private static long Create(PlayerMobile p,DruidCreeperBody body,DateTime at)
        =>DruidCreeperGround.Create(body,1,DruidCreeperVines.Heads(1,(p.X+3)*2,p.Y*2),at);
    private static void Through(PlayerMobile p,DateTime at,bool actions=true)=>DruidCreeperGround.Tick(at,actions,p.Serial.Value);
    private static void LifetimeAndFacet()
    {
        var p=Fresh();var point=p.Location;var map=p.Map;var at=TimedEffects.Now.AddDays(2);
        var body=DruidCreeperBody.CreatePrepared(p,new(p.X+2,p.Y,p.Z),at);var e=Enemy(new(p.X+3,p.Y,p.Z),map);
        try
        {
            long id=Create(p,body,at);Through(p,at.AddMilliseconds(12*40));var s=DruidCreeperGround.Get(id);
            Require(s.Fields.Heads.Length==3&&s.Fields.Mats.Length==6&&s.Fields.Mats.Any(m=>m.LastCollision==e.Serial.Value),"Independent fields did not acquire initial collision history");
            var copy=DruidCreeperGround.Get(id);var prior=copy.Fields.Heads[0].Path[1];copy.Fields.Heads[0].Path[1]=new(88,88);
            Require(DruidCreeperGround.Get(id).Fields.Heads[0].Path[1]==prior,"Independent snapshot aliases live path");
            body.Delete();p.MoveToWorld(point,Map.Felucca);var other=Enemy(e.Location,map);
            try
            {
                int enemyLife=SkillHealth.Balance256(e),otherLife=SkillHealth.Balance256(other);
                Through(p,at.AddMilliseconds(30*40));s=DruidCreeperGround.Get(id);
                Require(s is{Map:1} && s.Map==map.MapID && s.Fields.Mats.Length==6 && s.Fields.Heads.Length==0&&s.Fields.Contacts>0,
                    "Old-facet mats stopped/relocated/disappeared with their removed actor");
                Require(SkillHealth.Balance256(e)==enemyLife&&SkillHealth.Balance256(other)==otherLife,"Contact-capture fields delivered invented poison");
                int contacts=s.Fields.Contacts;Through(p,at.AddMilliseconds(35*40),false);
                Require(DruidCreeperGround.Get(id).Fields.Contacts==contacts,"Inactive field contacted a target");
                Through(p,at.AddSeconds(6),false);Require(DruidCreeperGround.Get(id)==null,"Inactive mat lifetime did not run to expiry");
            }
            finally{other.Delete();}
            p.MoveToWorld(point,map);var gone=DruidCreeperBody.CreatePrepared(p,new(p.X+2,p.Y,p.Z),at);
            long missing=Create(p,gone,at);gone.Delete();Through(p,at.AddMilliseconds(12*40));
            Require(DruidCreeperGround.Get(missing) is{Fields.Mats.Length:0},"Missing owner created fresh child mats");
            Through(p,at.AddMilliseconds(20*40));Require(DruidCreeperGround.Get(missing)==null,"Ownerless heads did not finish finite travel");
            var remote=DruidCreeperBody.CreatePrepared(p,new(p.X+2,p.Y,p.Z),at);long far=Create(p,remote,at);
            remote.MoveToWorld(new(p.X+65,p.Y,p.Z),map);Through(p,at.AddMilliseconds(12*40));
            Require(DruidCreeperGround.Get(far) is{Fields.Mats.Length:0},"Child wrapper ignored source-unit distance above100");
            remote.Delete();
        }
        finally{e.Delete();End(p);}
    }
    private static void MigrationAndRestore()
    {
        var p=Fresh();var at=TimedEffects.Now.AddDays(2);var body=DruidCreeperWorld.Create(p,new(p.X+2,p.Y,p.Z),at,823,1000);
        try
        {
            var embedded=new CreeperFields([], [new(1,1,(p.X+3)*2,p.Y*2,125,100,123){Z=100}],1,1,123);
            body.RestorePrepared(body.Prepared with{Controller=body.Prepared.Controller with{Fields=embedded,LastFieldId=0}});
            DruidCreeperGround.Migrate(body);var id=body.Prepared.Controller.LastFieldId;
            Require(id>0&&body.Prepared.Controller.Fields.Mats.Length==0&&JsonSerializer.Serialize(DruidCreeperGround.Get(id).Fields)==JsonSerializer.Serialize(embedded),"Legacy migration lost field age/history");
            long sequence=DruidCreeperGround.Snapshot().NextId;DruidCreeperGround.Migrate(body);
            Require(DruidCreeperGround.Snapshot().NextId==sequence,"Legacy migration duplicated the flight");
            var valid=DruidCreeperGround.Snapshot();string before=JsonSerializer.Serialize(valid);bool refused=false;
            try{DruidCreeperGround.Restore(valid with{Flights=[..valid.Flights,valid.Flights.Single(s=>s.Id==id)]});}catch(InvalidDataException){refused=true;}
            Require(refused&&JsonSerializer.Serialize(DruidCreeperGround.Snapshot())==before,"Duplicate restore was not atomic");
            refused=false;try{DruidCreeperGround.Restore(valid with{Flights=[valid.Flights.Single(s=>s.Id==id) with{Map=-1}]});}catch(InvalidDataException){refused=true;}
            Require(refused&&JsonSerializer.Serialize(DruidCreeperGround.Snapshot())==before,"Invalid facet restore was not atomic");
            p.Kill();Through(p,at.AddMilliseconds(40));Require(DruidCreeperGround.Get(id)==null,"Owner death retained damaging field preparation");
        }
        finally{End(p);}
    }
    private static void Check()
    {
        LifetimeAndFacet();MigrationAndRestore();
        var p=Fresh();var at=TimedEffects.Now.AddDays(2);var point=p.Location;var body=DruidCreeperBody.CreatePrepared(p,new(p.X+2,p.Y,p.Z),at);
        var e=Enemy(new(p.X+3,p.Y,p.Z),p.Map);long id=Create(p,body,at);Through(p,at.AddMilliseconds(12*40));uint removed=body.Serial.Value;body.Delete();e.Delete();
        var saved=new Saved(p.Serial.Value,removed,DruidCreeperGround.Get(id),p.Map.MapID,point);p.Internalize();
        File.WriteAllText(Manifest,JsonSerializer.Serialize(saved));
        Log.Information("Creeper independent fields: actor deletion, old-facet contacts, original allegiance, no invented poison, inactive expiry, missing-owner child refusal, copied state, legacy migration, atomic invalid restore and owner death pass; removed-actor fields saved");
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);
        var s=DruidCreeperGround.Get(saved.Ground.Id);
        Require(p is{Deleted:false,Alive:true} && World.FindMobile((Serial)saved.RemovedActor)==null
            && JsonSerializer.Serialize(s)==JsonSerializer.Serialize(saved.Ground),"Independent field/removed actor did not survive actual reload");
        int contacts=s.Fields.Contacts;var before=s.Fields.Heads[0];Through(p,s.NextUtc,false);s=DruidCreeperGround.Get(s.Id);
        Require(s.Fields.Heads[0].X==before.X&&s.Fields.Heads[0].Y==before.Y&&s.Fields.Heads[0].Remaining==before.Remaining-1
            && s.Fields.Contacts==contacts,"Loaded fields replayed movement/emission/contact");
        p.MoveToWorld(saved.Point,Map.Maps[saved.Map]);Through(p,s.NextUtc,true);s=DruidCreeperGround.Get(s.Id);
        Require(s.Fields.Heads.Length==3&&s.Fields.Mats.Length==6&&s.Fields.Contacts==contacts,"Resumed independent field lost its removed-owner state");
        p.Internalize();File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{Ground=s}));
    }
}
