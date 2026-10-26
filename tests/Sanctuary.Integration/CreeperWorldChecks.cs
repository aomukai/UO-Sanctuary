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

public static class CreeperWorldChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(CreeperWorldChecks));
    private sealed record Saved(uint Owner,uint Body,CreeperBodyState State,int Life,int Map,Point3D Point,CreeperGround Ground);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"creeper-world-fixtures.json");
    private static int _scene;
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static void Register(string name,Action check)=>ConsoleInputHandler.RegisterCommand(name,"Prepared Creeper world/launch capture",_=>Core.LoopContext.Post(()=>
    {try{check();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    public static void Configure()
    {
        Register("creeper-world-check",Check);Register("creeper-world-reload-check",Reload);
        Register("creeper-world-live-check",()=>FixtureNetwork.Connected("creeper-world-live-result",Live));
    }
    private static PlayerMobile Fresh()
    {
        var p=new PlayerMobile{Body=0x190,Player=true,Name="CreeperWorldFixture"};p.AddItem(new Backpack());ClassCreation.Profiles.Assign(p,DiabloClass.Druid);
        ClassCreation.Profiles.AdvanceLevel(p,99);Require(ClassCreation.Profiles.TryTrain(p.Serial,"druid.poisoncreeper",out _),"Train Creeper");
        int y=1800+64*_scene++,floor=Enumerable.Range(0x500,0x100).First(i=>TileData.ItemTable[i].Surface&&!TileData.ItemTable[i].Impassable&&TileData.ItemTable[i].CalcHeight==0);
        for(int x=7060;x<=7140;x++)for(int row=y-24;row<=y+24;row++){new Static(floor).MoveToWorld(new(x,row,100),Map.Trammel);}
        p.MoveToWorld(new(7100,y,100),Map.Trammel);return p;
    }
    private static void End(PlayerMobile p){DruidCreeperGround.Cancel(p);PersistentMinions.Dismiss(p);p.Delete();}
    private static GraveyardZombie Enemy(PlayerMobile p,int offset)
    {var e=new GraveyardZombie{CantWalk=true,NoKillAwards=true};e.MoveToWorld(new(p.X+offset,p.Y,p.Z),p.Map);e.AIObject.AITimer.Stop();return e;}
    private static void Through(DruidCreeperBody body,int frame,bool actions=true)
    {var s=body.Prepared.Controller;Require(frame>s.Frame,"Reverse Creeper test frame");var at=s.NextUtc.AddMilliseconds((frame-s.Frame-1)*40L);DruidCreeperWorld.Advance(body,at,actions);DruidCreeperGround.Tick(at,actions,body.Prepared.Owner);}
    private static void Movement()
    {
        var p=Fresh();var at=TimedEffects.Now.AddDays(2);var body=DruidCreeperWorld.Create(p,new(p.X+16,p.Y,p.Z),at,735,1000);
        try
        {
            int initial=body.X-p.X;Through(body,24);Require(body.Prepared.Controller.Frame==24 && body.X-p.X==initial,"Creeper thought before source frame25");
            Through(body,120);Require(body.Prepared.Controller.Fault==null && Math.Abs(body.X-p.X)<initial,"Creeper common-pet follow did not move");
            var before=body.Prepared.Controller;int life=body.MaximumLife256/2;SkillHealth.SetBalance256(body,life);
            DruidCreeperWorld.Advance(body,before.NextUtc.AddMinutes(1),false);
            Require(body.Map==Map.Internal && SkillHealth.Balance256(body)==life && body.Prepared.Controller.Seed==before.Seed,"Offline Creeper moved/drew/healed");
            DruidCreeperWorld.Advance(body,body.Prepared.Controller.NextUtc,true);Require(body.Map==p.Map && SkillHealth.Balance256(body)==life+9,"Creeper resume failed or replayed recovery");
            string saved=body.SavedText;var valid=body.Prepared;bool refused=false;
            try{body.RestorePrepared(valid with{Controller=valid.Controller with{GameBase=-1}});}catch(InvalidDataException){refused=true;}
            Require(refused && body.SavedText==saved,"Malformed world restore changed Creeper");
            body.RestorePrepared(valid with{Controller=valid.Controller with{Profile="source-ai-native-ground-vine-launch-capture-v1"}});
            Require(body.Prepared.Controller.Profile=="source-ai-independent-vine-poison-provisional-v4"
                && body.Prepared.Controller.SkillEvents==valid.Controller.SkillEvents&&body.Prepared.Controller.Fields.Heads.Length==0,
                "Old capture profile did not migrate without replay");
        }
        finally{End(p);}
    }
    private static void SkillAndEscape()
    {
        var clock=DruidCreeperWorld.SkillClock();Require(clock.Action>0 && clock.End>clock.Action,"Creeper kind2clock missing");
        var p=Fresh();var at=TimedEffects.Now.AddDays(2);var e=Enemy(p,3);var body=DruidCreeperWorld.Create(p,new(p.X+2,p.Y,p.Z),at,736,1000);
        try
        {
            int life=SkillHealth.Balance256(e);Through(body,25);
            Require(body.Prepared.Controller is{Mode:8,Target:>0,Ai.LastSkillFrame:1025,SkillEvents:0},"Mature game imposed a fresh100frame delay");
            Through(body,25+clock.Action);var s=body.Prepared.Controller;
            Require(s.SkillEvents==1 && s.LastHeads.Length==3 && s.LastHeads.All(h=>h.X==e.X*2&&h.Y==e.Y*2) && SkillHealth.Balance256(e)==life,"Creeper action did not capture source heads or invented damage");
            var copied=body.Prepared.Controller;var original=copied.LastHeads[0];copied.LastHeads[0]=original with{X=original.X+1};
            Require(body.Prepared.Controller.LastHeads[0]==original,"Captured heads aliased caller state");body.Delete();
            var early=DruidCreeperWorld.Create(p,new(p.X+2,p.Y,p.Z),at,737,0);Through(early,100);
            Require(early.Prepared.Controller.SkillEvents==0 && early.Prepared.Controller.Ai.LastSkillFrame==0,"Early-game strict delay was lost");
            Through(early,125+clock.Action);Require(early.Prepared.Controller.SkillEvents==1,"Early-game source decision did not resume after strict delay");early.Delete();
            var retreat=DruidCreeperWorld.Create(p,new(p.X+2,p.Y,p.Z),at,738,1000);e.SetResistance(ResistanceType.Poison,100);Require(e.PoisonResistance==100,"Retreat fixture poison resistance");
            Through(retreat,25);Require(retreat.Prepared.Controller is{Mode:2,SkillEvents:0,Ai.LastSkillFrame:0} && retreat.Prepared.Controller.Motion.FinalTarget.X<retreat.X*2,"Exact100resist target did not trigger escape away");
        }
        finally{e.Delete();End(p);}
    }
    private static void Check()
    {
        Require(!DruidCombat.Implemented(CombatAbility.PoisonCreeper),"Capture-only world enabled learned skill");Movement();SkillAndEscape();
        var p=Fresh();var at=TimedEffects.Now.AddDays(2);var e=Enemy(p,3);var body=DruidCreeperWorld.Create(p,new(p.X+2,p.Y,p.Z),at,739,1000);
        Through(body,25+DruidCreeperWorld.SkillClock().Action+12);
        var ground=DruidCreeperGround.Get(body.Prepared.Controller.LastFieldId);
        Require(ground.Fields.Heads.Length==3 && ground.Fields.Mats.Length==6 && body.Prepared.Controller.Fields.Heads.Length==0,"World attack did not create independent heads/mats");
        e.Delete();SkillHealth.SetBalance256(body,6007);PersistentMinions.Suspend(p);
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,body.Serial.Value,body.Prepared,SkillHealth.Balance256(body),p.Map.MapID,p.Location,ground)));
        Log.Information("Creeper world: common-pet follow, mature/early game-frame distinction, kind2moving heads and finite mats without damage, exact100resistance escape, offline/restore controls pass; poison and VINE_BEAST remain pending");
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);var body=World.FindMobile((Serial)saved.Body) as DruidCreeperBody;
        Require(body is{Alive:true,Deleted:false} && body.ControlMaster==p && body.Map==Map.Internal && body.SavedText==JsonSerializer.Serialize(saved.State)
            && SkillHealth.Balance256(body)==saved.Life && body.Prepared.Controller.SkillEvents==1
            && JsonSerializer.Serialize(DruidCreeperGround.Get(body.Prepared.Controller.LastFieldId))==JsonSerializer.Serialize(saved.Ground),"Saved Creeper controller/birth/life/independent fields changed");
        if(p.Map==Map.Internal){p.MoveToWorld(saved.Point,Map.Maps[saved.Map]);}
        ulong seed=body.Prepared.Controller.Seed;var at=body.Prepared.Controller.NextUtc;DruidCreeperWorld.Advance(body,at,false);DruidCreeperGround.Tick(at,false,p.Serial.Value);
        Require(SkillHealth.Balance256(body)==saved.Life && body.Prepared.Controller.Seed==seed && body.Prepared.Controller.SkillEvents==1,"Loaded Creeper replayed actions/RNG/healing");
        at=body.Prepared.Controller.NextUtc;DruidCreeperWorld.Advance(body,at,true);DruidCreeperGround.Tick(at,true,p.Serial.Value);Require(body.Map==p.Map && SkillHealth.Balance256(body)==saved.Life+9,"Creeper world did not resume one active frame");
        PersistentMinions.Suspend(p);File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{State=body.Prepared,Life=SkillHealth.Balance256(body),Ground=DruidCreeperGround.Get(body.Prepared.Controller.LastFieldId)}));
    }
    private static void Live(NetState network)
    {
        var p=Fresh();network.Seeded=network.SentFirstPacket=true;network.CompressionEnabled=false;
        var field=typeof(NetState).GetField("_protocolState",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
        Require(field!=null,"Creeper protocol bootstrap");field.SetValue(network,Enum.Parse(field.FieldType,"GameServer_LoggedIn"));
        var account=new Account("creeper-world-"+Guid.NewGuid().ToString("N"),Guid.NewGuid().ToString("N"));account[0]=p;
        network.Account=account;network.Mobile=p;p.NetState=network;network.ResetMovementState();
        var body=DruidCreeperWorld.Create(p,new(p.X+16,p.Y,p.Z),TimedEffects.Now,740,1000);var origin=body.Location;long deadline=Core.TickCount+12000,flight=0;bool removed=false,damaged=false;
        GraveyardZombie first=null,second=null;int initialLife=0;
        void Inspect()
        {
            try
            {
                Require(network.Running && Core.TickCount<deadline && (removed||!body.Deleted&&body.Prepared.Controller.Fault==null),"Natural Creeper follow/field clock timed out/refused");
                if(flight==0)
                {
                    if(body.Location==origin){Timer.StartTimer(TimeSpan.FromMilliseconds(30),Inspect);return;}
                    Require(Math.Abs(body.X-p.X)<16 && body.Prepared.Controller.Frame>=25,"Natural clock did not move Creeper toward owner");
                    flight=DruidCreeperGround.Create(body,1,DruidCreeperVines.Heads(1,(p.X+3)*2,p.Y*2),TimedEffects.Now);
                }
                var ground=DruidCreeperGround.Get(flight);
                if(!removed&&ground?.Fields.Mats.Length==6){body.Delete();removed=true;}
                if(removed&&first==null&&ground?.Fields.Mats.FirstOrDefault(m=>m.Total-m.Remaining>16) is{} mat)
                {
                    var point=new Point3D((mat.X+1)/2,(mat.Y+1)/2,mat.Z);
                    first=new GraveyardZombie{CantWalk=true,NoKillAwards=true};second=new GraveyardZombie{CantWalk=true,NoKillAwards=true};
                    foreach(var target in new[]{first,second}){target.SetHits(500);target.MoveToWorld(point,p.Map);target.AIObject.AITimer.Stop();target.SetResistance(ResistanceType.Poison,0);}
                    initialLife=SkillHealth.Balance256(first);
                }
                if(first!=null){damaged|=SkillHealth.Balance256(first)<initialLife||SkillHealth.Balance256(second)<initialLife;}
                if(removed&&ground==null)
                {
                    Require(damaged,"Natural independent fields never lowered monster life with poison");first.Delete();second.Delete();
                    End(p);account.Delete();Log.Information("creeper-poison-live-result: passed");Log.Information("creeper-fields-live-result: passed");Log.Information("creeper-world-live-result: passed");return;
                }
                Timer.StartTimer(TimeSpan.FromMilliseconds(30),Inspect);
            }
            catch(Exception e){first?.Delete();second?.Delete();End(p);account.Delete();Log.Error(e,"creeper-world-live-result: failed");}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("creeper-world-live: ready");Inspect();});
    }
}
