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

public static class SpiritWolfWorldChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(SpiritWolfWorldChecks));
    private sealed record Saved(uint Owner,uint Wolf,SpiritWolfBodyState State,int Life,PetOwnerRoute Route,int Map,Point3D Point);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"spirit-wolf-world-fixtures.json");
    private static int _scene;
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    public static void Configure()
    {
        Register("spirit-wolf-world-check",Check);Register("spirit-wolf-world-reload-check",Reload);
        Register("spirit-wolf-world-live-check",()=>FixtureNetwork.Connected("spirit-wolf-world-live-result",Live));
        Register("spirit-wolf-hit-live-check",()=>FixtureNetwork.Connected("spirit-wolf-hit-live-result",n=>Live(n,true)));
        Register("spirit-wolf-rate-live-check",()=>FixtureNetwork.Connected("spirit-wolf-rate-live-result",n=>Live(n,true,50)));
        Register("spirit-wolf-slow-live-check",()=>FixtureNetwork.Connected("spirit-wolf-slow-live-result",n=>Live(n,true,slowed:true)));
    }
    private static void Register(string command,Action action)=>ConsoleInputHandler.RegisterCommand(command,"Prepared live wolf movement; ordinary hit and learned cast pending",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Command}: passed",command);}catch(Exception e){Log.Error(e,"{Command}: failed",command);}}));
    private static void Train(PlayerMobile p,string id)
    {
        Require(TalentCatalog.TryGet(DiabloClass.Druid,id,out var talent),"Wolf talent");
        foreach(string prior in talent.Prerequisites){Train(p,prior);}
        if(ClassCreation.Profiles.GetRank(p.Serial,id)==0){Require(ClassCreation.Profiles.TryTrain(p.Serial,id,out _),"Train "+id);}
    }
    internal static PlayerMobile Fresh(int sceneOffset=0)
    {
        var p=new PlayerMobile{Body=0x190,Player=true,Name="SpiritWolfWorldFixture"};p.AddItem(new Backpack());
        ClassCreation.Profiles.Assign(p,DiabloClass.Druid);ClassCreation.Profiles.AdvanceLevel(p,99);Train(p,"druid.summonspiritwolf");
        int floor=Enumerable.Range(0x500,0x100).First(i=>TileData.ItemTable[i].Surface&&!TileData.ItemTable[i].Impassable&&TileData.ItemTable[i].CalcHeight==0);
        // Isolate a flat native terrain adapter above shipped dungeon geometry.
        // Adding live scenes must not accidentally select an underlying wall.
        const int z=100;
        for(int candidate=0;candidate<64;candidate++)
        {
            int y=1800+64*(_scene++)+sceneOffset;
            if(y<24||y+24>=Map.Trammel.Height){break;}
            // Scene counters restart with the process; saved fixtures do not.
            // Select an empty native test area rather than moving or deleting
            // existing actors to make a reused hard-coded location pass.
            bool occupied=false;
            foreach(var mobile in Map.Trammel.GetMobilesInRange<Mobile>(new Point3D(7100,y,z),40))
            {if(!mobile.Deleted&&Math.Abs(mobile.Z-z)<16){occupied=true;break;}}
            if(occupied){continue;}
            for(int x=7060;x<=7140;x++)for(int row=y-24;row<=y+24;row++){new Static(floor).MoveToWorld(new(x,row,z),Map.Trammel);}
            if(!new[]{0,1,2,3,8,16,30}.All(dx=>Map.Trammel.CanSpawnMobile(new Point3D(7100+dx,y,z))))
            {Log.Information("Wolf fixture skipped occupied/obstructed floor at7100,{Y},100",y);continue;}
            p.MoveToWorld(new(7100,y,z),Map.Trammel);return p;
        }
        p.Delete();throw new InvalidOperationException($"No free wolf fixture scene in facet{Map.Trammel.Width}x{Map.Trammel.Height},offset{sceneOffset}");
    }
    internal static void End(PlayerMobile p){PersistentMinions.Dismiss(p);p.Delete();}
    private static void Through(DruidSpiritWolfBody body,int frame,bool actions=true)
    {var s=body.Prepared.Controller;Require(frame>s.Frame,"Reverse/equal wolf fixture clock");DruidSpiritWolfWorld.Advance(body,s.NextUtc.AddMilliseconds((frame-s.Frame-1)*40L),actions);}
    private static void Routes()
    {
        var p=Fresh();var at=TimedEffects.Now.AddHours(1);var s=PetOwnerRoutes.For(p,at);var start=p.Location;
        try
        {
            Require(s.Cursor==0 && s.Trail.All(x=>x==new Point2D(0,0)),"New owner route was fabricated");
            PetOwnerRoutes.Moved(p,at.AddMilliseconds(25));Require(PetOwnerRoutes.For(p,at).Cursor==0,"Strict25ms gate");
            PetOwnerRoutes.Moved(p,at.AddMilliseconds(26));s=PetOwnerRoutes.For(p,at.AddMilliseconds(26));Require(s.Cursor==1 && s.Trail[0]==PetOwnerRoutes.Cell(p),"First accepted history sample");
            p.Location=new(start.X+3,start.Y,start.Z);PetOwnerRoutes.Moved(p,at.AddMilliseconds(60));Require(PetOwnerRoutes.For(p,at).Cursor==1,"Displacement36accepted below45");
            p.Location=new(start.X+4,start.Y,start.Z);PetOwnerRoutes.Moved(p,at.AddMilliseconds(90));Require(PetOwnerRoutes.For(p,at).Cursor==2,"Displacement64did not append");
            for(int i=0;i<22;i++)
            {p.Location=new(start.X+(i%2==0?8:4),start.Y,start.Z);PetOwnerRoutes.Moved(p,at.AddMilliseconds(120+30*i));}
            s=PetOwnerRoutes.For(p,at.AddMilliseconds(800));Require(s.Cursor==4 && s.Trail[3]==PetOwnerRoutes.Cell(p),"20entry route wrap");
            PetOwnerRoutes.Teleported(p,at.AddSeconds(1));s=PetOwnerRoutes.For(p,at.AddSeconds(1));
            Require(s.LastTeleport==PetOwnerRoutes.Cell(p) && s.Cursor==5,"Teleport must append unconditionally");
            Require(PetOwnerRoutes.For(p,at.AddMilliseconds(2999)).LastTeleport==s.LastTeleport
                && PetOwnerRoutes.For(p,at.AddMilliseconds(3000)).LastTeleport==new Point2D(0,0),"50frame teleport marker expiration");
            var snapshot=PetOwnerRoutes.Snapshot();var original=JsonSerializer.Serialize(snapshot);var bad=snapshot.Select(x=>x.Owner==p.Serial.Value?x with{Cursor=20}:x).ToArray();
            bool refused=false;try{PetOwnerRoutes.Restore(bad);}catch(InvalidDataException){refused=true;}Require(refused && JsonSerializer.Serialize(PetOwnerRoutes.Snapshot())==original,"Malformed route restore mutated live history");
            PetOwnerRoutes.Restore(snapshot);snapshot.Single(x=>x.Owner==p.Serial.Value).Trail[0]=new(1,2);
            Require(JsonSerializer.Serialize(PetOwnerRoutes.Snapshot())==original,"Route restore aliases caller arrays");
        }
        finally{End(p);}
    }
    private static void Movement()
    {
        var p=Fresh();var at=TimedEffects.Now.AddHours(1);var body=DruidSpiritWolfWorld.Create(p,new(p.X+16,p.Y,p.Z),at,71);
        try
        {
            var origin=body.Location;Through(body,24);Require(body.Location==origin && body.Prepared.Controller.Mode==8,"Wolf acted before initial AI event");
            Through(body,25);Require(body.Prepared.Controller is{Mode:15,NextThink:0},"Far owner follow did not enter run: "+JsonSerializer.Serialize(body.Prepared.Controller));
            Through(body,60);Require(body.Location!=origin && body.GetDistanceToSqrt(p)<p.GetDistanceToSqrt(origin),"Actual source movement did not approach owner");
            var saved=body.Prepared;string text=body.SavedText;var input=body.Prepared;
            body.RestorePrepared(input);if(input.Controller.Motion.Points.Length>0){input.Controller.Motion.Points[0]=new(1,2);}
            var output=body.Prepared;if(output.Controller.Motion.Points.Length>0){output.Controller.Motion.Points[0]=new(3,4);}
            Require(body.SavedText==text,"World pose snapshot array alias");
            bool refused=false;try{body.RestorePrepared(saved with{Controller=saved.Controller with{NextThink=saved.Controller.Frame}});}catch(InvalidDataException){refused=true;}
            Require(refused && body.SavedText==text,"Invalid source clock restore was not atomic");
            SkillHealth.SetBalance256(body,100*256+7);var before=body.Prepared.Controller;int life=SkillHealth.Balance256(body);
            PersistentMinions.Suspend(p);DruidSpiritWolfWorld.Advance(body,at.AddMinutes(5),false);
            Require(body.Map==Map.Internal && body.Prepared.Controller.Seed==before.Seed && body.Prepared.Controller.StrikeRequests==before.StrikeRequests
                && SkillHealth.Balance256(body)==life && body.Prepared.Controller.Motion.Points.Length==0,"Offline movement/RNG/heal/strike replay");
            DruidSpiritWolfWorld.Advance(body,body.Prepared.Controller.NextUtc,true);
            Require(body.Map==p.Map && SkillHealth.Balance256(body)==life+32,"Resume one source recovery frame");
            Require(WardrobeTransaction.ChangeClass(p,DiabloClass.Paladin,out _,out _),"World wolf wardrobe park");
            before=body.Prepared.Controller;DruidSpiritWolfWorld.Advance(body,before.NextUtc.AddMinutes(1),true);
            Require(body.Map==Map.Internal && body.Prepared.Controller.Seed==before.Seed,"Parked class consumed world RNG");
            Require(WardrobeTransaction.ChangeClass(p,DiabloClass.Druid,out _,out _),"World wolf wardrobe return");
            DruidSpiritWolfWorld.Advance(body,body.Prepared.Controller.NextUtc,true);Require(body.Map==p.Map,"World wolf return placement");
        }
        finally{End(p);}
    }
    private static GraveyardZombie Enemy(PlayerMobile p,int dx,int dy=0)
    {var t=new GraveyardZombie{CantWalk=true,NoKillAwards=true};t.SetHits(10000);t.MoveToWorld(new(p.X+dx,p.Y+dy,p.Z),p.Map);t.AIObject.AITimer.Stop();return t;}
    private static void TargetsAndTeleport()
    {
        var p=Fresh();var at=TimedEffects.Now.AddHours(1);var a=Enemy(p,3);var b=Enemy(p,3,3);
        var wolf=DruidSpiritWolfWorld.Create(p,new(p.X+1,p.Y,p.Z),at,812);
        try
        {
            var s=wolf.Prepared;wolf.RestorePrepared(s with{Controller=s.Controller with{Target=a.Serial.Value}});
            Through(wolf,25);Require(wolf.Prepared.Controller is{Mode:4} && wolf.Prepared.Controller.Target==a.Serial.Value,"Existing individual target was lost");
            int action=wolf.Prepared.Controller.ActionAt;int life=SkillHealth.Balance256(a);
            Through(wolf,action-1);Require(wolf.Prepared.Controller.StrikeRequests==0,"Pending hit requested before A1action event");
            Through(wolf,action);Require(wolf.Prepared.Controller.StrikeRequests==1 && wolf.Prepared.Controller.PendingStrikeTarget==a.Serial.Value
                && wolf.Prepared.Controller.LastHit is{Outcome:not SpiritWolfHitOutcome.Unsupported}
                && (wolf.Prepared.Controller.LastHit.Outcome==SpiritWolfHitOutcome.Hit?SkillHealth.Balance256(a)<life:SkillHealth.Balance256(a)==life),"Ordinary action did not resolve its recorded hit outcome");
            var attack=wolf.Prepared.Controller;
            wolf.RestorePrepared(wolf.Prepared with{Controller=attack with{ActionAt=attack.Frame+2,EndAt=attack.Frame+7}});
            CreatureHitRecovery.Apply(wolf,p,wolf.HitsMax);
            Require(CreatureHitRecovery.Remaining(wolf)>0 && wolf.Prepared.Controller is{Mode:1,ActionAt:0,Teleport:false}
                && wolf.Prepared.Controller.StrikeRequests==1 && wolf.Prepared.Controller.Seed==attack.Seed,"Shared hit recovery did not cancel queued wolf action");
            Through(wolf,attack.Frame+2);Require(wolf.Prepared.Controller.StrikeRequests==1,"Interrupted A1 action survived recovery");
            var second=DruidSpiritWolfWorld.Create(p,new(p.X+1,p.Y+3,p.Z),at,813);s=second.Prepared;
            second.RestorePrepared(s with{Controller=s.Controller with{Target=b.Serial.Value}});Through(second,25);
            Require(second.Prepared.Controller.Target==b.Serial.Value && wolf.Prepared.Controller.Target==a.Serial.Value,"New wolf retargeted existing wolves");second.Delete();
            a.Delete();b.Delete();wolf.Delete();
            var chased=Enemy(p,9);var chaser=DruidSpiritWolfWorld.Create(p,new(p.X+1,p.Y,p.Z),at,815);
            Through(chaser,25);Require(chaser.Prepared.Controller is{Mode:15} && chaser.Prepared.Controller.Target==chased.Serial.Value,"Out-of-melee target did not select corrected chase");
            var start=chaser.Location;Through(chaser,55);
            Require(chaser.GetDistanceToSqrt(chased)<chased.GetDistanceToSqrt(start) && chaser.Prepared.Controller.Target==chased.Serial.Value,"Actual wolf did not chase toward an occupied target footprint");chaser.Delete();chased.Delete();
            var far=DruidSpiritWolfWorld.Create(p,new(p.X+30,p.Y,p.Z),at,814);var original=far.Location;
            Through(far,25);Require(far.Prepared.Controller is{Mode:8,Teleport:true,ActionAt:29,NextThink:35},"Distinct teleport request/event4/idle10");
            Through(far,28);Require(far.Location==original,"Teleport moved before its requested event");
            Through(far,29);Require(far.Location==p.Location && far.Prepared.Controller is{Teleport:false,ActionAt:0},"Actual delayed Teleport2placement");far.Delete();
        }
        finally{a.Delete();b.Delete();End(p);}
    }
    private static void Check()
    {
        Require(DruidCombat.Implemented(CombatAbility.SummonSpiritWolf),"Learned wolf route missing");Routes();Movement();TargetsAndTeleport();
        var p=Fresh();var at=TimedEffects.Now.AddDays(2);var body=DruidSpiritWolfWorld.Create(p,new(p.X+1,p.Y,p.Z),at,9001);var target=Enemy(p,2);
        PetOwnerRoutes.Teleported(p,at);Through(body,45);Require(body.Prepared.Controller.StrikeRequests>0 && body.Prepared.Controller.LastHit!=null,"No resolved ordinary hit saved");
        NecromancerEffects.Profiles.Store(body,new(Curse:new(CombatAbility.Decrepify,p.Serial.Value,1,at.AddDays(1),body.X,body.Y)));
        body.RestorePrepared(body.Prepared with{Controller=DruidSpiritWolfWorld.Park(body.Prepared.Controller)});
        Through(body,70);Require(body.Prepared.Controller is{AttackSpeed:88,DeferredThinks:>0,Fault:null},"No continuing slow policy saved");
        target.Delete();SkillHealth.SetBalance256(body,100*256+17);PersistentMinions.Suspend(p);
        var route=PetOwnerRoutes.Snapshot().Single(x=>x.Owner==p.Serial.Value);
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,body.Serial.Value,body.Prepared,SkillHealth.Balance256(body),route,p.Map.MapID,p.Location)));
        Log.Information("Prepared Spirit Wolf world: actual run/follow, individual target retention, resolved A1event, delayed teleport, owner history, offline/wardrobe and atomic pose controls pass; learned cast and full geometry pending");
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=World.FindMobile((Serial)saved.Owner) as PlayerMobile;
        var body=World.FindMobile((Serial)saved.Wolf) as DruidSpiritWolfBody;
        Require(body is{Deleted:false,Alive:true} && body.ControlMaster==p && body.Map==Map.Internal && body.SavedText==JsonSerializer.Serialize(saved.State)
            && SkillHealth.Balance256(body)==saved.Life,"Saved wolf actor pose/clock/ownership/injury changed");
        Require(JsonSerializer.Serialize(PetOwnerRoutes.Snapshot().Single(x=>x.Owner==saved.Owner))==JsonSerializer.Serialize(saved.Route),"Saved owner trail/teleport marker changed");
        if(p.Map==Map.Internal){p.MoveToWorld(saved.Point,Map.Maps[saved.Map]);}
        var prior=body.Prepared.Controller;DruidSpiritWolfWorld.Advance(body,prior.NextUtc,false);
        Require(body.Prepared.Controller.Seed==prior.Seed && body.Prepared.Controller.StrikeRequests==prior.StrikeRequests && SkillHealth.Balance256(body)==saved.Life,"Loaded source frames replayed offline effects");
        DruidSpiritWolfWorld.Advance(body,body.Prepared.Controller.NextUtc,true);
        Require(body.Map==p.Map && SkillHealth.Balance256(body)==saved.Life+32,"Actual world reload resume/recovery");
        var route=PetOwnerRoutes.Snapshot().Single(x=>x.Owner==saved.Owner);
        Require(route.LastTeleport==new Point2D(0,0),"Resumed owner lookup did not expire the50-frame teleport marker");
        PersistentMinions.Suspend(p);File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{State=body.Prepared,Life=SkillHealth.Balance256(body),Route=route}));
    }
    private static void Live(NetState network)=>Live(network,false);
    private static void Live(NetState network,bool attacking,int haste=0,bool slowed=false)
    {
        var p=Fresh();network.Seeded=true;network.SentFirstPacket=true;network.CompressionEnabled=false;
        var field=typeof(NetState).GetField("_protocolState",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
        Require(field!=null,"World wolf protocol bootstrap");field.SetValue(network,Enum.Parse(field.FieldType,"GameServer_LoggedIn"));
        // The synthetic session must cross the same authenticated buffer
        // promotion gate as a real login before streaming the test floor.
        var account=new Account("wolf-world-"+Guid.NewGuid().ToString("N"),Guid.NewGuid().ToString("N"));account[0]=p;
        network.Account=account;network.Mobile=p;p.NetState=network;network.ResetMovementState();
        var target=attacking?Enemy(p,3):null;if(target!=null){TimedEffects.PreventHealing(target,TimedEffects.Now);target.SetResistance(ResistanceType.Cold,0);}
        int life=target==null?0:SkillHealth.Balance256(target);
        string result=slowed?"spirit-wolf-slow-live":haste>0?"spirit-wolf-rate-live":attacking?"spirit-wolf-hit-live":"spirit-wolf-world-live";
        var body=DruidSpiritWolfWorld.Create(p,new(p.X+(attacking?1:16),p.Y,p.Z),TimedEffects.Now,9821);var origin=body.Location;long deadline=Core.TickCount+(slowed?20000:10000);
        if(haste>0)
        {
            var auras=PaladinAuras.Snapshot();PaladinAuras.Restore(auras with{Leases=auras.Leases.Append(new PaladinAuraLease(body.Serial.Value,p.Serial.Value,
                CombatAbility.Fanaticism,1,haste,TimedEffects.Now.AddSeconds(30))).ToArray()});
        }
        if(slowed){NecromancerEffects.Profiles.Store(body,new(Curse:new(CombatAbility.Decrepify,p.Serial.Value,1,TimedEffects.Now.AddMinutes(1),body.X,body.Y)));}
        void Inspect()
        {
            try
            {
                Require(network.Running && !body.Deleted && body.Prepared.Controller.Fault==null,"Natural world actor disconnected/deleted/faulted: "+JsonSerializer.Serialize(body.Prepared.Controller));
                Require(Core.TickCount<deadline,"Natural world follow timeout");
                if(attacking?body.Prepared.Controller.Hits<(slowed?2:1):body.GetDistanceToSqrt(origin)<2){Timer.StartTimer(TimeSpan.FromMilliseconds(50),Inspect);return;}
                if(attacking){Require(SkillHealth.Balance256(target)<life && body.Prepared.Controller.LastHit is{Outcome:SpiritWolfHitOutcome.Hit,Cold256:>0,Fire256:0,Lightning256:0}
                    && GearEffects.ChillPercent(target)>0,"Natural wolf ordinary cold hit/state did not commit");}
                else{Require(body.GetDistanceToSqrt(p)<p.GetDistanceToSqrt(origin) && body.Prepared.Controller.Frame>=25,"Natural source frames moved away from owner");}
                if(haste>0){Require(body.Prepared.Controller is{AttackSpeed:264,PendingStrikeFrame:34},"Natural haste clock did not deliver at captured source event");}
                if(slowed){Require(body.Prepared.Controller is{AttackSpeed:88,DeferredThinks:>=2,StrikeRequests:>=2} && PaladinOffense.AttackRate(body)==0,"Natural Decrepify-only bites stopped or acquired hidden haste");}
                target?.Delete();End(p);account.Delete();Log.Information("{Result}-result: passed",result);
            }
            catch(Exception e){target?.Delete();End(p);account.Delete();Log.Error(e,"{Result}-result: failed",result);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(40),()=>{Log.Information("{Result}: ready",result);Inspect();});
    }
}
