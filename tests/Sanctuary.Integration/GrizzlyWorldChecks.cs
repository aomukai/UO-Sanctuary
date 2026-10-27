// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Accounting;
using Server.Collections;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary.Integration;

public static class GrizzlyWorldChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(GrizzlyWorldChecks));
    private sealed record Saved(uint Owner,uint Bear,GrizzlyBodyState State,uint Target,int TargetLife,int Life,int Map,Point3D Point);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"grizzly-world-fixtures.json");
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    public static void Configure()
    {
        Register("grizzly-world-check",Check);Register("grizzly-world-reload-check",Reload);
        Register("grizzly-world-snapshot-check",Snapshot);
        Register("grizzly-world-live-check",()=>FixtureNetwork.Connected("grizzly-world-live-result",Live));
    }
    private static void Register(string command,Action action)=>ConsoleInputHandler.RegisterCommand(command,"Grizzly AI112 movement,ordinary hit and cached BearSmite release",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Command}: passed",command);}catch(Exception e){Log.Error(e,"{Command}: failed",command);}}));
    private static void Train(PlayerMobile p,string id)
    {
        Require(TalentCatalog.TryGet(DiabloClass.Druid,id,out var talent),"Grizzly talent");
        foreach(var prior in talent.Prerequisites){Train(p,prior);}
        if(ClassCreation.Profiles.GetRank(p.Serial,id)==0){Require(ClassCreation.Profiles.TryTrain(p.Serial,id,out _),"Train "+id);}
    }
    private static PlayerMobile Fresh(){var p=SpiritWolfWorldChecks.Fresh();p.Name="GrizzlyWorldFixture";Train(p,"druid.summongrizzly");return p;}
    private static void End(PlayerMobile p)=>SpiritWolfWorldChecks.End(p);
    private static GraveyardZombie Target(PlayerMobile p,int dx=2)
    {
        var target=new GraveyardZombie{CantWalk=true,NoKillAwards=true};target.SetHits(20000);target.MoveToWorld(new(p.X+dx,p.Y,p.Z),p.Map);target.AIObject.AITimer.Stop();
        foreach(var r in new[]{ResistanceType.Physical,ResistanceType.Fire,ResistanceType.Cold,ResistanceType.Energy}){target.SetResistance(r,0);}return target;
    }
    private static void Through(DruidGrizzlyBody body,int frame,bool actions=true)
    {
        var state=body.Prepared.Controller;Require(frame>state.Frame,"Reverse Grizzly clock");
        DruidGrizzlyWorld.Advance(body,state.NextUtc.AddMilliseconds((frame-state.Frame-1)*40L),actions);
        Require(body.Prepared.Controller.Fault==null,"Grizzly world fault: "+body.Prepared.Controller.Fault);
    }
    private static ulong Seed(DruidGrizzlyBody body,BaseCreature target,bool sequence,bool hit=true)
    {
        var state=body.Prepared.Controller with{AttackPacket=DruidBeastAttackStats.Capture(SkillMechanics.Values((PlayerMobile)body.ControlMaster,CombatAbility.SummonGrizzly),body.SummonDifficulty,sequence?14:4)};
        int chance=DruidGrizzlyHit.Chance(body,state,target);
        for(ulong initial=1;initial<10000;initial++)
        {ulong stream=initial;if((SourceRandom.Roll(ref stream)%100<50)==sequence&&AssassinTrapAttack.Hit(chance,SourceRandom.Roll(ref stream))==hit){return initial;}}
        throw new InvalidOperationException("No Grizzly AI/hit seed");
    }
    private static void Enter(DruidGrizzlyBody body,BaseCreature target,bool sequence,bool hit=true)
    {body.RestorePrepared(body.Prepared with{Controller=body.Prepared.Controller with{Seed=Seed(body,target,sequence,hit)}});Through(body,25);}
    private static void Combat()
    {
        var p=Fresh();var at=TimedEffects.Now.AddHours(1);var bear=DruidGrizzlyWorld.Create(p,new(p.X+1,p.Y,p.Z),at,1);var target=Target(p);
        try
        {
            int life=SkillHealth.Balance256(target);Enter(bear,target,true);
            var entry=bear.Prepared.Controller;var packet=entry.Sequence?.Packet;
            Require(entry is{Mode:14,Sequences:1,AttackSpeed:248,StrikeRequests:0}&&packet is{Total256:>0,StunFrames:0}
                &&packet.Physical256==packet.Total256+250*256&&SkillHealth.Balance256(target)==life,"BearSmite entry/cached total/physical field");
            ulong seed=entry.Seed;target.SetResistance(ResistanceType.Physical,100);
            SorceressEffects.Profiles.Store(bear,new(Enchantment:new(at.AddHours(1),1,5000,5000,100,p.Serial.Value)));
            Through(bear,34);Require(SkillHealth.Balance256(target)==life&&bear.Prepared.Controller.Seed==seed,"BearSmite hit or rerolled before callback10");
            Through(bear,35);var hit=bear.Prepared.Controller.LastHit;
            Require(hit is{Outcome:GrizzlyHitOutcome.Hit}&&life-SkillHealth.Balance256(target)==packet.Total256&&hit.Total256==packet.Total256
                &&bear.Prepared.Controller.Seed==seed&&SourceKnockbacks.Remaining(target)>0&&!target.Frozen&&!target.Paralyzed,
                "Cached release/zero stun/real knockback failed: "+JsonSerializer.Serialize(bear.Prepared.Controller));
            Through(bear,44);Require(bear.Prepared.Controller is{Mode:1,Sequence:null,StrikeRequests:1,NextThink:59},"Sequence end repeated action or lost neutral delay");
        }
        finally{SourceKnockbacks.Clear(target);target.Delete();End(p);}
        p=Fresh();at=TimedEffects.Now.AddHours(1);bear=DruidGrizzlyWorld.Create(p,new(p.X+1,p.Y,p.Z),at,1);target=Target(p);
        try
        {
            int life=SkillHealth.Balance256(target);Enter(bear,target,false);Require(bear.Prepared.Controller is{Mode:4,ActionAt:32,EndAt:38},"Grizzly ordinary A1clock: "+JsonSerializer.Serialize(bear.Prepared.Controller));
            Through(bear,31);Require(SkillHealth.Balance256(target)==life,"Early ordinary hit");Through(bear,32);
            Require(bear.Prepared.Controller.LastHit is{Outcome:GrizzlyHitOutcome.Hit,Physical256:>0} hit&&hit.Total256==hit.Physical256
                &&life-SkillHealth.Balance256(target)==hit.Total256,"Ordinary physical hit borrowed BearSmite flat field");
        }
        finally{target.Delete();End(p);}
    }
    private static void Cancellation()
    {
        foreach(int control in new[]{0,1,2})
        {
            var p=Fresh();var at=TimedEffects.Now.AddHours(1);var bear=DruidGrizzlyWorld.Create(p,new(p.X+1,p.Y,p.Z),at,1);var target=Target(p);
            try
            {
                int life=SkillHealth.Balance256(target);Enter(bear,target,true);ulong seed=bear.Prepared.Controller.Seed;
                if(control==0){target.Location=new(p.X+12,p.Y,p.Z);Through(bear,35);Require(bear.Prepared.Controller.LastHit?.Outcome==GrizzlyHitOutcome.Invalid,"Lost release range accepted");}
                else if(control==1){bear.InterruptForRecovery();Through(bear,26,false);Require(bear.Prepared.Controller.Sequence==null,"Interrupted sequence retained packet");}
                else{Through(bear,100,false);Require(bear.Map==Map.Internal&&bear.Prepared.Controller.Sequence==null,"Offline pending sequence survived");}
                Require(SkillHealth.Balance256(target)==life&&bear.Prepared.Controller.Seed==seed,"Canceled/invalid release hit or rerolled");
            }
            finally{target.Delete();End(p);}
        }
    }
    private static void DeferredBlock()
    {
        var choice=SourceBlocking.ChoiceRoll;var roll=SourceBlocking.BlockRoll;SourceBlocking.ChoiceRoll=_=>1;SourceBlocking.BlockRoll=_=>0;
        var p=Fresh();var at=TimedEffects.Now.AddHours(1);var bear=DruidGrizzlyWorld.Create(p,new(p.X+1,p.Y,p.Z),at,1);
        var target=new GraveyardSkeleton{CantWalk=true,NoKillAwards=true};target.SetHits(20000);target.MoveToWorld(new(p.X+2,p.Y,p.Z),p.Map);target.AIObject.AITimer.Stop();
        var pending=typeof(GraveyardUndead).GetField("_actionTarget",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
        try
        {
            Require(pending!=null,"Native block cancellation component");pending.SetValue(target,p);int life=SkillHealth.Balance256(target);Enter(bear,target,true);
            Require(bear.Prepared.Controller is{BlockMode:true,Sequence:{Packet:{ResultFlags:16,Total256:0}}}&&pending.GetValue(target)==p,"Prepared block presented early");
            Through(bear,35);Require(bear.Prepared.Controller.LastHit?.Outcome==GrizzlyHitOutcome.Blocked&&pending.GetValue(target)==null&&SkillHealth.Balance256(target)==life,"Deferred block release/presentation");
        }
        finally{SourceBlocking.ChoiceRoll=choice;SourceBlocking.BlockRoll=roll;target.Delete();End(p);}
    }
    private static void MovementAndRank()
    {
        var p=Fresh();var at=TimedEffects.Now.AddHours(1);var bear=DruidGrizzlyWorld.Create(p,new(p.X+16,p.Y,p.Z),at,23);
        try
        {
            var point=bear.Location;PetOwnerRoutes.Moved(p,at.AddMilliseconds(26));Through(bear,150);
            Require(bear.Location!=point&&p.InRange(bear,20),"Grizzly shared owner follow did not move");
            var state=bear.Prepared;string saved=bear.SavedText;bool refused=false;
            try{bear.RestorePrepared(state with{Controller=state.Controller with{Mode=14,Sequence=null}});}catch(InvalidDataException){refused=true;}
            Require(refused&&bear.SavedText==saved,"Malformed controller restore mutated body");
        }
        finally{End(p);}
        p=Fresh();bear=DruidGrizzlyWorld.Create(p,new(p.X+1,p.Y,p.Z),at,1);var target=Target(p);
        try
        {
            var birth=bear.Prepared.Snapshot;Require(ClassCreation.Profiles.TryTrain(p.Serial,"druid.summongrizzly",out _)&&ClassCreation.Profiles.TryTrain(p.Serial,"druid.summongrizzly",out _),"Live Grizzly rank3");
            BattleCommand.Apply(p,bear,1,at);Enter(bear,target,true);
            Require(bear.Prepared.Controller is{AttackPacket:{Rank:3},Sequence:{NpcRank:2}}&&bear.Prepared.Snapshot==birth,"Owner live mode/innate NPC/Battle Command rewrote copied birth");
        }
        finally{target.Delete();End(p);}
    }
    private static void Stage()
    {
        var p=Fresh();var at=TimedEffects.Now.AddDays(2);var bear=DruidGrizzlyWorld.Create(p,new(p.X+1,p.Y,p.Z),at,1);var target=Target(p);
        Enter(bear,target,true);SkillHealth.SetBalance256(bear,20007);
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,bear.Serial.Value,bear.Prepared,target.Serial.Value,
            SkillHealth.Balance256(target),SkillHealth.Balance256(bear),p.Map.MapID,p.Location)));
    }
    private static void Check()
    {
        Require(!DruidCombat.Implemented(CombatAbility.SummonGrizzly),"World backend exposed unfinished learned casting");
        Combat();Cancellation();DeferredBlock();MovementAndRank();Stage();
        Log.Information("Grizzly world:AI112shared movement,A1physical,cached BearSmite callback10/end19,real knockback/zero stun,entry mitigation/RNG,range/offline/interruption cancellation,deferred block and distinct owner/NPC/Battle Command ranks pass; learned casting and full modern event/geometry acceptance remain pending");
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);
        var bear=(DruidGrizzlyBody)World.FindMobile((Serial)saved.Bear);var target=(BaseCreature)World.FindMobile((Serial)saved.Target);
        Require(bear is{Alive:true,Map:var map}&&map==Map.Internal&&bear.ControlMaster==p&&bear.Prepared.Controller is{Mode:1,Sequence:null,StrikeRequests:0}
            &&bear.Prepared.Snapshot==saved.State.Snapshot&&bear.Prepared.Controller.Seed==saved.State.Controller.Seed
            &&SkillHealth.Balance256(target)==saved.TargetLife&&SkillHealth.Balance256(bear)==saved.Life,
                "Reload changed Grizzly: "+JsonSerializer.Serialize(new{Alive=bear.Alive,Map=bear.Map.MapID,Master=bear.ControlMaster?.Serial.Value,
                    State=bear.Prepared,TargetLife=SkillHealth.Balance256(target),Life=SkillHealth.Balance256(bear),Saved=saved}));
        if(p.Map==Map.Internal){p.MoveToWorld(saved.Point,Map.Maps[saved.Map]);}
        DruidGrizzlyWorld.Advance(bear,bear.Prepared.Controller.NextUtc,false);
        Require(SkillHealth.Balance256(target)==saved.TargetLife&&SkillHealth.Balance256(bear)==saved.Life,"Loaded clock skip attacked or healed");
        target.Delete();End(p);Stage();
    }
    private static void Snapshot()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var bear=(DruidGrizzlyBody)World.FindMobile((Serial)saved.Bear);
        var target=(BaseCreature)World.FindMobile((Serial)saved.Target);var damage=new System.Collections.Generic.List<string>();
        foreach(var entry in target.DamageEntries){damage.Add($"{entry.Damager?.Serial.Value}:{entry.Damager?.GetType().Name}:{entry.Damager?.Name}:{entry.DamageGiven}");}
        Require(SkillHealth.Balance256(target)==saved.TargetLife&&SkillHealth.Balance256(bear)==saved.Life
            &&bear.Prepared.Controller.StrikeRequests==0&&bear.Prepared.Controller.Seed==saved.State.Controller.Seed,
            "Pending fixture changed before save: "+JsonSerializer.Serialize(new{TargetLife=SkillHealth.Balance256(target),SavedTargetLife=saved.TargetLife,
                Life=SkillHealth.Balance256(bear),SavedLife=saved.Life,Damage=damage,State=bear.Prepared.Controller}));
    }
    private static void Live(NetState network)
    {
        var p=Fresh();network.Seeded=true;network.SentFirstPacket=true;network.CompressionEnabled=false;
        var field=typeof(NetState).GetField("_protocolState",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
        Require(field!=null,"Grizzly protocol bootstrap");field.SetValue(network,Enum.Parse(field.FieldType,"GameServer_LoggedIn"));
        var account=new Account("grizzly-world-"+Guid.NewGuid().ToString("N"),Guid.NewGuid().ToString("N"));account[0]=p;network.Account=account;network.Mobile=p;p.NetState=network;network.ResetMovementState();
        var target=Target(p);TimedEffects.PreventHealing(target,TimedEffects.Now);int life=SkillHealth.Balance256(target);
        var bear=DruidGrizzlyWorld.Create(p,new(p.X+1,p.Y,p.Z),TimedEffects.Now,1);
        bear.RestorePrepared(bear.Prepared with{Controller=bear.Prepared.Controller with{Seed=Seed(bear,target,true)}});
        long deadline=Core.TickCount+12000;
        void Inspect()
        {
            try
            {
                var state=bear.Prepared.Controller;Require(network.Running&&!bear.Deleted&&state.Fault==null,"Natural Grizzly session/body failed: "+JsonSerializer.Serialize(state));
                Require(Core.TickCount<deadline,"Natural Grizzly combat timeout: "+JsonSerializer.Serialize(state));
                if(state.Hits==0){Timer.StartTimer(TimeSpan.FromMilliseconds(50),Inspect);return;}
                Require(state.Sequences>0&&SkillHealth.Balance256(target)<life&&state.LastHit is{Outcome:GrizzlyHitOutcome.Hit} hit
                    &&hit.Physical256>hit.Total256&&SourceKnockbacks.Remaining(target)>0,"Natural Grizzly did not release cached BearSmite/knockback");
                SourceKnockbacks.Clear(target);target.Delete();End(p);account.Delete();Log.Information("grizzly-world-live-result: passed");
            }
            catch(Exception e){SourceKnockbacks.Clear(target);target.Delete();End(p);account.Delete();Log.Error(e,"grizzly-world-live-result: failed");}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(40),()=>{Log.Information("grizzly-world-live: ready");Inspect();});
    }
}
