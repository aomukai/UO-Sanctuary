// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using Server.Network;

namespace Sanctuary.Integration;

public static class ArcticStreamChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(ArcticStreamChecks));
    private sealed record Saved(uint Owner,uint Target,int Life256,ArcticStreamSave State,
        uint ExpiryOwner=0,uint ExpiryTarget=0,int ExpiryLife256=0,int ExpiredChecks=0);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"arctic-stream-fixtures.json");
    private static int _scene;
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value);
    private static void Require(bool b,string why){if(!b){throw new InvalidOperationException(why);}}
    private static void Register(string name,Action run)=>ConsoleInputHandler.RegisterCommand(name,"Prepared Arctic cold stream",_=>Core.LoopContext.Post(()=>
    {try{run();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    public static void Configure()
    {
        Register("arctic-stream-check",Check);Register("arctic-stream-reload-check",Reload);Register("arctic-stream-expiry-stage-check",ExpiryStage);
        Register("arctic-stream-live-check",()=>FixtureClock.NextLoop("arctic-stream-live-result",()=>FixtureNetwork.Connected("arctic-stream-live-result",Live)));
    }
    private static PlayerMobile Fresh()
    {
        var p=new PlayerMobile{Name="ArcticStreamFixture",Body=0x190,Player=true};p.AddItem(new Backpack());
        ClassCreation.Profiles.Assign(p,DiabloClass.Druid);ClassCreation.Profiles.AdvanceLevel(p,99);FixtureSpace.Corridor(p,6230,20,2200+32*_scene++);return p;
    }
    private static ShockWaveTarget Target(PlayerMobile p,int offset)
    {
        var t=new ShockWaveTarget{Frozen=true,CantWalk=true,NoKillAwards=true};t.SetHits(50000);t.Hits=t.HitsMax;t.SetResistance(ResistanceType.Cold,0);
        t.MoveToWorld(new(p.X+offset,p.Y,p.Z),p.Map);t.AIObject.AITimer.Stop();return t;
    }
    private static SkillValues Values(int rank=1,int hard=0)=>new(SkillMechanics.For(CombatAbility.ArcticBlast),_=>rank,key=>key=="Cyclone Armor"?hard:0,99);
    private static ArcticMissile One(PlayerMobile p,DateTime at,int rank=1)
    {long id=DruidArcticStream.Create(p,new(p.X*2+20,p.Y*2),Values(rank),at);Require(id>0,"Missing Arctic emission");return DruidArcticStream.Snapshot().Missiles.Single(x=>x.Id==id);}
    private static void Arithmetic()
    {
        using var damage=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"arctic-stream-damage-source-probe.json")));int count=0;
        foreach(var c in damage.RootElement.EnumerateArray())
        {
            var p=DruidArcticStream.Capture(Values(c[0].GetInt32(),c[1].GetInt32()),c[2].GetInt32());
            Require(p.Minimum256==c[4].GetInt32()&&p.Maximum256==c[5].GetInt32()&&p.ColdFrames==c[6].GetInt32(),"Cold source packet differs");count++;
        }
        Require(count==4590,"Missing cold packet controls");
        var held=DruidArcticStream.Snapshot();var p1=Fresh();int factories=0;var slow=AmazonSlowMissiles.Snapshot();var allocation=DruidArcticStream.Allocate;int calls=0;
        try
        {
            using var data=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"arctic-stream-factory-source-probe.json")));
            foreach(var c in data.RootElement.EnumerateArray())
            {
                int rank=c[0].GetInt32(),dx=c[1].GetInt32(),dy=c[2].GetInt32();bool slowed=c[3].GetInt32()!=0,allocate=c[4].GetInt32()!=0;
                AmazonSlowMissiles.Restore(slow.Where(x=>x.Target!=p1.Serial.Value).Concat(slowed?[new SlowMissilesState(p1.Serial.Value,p1.Serial.Value,1,50,100,TimedEffects.Now.AddHours(2))]:[]).ToArray());
                DruidArcticStream.Allocate=()=>{calls++;return allocate;};int before=calls;
                long id=DruidArcticStream.Create(p1,new(p1.X*2+dx,p1.Y*2+dy),Values(rank),TimedEffects.Now.AddHours(1));
                Require((id!=0)==(c[5].GetInt32()!=0)&&calls-before==c[6].GetInt32(),"Factory allocation/delta boundary differs");
                if(id!=0)
                {
                    var shot=DruidArcticStream.Snapshot().Missiles.Single(x=>x.Id==id);var source=c[8];
                    if(dx==dy&&dx==0){dx=dy=1;}var vector=MissileVectors.VelocityFp8(dx,dy,source[4].GetInt32());
                    Require(shot.Vx==vector.X&&shot.Vy==vector.Y&&shot.Remaining==source[6].GetInt32()&&shot.Damage.Frames==source[5].GetInt32()
                        &&shot.LastCollision==p1.Serial.Value&&shot.Rank==rank,"Factory owner/vector/lifetime differs");
                }
                DruidArcticStream.Restore(held);factories++;
            }
            Require(factories==14280,"Missing factory controls");
        }
        finally{DruidArcticStream.Restore(held);DruidArcticStream.Allocate=allocation;AmazonSlowMissiles.Restore(slow);p1.Delete();}
        Log.Information("Arctic:4590cold source packets/14280complete-factory controls pass");
    }
    private static void Cases()
    {
        var held=DruidArcticStream.Snapshot();var p=Fresh();var a=Target(p,1);var b=Target(p,1);var at=TimedEffects.Now.Add(FixtureSpace.Retention);
        var seed=DruidArcticStream.SeedLow;var allocation=DruidArcticStream.Allocate;var shields=SourceBlocking.Snapshot();var block=SourceBlocking.BlockRoll;
        try
        {
            DruidArcticStream.SeedLow=()=>1;SourceBlocking.BlockRoll=_=>0;int mana=NecromancerEffects.ManaBalance256(p);
            var s=One(p,at);Require(s.Damage==new ArcticPacket(256,512,100,17)&&s.LastCollision==p.Serial.Value,"Rank1 birth packet");
            var cell=new SourceMissileCell(a.X*2,a.Y*2);Require(DruidArcticStream.Recipient(p,s,cell)!=null,"Size1 contact missing");
            var candidate=DruidArcticStream.Recipient(p,s with{LastCollision=a.Serial.Value},cell);Require(candidate==b,"Most recent collider not excluded");
            var fixedHit=s with{Damage=s.Damage with{Minimum256=25600,Maximum256=25600}};
            int life=SkillHealth.Balance256(a);var result=DruidArcticStream.Impact(p,a,fixedHit);
            Require(SkillHealth.Balance256(a)==life-25600&&result.LastCollision==a.Serial.Value&&ColdStates.OtherRate(a)<0,"Actual cold damage/history/chill");
            // Pure cold is not blocked by an equipped shield,even on roll0.
            string reference=CreatureProfiles.Definition(a).Reference;var rule=BalanceCatalog.Read<SourceBlockRule>("source-block-rules").Single(x=>x.Source==reference);
            int choice=Array.FindIndex(rule.ShieldItems,x=>x);Require(choice>=0,"Missing shield fixture");
            SourceBlocking.Restore(shields.Where(x=>x.Actor!=a.Serial.Value).Append(new SourceShield(a.Serial.Value,reference,choice)).ToArray());
            Require(SourceBlocking.TryRate(a,out int rate,out _)&&rate>0,"Shield fixture cannot block");life=SkillHealth.Balance256(a);
            DruidArcticStream.Impact(p,a,fixedHit);Require(SkillHealth.Balance256(a)==life-25600,"Shield blocked pure cold");
            a.SetResistance(ResistanceType.Cold,100);Require(TimedEffects.TryActor(a,out var chilled),"Missing cold state");chilled.ChillUntil=DateTime.MinValue;life=SkillHealth.Balance256(a);
            DruidArcticStream.Impact(p,a,fixedHit);Require(SkillHealth.Balance256(a)==life&&ColdStates.OtherRate(a)==0,"Cold immunity damaged/chilled");a.SetResistance(ResistanceType.Cold,0);
            // One actual due collision per phase; no per-cast NHD. Two stacked
            // recipients may alternate as the retained step stays in contact.
            DruidArcticStream.Tick(at.AddMilliseconds(40),true);var first=DruidArcticStream.Snapshot().Missiles.Single(x=>x.Id==s.Id);
            Require(first.LastCollision==a.Serial.Value||first.LastCollision==b.Serial.Value,"Travelling emission never contacted stacked targets");
            DruidArcticStream.Tick(at.AddMilliseconds(80),true);var second=DruidArcticStream.Snapshot().Missiles.Single(x=>x.Id==s.Id);
            Require(second.LastCollision!=first.LastCollision&&second.LastCollision!=p.Serial.Value,"Non-killing LastCollide flight did not continue");
            string before=Json(DruidArcticStream.Snapshot());var saved=DruidArcticStream.Snapshot();
            try{DruidArcticStream.Restore(saved with{Missiles=[..saved.Missiles,saved.Missiles[0]]});throw new InvalidOperationException("Duplicate accepted");}catch(InvalidDataException){}
            Require(Json(DruidArcticStream.Snapshot())==before,"Invalid restore mutated pool");saved.Missiles[0].Steps.Cells[0]=new(1,1);
            Require(Json(DruidArcticStream.Snapshot())==before,"Snapshot aliases mutable cells");
            DruidArcticStream.Tick(at.AddSeconds(2),false);Require(DruidArcticStream.Snapshot().Missiles.All(x=>x.Owner!=p.Serial.Value),"Offline lifetime frozen");
            // An expired restored emission cannot replay elapsed contacts.
            var overdue=One(p,TimedEffects.Now.AddSeconds(-2));var overdueSave=DruidArcticStream.Snapshot();life=SkillHealth.Balance256(a);
            DruidArcticStream.Restore(JsonSerializer.Deserialize<ArcticStreamSave>(Json(overdueSave)));DruidArcticStream.Tick(TimedEffects.Now,true);
            Require(DruidArcticStream.Snapshot().Missiles.All(x=>x.Id!=overdue.Id)&&SkillHealth.Balance256(a)==life,"Restored expiry backfilled damage");
            DruidArcticStream.Allocate=()=>false;Require(DruidArcticStream.Create(p,new(p.X*2+20,p.Y*2),Values(),at)==0,"Failed factory made cold stream");
            Require(NecromancerEffects.ManaBalance256(p)==mana,"Emission charged mana outside start wrapper");
        }
        finally{DruidArcticStream.Restore(held);DruidArcticStream.SeedLow=seed;DruidArcticStream.Allocate=allocation;SourceBlocking.BlockRoll=block;a.Delete();b.Delete();p.Delete();SourceBlocking.Restore(shields);}
    }
    private static void Check()
    {
        Arithmetic();Cases();var p=Fresh();var t=Target(p,1);One(p,TimedEffects.Now.Add(FixtureSpace.Retention));
        File.WriteAllText(Manifest,Json(new Saved(p.Serial.Value,t.Serial.Value,SkillHealth.Balance256(t),DruidArcticStream.Snapshot())));
    }
    private static void Reload()
    {
        Arithmetic();Cases();var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));DruidArcticStream.Tick(TimedEffects.Now,true);
        Require(s.ExpiryOwner!=0&&DruidArcticStream.Snapshot().Missiles.All(x=>x.Owner!=s.ExpiryOwner)
            &&World.FindMobile((Serial)s.ExpiryTarget) is ShockWaveTarget expired&&SkillHealth.Balance256(expired)==s.ExpiryLife256,"Restart replayed overdue flight");
        Require(Json(DruidArcticStream.Snapshot())==Json(s.State)&&World.FindMobile((Serial)s.Target) is ShockWaveTarget t&&SkillHealth.Balance256(t)==s.Life256,"Retained Arctic owner/packet/RNG/history/steps differ");
        World.FindMobile((Serial)s.ExpiryTarget).Delete();World.FindMobile((Serial)s.ExpiryOwner).Delete();
        File.WriteAllText(Manifest,Json(s with{ExpiryOwner=0,ExpiryTarget=0,ExpiredChecks=s.ExpiredChecks+1}));
    }
    private static void ExpiryStage()
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=Fresh();var t=Target(p,1);One(p,TimedEffects.Now);
        File.WriteAllText(Manifest,Json(s with{ExpiryOwner=p.Serial.Value,ExpiryTarget=t.Serial.Value,ExpiryLife256=SkillHealth.Balance256(t),State=s.State with{NextId=DruidArcticStream.Snapshot().NextId}}));
    }
    private static void Live(NetState network)
    {
        var p=Fresh();var t=Target(p,2);int life=SkillHealth.Balance256(t);var at=TimedEffects.Now;long deadline=Core.TickCount+5000;
        network.Seeded=network.SentFirstPacket=true;network.CompressionEnabled=false;network.Mobile=p;p.NetState=network;
        var field=typeof(NetState).GetField("_protocolState",BindingFlags.Instance|BindingFlags.NonPublic);field.SetValue(network,Enum.Parse(field.FieldType,"GameServer_LoggedIn"));network.ResetMovementState();
        // Explicit supplied sequence intents,not a claim of modern input cadence.
        NecromancerEffects.SetMana256(p,p.ManaMax*256);int mana=NecromancerEffects.ManaBalance256(p),emits=0;
        var services=new PlayerInfernoSequenceServices(()=>NecromancerEffects.ManaBalance256(p),cost=>NecromancerEffects.Pay(p,cost),()=>true,()=>true,
            frames=>{emits++;long id=DruidArcticStream.Create(p,new(p.X*2+20,p.Y*2),Values(),at);Require(DruidArcticStream.Snapshot().Missiles.Single(x=>x.Id==id).Remaining==frames,"Sequence-to-factory range changed");return id>0;});
        var s=PlayerInfernoSequence.EnterFresh(1000,CombatAbility.ArcticBlast,1,PlayerInfernoCostModel.CurrentTablePerInvocation,services).State;
        s=PlayerInfernoSequence.Advance(s,1010,1,services);
        Require(emits==0,"First source do emitted");
        s=PlayerInfernoSequence.StartCallback(s,1010,1,services).State;
        Require(emits==1&&NecromancerEffects.ManaBalance256(p)==mana-192,"Supplied start-to-factory payment/attempt differs");
        void Inspect()
        {
            try
            {
                Require(network.Running&&Core.TickCount<deadline,"Natural Arctic expiry timeout/disconnect");
                if(DruidArcticStream.Snapshot().Missiles.Any(x=>x.Owner==p.Serial.Value)){Timer.StartTimer(TimeSpan.FromMilliseconds(10),Inspect);return;}
                Require(SkillHealth.Balance256(t)<life&&ColdStates.OtherRate(t)<0,"Natural Arctic stream did no damage/chill");
                t.Delete();p.Delete();var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));
                File.WriteAllText(Manifest,Json(saved with{State=DruidArcticStream.Snapshot()}));Log.Information("arctic-stream-live-result: passed");
            }
            catch(Exception e){t.Delete();p.Delete();Log.Error(e,"arctic-stream-live-result: failed");}
        }
        Log.Information("arctic-stream-live: ready");Timer.StartTimer(TimeSpan.FromMilliseconds(10),Inspect);
    }
}
