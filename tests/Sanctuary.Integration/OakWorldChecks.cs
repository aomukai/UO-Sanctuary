// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Accounting;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary.Integration;

public static class OakWorldChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(OakWorldChecks));
    private sealed record Saved(uint Owner,uint Body,OakBodyState State,int Life,int Map,Point3D Point);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"oak-world-fixtures.json");
    private static int _scene;
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Prepared saved Oak source motion/life aura",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    public static void Configure()
    {
        Register("oak-world-check",Check);Register("oak-world-reload-check",Reload);
        Register("oak-world-live-check",()=>FixtureNetwork.Connected("oak-world-live-result",Live));
    }
    private static void Floor(Map map,int x,int y,int z)
    {
        int floor=Enumerable.Range(0x500,0x100).First(i=>TileData.ItemTable[i].Surface&&!TileData.ItemTable[i].Impassable&&TileData.ItemTable[i].CalcHeight==0);
        for(int col=x-20;col<=x+50;col++)for(int row=y-16;row<=y+16;row++){new Static(floor).MoveToWorld(new(col,row,z),map);}
    }
    private static PlayerMobile Fresh()
    {
        var p=new PlayerMobile{Body=0x190,Player=true,Name="OakWorldFixture"};p.AddItem(new Backpack());
        ClassCreation.Profiles.Assign(p,DiabloClass.Druid);ClassCreation.Profiles.AdvanceLevel(p,99);
        Require(ClassCreation.Profiles.TryTrain(p.Serial,"druid.oaksage",out _),"Train Oak world fixture");
        int y=2850+64*_scene++;Floor(Map.Trammel,7080,y,100);p.MoveToWorld(new(7080,y,100),Map.Trammel);return p;
    }
    private static void End(PlayerMobile p)
    {
        foreach(var pet in (p.AllFollowers??[]).OfType<BaseCreature>().ToArray()){DruidOakAuras.Clear(pet);}
        DruidOakAuras.Clear(p);PersistentMinions.Dismiss(p);p.Delete();
    }
    private static void Through(DruidOakBody body,int frame,bool actions=true)
    {
        var s=body.Prepared.Controller;Require(frame>s.Frame,"Reverse Oak test frame");
        body.Advance(s.NextUtc.AddMilliseconds((frame-s.Frame-1)*40L),actions);
    }
    private static void Movement()
    {
        var p=Fresh();var at=TimedEffects.Now.AddDays(2);var body=DruidOakWorld.Create(p,new(p.X+14,p.Y,p.Z),at,735);
        try
        {
            var origin=body.Location;Through(body,14);Require(body.Location==origin&&body.Prepared.Controller.Thinks==0,"Oak ignored labelled initial aidel timing");
            Through(body,15);var path=body.Prepared.Controller;Require(path.Motion.Points.Length>0,"Follow fixture has no saved path");
            var first=path.Motion.Points[0];path.Motion.Points[0]=new(1,1);Require(body.Prepared.Controller.Motion.Points[0]==first,"Caller changed saved Oak path points");
            Through(body,120);Require(body.Prepared.Controller.Fault==null&&Math.Abs(body.X-p.X)<14&&body.Prepared.Controller.Thinks>0,"Oak common-pet follow did not move");
            var before=body.Prepared.Controller;SkillHealth.SetBalance256(body,6407);body.Advance(before.NextUtc.AddMinutes(1),false);
            Require(body.Map==Map.Internal&&SkillHealth.Balance256(body)==6407&&body.Prepared.Controller.Seed==before.Seed,"Offline Oak moved/drew/healed");
            int regen=BattleOrders.Regeneration256(body);body.Advance(body.Prepared.Controller.NextUtc,true);
            Require(body.Map==p.Map&&SkillHealth.Balance256(body)==6407+regen,"Oak resume replayed offline recovery");
            var good=body.Prepared;string bytes=body.SavedText;bool refused=false;
            try{body.RestorePrepared(good with{Controller=good.Controller with{Frame=-1}});}catch(InvalidDataException){refused=true;}
            Require(refused&&body.SavedText==bytes,"Invalid Oak controller changed saved state");
            var legacy=JsonSerializer.SerializeToNode(new OakBodyState(p.Serial.Value,good.Birth,good.Difficulty,good.NextRecoveryUtc));legacy.AsObject().Remove("Controller");
            var decoded=legacy.Deserialize<OakBodyState>();DruidOakBody.Validate(decoded);
            Require(decoded.Controller==null&&decoded.Birth==good.Birth,"Legacy body-only JSON lost birth or invented motion");
        }
        finally{End(p);}
    }
    private static void WarpAndEscape()
    {
        var p=Fresh();var at=TimedEffects.Now.AddDays(2);var body=DruidOakWorld.Create(p,new(p.X+24,p.Y,p.Z),at,736);
        try
        {
            Through(body,15);Require(body.Prepared.Controller is{Teleports:1,Mode:1,NextThink:40}&&Math.Abs(body.X-p.X)<=4,"Oak far-owner free-position teleport/idle");body.Delete();
            var enemy=new GraveyardZombie{CantWalk=true,NoKillAwards=true};enemy.MoveToWorld(new(p.X+3,p.Y,p.Z),p.Map);enemy.AIObject.AITimer.Stop();
            try
            {
                ulong chosen=0;for(ulong seed=1;seed<100;seed++){ulong cursor=seed;if(SourceRandom.Roll(ref cursor)%100<20){chosen=seed;break;}}
                Require(chosen!=0,"No escape fixture seed");var retreat=DruidOakWorld.Create(p,new(p.X+2,p.Y,p.Z),at,chosen);Through(retreat,15);
                Require(retreat.Prepared.Controller is{EscapeAttempts:1,Mode:2}&&retreat.Prepared.Controller.Motion.FinalTarget.X<retreat.X*2,"Oak did not retreat from combat target");
                var before=retreat.Prepared.Controller;var position=retreat.Location;retreat.Frozen=true;Through(retreat,40);
                Require(retreat.Location==position&&retreat.Prepared.Controller.Seed==before.Seed&&retreat.Prepared.Controller.Thinks==before.Thinks,"Frozen Oak moved or ran AI");
                retreat.Frozen=false;
            }
            finally{enemy.Delete();}
        }
        finally{End(p);}
    }
    private static void Lifecycle()
    {
        var p=Fresh();var facets=SummonDifficulties.Facets();var at=TimedEffects.Now.AddDays(2);var body=DruidOakWorld.Create(p,new(p.X+2,p.Y,p.Z),at,737);
        try
        {
            Through(body,10);SkillHealth.SetBalance256(body,6407);var seed=body.Prepared.Controller.Seed;
            Require(WardrobeTransaction.ChangeClass(p,DiabloClass.Paladin,out _,out _),"Oak world wardrobe park");
            body.Advance(body.Prepared.Controller.NextUtc.AddMinutes(1),true);
            Require(body.Map==Map.Internal&&SkillHealth.Balance256(body)==6407&&body.Prepared.Controller.Seed==seed,"Inactive Oak AI/healing replay");
            Require(WardrobeTransaction.ChangeClass(p,DiabloClass.Druid,out _,out _),"Oak world wardrobe restore");body.Advance(body.Prepared.Controller.NextUtc,true);
            Floor(Map.Felucca,p.X,p.Y,p.Z);SummonDifficulties.SetFacets([new(Map.Trammel.MapID,MonsterDifficulty.Normal),new(Map.Felucca.MapID,MonsterDifficulty.Hell)]);
            p.MoveToWorld(p.Location,Map.Felucca);int life=SkillHealth.Balance256(body),regen=BattleOrders.Regeneration256(body);
            body.Advance(body.Prepared.Controller.NextUtc,true);
            Require(body.Map==p.Map&&body.Prepared.Controller.Map==p.Map.MapID&&body.Prepared.Difficulty==MonsterDifficulty.Hell&&body.PoisonResistance==200
                && SkillHealth.Balance256(body)==life+regen,"Active Oak facet transport/rescale altered injury or replayed recovery");
            p.Kill();Require(body.Deleted,"Owner death retained world Oak");p.Corpse?.Delete();
        }
        finally{SummonDifficulties.SetFacets(facets);End(p);}
    }
    private static void Check()
    {
        Require(!DruidCombat.Implemented(CombatAbility.OakSage),"Motion preparation exposed learned casting");Movement();WarpAndEscape();Lifecycle();
        var p=Fresh();var body=DruidOakWorld.Create(p,new(p.X+14,p.Y,p.Z),TimedEffects.Now.AddDays(2),738);Through(body,80);SkillHealth.SetBalance256(body,6407);PersistentMinions.Suspend(p);
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,body.Serial.Value,body.Prepared,SkillHealth.Balance256(body),p.Map.MapID,p.Location)));
        Log.Information("Oak world: source-pet follow, size-aware free-position warp, chance-gated escape, frozen/offline RNG suppression, wardrobe/facet/owner-death and legacy body-only decode pass; first-think timing and geometry remain labelled adapters");
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);var body=World.FindMobile((Serial)saved.Body) as DruidOakBody;
        Require(body is{Alive:true,Deleted:false}&&body.ControlMaster==p&&body.Map==Map.Internal&&body.SavedText==JsonSerializer.Serialize(saved.State)
            && SkillHealth.Balance256(body)==saved.Life,"Saved Oak controller/birth/pose/injury changed");
        if(p.Map==Map.Internal){p.MoveToWorld(saved.Point,Map.Maps[saved.Map]);}
        var before=body.Prepared.Controller;body.Advance(before.NextUtc,false);
        Require(SkillHealth.Balance256(body)==saved.Life&&body.Prepared.Controller.Seed==before.Seed&&body.Prepared.Controller.Thinks==before.Thinks,"Reload replayed Oak RNG/AI/healing");
        int regen=BattleOrders.Regeneration256(body);body.Advance(body.Prepared.Controller.NextUtc,true);
        Require(body.Map==p.Map&&SkillHealth.Balance256(body)==Math.Min(body.MaximumLife256,saved.Life+regen),"Loaded Oak did not resume one active recovery frame");
        PersistentMinions.Suspend(p);File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{State=body.Prepared,Life=SkillHealth.Balance256(body)}));
    }
    private static void Live(NetState network)
    {
        var p=Fresh();network.Seeded=network.SentFirstPacket=true;network.CompressionEnabled=false;
        var field=typeof(NetState).GetField("_protocolState",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
        Require(field!=null,"Oak protocol bootstrap");field.SetValue(network,Enum.Parse(field.FieldType,"GameServer_LoggedIn"));
        var account=new Account("oak-world-"+Guid.NewGuid().ToString("N"),Guid.NewGuid().ToString("N"));account[0]=p;
        network.Account=account;network.Mobile=p;p.NetState=network;network.ResetMovementState();
        int maximum=p.HitsMax;var body=DruidOakWorld.Create(p,new(p.X+14,p.Y,p.Z),TimedEffects.Now,739);var origin=body.Location;long deadline=Core.TickCount+12000;bool followed=false;
        void Inspect()
        {
            try
            {
                Require(network.Running&&Core.TickCount<deadline&&(followed||!body.Deleted&&body.Prepared.Controller.Fault==null),"Natural Oak follow/aura clock timed out/refused");
                if(!followed&&body.Location!=origin&&DruidOakAuras.Percent(p)==30)
                {Require(Math.Abs(body.X-p.X)<14&&body.Prepared.Controller.Thinks>0,"Natural Oak moved away from owner");body.Delete();followed=true;}
                if(followed&&DruidOakAuras.State(p)==null)
                {
                    Require(p.HitsMax==maximum,"Natural Oak expiry did not restore player maximum");End(p);account.Delete();Log.Information("oak-world-live-result: passed");return;
                }
                Timer.StartTimer(TimeSpan.FromMilliseconds(30),Inspect);
            }
            catch(Exception e){body.Delete();End(p);account.Delete();Log.Error(e,"oak-world-live-result: failed");}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("oak-world-live: ready");Inspect();});
    }
}
