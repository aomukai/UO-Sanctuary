// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
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

public static class DireWolfWorldChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(DireWolfWorldChecks));
    private sealed record Saved(uint Owner,uint Queued,uint Corpse,DireWolfBodyState QueuedState,uint Raging,DireWolfBodyState RageState,uint Target,int Life,int Map,Point3D Point);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"dire-wolf-world-fixtures.json");
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    public static void Configure()
    {
        Register("dire-wolf-world-check",Check);Register("dire-wolf-world-reload-check",Reload);
        Register("dire-wolf-world-live-check",()=>FixtureNetwork.Connected("dire-wolf-world-live-result",Live));
    }
    private static void Register(string command,Action action)=>ConsoleInputHandler.RegisterCommand(command,"Saved Fenris movement, feeding/rage and physical bite consumers",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Command}: passed",command);}catch(Exception e){Log.Error(e,"{Command}: failed",command);}}));
    private static void Train(PlayerMobile p,string id)
    {
        Require(TalentCatalog.TryGet(DiabloClass.Druid,id,out var talent),"Druid talent");
        foreach(var prerequisite in talent.Prerequisites){Train(p,prerequisite);}
        if(ClassCreation.Profiles.GetRank(p.Serial,id)==0){Require(ClassCreation.Profiles.TryTrain(p.Serial,id,out _),"Train "+id);}
    }
    private static PlayerMobile Fresh()
    {
        var p=SpiritWolfWorldChecks.Fresh();p.Name="DireWolfWorldFixture";Train(p,"druid.summondirewolf");
        Require(!Server.Spells.SpellHelper.IsTown(p.Location,p),"Fenris combat/feeding fixture must be outside town");return p;
    }
    private static void End(PlayerMobile p)=>SpiritWolfWorldChecks.End(p);
    private static void Through(DruidDireWolfBody wolf,int frame,bool actions=true)
    {
        var s=wolf.Prepared.Controller;Require(frame>s.Frame,"Reverse/equal Fenris fixture clock");
        DruidDireWolfWorld.Advance(wolf,s.NextUtc.AddMilliseconds((frame-s.Frame-1)*40L),actions);
        Require(wolf.Prepared.Controller.Fault==null,"Fenris world fault: "+wolf.Prepared.Controller.Fault);
    }
    private static GraveyardZombie Target(PlayerMobile p,int dx=2)
    {
        var target=new GraveyardZombie{CantWalk=true,NoKillAwards=true};target.SetHits(20000);target.MoveToWorld(new(p.X+dx,p.Y,p.Z),p.Map);target.AIObject.AITimer.Stop();
        foreach(var resistance in new[]{ResistanceType.Physical,ResistanceType.Fire,ResistanceType.Cold,ResistanceType.Energy}){target.SetResistance(resistance,0);}return target;
    }
    private static void Numeric()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"dire-wolf-hit-source-probe.json")));int cases=0;
        foreach(var row in doc.RootElement.EnumerateArray())
        {
            int N(int i)=>row[i].GetInt32();var bounds=new List<int>();
            int Limited(int bound){bounds.Add(bound);return N(4)==0?0:bound-1;}uint Raw(){bounds.Add(-1);return(uint)N(5);}
            bool typed=N(3)!=0;
            var packet=DruidDireWolfHit.Packet(N(0),N(1),N(2),typed?512:0,typed?1536:0,typed?768:0,typed?1792:0,typed?1280:0,typed?2048:0,typed?31:0,5,Limited,Raw);
            Require(packet.Physical256==N(6) && packet.Fire256==N(7) && packet.Lightning256==N(8) && packet.Cold256==N(9)
                && packet.ColdFrames==N(10) && packet.Critical==(N(5)<5) && bounds.SequenceEqual(row[12].EnumerateArray().Select(x=>x.GetInt32())),"Fenris complete packet/RNG source mismatch: "+row.GetRawText());cases++;
        }
        Require(cases==576,"Incomplete Fenris packet fixture");
    }
    private static ulong Seed(DruidDireWolfBody wolf,BaseCreature target,bool hit,bool critical=false)
    {
        var state=wolf.Prepared.Controller;var attack=state.AttackPacket.Value;int chance=DruidDireWolfHit.Chance(wolf,state,target);
        for(ulong initial=1;initial<10000;initial++)
        {
            ulong stream=initial;if(AssassinTrapAttack.Hit(chance,SourceRandom.Roll(ref stream))!=hit){continue;}if(!hit){return initial;}
            // Inspect the packet's actual critical result rather than assume
            // a fixed count of bounded damage rolls.
            var packet=DruidDireWolfHit.Packet(attack.Minimum,attack.Maximum,DruidDireWolfHit.DamagePercent(wolf,state),0,0,0,0,0,0,0,5,
                bound=>(int)SourceRandom.Limited(ref stream,bound),()=>SourceRandom.Roll(ref stream));
            if(packet.Critical==critical){return initial;}
        }
        throw new InvalidOperationException("No Fenris seed");
    }
    private static void Feeding()
    {
        var p=Fresh();var at=TimedEffects.Now.AddHours(1);var wolf=DruidDireWolfWorld.Create(p,new(p.X+1,p.Y,p.Z),at,1);
        var corpse=DeathSentryExplosionChecks.Body(p,new(p.X+2,p.Y,p.Z));
        try
        {
            Through(wolf,25);var queued=wolf.Prepared.Controller;
            Require(queued is{Mode:4,Feeding:true,AttackPacket:{Rank:1,Attack:300}} && queued.FeedingCorpse==corpse.Serial.Value && !corpse.SanctuaryConsumed,"Fenris did not queue NPC314 A1 without early consumption: "+JsonSerializer.Serialize(queued)+" corpse="+corpse.Location+" usable="+CorpseSkillRules.DireWolfUsableAt(p,corpse,wolf,double.PositiveInfinity)+" owner="+p.Location+" wolf="+wolf.Location);
            int mana=NecromancerEffects.ManaBalance256(p),life=SkillHealth.Balance256(wolf);Through(wolf,queued.ActionAt);
            var fed=wolf.Prepared.Controller;
            Require(corpse.SanctuaryConsumed && CorpseSkillRules.NoSelect(corpse) && corpse.GetAmount(typeof(Gold))==1
                && fed.Fed==1 && fed.RagePercent==100 && fed.RageUntil==fed.Frame+500 && fed.StrikeRequests==0
                && SkillHealth.Balance256(wolf)==life && NecromancerEffects.ManaBalance256(p)==mana,"Feeding release/rage, loot, direct-resource or weapon-event isolation");
            Require(!DruidDireWolfCorpses.Consume(wolf,corpse,()=>throw new InvalidOperationException("Consumed corpse rage duplicated")).Consumed,"Repeated feeding reused corpse");
            wolf.Frozen=true;Through(wolf,fed.RageUntil-1);Require(wolf.Prepared.Controller.RagePercent==100,"Rage expired early");
            Through(wolf,fed.RageUntil);Require(wolf.Prepared.Controller is{RageUntil:0,RagePercent:0},"Rage did not expire on frame500");
        }
        finally{corpse.Delete();End(p);}
        // A competing skill consumes the selected corpse during the windup.
        p=Fresh();wolf=DruidDireWolfWorld.Create(p,new(p.X+1,p.Y,p.Z),at,3);corpse=DeathSentryExplosionChecks.Body(p,new(p.X+2,p.Y,p.Z));
        try
        {
            Through(wolf,25);var queued=wolf.Prepared.Controller;Require(queued.Feeding,"Competition fixture did not queue feeding");
            Require(CorpseSkillRules.Reserve(corpse),"Competing reservation");NecromancerCombat.Consume(corpse);Through(wolf,queued.ActionAt);
            Require(wolf.Prepared.Controller is{Fed:0,RageUntil:0,RagePercent:0},"Lost corpse created rage");
        }
        finally{corpse.Delete();End(p);}
    }
    private static void Combat()
    {
        var p=Fresh();Train(p,"druid.summongrizzly");Train(p,"druid.heartofwolverine");var at=TimedEffects.Now.AddHours(1);
        var wolf=DruidDireWolfWorld.Create(p,new(p.X+1,p.Y,p.Z),at,1);var target=Target(p);var block=SourceBlocking.BlockRoll;
        var heart=DruidOakBody.CreatePrepared(p,new(p.X-1,p.Y,p.Z),at,CombatAbility.HeartOfWolverine);
        DireWolfHitResult Hit(ref ulong seed)=>DruidDireWolfHit.Resolve(wolf,p,wolf.Prepared.Controller,target,ref seed);
        try
        {
            SourceBlocking.BlockRoll=_=>99;Through(wolf,25);var state=wolf.Prepared.Controller;
            Require(state is{Mode:4,Feeding:false,AttackPacket:{Rank:1,Minimum:7,Maximum:12,Attack:300}},"Fenris physical mode entry");
            Require(wolf.Prepared.Snapshot.Birth.DamagePercent==25,"Copied effective Grizzly rank");
            int chance=DruidDireWolfHit.Chance(wolf,state,target);wolf.Dex=200;p.Dex=400;
            Require(DruidDireWolfHit.Chance(wolf,state,target)==chance,"Owner/native Dex leaked into Fenris AR");
            int life=SkillHealth.Balance256(target);ulong seed=Seed(wolf,target,false);
            Require(Hit(ref seed).Outcome==DireWolfHitOutcome.Miss && SkillHealth.Balance256(target)==life,"Miss damaged target");
            seed=Seed(wolf,target,true);var physical=Hit(ref seed);
            Require(physical is{Outcome:DireWolfHitOutcome.Hit,Critical:false,Cold256:0,Fire256:0,Lightning256:0}
                && physical.Physical256>=2240 && physical.Physical256<3840 && SkillHealth.Balance256(target)==life-physical.Physical256,"Copied Grizzly/FP256 physical bite");
            wolf.RestorePrepared(wolf.Prepared with{Controller=DruidDireWolfWorld.Rage(wolf.Prepared.Controller)});
            Require(DruidWolverineAuras.Apply(heart,wolf,1,at),"Actual Heart aura application");
            NecromancerEffects.Profiles.Store(wolf,new(Curse:new(CombatAbility.Decrepify,p.Serial.Value,1,at.AddHours(1),wolf.X,wolf.Y)));
            Require(DruidDireWolfHit.DamagePercent(wolf,wolf.Prepared.Controller)==95,"Grizzly25+rage100+Heart20+Decrepify-50 did not add once");
            DruidWolverineAuras.Clear(wolf);NecromancerEffects.Profiles.Clear(wolf);
            target.SetResistance(ResistanceType.Physical,100);life=SkillHealth.Balance256(target);seed=Seed(wolf,target,true,true);
            var immune=Hit(ref seed);Require(immune is{Outcome:DireWolfHitOutcome.Hit,Critical:true} && SkillHealth.Balance256(target)==life,"Physical immunity/critical double mitigation");
            target.SetResistance(ResistanceType.Physical,0);SorceressEffects.Profiles.Store(wolf,new(Enchantment:new(at.AddHours(1),1,1024,1024,100,p.Serial.Value)));
            seed=Seed(wolf,target,true);var enchanted=Hit(ref seed);Require(enchanted.Outcome==DireWolfHitOutcome.Hit && enchanted.Fire256==(enchanted.Critical?2048:1024),"Actor Enchant fire was omitted or multiplied by physical rage");SorceressEffects.Profiles.Clear(wolf);
            var birth=wolf.Prepared.Snapshot;var helm=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey=="Tarnhelm").Id,99,new Random(173),true);
            Require(p.EquipItem(helm),"Actual Tarnhelm equip");ClassGear.Refresh(p);
            Require(wolf.Prepared.Controller.AttackPacket==state.AttackPacket && wolf.Prepared.Snapshot==birth,"Live gear rewrote current attack or copied birth");
            target.Location=new(p.X+10,p.Y,p.Z);seed=1;Require(Hit(ref seed).Outcome==DireWolfHitOutcome.Invalid && seed==1,"Lost-range impact rolled or hit");
            target.Location=new(p.X+2,p.Y,p.Z);NecromancerEffects.Profiles.Store(wolf,new(Curse:new(CombatAbility.Decrepify,p.Serial.Value,1,at.AddHours(1),wolf.X,wolf.Y)));
            int hits=wolf.Prepared.Controller.Hits;Through(wolf,wolf.Prepared.Controller.Frame+250);
            Require(wolf.Prepared.Controller.Hits>hits && wolf.Prepared.Controller.AttackPacket is null or{Rank:2},"Repeated slowed physical bites or next-entry rank refresh failed");
        }
        finally{SourceBlocking.BlockRoll=block;target.Delete();End(p);}
    }
    private static void Movement()
    {
        var p=Fresh();var at=TimedEffects.Now.AddHours(1);var wolf=DruidDireWolfWorld.Create(p,new(p.X+16,p.Y,p.Z),at,23);
        try
        {
            var origin=wolf.Location;PetOwnerRoutes.Moved(p,at.AddMilliseconds(26));Through(wolf,24);Require(wolf.Location==origin,"Fenris moved before first AI event");
            Through(wolf,150);Require(wolf.Location!=origin && p.InRange(wolf,20),"Fenris did not consume shared owner follow/path motion");
            int frame=wolf.Prepared.Controller.Frame;var point=wolf.Location;ulong seed=wolf.Prepared.Controller.Seed;
            Through(wolf,frame+250,false);Require(wolf.Map==Map.Internal && wolf.Prepared.Controller.Seed==seed,"Offline AI/movement consumed RNG");
            Through(wolf,wolf.Prepared.Controller.Frame+1);Require(wolf.Map==p.Map,"Fenris did not resume shared legal placement");
            var state=wolf.Prepared;string before=wolf.SavedText;bool refused=false;
            try{wolf.RestorePrepared(state with{Controller=state.Controller with{RageUntil=state.Controller.Frame+501,RagePercent=100}});}catch(InvalidDataException){refused=true;}
            Require(refused && wolf.SavedText==before,"Invalid rage restore mutated live state");
        }
        finally{End(p);}
    }
    private static void Stage()
    {
        var p=Fresh();var at=TimedEffects.Now.AddDays(2);var queued=DruidDireWolfWorld.Create(p,new(p.X+1,p.Y,p.Z),at,11);
        var corpse=DeathSentryExplosionChecks.Body(p,new(p.X+2,p.Y,p.Z));Through(queued,25);Require(queued.Prepared.Controller.Feeding,"Save fixture did not queue feeding");
        var raging=DruidDireWolfWorld.Create(p,new(p.X+8,p.Y,p.Z),at,31);var target=Target(p,9);
        Through(raging,25);Require(raging.Prepared.Controller is{Mode:4,Feeding:false},"Save fixture did not queue physical bite");
        raging.RestorePrepared(raging.Prepared with{Controller=DruidDireWolfWorld.Rage(raging.Prepared.Controller)});SkillHealth.SetBalance256(raging,20007);
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,queued.Serial.Value,corpse.Serial.Value,queued.Prepared,raging.Serial.Value,raging.Prepared,target.Serial.Value,SkillHealth.Balance256(raging),p.Map.MapID,p.Location)));
    }
    private static void Check()
    {
        Require(DruidCombat.Implemented(CombatAbility.SummonDireWolf),"Learned Fenris handler missing");Numeric();Feeding();Combat();Movement();Stage();
        Log.Information("Fenris world:576complete physical/typed source packets; automatic feeding/rage500frames, competing corpse, physical bites/critical/immunity/Enchant, additive Grizzly/aura/curse, live rank capture, slowed repeat bites and shared owner movement pass; learned casting and exact modern cadence remain pending");
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);
        var queued=(DruidDireWolfBody)World.FindMobile((Serial)saved.Queued);var raging=(DruidDireWolfBody)World.FindMobile((Serial)saved.Raging);
        var corpse=(Corpse)World.FindItem((Serial)saved.Corpse);var target=(BaseCreature)World.FindMobile((Serial)saved.Target);
        Require(queued is{Alive:true,Map:var qm} && qm==Map.Internal && raging.Map==Map.Internal && queued.ControlMaster==p && raging.ControlMaster==p
            && queued.Prepared.Controller is{Mode:1,Feeding:false,ActionAt:0} && raging.Prepared.Controller is{Mode:1,ActionAt:0,RagePercent:100}
            && !corpse.SanctuaryConsumed && queued.Prepared.Snapshot==saved.QueuedState.Snapshot && raging.Prepared.Snapshot==saved.RageState.Snapshot
            && SkillHealth.Balance256(raging)==saved.Life,"Reload changed birth/life/rage or replayed pending feeding/attack");
        if(p.Map==Map.Internal){p.MoveToWorld(saved.Point,Map.Maps[saved.Map]);}
        int targetLife=SkillHealth.Balance256(target);DruidDireWolfWorld.Advance(queued,queued.Prepared.Controller.NextUtc,false);DruidDireWolfWorld.Advance(raging,raging.Prepared.Controller.NextUtc,false);
        Require(!corpse.SanctuaryConsumed && SkillHealth.Balance256(target)==targetLife && SkillHealth.Balance256(raging)==saved.Life,"Loaded skip executed a pending skill or healed");
        // Legal resume placement need not be the old melee square. Isolate
        // the saved corpse after checking the retained target's unchanged HP,
        // and allow real approach frames before a fresh feeding windup.
        target.Delete();
        for(int i=0;i<150 && !queued.Prepared.Controller.Feeding;i++)
        {
            Through(queued,queued.Prepared.Controller.Frame+1);
            Require(!corpse.SanctuaryConsumed,"Loaded pending feeding bypassed a new windup");
        }
        Require(queued.Prepared.Controller is{Mode:4,Feeding:true} && queued.Prepared.Controller.ActionAt>queued.Prepared.Controller.Frame,"Fresh post-load approach did not start a new feeding windup");
        Through(queued,queued.Prepared.Controller.ActionAt);Require(corpse.SanctuaryConsumed && queued.Prepared.Controller.Fed==1,"Fresh post-load feeding did not consume once");
        Through(raging,raging.Prepared.Controller.Frame+1);Require(SkillHealth.Balance256(raging)==saved.Life+54 && raging.Prepared.Controller.RagePercent==100,"Post-load one-frame recovery/rage retention");
        target.Delete();corpse.Delete();End(p);Stage();
    }
    private static void Live(NetState network)
    {
        var p=Fresh();network.Seeded=true;network.SentFirstPacket=true;network.CompressionEnabled=false;
        var field=typeof(NetState).GetField("_protocolState",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
        Require(field!=null,"Fenris protocol bootstrap");field.SetValue(network,Enum.Parse(field.FieldType,"GameServer_LoggedIn"));
        var account=new Account("fenris-world-"+Guid.NewGuid().ToString("N"),Guid.NewGuid().ToString("N"));account[0]=p;
        network.Account=account;network.Mobile=p;p.NetState=network;network.ResetMovementState();
        var corpse=DeathSentryExplosionChecks.Body(p,new(p.X+2,p.Y,p.Z));
        var wolf=DruidDireWolfWorld.Create(p,new(p.X+1,p.Y,p.Z),TimedEffects.Now,9821);GraveyardZombie target=null;int life=0;long deadline=Core.TickCount+12000;
        void Inspect()
        {
            try
            {
                var state=wolf.Prepared.Controller;
                Require(network.Running && !wolf.Deleted && state.Fault==null,"Natural Fenris session/body failed: "+JsonSerializer.Serialize(state));
                Require(Core.TickCount<deadline,"Natural Fenris feeding/bite timeout: "+JsonSerializer.Serialize(state));
                if(state.Fed==0){Timer.StartTimer(TimeSpan.FromMilliseconds(50),Inspect);return;}
                Require(state is{Fed:1,RagePercent:100} && state.RageUntil>state.Frame && corpse.SanctuaryConsumed && corpse.GetAmount(typeof(Gold))==1,"Natural Fenris feeding/rage/loot failed");
                if(target==null){target=Target(p);TimedEffects.PreventHealing(target,TimedEffects.Now);life=SkillHealth.Balance256(target);}
                if(state.Hits==0){Timer.StartTimer(TimeSpan.FromMilliseconds(50),Inspect);return;}
                Require(state.LastHit is{Outcome:DireWolfHitOutcome.Hit,Physical256:>0} && SkillHealth.Balance256(target)<life,"Natural raging physical bite did not commit");
                target.Delete();corpse.Delete();End(p);account.Delete();Log.Information("dire-wolf-world-live-result: passed");
            }
            catch(Exception e){target?.Delete();corpse.Delete();End(p);account.Delete();Log.Error(e,"dire-wolf-world-live-result: failed");}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(40),()=>{Log.Information("dire-wolf-world-live: ready");Inspect();});
    }
}
