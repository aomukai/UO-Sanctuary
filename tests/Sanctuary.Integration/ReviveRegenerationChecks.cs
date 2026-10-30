// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Server;
using Server.Accounting;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary.Integration;

public static class ReviveRegenerationChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(ReviveRegenerationChecks));
    private sealed record Saved(uint Owner,uint Actor,int Life256,int Rate);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"revive-regeneration-fixtures.json");
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    public static void Configure()
    {
        Register("revive-regeneration-check",Check);Register("revive-regeneration-reload-check",Reload);
        Register("revive-regeneration-live-check",()=>FixtureClock.NextLoop("revive-regeneration-live-result",()=>FixtureNetwork.Connected("revive-regeneration-live-result",Live)));
        var old=Mobile.HitsRegenRateHandler;
        Mobile.HitsRegenRateHandler=actor=>actor.Name is "ReviveRegenFast" or "ReviveRegenControl"?TimeSpan.FromMilliseconds(1):old?.Invoke(actor)??Mobile.DefaultHitsRate;
    }
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Source FP256 Revive recovery",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static BaseCreature Create(PlayerMobile owner,bool graveyard=false)
    {
        BaseCreature original=graveyard?new GraveyardSkeleton():new Skeleton();
        original.NoKillAwards=true;original.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);original.AIObject.AITimer.Stop();
        Corpse corpse=null;var died=ClassLootHooks.CreatureDied;
        ClassLootHooks.CreatureDied=(who,container)=>{died?.Invoke(who,container);if(who==original){corpse=container as Corpse;corpse?.DropItem(new Gold(1));}};
        try{owner.DoHarmful(original,true);original.Damage(original.Hits+1,owner);CombatDamage.Finish(original,owner);}
        finally{ClassLootHooks.CreatureDied=died;}
        owner.Mana=owner.ManaMax;
        Require(corpse!=null&&ReviveCasts.Release(owner,corpse)&&ReviveRegeneration.Handles(original),"Actual learned cast did not opt into source regeneration");
        Require(ReviveNativeActivation.State(original).Setup.RegenerationRate==2,"Skeleton source DamageRegen must be2");
        original.AIObject.AITimer.Stop();corpse.Delete();return original;
    }
    private static void Frames(BaseCreature actor)
    {
        int maximum=SkillHealth.Maximum256(actor),per=ReviveRegeneration.PerFrame(maximum,2);
        Require(per>0,"Fractional source recovery fixture too small");
        SkillHealth.SetBalance256(actor,maximum-256);int before=SkillHealth.Balance256(actor);
        ReviveRegeneration.Recover(actor,false,10000);
        ReviveRegeneration.Recover(actor,true,10039);Require(SkillHealth.Balance256(actor)==before,"Sub-frame elapsed time healed early");
        ReviveRegeneration.Recover(actor,true,10040);Require(SkillHealth.Balance256(actor)==before+per,"First complete frame did not heal source FP256");
        ReviveRegeneration.Recover(actor,true,10081);Require(SkillHealth.Balance256(actor)==before+2*per,"Fractional frame carry was lost");
        ReviveRegeneration.Recover(actor,false,90000);int parked=SkillHealth.Balance256(actor);
        ReviveRegeneration.Recover(actor,true,90039);Require(SkillHealth.Balance256(actor)==parked,"Offline time was banked as healing");
        ReviveRegeneration.Recover(actor,true,90040);Require(SkillHealth.Balance256(actor)==parked+per,"Recovery did not resume after inactive reset");
        var effects=TimedEffects.Snapshot();TimedEffects.PreventHealing(actor,TimedEffects.Now);
        int blocked=SkillHealth.Balance256(actor);ReviveRegeneration.Recover(actor,true,91000);
        Require(SkillHealth.Balance256(actor)==blocked,"Prevent Monster Heal was ignored");TimedEffects.Load(effects);
        ReviveRegeneration.Recover(actor,true,91039);Require(SkillHealth.Balance256(actor)==blocked,"Blocked time was banked");
        ReviveRegeneration.Recover(actor,true,91040);Require(SkillHealth.Balance256(actor)==blocked+per,"Prevention removal did not resume recovery");
        SkillHealth.SetBalance256(actor,maximum-1);ReviveRegeneration.Recover(actor,true,91080);
        Require(SkillHealth.Balance256(actor)==maximum,"Healing did not cap at exact source maximum");
        ReviveRegeneration.Recover(actor,true,92000);SkillHealth.SetBalance256(actor,maximum-256);
        ReviveRegeneration.Recover(actor,true,92039);Require(SkillHealth.Balance256(actor)==maximum-256,"Full-life idle time was banked");
        if(actor is GraveyardUndead body)
        {body.RecoverLife(100000);body.RecoverLife(101000);Require(SkillHealth.Balance256(actor)==maximum-256,"Graveyard recovery double-healed an opted-in Revive");}
        SkillHealth.SetBalance256(actor,maximum);
        Require(ClassGearHooks.CanRegenerateHits(actor)==false,"Native automatic HP recovery was not suppressed");
    }
    private static void Compatibility(BaseCreature actor)
    {
        var rows=ReviveNativeActivation.Snapshot();var row=ReviveNativeActivation.State(actor);
        foreach(int bad in new[]{-1,3,256})
        {
            bool refused=false;try{ReviveNativeActivation.Restore(rows.Select(x=>x.Actor==row.Actor?x with{Setup=x.Setup with{RegenerationRate=bad}}:x).ToArray());}
            catch(InvalidDataException){refused=true;}
            Require(refused&&ReviveNativeActivation.Snapshot().SequenceEqual(rows),"Malformed regeneration payload was not refused atomically");
        }
        var node=JsonSerializer.SerializeToNode(row).AsObject();node["Setup"].AsObject().Remove("RegenerationRate");
        var legacy=JsonSerializer.Deserialize<ReviveNativeState>(node.ToJsonString());
        ReviveNativeActivation.Restore(rows.Select(x=>x.Actor==row.Actor?legacy:x).ToArray());
        Require(!ReviveRegeneration.Handles(actor)&&ReviveNativeActivation.State(actor).Setup.RegenerationRate==null,"Old saved setup invented a regeneration rate");
        ReviveNativeActivation.Restore(rows);
    }
    private static void Check()
    {
        Require(ReviveRegeneration.PerFrame(8*256,2)==1&&ReviveRegeneration.PerFrame(33*256+255,2)==4
            &&ReviveRegeneration.PerFrame(65001*256,2)==8125&&ReviveRegeneration.PerFrame(int.MaxValue-255,15)==7864319
            &&ReviveRegeneration.PerFrame(1000*256,0)==0,"Source maximum-life truncation/rate boundaries");
        var owner=ReviveNativeActivationChecks.Owner();FixtureSpace.Corridor(owner,6480,28,2800);owner.RawInt=200;
        var actor=Create(owner);Frames(actor);Compatibility(actor);
        var graveyard=Create(owner,true);Frames(graveyard);graveyard.Delete();
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(owner.Serial.Value,actor.Serial.Value,SkillHealth.Balance256(actor),2)));
        owner.Internalize();Report();
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var actor=(BaseCreature)World.FindMobile((Serial)saved.Actor);
        Require(actor is{Alive:true,Deleted:false}&&actor.ControlMaster?.Serial.Value==saved.Owner&&actor.Map==Map.Internal
            &&ReviveNativeActivation.State(actor).Setup.RegenerationRate==saved.Rate&&SkillHealth.Balance256(actor)==saved.Life256,"Actual reload lost source recovery/HP identity");
        Compatibility(actor);Frames(actor);Report();
    }
    private static void Live(NetState network)
    {
        var owner=ReviveNativeActivationChecks.Owner();FixtureSpace.Corridor(owner,6500,24,2900);owner.RawInt=200;
        var account=new Account("revive-recovery-"+Guid.NewGuid().ToString("N"),Guid.NewGuid().ToString("N"));account[0]=owner;network.Account=account;ReviveNativeActivationChecks.Attach(network,owner);
        var actor=Create(owner,true);actor.Name="ReviveRegenFast";
        var control=new Skeleton{Name="ReviveRegenControl",NoKillAwards=true,Frozen=true,CantWalk=true};control.MoveToWorld(new(owner.X+3,owner.Y,owner.Z),owner.Map);control.AIObject.AITimer.Stop();
        control.SetHits(100);control.Hits=90;
        int maximum=SkillHealth.Maximum256(actor);SkillHealth.SetBalance256(actor,maximum-512);int start=SkillHealth.Balance256(actor);
        Require(ReviveRegeneration.PerFrame(maximum,2)>0,"Live recovery fixture has zero source regeneration after integer truncation");
        // Constructors can cache a native timer before the fixture name/rate
        // exists. Arm that actual timer explicitly, including on the Revive,
        // so a slow cached first delay cannot produce a false negative control.
        void ArmNative(Mobile body)
        {
            var field=typeof(Mobile).GetField("m_HitsTimer",BindingFlags.Instance|BindingFlags.NonPublic);
            var timer=field.GetValue(body) as Timer;
            if(timer==null)
            {
                var type=typeof(Mobile).GetNestedType("HitsTimer",BindingFlags.NonPublic);
                timer=(Timer)Activator.CreateInstance(type,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,null,[body],null);
                field.SetValue(body,timer);
            }
            timer.Stop();timer.Delay=timer.Interval=TimeSpan.FromMilliseconds(1);timer.Start();
        }
        ArmNative(actor);ArmNative(control);
        Require(actor.Map==owner.Map&&!actor.Blessed&&ClassGearHooks.CanRegenerateHits(actor)==false,"Connected learned original did not activate");
        Require(control.CanRegenHits&&Mobile.GetHitsRegenRate(control)==TimeSpan.FromMilliseconds(1),"Ordinary native timer control is not eligible");
        void Cleanup()
        {
            // Native Dispose is explicitly available for testing cleanup.
            // Remove the socket immediately, rather than exposing an old
            // Running/Mobile-null connection to the next fixture selector.
#pragma warning disable CS0618
            network.Dispose();
#pragma warning restore CS0618
            actor.Delete();control.Delete();owner.Delete();
        }
        Log.Information("revive-regeneration-live: ready");
        Timer.StartTimer(TimeSpan.FromMilliseconds(240),()=>Core.LoopContext.Post(()=>
        {
            try
            {
                int gained=SkillHealth.Balance256(actor)-start;
                var table=typeof(ReviveRegeneration).GetField("Clocks",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
                object[] clockArgs=[actor,null];table.GetType().GetMethod("TryGetValue").Invoke(table,clockArgs);
                var clock=clockArgs[1];long last=clock==null?-1:(long)clock.GetType().GetField("Last",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(clock);
                bool active=(bool)typeof(ReviveNativeActivation).GetMethod("CanAct",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,[actor]);
                Require(network.Running&&gained>0&&gained<256&&control.Hits>90,
                    $"Actual timer recovery:sourceFP={gained},maximum={SkillHealth.Maximum256(actor)},perFrame={ReviveRegeneration.PerFrame(SkillHealth.Maximum256(actor),2)},ordinary={control.Hits},network={network.Running},map={actor.Map}/{owner.Map},blessed={actor.Blessed},alive={actor.Alive},handled={ReviveRegeneration.Handles(actor)},active={active},canHeal={TimedEffects.CanRegenerateHits(actor)},full={SkillHealth.Full(actor)},last={last},now={Core.TickCount}");
                PersistentMinions.Suspend(owner);int parked=SkillHealth.Balance256(actor);
                Timer.StartTimer(TimeSpan.FromMilliseconds(160),()=>Core.LoopContext.Post(()=>
                {
                    try{Require(actor.Map==Map.Internal&&SkillHealth.Balance256(actor)==parked,"Parked original kept source healing");Log.Information("revive-regeneration-live-result: passed");}
                    catch(Exception e){Log.Error(e,"revive-regeneration-live-result: failed");}
                    finally{Cleanup();}
                }));
            }
            catch(Exception e){Log.Error(e,"revive-regeneration-live-result: failed");Cleanup();}
        }));
    }
    private static void Report()=>Log.Information("Revive regeneration:source FP256 frame carry,prevention/inactive/full resets,exact cap,Graveyard/native exclusion,atomic invalid rates and omitted-field legacy setup pass;actual optional-rate/HP persistence checked separately from live timer recovery");
}
