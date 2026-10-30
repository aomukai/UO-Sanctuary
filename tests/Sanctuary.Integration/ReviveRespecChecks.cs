// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
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

public static class ReviveRespecChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(ReviveRespecChecks));
    private sealed record Saved(uint Owner,uint Actor,uint Corpse,uint Loot,int AvailablePoints,int Gold);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"revive-respec-fixtures.json");
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    public static void Configure()
    {
        Register("revive-respec-check",Check);Register("revive-respec-reload-check",Reload);
        Register("revive-respec-live-check",()=>FixtureClock.NextLoop("revive-respec-live-result",
            ()=>FixtureNetwork.Connected("revive-respec-live-result",Live)));
    }
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Actual learned Revive through public class reset",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static (BaseCreature Actor,Corpse Corpse,Gold Loot) Revive(PlayerMobile owner)
    {
        var actor=new Skeleton{NoKillAwards=true};actor.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);actor.AIObject.AITimer.Stop();
        Corpse corpse=null;var died=ClassLootHooks.CreatureDied;
        ClassLootHooks.CreatureDied=(who,container)=>{died?.Invoke(who,container);if(who==actor){corpse=container as Corpse;}};
        try{owner.DoHarmful(actor,true);actor.Damage(actor.Hits+1,owner);CombatDamage.Finish(actor,owner);}
        finally{ClassLootHooks.CreatureDied=died;}
        Require(corpse is{Deleted:false}&&ReviveOriginals.Find(corpse)==actor,"Ordinary ownerless death did not retain its original");
        var loot=new Gold(1);corpse.DropItem(loot);corpse.BeginDecay(TimeSpan.FromHours(1));owner.Mana=owner.ManaMax;
        Require(ReviveCasts.Plan(owner,corpse,out _,out _,out var issue),"Learned original preflight: "+issue);
        Require(ReviveCasts.Release(owner,corpse)&&actor.Alive&&actor.ControlMaster==owner,"Learned original-actor release failed");
        actor.AIObject.AITimer.Stop();return(actor,corpse,loot);
    }
    private static Saved Reset(PlayerMobile owner,BaseCreature actor,Corpse corpse,Gold loot)
    {
        var profiles=ClassCreation.Profiles;
        ClassCreation.Profiles.AdvanceLevel(owner,80);owner.Skills[SkillName.Cartography].Base=100;
        string site=RecognitionChecks.Place(owner);var wardrobe=TownRecognition.PublicWardrobe(site);
        Require(TownRecognition.Claim(owner,site,RecognitionHonor.Hero)&&TownRecognition.Claim(owner,site,RecognitionHonor.Pillar),"Required public ceremonies failed");
        owner.Backpack.DropItem(new Gold(6000));
        Require(profiles.TryGet(owner.Serial,out var quote),"Missing active Necromancer profile");
        var stats=profiles.Stats(owner.Serial);int points=profiles.AvailablePoints(owner.Serial);
        var action=ClassCombat.For(owner);action.PendingAbility=CombatAbility.Revive;
        Require(!ClassRespec.Commit(owner,wardrobe,quote,stats,points)&&!actor.Deleted&&owner.Backpack.GetAmount(typeof(Gold))==6000,
            "Pending cast reset charged gold or dismissed its original");
        ClassCombat.Stop(owner);
        Require(ClassRespec.Ready(owner,wardrobe),"Actual learned caster did not meet public reset requirements");
        owner.Hits=20;owner.Mana=10;owner.Stam=20;
        int life=owner.Hits,mana=owner.Mana,stamina=owner.Stam;
        Require(ClassRespec.Commit(owner,wardrobe,quote,stats,points),"Public reset refused actual learned Revive owner");
        Require(actor.Deleted&&!(owner.AllFollowers??[]).OfType<BaseCreature>().Any(x=>!x.Deleted&&x.ControlMaster==owner)
            &&profiles.GetRank(owner.Serial,"necromancer.revive")==0&&profiles.AvailablePoints(owner.Serial)==79,
            "Reset retained original ownership or failed to refund Revive ranks");
        Require(corpse is{Deleted:false}&&loot.Parent==corpse&&loot.Amount==1&&owner.Backpack.GetAmount(typeof(Gold))==1000
            &&owner.Hits==life&&owner.Mana==mana&&owner.Stam==stamina,"Reset lost separate loot, refilled resources or charged incorrectly");
        Require(!ClassRespec.Commit(owner,wardrobe,quote,stats,points)&&owner.Backpack.GetAmount(typeof(Gold))==1000,
            "Stale reset quote charged twice");
        return new(owner.Serial.Value,actor.Serial.Value,corpse.Serial.Value,loot.Serial.Value,79,1000);
    }
    private static void Check()
    {
        var owner=ReviveNativeActivationChecks.Owner();FixtureSpace.Corridor(owner,6520,24,3000);owner.RawInt=200;
        var original=Revive(owner);Require(original.Actor.Map==Map.Internal,"Disconnected original was not parked");
        AfterCombat(()=>
        {
            try
            {
                File.WriteAllText(Manifest,JsonSerializer.Serialize(Reset(owner,original.Actor,original.Corpse,original.Loot)));
                owner.Internalize();Report();Log.Information("revive-respec-result: passed");
            }
            catch(Exception e){Log.Error(e,"revive-respec-result: failed");}
        });
    }
    private static void AfterCombat(Action action)
    {
        // Honor the real ten-second wardrobe combat exclusion rather than
        // clearing aggression records or weakening the public entry point.
        var until=Core.Now+TimeSpan.FromMilliseconds(10200);
        void Wait(){if(Core.Now<until){Timer.StartTimer(TimeSpan.FromMilliseconds(100),()=>Core.LoopContext.Post(Wait));}else{action();}}
        Timer.StartTimer(TimeSpan.FromMilliseconds(10200),()=>Core.LoopContext.Post(Wait));
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));
        var owner=(PlayerMobile)World.FindMobile((Serial)saved.Owner);var corpse=(Corpse)World.FindItem((Serial)saved.Corpse);
        var parked=(Dictionary<Serial,ClassMinionState>)typeof(MinionWardrobeProfiles).GetField("_pets",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(PersistentMinions.Profiles);
        Require(owner is{Deleted:false}&&World.FindMobile((Serial)saved.Actor)==null
            &&!ReviveNativeActivation.Snapshot().Any(x=>x.Actor==saved.Actor)&&!parked.ContainsKey((Serial)saved.Actor),
            "Actual reload resurrected reset actor or retained its activation/wardrobe state");
        Require(ClassCreation.Profiles.GetRank(owner.Serial,"necromancer.revive")==0&&ClassCreation.Profiles.AvailablePoints(owner.Serial)==saved.AvailablePoints
            &&owner.Backpack.GetAmount(typeof(Gold))==saved.Gold&&corpse is{Deleted:false}
            &&World.FindItem((Serial)saved.Loot) is Gold{Amount:1} loot&&loot.Parent==corpse,"Actual reload lost refund/payment or separate corpse loot");
        Report();
    }
    private static void Live(NetState network)
    {
        var owner=ReviveNativeActivationChecks.Owner();FixtureSpace.Corridor(owner,6540,24,3000);owner.RawInt=200;
        var account=new Account("revive-respec-"+Guid.NewGuid().ToString("N"),Guid.NewGuid().ToString("N"));account[0]=owner;network.Account=account;ReviveNativeActivationChecks.Attach(network,owner);
        BaseCreature actor=null;Corpse corpse=null;
        try
        {
            var original=Revive(owner);actor=original.Actor;corpse=original.Corpse;
            Require(network.Running&&actor.Map==owner.Map&&actor.Map!=Map.Internal&&!actor.Blessed,"Connected original did not activate in the world");
            AfterCombat(()=>
            {
                try
                {
                    Require(network.Running,"Actual respec connection closed during combat cooldown");
                    Reset(owner,actor,corpse,original.Loot);Log.Information("revive-respec-live-result: passed");
                }
                catch(Exception e){Log.Error(e,"revive-respec-live-result: failed");}
                finally{Cleanup();}
            });
        }
        catch(Exception e){Log.Error(e,"revive-respec-live-result: failed");Cleanup();}
        void Cleanup()
        {
#pragma warning disable CS0618
            network.Dispose();
#pragma warning restore CS0618
            actor?.Delete();corpse?.Delete();owner.Delete();
        }
    }
    private static void Report()=>Log.Information("Revive public reset:actual learned original,parked/connected lifecycle,pending-cast refusal,once-only payment/rank refund,unchanged pools,separate loot and no actor replay across actual reloads");
}
