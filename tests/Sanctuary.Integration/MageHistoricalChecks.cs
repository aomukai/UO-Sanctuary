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
public static class MageHistoricalChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(MageHistoricalChecks));
    private sealed record Saved(uint Owner,uint Mage,uint Deferred,uint Target,int Life,int DeferredLife,int TargetLife,int Maximum,int Rank,int Mastery,long Order,MageCriticalSeed Seed,MageCriticalSeed DeferredSeed,SkeletonWorldState State,SkeletonWorldState DeferredState,Point3D Origin,int Map,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"mage-historical-fixtures.json");
    public static void Configure()
    {
        Register("mage-historical-check",Check);Register("mage-historical-reload-check",Reload);
        ConsoleInputHandler.RegisterCommand("mage-historical-live","Automatic historical Mage adoption/ranged casting",_=>Core.LoopContext.Post(()=>FixtureNetwork.Connected("mage-historical-live-result",Live)));
    }
    private static void Register(string name,Action run)=>ConsoleInputHandler.RegisterCommand(name,"Original Mage identity with shared saved ranged controller",_=>Core.LoopContext.Post(()=>
    {try{run();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool yes,string why){if(!yes){throw new InvalidOperationException(why);}}
    private static PlayerMobile Owner()=>MagePetBuffChecks.Owner(20,6300,3000);
    private static NecromancerMinion Body(PlayerMobile p,int element,int dx=2,bool seed=true)
    {
        var mage=new NecromancerMinion();var old=NecromancerSummons.ElementRoll;
        try{NecromancerSummons.ElementRoll=()=>element;mage.Prepare(p,CombatAbility.RaiseSkeletalMage);}finally{NecromancerSummons.ElementRoll=old;}
        Require(mage.SetControlMaster(p),"Original Mage owner");mage.MoveToWorld(new(p.X+dx,p.Y,p.Z),p.Map);mage.AIObject.AITimer.Stop();PersistentMinions.Profiles.Track(mage);BattleCommand.Track(mage);if(seed){NecromancerMageMissiles.SetActorSeed(mage,12345);}
        SkillHealth.SetBalance256(mage,SkillHealth.Maximum256(mage)/2+17);TimedEffects.PreventHealing(mage,TimedEffects.Now);return mage;
    }
    private static GraveyardZombie Target(PlayerMobile p,Mobile actor,int dx=4)
    {var target=new GraveyardZombie{Name="PoisonDaggerFixtureHistoricalMage",NoKillAwards=true,Frozen=true,CantWalk=true};target.MoveToWorld(new(actor.X+dx,actor.Y,actor.Z),p.Map);target.AIObject.AITimer.Stop();target.SetHits(20000);target.Hits=target.HitsMax;foreach(var r in Enum.GetValues<ResistanceType>()){target.SetResistance(r,0);}TimedEffects.PreventHealing(target,TimedEffects.Now);return target;}
    private static NecromancerGroundWorld Ground(NecromancerMinion mage)=>MageWorld.For(mage).World;
    private static void Through(NecromancerMinion mage,int frame,bool actions=true)
    {var c=Ground(mage).State.Controller;IronGolemWorld.Advance(mage,c.NextUtc.AddMilliseconds((frame-c.Frame-1)*40L),actions);Require(Ground(mage).State.Controller.Fault==null,"Historical Mage world fault");}
    private static void Clear(NecromancerMinion mage)
    {FireGround.Cancel(mage);ChainLightning.Cancel(mage);var all=NecromancerMageMissiles.Snapshot();NecromancerMageMissiles.Restore(all with{Bolts=all.Bolts.Where(x=>x.Actor!=mage.Serial.Value).ToArray()});}
    private static void Hold(NecromancerMinion mage)=>Ground(mage).Store(IronGolemWorld.Park(Ground(mage).State.Controller) with{NextUtc=TimedEffects.Now.AddHours(1)});
    private static void Direct()
    {
        foreach(int element in new[]{1,2,3})
        {
            var p=Owner();var mage=Body(p,element);var target=Target(p,mage);var serial=mage.Serial;int life=SkillHealth.Balance256(mage),maximum=SkillHealth.Maximum256(mage);long order=BattleCommand.PetOrder(mage);var seed=NecromancerMageMissiles.ActorSeed(mage);
            try
            {
                Require(MageWorld.TryAdopt(mage,TimedEffects.Now)&&mage.GetType()==typeof(NecromancerMinion)&&mage.Serial==serial&&mage.ControlMaster==p&&SkillHealth.Balance256(mage)==life&&SkillHealth.Maximum256(mage)==maximum&&BattleCommand.PetOrder(mage)==order&&NecromancerMageMissiles.ActorSeed(mage)==seed,$"Adoption identity/life/order/seed actor={mage.Serial}/{serial},life={SkillHealth.Balance256(mage)}/{life},max={SkillHealth.Maximum256(mage)}/{maximum},order={BattleCommand.PetOrder(mage)}/{order},seed={NecromancerMageMissiles.ActorSeed(mage)}/{seed}");
                Require(Ground(mage).State.Controller is{Mode:1,Frame:0,NextThink:1,ActionAt:0,EndAt:0,Ai.Parameter0:1}&&!mage.UsesNativeSummonTimer&&MageWorld.TryAdopt(mage,TimedEffects.Now),"Neutral one-time adoption or native timer suppression");
                Require(mage.BeginMelee(target)==TimeSpan.FromMilliseconds(800)&&typeof(SkeletonGuardian).GetField("_target",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(mage)==null,"Original native melee timer still armed");
                Ground(mage).Store(Ground(mage).State.Controller with{NextUtc=TimedEffects.Now.AddHours(1)});NecromancerMageMissiles.SetActorSeed(mage,MageStaticFieldChecks.Seed(0,4));Through(mage,1);
                Require(Ground(mage).State.Controller is{Mode:4,ActionAt:11,EndAt:21},"Historical source kind2 skill windup");Through(mage,11);Require(SkillHealth.Balance256(mage)==life,"Shared frame controller omitted PMH/doubled recovery");
                if(element==1){Require(NecromancerMageMissiles.Snapshot().Bolts.Any(x=>x.Actor==mage.Serial.Value&&x.Packet.Spell==CombatAbility.GlacialSpike)&&SorceressEffects.Armor(mage)!=null,"Historical Ice Glacial/armor");}
                if(element==2){Require(FireGround.Snapshot().Meteors.Any(x=>x.NpcCaster==mage.Serial.Value&&x.Rank==10)&&SorceressEffects.Enchantment(p) is{Rank:20,Caster:var who}&&who==mage.Serial.Value,"Historical Fire Meteor/Enchant");}
                if(element==3)
                {
                    Require(ChainLightning.Snapshot().Any(x=>x.NpcCaster==mage.Serial.Value&&x.Rank==10),"Historical Chain");Clear(mage);Hold(mage);int frame=Ground(mage).State.Controller.Frame;NecromancerMageMissiles.SetActorSeed(mage,MageStaticFieldChecks.Seed(5,9));Through(mage,frame+1);Through(mage,frame+11);Require(SkillHealth.Balance256(target)<20000*256,"Historical Static");
                }
                var before=JsonSerializer.Serialize(NecromancerMageMissiles.Snapshot());var all=NecromancerMageMissiles.Snapshot();bool invalid=false;
                try{NecromancerMageMissiles.Restore(all with{LegacyWorlds=all.LegacyWorlds.Select(x=>x.Actor==mage.Serial.Value?x with{Owner=p.Serial.Value+100000}:x).ToArray()});}catch(InvalidDataException){invalid=true;}
                Require(invalid&&JsonSerializer.Serialize(NecromancerMageMissiles.Snapshot())==before,"Invalid legacy ground entry cleared shared flights/seeds/worlds");
            }
            finally{Clear(mage);target.Delete();mage.Delete();p.Delete();}
        }
        var owner=Owner();var missing=Body(owner,1,seed:false);var oldRoll=NecromancerMageMissiles.SeedLow;int calls=0;
        try
        {
            NecromancerMageMissiles.SeedLow=()=>{calls++;return 91;};Require(MageWorld.TryAdopt(missing,TimedEffects.Now)&&MageWorld.TryAdopt(missing,TimedEffects.Now)&&calls==1&&NecromancerMageMissiles.ActorSeed(missing).Seed==SourceRandom.InitLow(91),"Missing legacy seed was not initialized exactly once");
            var old=JsonSerializer.SerializeToNode(NecromancerMageMissiles.Snapshot());old.AsObject().Remove("LegacyWorlds");Require(old.Deserialize<MageMissileSave>().LegacyWorlds.Length==0,"Old Mage persistence JSON default");
            var golem=new NecromancerMinion();try{golem.Prepare(owner,CombatAbility.ClayGolem);Require(!MageWorld.TryAdopt(golem,TimedEffects.Now)&&MageWorld.For(golem)==null,"Golem adopted as ranged Mage");}finally{golem.Delete();}
        }
        finally{NecromancerMageMissiles.SeedLow=oldRoll;Clear(missing);missing.Delete();owner.Delete();}
        Log.Information("Historical Mage:three original types share ranged clock/actions/all fixed secondaries/Enchant/armor,neutral adoption/no HP or seed reset,no native melee timer,one-time missing seed,atomic restore/old JSON and golem refusal pass");
    }
    private static void Check()
    {
        Direct();var p=Owner();var mage=Body(p,3);var deferred=Body(p,1,3);var target=Target(p,mage,5);Require(MageWorld.TryAdopt(mage,TimedEffects.Now),"Saved historical adoption");NecromancerMageMissiles.SetActorSeed(mage,MageStaticFieldChecks.Seed(20,99));Through(mage,1);
        Ground(mage).Store(Ground(mage).State.Controller with{NextUtc=TimedEffects.Now.AddHours(1)});SkillHealth.SetBalance256(mage,SkillHealth.Maximum256(mage)-257);SkillHealth.SetBalance256(deferred,SkillHealth.Maximum256(deferred)-1);deferred.Internalize();
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,mage.Serial.Value,deferred.Serial.Value,target.Serial.Value,SkillHealth.Balance256(mage),SkillHealth.Balance256(deferred),SkillHealth.Balance256(target),SkillHealth.Maximum256(mage),mage.Rank,mage.Mastery,BattleCommand.PetOrder(mage),NecromancerMageMissiles.ActorSeed(mage),NecromancerMageMissiles.ActorSeed(deferred),Ground(mage).State,null,mage.Location,p.Map.MapID,0)));
    }
    private static void Reload()
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);var mage=(NecromancerMinion)World.FindMobile((Serial)s.Mage);var deferred=(NecromancerMinion)World.FindMobile((Serial)s.Deferred);var target=World.FindMobile((Serial)s.Target);var expected=s.Generation==0?s.State with{Controller=IronGolemWorld.Park(s.State.Controller)}:s.State;
        Require(mage.GetType()==typeof(NecromancerMinion)&&deferred.GetType()==typeof(NecromancerMinion)&&mage.ControlMaster==p&&deferred.ControlMaster==p&&SkillHealth.Balance256(mage)==s.Life&&SkillHealth.Balance256(deferred)==s.DeferredLife&&SkillHealth.Maximum256(mage)==s.Maximum&&mage.Rank==s.Rank&&mage.Mastery==s.Mastery&&BattleCommand.PetOrder(mage)==s.Order&&NecromancerMageMissiles.ActorSeed(mage)==s.Seed&&NecromancerMageMissiles.ActorSeed(deferred)==s.DeferredSeed&&SkillHealth.Balance256(target)==s.TargetLife,"Historical saved identity/resources/order/seed changed");
        Require(JsonSerializer.Serialize(Ground(mage).State)==JsonSerializer.Serialize(expected)&&!NecromancerMageMissiles.Snapshot().Bolts.Any(x=>x.Actor==mage.Serial.Value),"Saved historical windup phase/no-replay cancellation");
        Require(s.DeferredState==null?NecromancerMageMissiles.LegacyWorld(deferred)=="{}":JsonSerializer.Serialize(Ground(deferred).State)==JsonSerializer.Serialize(s.DeferredState),"Deferred/previously adopted historical state changed");
        p.MoveToWorld(s.Origin,Map.Maps[s.Map]);mage.MoveToWorld(s.Origin,p.Map);Ground(mage).Loaded=false;Ground(mage).Store(expected.Controller with{NextUtc=TimedEffects.Now});NecromancerMageMissiles.SetActorSeed(mage,MageStaticFieldChecks.Seed(20,99));int frame=Ground(mage).State.Controller.Frame;Through(mage,frame+11);var bolt=NecromancerMageMissiles.Snapshot().Bolts.Single(x=>x.Actor==mage.Serial.Value);
        Require(bolt.Packet.Element=="ltng"&&SkillHealth.Balance256(target)==s.TargetLife,"Resumed historical cast source/early damage");for(int i=0;i<30&&NecromancerMageMissiles.Snapshot().Bolts.Any(x=>x.Id==bolt.Id);i++){var f=NecromancerMageMissiles.Snapshot().Bolts.Single(x=>x.Id==bolt.Id);NecromancerMageMissiles.Tick(f.NextUtc,true,f.Id);}Require(SkillHealth.Balance256(target)<s.TargetLife,"Resumed historical real collision");Clear(mage);
        TimedEffects.Clear(target);TimedEffects.PreventHealing(target,TimedEffects.Now);SkillHealth.SetBalance256(target,s.TargetLife);Ground(mage).Store(expected.Controller);NecromancerMageMissiles.SetActorSeed(mage,s.Seed.Seed);PersistentMinions.Resume(p);Require(Ground(deferred).State!=null&&deferred.ControlMaster==p&&SkillHealth.Balance256(deferred)==s.DeferredLife&&NecromancerMageMissiles.ActorSeed(deferred)==s.DeferredSeed,"Resume did not adopt original deferred Mage without reset");Hold(deferred);mage.Internalize();deferred.Internalize();
        File.WriteAllText(Manifest,JsonSerializer.Serialize(s with{State=expected,DeferredState=Ground(deferred).State,Generation=s.Generation+1}));
    }
    private static void Live(NetState network)
    {
        var p=MagePetBuffChecks.Owner(20,6300,3400);FixtureNetwork.Authenticate(network,p,"historical");var mage=Body(p,2);var target=Target(p,mage);NecromancerMageMissiles.SetActorSeed(mage,MageStaticFieldChecks.Seed(0,4));var serial=mage.Serial;int life=SkillHealth.Balance256(mage),mana=NecromancerEffects.ManaBalance256(p);bool adopted=false,launched=false;long deadline=Core.TickCount+10000;
        void End(){Clear(mage);target.Delete();mage.Delete();p.Delete();}
        void Wait()
        {
            try
            {
                Require(Core.TickCount<deadline,"Automatic historical adoption/cast timeout");
                if(Ground(mage).State is{} world)
                {
                    Require(world.Controller.Fault==null&&mage.GetType()==typeof(NecromancerMinion)&&mage.Serial==serial&&SkillHealth.Balance256(mage)==life,"Automatic adoption changed identity/life/fault");adopted=true;
                    if(!launched&&FireGround.Snapshot().Meteors.Any(x=>x.NpcCaster==mage.Serial.Value)){Require(world.Controller.Frame>=11&&SorceressEffects.Enchantment(p)?.Caster==mage.Serial.Value,"Natural historical source action/Enchant");launched=true;mage.SuspendActions();Hold(mage);}
                }
                if(SkillHealth.Balance256(target)==20000*256){Timer.StartTimer(TimeSpan.FromMilliseconds(10),Wait);return;}
                Require(adopted&&launched&&network.Running&&NecromancerEffects.ManaBalance256(p)==mana&&FireGround.Snapshot().Patches.Any(x=>x.NpcCaster==mage.Serial.Value),"Historical actual Meteor damage/field/allegiance/mana");End();Log.Information("mage-historical-live-result: passed");
            }
            catch(Exception e){End();Log.Error(e,"mage-historical-live-result: failed");}
        }
        Log.Information("mage-historical-live: ready");Timer.StartTimer(TimeSpan.FromMilliseconds(10),Wait);
    }
}
