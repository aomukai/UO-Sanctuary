// SPDX-License-Identifier: GPL-3.0-or-later
using System;
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

public static class ReviveLifecycleChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(ReviveLifecycleChecks));
    private sealed record DeathPair(uint Owner,uint Actor,uint OldCorpse,uint NewCorpse,CorpseSkillRecord Old,CorpseSkillRecord New,uint[] OldLoot,uint[] NewLoot);
    private sealed record Session(PlayerMobile Owner,BaseCreature Actor,Corpse Corpse,Zombie Enemy,NetState Network,DeathPair Pair,bool KeepPair,int EnemyLife,ReviveNativeState State);
    private static Session Pending;
    private static bool Disconnected;
    private static string Manifest=>Path.Join(Core.BaseDirectory,"revive-lifecycle-death-fixtures.json");
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Original Revive death identity and native socket lifecycle",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    public static void Configure()
    {
        Register("revive-lifecycle-check",()=>FixtureClock.NextLoop("revive-lifecycle-result",()=>FixtureNetwork.Connected("revive-lifecycle-result",Start)));
        Register("revive-lifecycle-reconnect-check",()=>FixtureNetwork.Connected("revive-lifecycle-result",Reconnect));
        Register("revive-lifecycle-reload-check",Reload);
    }
    private static uint[] Loot(Corpse corpse)=>corpse.Items.Select(i=>i.Serial.Value).Order().ToArray();
    private static void VerifyPair(DeathPair pair)
    {
        var old=World.FindItem((Serial)pair.OldCorpse) as Corpse;var fresh=World.FindItem((Serial)pair.NewCorpse) as Corpse;
        Require(old is{Deleted:false}&&fresh is{Deleted:false}&&old!=fresh&&World.FindMobile((Serial)pair.Actor)==null,"Original death lost either loot corpse or retained a live actor");
        Require(CorpseSkillRules.Profiles.Find(old)==pair.Old&&CorpseSkillRules.Profiles.Find(fresh)==pair.New
            &&pair.Old is{UnitReactivated:true,NoSelect:true}&&pair.New is{UnitReactivated:false,Owned:true},"Original/new corpse metadata changed or a pet became an eligible wild corpse");
        Require(Loot(old).SequenceEqual(pair.OldLoot)&&Loot(fresh).SequenceEqual(pair.NewLoot),"Original/new corpse loot moved, duplicated or disappeared");
        Require(CorpseSkillRules.Profiles.ForOriginal(pair.Actor).Select(r=>r.Corpse).SequenceEqual(new[]{pair.NewCorpse})
            &&SourceCorpseIdentity.Find((int)pair.Actor)?.Body==fresh&&new GenericPetWorldIdentity(43).Resolve(43,1,(int)pair.Actor)==pair.Actor,"Consumed old corpse shadows the new death identity");
        bool refused=false;try{SourceCorpseIdentity.For(old);}catch(NotSupportedException){refused=true;}
        Require(refused&&ReviveOriginals.Find(fresh)==null,"Old loot corpse regained a dead-unit identity or the owned death was retained for another Revive");
    }
    private static void Start(NetState network)
    {
        Require(Pending==null,"Previous socket lifecycle fixture has not finished");Disconnected=false;
        var owner=ReviveNativeActivationChecks.Owner();var account=new Account("revive-life-"+Guid.NewGuid().ToString("N"),Guid.NewGuid().ToString("N"));
        account[0]=owner;network.Account=account;ReviveNativeActivationChecks.Attach(network,owner);
        var old=ReviveNativeActivationChecks.Body(owner,false,out var original);ReviveNativeActivationChecks.Activate(owner,old,original);
        uint originalId=original.Serial.Value;
        var enemy=new Zombie{Frozen=true,CantWalk=true,NoKillAwards=true};enemy.MoveToWorld(new(owner.X+3,owner.Y,owner.Z),owner.Map);enemy.AIObject?.AITimer.Stop();
        Corpse fresh=null;var died=ClassLootHooks.CreatureDied;
        ClassLootHooks.CreatureDied=(who,container)=>{died?.Invoke(who,container);if(who==original){fresh=container as Corpse;fresh?.DropItem(new Gold(1));}};
        try{enemy.DoHarmful(original,true);original.Damage(original.Hits+1,enemy);CombatDamage.Finish(original,enemy);}
        finally{ClassLootHooks.CreatureDied=died;}
        Require(fresh is{Deleted:false},"Active original death did not create a new native corpse");fresh.BeginDecay(TimeSpan.FromHours(1));
        var pair=new DeathPair(owner.Serial.Value,originalId,old.Serial.Value,fresh.Serial.Value,CorpseSkillRules.Profiles.Find(old),CorpseSkillRules.Profiles.Find(fresh),Loot(old),Loot(fresh));
        bool keep=!File.Exists(Manifest);
        Timer.StartTimer(TimeSpan.FromMilliseconds(150),()=>
        {
            try
            {
                VerifyPair(pair);Require(ReviveNativeActivation.State(original)==null&&owner.AllFollowers?.Contains(original)!=true,"Native death left an active Revive row/follower");
                if(keep){File.WriteAllText(Manifest,JsonSerializer.Serialize(pair));}
                var corpse=ReviveNativeActivationChecks.Body(owner,true,out var actor);ReviveNativeActivationChecks.Activate(owner,corpse,actor,321);
                actor.GetAbility(MonsterAbilityType.FireBreath).Trigger(MonsterAbilityTrigger.CombatAction,actor,enemy);
                Pending=new(owner,actor,corpse,enemy,network,pair,keep,enemy.Hits,ReviveNativeActivation.State(actor));
                Log.Information("revive-lifecycle: disconnect-ready");long deadline=Core.TickCount+7000;
                void Observe()
                {
                    try
                    {
                        Require(Core.TickCount<deadline,"Actual socket disposal did not detach the player");
                        if(network.Running||owner.NetState!=null){Timer.StartTimer(TimeSpan.FromMilliseconds(20),()=>Core.LoopContext.Post(Observe));return;}
                        Require(actor is{Alive:true,Deleted:false,Blessed:true}&&actor.Map==Map.Internal&&actor.ControlMaster==owner
                            &&!actor.CanBeHarmful(enemy,false),"Native disconnect event did not park/protect the original");
                        int life=actor.Hits;actor.Damage(life+1,enemy);Require(actor.Hits==life,"Direct incoming damage reached disconnected original");
                        Timer.StartTimer(TimeSpan.FromSeconds(3),()=>
                        {
                            try
                            {
                                Require(owner.NetState==null&&actor is{Alive:true,Deleted:false,Blessed:true}&&actor.Map==Map.Internal
                                    &&enemy.Hits==Pending.EnemyLife&&ReviveNativeActivation.State(actor)==Pending.State,"Offline original attacked, expired or lost its metadata");
                                Disconnected=true;Log.Information("revive-lifecycle: disconnected");
                            }
                            catch(Exception e){Log.Error(e,"revive-lifecycle-result: failed");}
                        });
                    }
                    catch(Exception e){Log.Error(e,"revive-lifecycle-result: failed");}
                }
                Observe();
            }
            catch(Exception e){Log.Error(e,"revive-lifecycle-result: failed");}
        });
    }
    private static void Reconnect(NetState network)
    {
        var s=Pending;Require(s!=null&&Disconnected&&!s.Network.Running&&s.Owner.NetState==null&&network!=s.Network,"Reconnect did not follow actual disposal of the first socket");
        network.Account=s.Owner.Account;ReviveNativeActivationChecks.Attach(network,s.Owner);
        // The native login event invokes ClaimAutoStabledPets and its common
        // resume hook. Fixture attachment is not a normal login-packet test.
        PlayerMobile.PlayerLoginEvent(s.Owner);
        Require(s.Actor is{Alive:true,Deleted:false,Blessed:false}&&s.Actor.Map==s.Owner.Map&&s.Actor.ControlMaster==s.Owner
            &&s.Actor.ControlSlots==0&&s.Owner.AllFollowers.Contains(s.Actor)&&s.Actor.CanBeHarmful(s.Enemy,false)
            &&ReviveNativeActivation.State(s.Actor)==s.State&&PersistentMinions.Profiles.Find(s.Actor).ExpiresUtc==null,"Native login event did not return the same living original without an expiry");
        VerifyPair(s.Pair);
        if(!s.KeepPair)
        {
            World.FindItem((Serial)s.Pair.OldCorpse).Delete();
            Require(SourceCorpseIdentity.Find((int)s.Pair.Actor)?.Body?.Serial.Value==s.Pair.NewCorpse,"Old corpse cleanup removed the subsequent death identity");
            World.FindItem((Serial)s.Pair.NewCorpse).Delete();
            Require(SourceCorpseIdentity.Find((int)s.Pair.Actor)==null,"Final corpse cleanup retained a dead-unit identity");
        }
        s.Actor.Delete();s.Corpse.Delete();s.Enemy.Delete();network.Mobile=null;s.Owner.NetState=null;
        if(s.KeepPair){s.Owner.Internalize();}else{s.Owner.Delete();}
        Pending=null;Disconnected=false;
        Log.Information("Revive lifecycle:actual original death creates a distinct owned corpse while consumed loot stays separate;native socket disposal parks/protects and cancels queued breath;new socket plus fixture login bootstrap returns the same original;no normal login-packet or source-stat/AI completeness claimed");
        Log.Information("revive-lifecycle-result: passed");
    }
    private static void Reload()
    {
        var pair=JsonSerializer.Deserialize<DeathPair>(File.ReadAllText(Manifest));VerifyPair(pair);
        Require(World.FindMobile((Serial)pair.Owner) is PlayerMobile{Alive:true,Deleted:false},"Saved death-pair owner lost");
    }
}
