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

public static class RecyclerRecoveryChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(RecyclerRecoveryChecks));
    private sealed record Saved(uint Owner,uint RemovedActor,uint Corpse,long Effect,RecyclerRecovery State,int Life,int Mana,int Map,Point3D Point);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"recycler-recovery-fixtures.json");
    private static void Require(bool b,string why){if(!b){throw new InvalidOperationException(why);}}
    private static void Register(string name,Action check)=>ConsoleInputHandler.RegisterCommand(name,"Prepared recycler corpse/delay/resource acceptance",_=>Core.LoopContext.Post(()=>
    {try{check();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    public static void Configure()
    {
        Register("recycler-recovery-check",Check);Register("recycler-recovery-reload-check",Reload);
        Register("recycler-recovery-live-check",()=>FixtureClock.NextLoop("recycler-recovery-live-result",
            ()=>FixtureNetwork.Connected("recycler-recovery-live-result",Live)));
    }
    private static Corpse Corpse(PlayerMobile owner)
    {
        var victim=new GraveyardZombie{CantWalk=true,NoKillAwards=true};victim.MoveToWorld(new(owner.X+3,owner.Y,owner.Z),owner.Map);victim.AIObject.AITimer.Stop();Corpse corpse=null;
        var previous=ClassLootHooks.CreatureDied;
        ClassLootHooks.CreatureDied=(actor,container)=>{if(actor!=victim){previous?.Invoke(actor,container);return;}CorpseSkillRules.Capture(actor,container);corpse=container as Corpse;corpse?.DropItem(new Gold(1));};
        try{owner.DoHarmful(victim,true);victim.Damage(victim.Hits+1,owner);CombatDamage.Finish(victim,owner);}finally{ClassLootHooks.CreatureDied=previous;}
        Require(corpse is{Deleted:false}&&CorpseSkillRules.Profiles.Find(corpse)!=null,"Actual zombie corpse metadata");corpse.BeginDecay(TimeSpan.FromHours(1));return corpse;
    }
    private static void End(PlayerMobile p){DruidRecyclerRecovery.Cancel(p);PersistentMinions.Dismiss(p);p.Delete();}
    private static void Through(PlayerMobile p,DateTime at,bool active=true)=>DruidRecyclerRecovery.Tick(at,active,p.Serial.Value);
    private static void Case(CombatAbility ability,RecyclerRecoveryModel model)
    {
        var p=RecyclerVineChecks.Fresh();var at=TimedEffects.Now.AddDays(2);var body=DruidRecyclerBody.CreatePrepared(p,ability,new(p.X+1,p.Y,p.Z),at);var corpse=Corpse(p);
        try
        {
            SkillHealth.SetBalance256(p,SkillHealth.Maximum256(p)/2+17);NecromancerEffects.SetMana256(p,p.ManaMax*128+17);
            int life=SkillHealth.Balance256(p),mana=NecromancerEffects.ManaBalance256(p);
            var result=DruidRecyclerRecovery.Commit(body,corpse,at,model);
            Require(result.Reserved&&result.Allocated&&CorpseSkillRules.NoSelect(corpse)&&!corpse.SanctuaryConsumed&&corpse.GetAmount(typeof(Gold))==1,"Reservation/independent allocation/loot changed");
            Require(!DruidRecyclerRecovery.Commit(body,corpse,at,model).Reserved,"Reserved corpse reused");body.Delete();
            Through(p,at.AddMilliseconds(45*40));var before=DruidRecyclerRecovery.Get(result.Effect);
            Require(before is{Remaining:2,Triggered:false}&&SkillHealth.Balance256(p)==life&&NecromancerEffects.ManaBalance256(p)==mana,"Recovery before elapsed45or lost removed actor effect");
            Through(p,before.NextUtc);var fired=DruidRecyclerRecovery.Get(result.Effect);
            var expected=DruidRecyclerRecovery.Gain(ability,1,45,ability==CombatAbility.CarrionVine?life:mana,
                ability==CombatAbility.CarrionVine?SkillHealth.Maximum256(p):p.ManaMax*256,true,true,model);
            Require(fired is{Remaining:1,Triggered:true}&&(ability==CombatAbility.CarrionVine?SkillHealth.Balance256(p):NecromancerEffects.ManaBalance256(p))==expected.After256,
                "Actual delayed stat writer ignored selected profile/current maximum");
            int afterLife=SkillHealth.Balance256(p),afterMana=NecromancerEffects.ManaBalance256(p);Through(p,fired.NextUtc);
            Require(DruidRecyclerRecovery.Get(result.Effect)==null&&SkillHealth.Balance256(p)==afterLife&&NecromancerEffects.ManaBalance256(p)==afterMana
                &&corpse.GetAmount(typeof(Gold))==1,"Recovery repeated/failed47callback expiry/lost loot");
        }
        finally{corpse.Delete();End(p);}
    }
    private static void FailureAndOffline()
    {
        var p=RecyclerVineChecks.Fresh();var at=TimedEffects.Now.AddDays(2);var body=DruidRecyclerBody.CreatePrepared(p,CombatAbility.CarrionVine,new(p.X+1,p.Y,p.Z),at);var first=Corpse(p);var allocate=DruidRecyclerRecovery.Allocate;
        try
        {
            long sequence=DruidRecyclerRecovery.Snapshot().NextId;DruidRecyclerRecovery.Allocate=_=>false;
            Require(DruidRecyclerRecovery.Commit(body,first,at,RecyclerRecoveryModel.LegacySource) is{Reserved:true,Allocated:false,Effect:0}
                &&CorpseSkillRules.NoSelect(first)&&first.GetAmount(typeof(Gold))==1&&DruidRecyclerRecovery.Snapshot().NextId==sequence,"Failed allocation restored reservation or changed sequence/loot");
            DruidRecyclerRecovery.Allocate=allocate;var corpse=Corpse(p);
            try
            {
                SkillHealth.SetBalance256(p,SkillHealth.Maximum256(p)/2+17);int life=SkillHealth.Balance256(p);
                var commit=DruidRecyclerRecovery.Commit(body,corpse,at,RecyclerRecoveryModel.LegacySource);Through(p,at.AddMilliseconds(46*40),false);
                Require(DruidRecyclerRecovery.Get(commit.Effect) is{Remaining:1,Triggered:false}&&SkillHealth.Balance256(p)==life,"Offline debt replayed recovery");
                Through(p,DruidRecyclerRecovery.Get(commit.Effect).NextUtc,true);
                Require(DruidRecyclerRecovery.Get(commit.Effect)==null&&SkillHealth.Balance256(p)==life,"Return backfilled a missed age45credit");
            }
            finally{corpse.Delete();}
            var pending=Corpse(p);
            try
            {
                var commit=DruidRecyclerRecovery.Commit(body,pending,at,RecyclerRecoveryModel.LegacySource);var valid=DruidRecyclerRecovery.Snapshot();string text=JsonSerializer.Serialize(valid);bool refused=false;
                try{DruidRecyclerRecovery.Restore(valid with{Effects=[..valid.Effects,valid.Effects.Single(e=>e.Id==commit.Effect)]});}catch(InvalidDataException){refused=true;}
                Require(refused&&JsonSerializer.Serialize(DruidRecyclerRecovery.Snapshot())==text,"Duplicate recovery restore was not atomic");
                refused=false;var effect=DruidRecyclerRecovery.Get(commit.Effect);
                try{DruidRecyclerRecovery.Restore(valid with{Effects=[effect with{Model=(RecyclerRecoveryModel)99}]});}catch(InvalidDataException){refused=true;}
                Require(refused&&JsonSerializer.Serialize(DruidRecyclerRecovery.Snapshot())==text,"Unknown recovery profile restore was not atomic");
            }
            finally{pending.Delete();}
        }
        finally{DruidRecyclerRecovery.Allocate=allocate;first.Delete();End(p);}
    }
    private static void Check()
    {
        Case(CombatAbility.CarrionVine,RecyclerRecoveryModel.LegacySource);
        Case(CombatAbility.SolarCreeper,RecyclerRecoveryModel.LegacySource);
        Case(CombatAbility.SolarCreeper,RecyclerRecoveryModel.CurrentTableSolarProvisional);FailureAndOffline();
        var p=RecyclerVineChecks.Fresh();var at=TimedEffects.Now.AddDays(2);var body=DruidRecyclerBody.CreatePrepared(p,CombatAbility.CarrionVine,new(p.X+1,p.Y,p.Z),at);var corpse=Corpse(p);
        SkillHealth.SetBalance256(p,SkillHealth.Maximum256(p)/2+17);var commit=DruidRecyclerRecovery.Commit(body,corpse,at,RecyclerRecoveryModel.LegacySource);
        Require(commit.Allocated,"Stage pending recovery");uint actor=body.Serial.Value;body.Delete();Through(p,at.AddMilliseconds(37*40));var effect=DruidRecyclerRecovery.Get(commit.Effect);
        Require(effect is{Remaining:10,Triggered:false},"Stage pending callback count");
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,actor,corpse.Serial.Value,effect.Id,effect,SkillHealth.Balance256(p),NecromancerEffects.ManaBalance256(p),p.Map.MapID,p.Location)));
        Log.Information("Recycler recovery:actual zombie reservation/loot,failed allocation,removed-NPC player ownership,age45stat commit/47callback expiry,offline no-replay,atomic restore and explicit Solar profiles pass; pending independent credit saved");
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);var corpse=(Corpse)World.FindItem((Serial)saved.Corpse);
        var effect=DruidRecyclerRecovery.Get(saved.Effect);
        Require(World.FindMobile((Serial)saved.RemovedActor)==null&&JsonSerializer.Serialize(effect)==JsonSerializer.Serialize(saved.State)
            &&SkillHealth.Balance256(p)==saved.Life&&NecromancerEffects.ManaBalance256(p)==saved.Mana&&CorpseSkillRules.NoSelect(corpse)&&corpse.GetAmount(typeof(Gold))==1,
            "Actual recovery reload lost state: actorMissing="+(World.FindMobile((Serial)saved.RemovedActor)==null)
            +" current="+JsonSerializer.Serialize(effect)+" expected="+JsonSerializer.Serialize(saved.State)
            +" life="+SkillHealth.Balance256(p)+"/"+saved.Life+" mana="+NecromancerEffects.ManaBalance256(p)+"/"+saved.Mana
            +" corpse="+(corpse?.Deleted==false)+" reserved="+CorpseSkillRules.NoSelect(corpse)+" gold="+(corpse?.GetAmount(typeof(Gold))??-1));
        Through(p,effect.NextUtc,true);effect=DruidRecyclerRecovery.Get(effect.Id);
        Require(effect.Remaining==saved.State.Remaining-1&&!effect.Triggered&&SkillHealth.Balance256(p)==saved.Life,"Loaded effect replayed an elapsed action");
        var at=effect.NextUtc.AddMilliseconds((effect.Remaining-2)*40L);Through(p,at,true);effect=DruidRecyclerRecovery.Get(effect.Id);
        int expected=Math.Min(SkillHealth.Maximum256(p)/256,saved.Life/256+(SkillHealth.Maximum256(p)/256)*4/100)*256;
        Require(effect is{Remaining:1,Triggered:true}&&SkillHealth.Balance256(p)==expected,"Resumed delayed credit changed percentage or replayed");Through(p,effect.NextUtc,true);
        // Offline player loading parks the owner internally. Restore the fixture's
        // saved world location before creating the next independent corpse/body.
        p.MoveToWorld(saved.Point,Map.Maps[saved.Map]);
        var body=DruidRecyclerBody.CreatePrepared(p,CombatAbility.CarrionVine,new(p.X+1,p.Y,p.Z),TimedEffects.Now.AddDays(2));var next=Corpse(p);corpse.Delete();
        var commit=DruidRecyclerRecovery.Commit(body,next,TimedEffects.Now.AddDays(2),RecyclerRecoveryModel.LegacySource);uint removed=body.Serial.Value;body.Delete();effect=DruidRecyclerRecovery.Get(commit.Effect);
        Through(p,effect.NextUtc.AddMilliseconds(36*40L),true);effect=DruidRecyclerRecovery.Get(commit.Effect);
        File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{RemovedActor=removed,Corpse=next.Serial.Value,Effect=effect.Id,State=effect,Life=SkillHealth.Balance256(p),Mana=NecromancerEffects.ManaBalance256(p)}));
    }
    private static void Live(NetState network)
    {
        var p=RecyclerVineChecks.Fresh();network.Seeded=network.SentFirstPacket=true;network.CompressionEnabled=false;network.Mobile=p;p.NetState=network;
        var field=typeof(NetState).GetField("_protocolState",BindingFlags.Instance|BindingFlags.NonPublic);field.SetValue(network,Enum.Parse(field.FieldType,"GameServer_LoggedIn"));network.ResetMovementState();
        var at=TimedEffects.Now;var body=DruidRecyclerBody.CreatePrepared(p,CombatAbility.CarrionVine,new(p.X+1,p.Y,p.Z),at);var corpse=Corpse(p);
        SkillHealth.SetBalance256(p,SkillHealth.Maximum256(p)/2+17);int life=SkillHealth.Balance256(p),expected=Math.Min(SkillHealth.Maximum256(p)/256,life/256+(SkillHealth.Maximum256(p)/256)*4/100)*256;
        var commit=DruidRecyclerRecovery.Commit(body,corpse,at,RecyclerRecoveryModel.LegacySource);Require(commit.Allocated,"Live recovery allocation");body.Delete();long deadline=Core.TickCount+7000;
        void Inspect()
        {
            try
            {
                Require(network.Running&&Core.TickCount<deadline,"Automatic recovery conductor timeout/disconnect");var effect=DruidRecyclerRecovery.Get(commit.Effect);
                if(effect is{Triggered:false}){Require(SkillHealth.Balance256(p)==life,"Natural recovery credited before its callback age");Timer.StartTimer(TimeSpan.FromMilliseconds(10),Inspect);return;}
                Require(SkillHealth.Balance256(p)==expected&&CorpseSkillRules.NoSelect(corpse)&&corpse.GetAmount(typeof(Gold))==1,"Automatic delayed recovery value/loot");
                Log.Information("recycler-recovery-live-result: passed");corpse.Delete();End(p);
            }
            catch(Exception e){Log.Error(e,"recycler-recovery-live-result: failed");corpse.Delete();End(p);}
        }
        Log.Information("recycler-recovery-live: ready");Timer.StartTimer(TimeSpan.FromMilliseconds(10),Inspect);
    }
}
