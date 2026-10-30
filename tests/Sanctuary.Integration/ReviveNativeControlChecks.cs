// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Server;
using Server.Accounting;
using Server.Collections;
using Server.ContextMenus;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary.Integration;

public static class ReviveNativeControlChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(ReviveNativeControlChecks));
    private sealed record Saved(uint Owner,uint Actor,ReviveNativeState State,int Ai,double Taming,int Loyalty);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"revive-native-control-fixtures.json");
    private static readonly int[] Commands=[3006111,3006108,3006107,3006112,3006114,3006109,3006098,3006110,3006099,3006113,3006118];
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Native class-minion commandability, loyalty and automatic AI",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    public static void Configure()
    {
        Register("revive-native-control-check",Check);Register("revive-native-control-reload-check",Reload);
        Register("revive-native-control-live-check",()=>FixtureClock.NextLoop("revive-native-control-live-result",()=>FixtureNetwork.Connected("revive-native-control-live-result",Live)));
    }
    private static int[] Menus(BaseCreature actor,PlayerMobile owner)
    {
        var entries=PooledRefList<ContextMenuEntry>.Create();
        try{actor.GetContextMenuEntries(owner,ref entries);return entries.ToArray().Select(e=>e.Number).Where(n=>Commands.Contains(n)).Order().ToArray();}
        finally{entries.Dispose();}
    }
    private static void Check()
    {
        var owner=ReviveNativeActivationChecks.Owner();var corpse=ReviveNativeActivationChecks.Body(owner,true,out var actor);
        Require(actor.Commandable&&!ClassCombatHooks.PersistentMinion(actor),"Inactive original lost ordinary native default behavior");
        var ai=actor.AI;double taming=actor.MinTameSkill;ReviveNativeActivationChecks.Activate(owner,corpse,actor,321);actor.Loyalty=1;
        Require(!actor.Commandable&&actor.AI==ai&&actor.MinTameSkill==taming&&actor.Map==Map.Internal,"Original class-minion identity did not suppress native commands without replacing AI/taming data");
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(owner.Serial.Value,actor.Serial.Value,ReviveNativeActivation.State(actor),(int)ai,taming,actor.Loyalty)));owner.Internalize();
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var owner=World.FindMobile((Serial)saved.Owner) as PlayerMobile;var actor=World.FindMobile((Serial)saved.Actor) as BaseCreature;
        Require(owner is{Alive:true,Deleted:false}&&actor is{Alive:true,Deleted:false,Commandable:false}&&actor.Map==Map.Internal&&actor.ControlMaster==owner
            &&ReviveNativeActivation.State(actor)==saved.State&&(int)actor.AI==saved.Ai&&actor.MinTameSkill==saved.Taming&&actor.Loyalty==saved.Loyalty,"Reload lost native class-minion commandability/original state");
    }
    private static void Hourly(LoyaltyTimer timer)
    {
        // Force this private native timer object's hourly boundary, without
        // advancing global time or timers. The world here is isolated from
        // the retained class fixtures and their ordinary prepared NPCs.
        typeof(LoyaltyTimer).GetField("m_NextHourlyCheck",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(timer,DateTime.MinValue);
        typeof(LoyaltyTimer).GetMethod("OnTick",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(timer,null);
    }
    private static void Live(NetState network)
    {
        var owner=ReviveNativeActivationChecks.Owner();FixtureSpace.Corridor(owner,6450,40,2150);
        var account=new Account("revive-control-"+Guid.NewGuid().ToString("N"),Guid.NewGuid().ToString("N"));account[0]=owner;network.Account=account;ReviveNativeActivationChecks.Attach(network,owner);
        Require(owner.AccessLevel==AccessLevel.Player,"Control fixture must not use GM obedience bypass");
        var corpse=ReviveNativeActivationChecks.Body(owner,true,out var actor);ReviveNativeActivationChecks.Activate(owner,corpse,actor,321);actor.AIObject.AITimer.Stop();var originalAi=actor.AI;
        var ordinary=new Dragon{NoKillAwards=true};ordinary.MoveToWorld(new(owner.X-1,owner.Y,owner.Z),owner.Map);ordinary.AIObject.AITimer.Stop();
        Require(ordinary.SetControlMaster(owner)&&ordinary.Commandable&&!ClassCombatHooks.PersistentMinion(ordinary),"Ordinary negative-control pet lost native ownership/commandability");
        Require(actor.GetType()==ordinary.GetType()&&actor.AI==ordinary.AI&&ordinary.MinTameSkill>29.1&&ordinary.GetControlChance(owner)<1,"Native original/control pet do not share the ordinary high-taming path");
        Require(Menus(actor,owner).Length==0&&Menus(ordinary,owner).Contains(3006118)&&Menus(ordinary,owner).Contains(3006113),"Class minion exposes native pet release/transfer or ordinary pet lost menus");
        actor.Loyalty=ordinary.Loyalty=1;var state=ReviveNativeActivation.State(actor);var clock=new LoyaltyTimer();
        Hourly(clock);
        Require(!ordinary.Deleted&&!ordinary.Controlled&&ordinary.ControlMaster==null&&ordinary.Loyalty==BaseCreature.MaxLoyalty,"Native hourly low-loyalty release stopped working for an ordinary pet");ordinary.Delete();
        for(int pass=1;pass<=12;pass++)
        {
            if(pass>1){Hourly(clock);}
            Require(actor is{Alive:true,Deleted:false,Controlled:true,Commandable:false}&&actor.ControlMaster==owner&&actor.Loyalty==1
                &&actor.ControlOrder==OrderType.Guard&&ReviveNativeActivation.State(actor)==state,"Native loyalty released/mutated the class minion");
        }
        Require(!actor.AIObject.HandlesOnSpeech(owner),"Class original accepted ordinary owner pet speech");
        actor.AIObject.OnSpeech(new SpeechEventArgs(owner,"all stop",MessageType.Regular,0,[0x167]));
        Require(actor.ControlOrder==OrderType.Guard&&actor.Loyalty==1,"Native pet speech stopped the class minion or entered taming/loyalty handling");
        actor.Stam=actor.StamMax;var origin=actor.Location;owner.MoveToWorld(new(owner.X+12,owner.Y,owner.Z),owner.Map);actor.AIObject.AITimer.Start();
        long deadline=Core.TickCount+12000;Log.Information("revive-native-control-live: ready");
        void Cleanup()
        {actor.Delete();ordinary.Delete();corpse.Delete();network.Mobile=null;owner.NetState=null;owner.Delete();}
        void Inspect()
        {
            try
            {
                Require(network.Running&&actor is{Alive:true,Deleted:false,Commandable:false}&&actor.ControlMaster==owner&&actor.Map==owner.Map
                    &&Core.TickCount<deadline,"Native non-commandable guard AI did not follow owner: "+actor.Location+"/"+owner.Location+" order="+actor.ControlOrder+" action="+actor.AIObject.Action);
                if(actor.Location!=origin&&actor.GetDistanceToSqrt(owner)<=3)
                {
                    Require(actor.GetType()==typeof(Dragon)&&actor.AI==originalAi&&actor.Loyalty==1&&PersistentMinions.Profiles.Find(actor).ExpiresUtc==null,"Following changed original AI,loyalty or lifetime");
                    Log.Information("Revive native control:12forced native hourly boundaries preserve class original at loyalty1;ordinary high-taming pet keeps menus/chance and releases at low loyalty;class pet menu/speech suppressed;natural original guard AI follows owner without a replacement brain;no global time advance");
                    Log.Information("revive-native-control-live-result: passed");Cleanup();return;
                }
                Timer.StartTimer(TimeSpan.FromMilliseconds(100),()=>Core.LoopContext.Post(Inspect));
            }
            catch(Exception e){Log.Error(e,"revive-native-control-live-result: failed");Cleanup();}
        }
        Inspect();
    }
}
