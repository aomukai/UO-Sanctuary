// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary.Integration;

public static class RecyclerWorldChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(RecyclerWorldChecks));
    private sealed record Saved(uint Owner,uint Body,RecyclerBodyState State,int Life,int Map,Point3D Point);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"recycler-world-fixtures.json");
    private static void Require(bool b,string why){if(!b){throw new InvalidOperationException(why);}}
    private static void Register(string name,Action check)=>ConsoleInputHandler.RegisterCommand(name,"Prepared recycler automatic world controller",_=>Core.LoopContext.Post(()=>
    {try{check();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    public static void Configure()
    {
        Register("recycler-world-check",Check);Register("recycler-world-reload-check",Reload);
        Register("recycler-world-live-check",()=>FixtureClock.NextLoop("recycler-world-live-result",
            ()=>FixtureNetwork.Connected("recycler-world-live-result",Live)));
    }
    private static void End(PlayerMobile p){DruidRecyclerRecovery.Cancel(p);PersistentMinions.Dismiss(p);p.Delete();}
    private static void Through(DruidRecyclerBody body,int frame,bool actions=true)
    {
        var s=body.Prepared.Controller;Require(frame>s.Frame,"Reverse recycler frame");
        DruidRecyclerWorld.Advance(body,s.NextUtc.AddMilliseconds((frame-s.Frame-1)*40L),actions);
        Require(body.Prepared.Controller.Fault==null,"Recycler controller fault:"+body.Prepared.Controller.Fault);
    }
    private static DruidRecyclerBody Create(PlayerMobile p,DateTime at,int offset,CombatAbility ability=CombatAbility.CarrionVine,
        RecyclerRecoveryModel model=RecyclerRecoveryModel.LegacySource,int gameFrame=1000)
        =>DruidRecyclerWorld.Create(p,ability,new(p.X+offset,p.Y,p.Z),at,731,model,gameFrame);
    private static void Movement()
    {
        var p=RecyclerVineChecks.Fresh();var at=TimedEffects.Now.AddDays(2);var body=Create(p,at,16);
        try
        {
            Through(body,24);Require(body.X==p.X+16,"Recycler moved before first think");Through(body,120);
            Require(body.X<p.X+16,"Recycler common-pet follow did not advance");
            var before=body.Prepared.Controller;int life=body.MaximumLife256/2;SkillHealth.SetBalance256(body,life);
            DruidRecyclerWorld.Advance(body,before.NextUtc.AddMinutes(1),false);
            Require(body.Map==Map.Internal&&body.Prepared.Controller.Seed==before.Seed&&SkillHealth.Balance256(body)==life,"Offline recycler moved/drew/healed");
            DruidRecyclerWorld.Advance(body,body.Prepared.Controller.NextUtc,true);
            Require(body.Map==p.Map&&SkillHealth.Balance256(body)==life+17,"Recycler resume replayed missed recovery");
            var valid=body.Prepared;string bytes=body.SavedText;bool refused=false;
            try{body.RestorePrepared(valid with{Controller=valid.Controller with{GameBase=-1}});}catch(InvalidDataException){refused=true;}
            Require(refused&&body.SavedText==bytes,"Invalid world restore was not atomic");
            var copy=body.Prepared.Controller;if(copy.Motion.Points.Length>0)
            {var first=copy.Motion.Points[0];copy.Motion.Points[0]=new(first.X+1,first.Y);Require(body.Prepared.Controller.Motion.Points[0]==first,"Saved movement points aliased caller");}
        }
        finally{End(p);}
    }
    private static void Feeding(CombatAbility ability,RecyclerRecoveryModel model)
    {
        var p=RecyclerVineChecks.Fresh();var at=TimedEffects.Now.AddDays(2);var corpse=RecyclerRecoveryChecks.Corpse(p);
        var body=Create(p,at,3,ability,model);
        try
        {
            SkillHealth.SetBalance256(p,SkillHealth.Maximum256(p)/2+17);NecromancerEffects.SetMana256(p,p.ManaMax*128+17);
            int life=SkillHealth.Balance256(p),mana=NecromancerEffects.ManaBalance256(p);Through(body,24);
            Require(!CorpseSkillRules.NoSelect(corpse),"Recycler reserved before first think");Through(body,25);var s=body.Prepared.Controller;
            Require(s is{Mode:8,SkillAttempts:1,Reservations:1,Allocations:1,Ai.LastSkillFrame:1025}&&CorpseSkillRules.NoSelect(corpse)
                &&DruidRecyclerRecovery.Get(s.LastRecovery) is{Remaining:47,Triggered:false}&&corpse.GetAmount(typeof(Gold))==1
                &&SkillHealth.Balance256(p)==life&&NecromancerEffects.ManaBalance256(p)==mana,"Mode entry did not reserve/create immediately without premature credit");
            Through(body,25+DruidRecyclerWorld.SkillEnd());Require(body.Prepared.Controller.SkillAttempts==1&&body.Prepared.Controller.Reservations==1,"Animation end repeated start63");
            long id=s.LastRecovery;body.Delete();var effect=DruidRecyclerRecovery.Get(id);
            DruidRecyclerRecovery.Tick(effect.NextUtc.AddMilliseconds(45*40L),true,p.Serial.Value);
            var expected=DruidRecyclerRecovery.Gain(ability,1,45,ability==CombatAbility.CarrionVine?life:mana,
                ability==CombatAbility.CarrionVine?SkillHealth.Maximum256(p):p.ManaMax*256,true,true,model);
            Require(DruidRecyclerRecovery.Get(id) is{Triggered:true,Remaining:1}
                &&(ability==CombatAbility.CarrionVine?SkillHealth.Balance256(p):NecromancerEffects.ManaBalance256(p))==expected.After256,
                "World-created recovery lost player ownership/profile");
        }
        finally{corpse.Delete();End(p);}
    }
    private static void FullAndApproach()
    {
        var p=RecyclerVineChecks.Fresh();var at=TimedEffects.Now.AddDays(2);var corpse=RecyclerRecoveryChecks.Corpse(p,7);var body=Create(p,at,3);
        try
        {
            int distance=Math.Abs(body.X-corpse.X);Through(body,150);
            Require(!CorpseSkillRules.NoSelect(corpse)&&body.Prepared.Controller.SkillAttempts==0&&Math.Abs(body.X-corpse.X)<distance,
                "Full owner must suppress feeding but retain corpse approach");
            SkillHealth.SetBalance256(p,SkillHealth.Maximum256(p)/2);Through(body,250);
            Require(CorpseSkillRules.NoSelect(corpse)&&body.Prepared.Controller.Reservations==1,"Injured owner did not enable feeding");
        }
        finally{corpse.Delete();End(p);}
        p=RecyclerVineChecks.Fresh();corpse=RecyclerRecoveryChecks.Corpse(p);body=Create(p,at,3,CombatAbility.SolarCreeper,RecyclerRecoveryModel.CurrentTableSolarProvisional);
        try
        {
            SkillHealth.SetBalance256(p,SkillHealth.Maximum256(p)/2);Through(body,100);
            Require(body.Prepared.Controller.SkillAttempts==0&&!CorpseSkillRules.NoSelect(corpse),"Solar used life deficit instead of mana deficit");
            NecromancerEffects.SetMana256(p,p.ManaMax*128);Through(body,150);
            Require(body.Prepared.Controller.Reservations==1,"Solar did not react to missing mana");
        }
        finally{corpse.Delete();End(p);}
    }
    private static void FailureAndEarlyGame()
    {
        var p=RecyclerVineChecks.Fresh();var at=TimedEffects.Now.AddDays(2);var corpse=RecyclerRecoveryChecks.Corpse(p);var body=Create(p,at,3,gameFrame:0);
        var allocate=DruidRecyclerRecovery.Allocate;
        try
        {
            SkillHealth.SetBalance256(p,SkillHealth.Maximum256(p)/2);Through(body,50);
            Require(body.Prepared.Controller.SkillAttempts==0&&!CorpseSkillRules.NoSelect(corpse),"Strict initial50frame delay was lost");
            DruidRecyclerRecovery.Allocate=_=>false;Through(body,100);
            Require(body.Prepared.Controller is{SkillAttempts:1,Reservations:1,Allocations:0,Ai.LastSkillFrame:>50}
                &&CorpseSkillRules.NoSelect(corpse)&&corpse.GetAmount(typeof(Gold))==1,"Failed factory changed attempt clock/reservation/mode");
            var collision=DruidRecyclerWorld.Collision;
            try
            {
                DruidRecyclerWorld.Collision=(_,_,_)=>0x3c01;
                var fallback=DruidRecyclerWorld.WalkToCorpse(body,body.Prepared.Controller,corpse,7);
                Require(fallback.PendingVisionReset&&fallback.Mode==1&&fallback.NextThink>fallback.Frame
                    &&fallback.Seed!=body.Prepared.Controller.Seed,"Blocked walk lost failure flags/vision reset/idle-or-close fallback");
            }
            finally{DruidRecyclerWorld.Collision=collision;}
        }
        finally{DruidRecyclerRecovery.Allocate=allocate;corpse.Delete();End(p);}
    }
    private static void Check()
    {
        using var walk=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"pet-walk-flags-source-probe.json")));int comparisons=0;
        foreach(var row in walk.RootElement.EnumerateArray())
        {
            var c=row[0];int N(int i)=>c[i].GetInt32();if(N(0)!=2||N(1)!=0||N(2)!=0||N(3)!=1){continue;}
            int deleted=0,flags=0,draws=0,idle=0,close=0;var events=new List<int>();
            bool result=PetWalkFailure.Apply(N(4),new(()=>{deleted++;events.Add(1);},()=>{flags=64;events.Add(2);},
                ()=>{draws++;events.Add(3);return N(5);},n=>{idle=n;events.Add(4);},n=>{close=n;events.Add(5);}));
            Require(new[]{result?1:0,deleted,flags,draws,idle,close}.SequenceEqual(row[1].EnumerateArray().Select(x=>x.GetInt32()))
                &&events.SequenceEqual(row[2].EnumerateArray().Select(x=>x.GetInt32())),"Complete-source walk failure flag dispatch");comparisons++;
        }
        Require(comparisons==800,"Incomplete walk flag comparison coverage");
        Require(!DruidCombat.Implemented(CombatAbility.CarrionVine)&&!DruidCombat.Implemented(CombatAbility.SolarCreeper),"Prepared world enabled learned route");
        Movement();Feeding(CombatAbility.CarrionVine,RecyclerRecoveryModel.LegacySource);
        Feeding(CombatAbility.SolarCreeper,RecyclerRecoveryModel.LegacySource);Feeding(CombatAbility.SolarCreeper,RecyclerRecoveryModel.CurrentTableSolarProvisional);
        FullAndApproach();FailureAndEarlyGame();
        var p=RecyclerVineChecks.Fresh();var at=TimedEffects.Now.AddDays(2);var body=Create(p,at,16);Through(body,26);
        Require(body.Prepared.Controller.Mode==2&&body.Prepared.Controller.Motion.Points.Length>0,"Stage actual recycler movement");
        SkillHealth.SetBalance256(body,12007);PersistentMinions.Suspend(p);
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,body.Serial.Value,body.Prepared,SkillHealth.Balance256(body),p.Map.MapID,p.Location)));
        Log.Information("Recycler world:800complete-source walk flag comparisons,shared follow/path,full-resource corpse approach,source-delay mature/early gating,entry reservation,failed factory,actor-independent delayed credit,explicit Solar profiles and offline controls pass; saved parked controller staged");
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);var body=(DruidRecyclerBody)World.FindMobile((Serial)saved.Body);
        Require(body.ControlMaster==p&&body.Map==Map.Internal&&body.SavedText==JsonSerializer.Serialize(saved.State)&&SkillHealth.Balance256(body)==saved.Life,"Actual recycler world reload lost state");
        p.MoveToWorld(saved.Point,Map.Maps[saved.Map]);var before=body.Prepared.Controller;
        DruidRecyclerWorld.Advance(body,before.NextUtc,false);
        Require(body.Prepared.Controller.Seed==before.Seed&&body.Prepared.Controller.SkillAttempts==before.SkillAttempts&&SkillHealth.Balance256(body)==saved.Life,"Loaded world replayed RNG/skills/healing");
        DruidRecyclerWorld.Advance(body,body.Prepared.Controller.NextUtc,true);
        Require(body.Map==p.Map&&SkillHealth.Balance256(body)==saved.Life+17,"Loaded recycler world did not resume once");
        PersistentMinions.Suspend(p);File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{State=body.Prepared,Life=SkillHealth.Balance256(body)}));
    }
    private static void Live(NetState network)
    {
        var p=RecyclerVineChecks.Fresh();network.Seeded=network.SentFirstPacket=true;network.CompressionEnabled=false;network.Mobile=p;p.NetState=network;
        var field=typeof(NetState).GetField("_protocolState",BindingFlags.Instance|BindingFlags.NonPublic);field.SetValue(network,Enum.Parse(field.FieldType,"GameServer_LoggedIn"));network.ResetMovementState();
        var corpse=RecyclerRecoveryChecks.Corpse(p,7);var body=Create(p,TimedEffects.Now,3);
        SkillHealth.SetBalance256(p,SkillHealth.Maximum256(p)/2+17);int life=SkillHealth.Balance256(p);bool sawEntry=false;long deadline=Core.TickCount+10000;
        void Inspect()
        {
            try
            {
                var s=body.Prepared.Controller;Require(network.Running&&Core.TickCount<deadline&&s.Fault==null,"Automatic recycler timeout/disconnect/fault:"+s.Fault);
                if(s.Allocations>0)
                {sawEntry=true;Require(CorpseSkillRules.NoSelect(corpse)&&corpse.GetAmount(typeof(Gold))==1,"Natural feed lost reservation/loot");}
                if(SkillHealth.Balance256(p)==life){Timer.StartTimer(TimeSpan.FromMilliseconds(10),Inspect);return;}
                int expected=Math.Min(SkillHealth.Maximum256(p)/256,life/256+(SkillHealth.Maximum256(p)/256)*4/100)*256;
                Require(sawEntry&&s.SkillAttempts==1&&s.Reservations==1&&SkillHealth.Balance256(p)==expected,"Natural corpse seek/move/feed/credit mismatch");
                Log.Information("recycler-world-live-result: passed");corpse.Delete();End(p);
            }
            catch(Exception e){Log.Error(e,"recycler-world-live-result: failed");corpse.Delete();End(p);}
        }
        Log.Information("recycler-world-live: ready");Timer.StartTimer(TimeSpan.FromMilliseconds(10),Inspect);
    }
}
