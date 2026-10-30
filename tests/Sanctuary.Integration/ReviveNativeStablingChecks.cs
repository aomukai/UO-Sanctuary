// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Linq;
using Server;
using Server.Accounting;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary.Integration;

public static class ReviveNativeStablingChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(ReviveNativeStablingChecks));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    public static void Configure()=>ConsoleInputHandler.RegisterCommand("revive-native-stabling-live-check","Class-minion stabling rejection and ordinary pet transaction",_=>Core.LoopContext.Post(()=>
    {
        try
        {
            FixtureClock.NextLoop("revive-native-stabling-live-result",()=>FixtureNetwork.Connected("revive-native-stabling-live-result",Live));
            Log.Information("revive-native-stabling-live-check: passed");
        }
        catch(Exception e){Log.Error(e,"revive-native-stabling-live-check: failed");}
    }));
    private static void Target(AnimalTrainer trainer,PlayerMobile owner,BaseCreature actor)
    {
        trainer.BeginStable(owner);Require(owner.Target!=null,"Native trainer did not create its stable target");owner.Target.Invoke(owner,actor);
    }
    private static void Live(NetState network)
    {
        var owner=ReviveNativeActivationChecks.Owner();FixtureSpace.Corridor(owner,6500,20,2050);
        var account=new Account("revive-stable-"+Guid.NewGuid().ToString("N"),Guid.NewGuid().ToString("N"));account[0]=owner;network.Account=account;ReviveNativeActivationChecks.Attach(network,owner);
        owner.Backpack.DropItem(new Gold(100));var trainer=new AnimalTrainer{NoKillAwards=true};trainer.MoveToWorld(new(owner.X,owner.Y+1,owner.Z),owner.Map);trainer.AIObject?.AITimer.Stop();
        var corpse=ReviveNativeActivationChecks.Body(owner,true,out var actor);ReviveNativeActivationChecks.Activate(owner,corpse,actor,321);actor.AIObject.AITimer.Stop();
        var ordinary=new Dragon{NoKillAwards=true};ordinary.MoveToWorld(new(owner.X-1,owner.Y,owner.Z),owner.Map);ordinary.AIObject.AITimer.Stop();
        Require(ordinary.SetControlMaster(owner)&&!ordinary.Summoned&&!ClassCombatHooks.PersistentMinion(ordinary),"Ordinary stable control did not retain its native identity");
        var state=ReviveNativeActivation.State(actor);var point=actor.Location;var map=actor.Map;int followers=owner.Followers,stabled=owner.Stabled?.Count??0;
        Require(!actor.Summoned&&ClassCombatHooks.PersistentMinion(actor)&&!actor.Commandable,"Class rejection would only test the old native Summoned branch");
        Target(trainer,owner,actor);
        Require(owner.Backpack.GetAmount(typeof(Gold))==100&&actor is{Alive:true,Deleted:false,IsStabled:false}&&actor.ControlMaster==owner&&actor.Map==map&&actor.Location==point
            &&owner.Followers==followers&&(owner.Stabled?.Count??0)==stabled&&ReviveNativeActivation.State(actor)==state,"Class stable target charged gold,changed ownership/location or entered stable roster");
        Log.Information("revive-native-stabling-live: ready");
        Timer.StartTimer(TimeSpan.FromMilliseconds(150),()=>
        {
            try
            {
                Require(actor is{Alive:true,Deleted:false}&&actor.ControlMaster==owner&&actor.Map==map&&actor.Location==point&&ReviveNativeActivation.State(actor)==state,"Rejected stable target caused delayed orphan deletion/mutation");
                Target(trainer,owner,ordinary);
                Require(owner.Backpack.GetAmount(typeof(Gold))==70&&ordinary is{Deleted:false,IsStabled:true,Controlled:false}&&ordinary.ControlMaster==null
                    &&ordinary.Map==Map.Internal&&ordinary.StabledBy==owner&&owner.Stabled?.Contains(ordinary)==true&&owner.Followers==followers-ordinary.ControlSlots,"Ordinary pet did not pay30and stable through the native target transaction");
                trainer.Claim(owner,ordinary.Name);
                Require(owner.Backpack.GetAmount(typeof(Gold))==70&&ordinary is{Alive:true,Deleted:false,IsStabled:false,Controlled:true}&&ordinary.ControlMaster==owner
                    &&ordinary.StabledBy==null&&ordinary.Map==owner.Map&&owner.Stabled?.Contains(ordinary)!=true&&owner.Followers==followers,"Ordinary native claim lost pet,charged again or changed follower accounting");
                Require(actor is{Alive:true,Deleted:false,IsStabled:false}&&actor.ControlMaster==owner&&ReviveNativeActivation.State(actor)==state,"Ordinary stable/claim affected class minion");
                Log.Information("Revive native stabling:actual trainer target rejects a nonsummoned class original before gold/owner/location/roster mutation and delayed orphan cleanup;ordinary high-taming pet pays30,stables and claims with native follower accounting;class identity remains unchanged");
                Log.Information("revive-native-stabling-live-result: passed");
            }
            catch(Exception e){Log.Error(e,"revive-native-stabling-live-result: failed");}
            finally{actor.Delete();ordinary.Delete();trainer.Delete();corpse.Delete();network.Mobile=null;owner.NetState=null;owner.Delete();}
        });
    }
}
