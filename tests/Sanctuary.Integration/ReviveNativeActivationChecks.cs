// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
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
public static class ReviveNativeActivationChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(ReviveNativeActivationChecks));
    private sealed record Saved(uint Owner,ReviveNativeState[] Actors,uint LootCorpse,uint[] Loot);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"revive-native-activation-fixtures.json");
    private static readonly ReviveAiStatus Ordinary=new(false,false,false,false,false,true);
    private static void Require(bool value,string reason){if(!value){throw new InvalidOperationException(reason);}}
    public static void Configure()
    {
        Register("revive-native-activation-check",Check);Register("revive-native-activation-reload-check",Reload);
        Register("revive-native-activation-live-check",()=>FixtureClock.NextLoop("revive-native-activation-live-result",()=>FixtureNetwork.Connected("revive-native-activation-live-result",Live)));
    }
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Prepared original NPC activation and shared persistent behavior",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    internal static PlayerMobile Owner()
    {
        var owner=new PlayerMobile{Name="NativeReviveOwner",Body=0x190,Player=true};owner.AddItem(new Backpack());ClassCreation.Profiles.Assign(owner,DiabloClass.Necromancer);ClassCreation.Profiles.AdvanceLevel(owner,50);
        FixtureSpace.Place(owner,new(6100,1700,0),Map.Trammel);
        void Train(string id)
        {
            Require(TalentCatalog.TryGet(DiabloClass.Necromancer,id,out var row),"Missing skill prerequisite:"+id);
            foreach(string prerequisite in row.Prerequisites){if(ClassCreation.Profiles.GetRank(owner.Serial,prerequisite)==0){Train(prerequisite);}}
            Require(ClassCreation.Profiles.TryTrain(owner.Serial,id,out var reason),"Prepared activation training:"+reason);
        }
        Train("necromancer.revive");Train("necromancer.revive");return owner;
    }
    internal static Corpse Body(PlayerMobile owner,bool dragon,out BaseCreature actor)
    {
        actor=dragon?new Dragon():new Skeleton();var original=actor;actor.NoKillAwards=true;actor.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);actor.AIObject?.AITimer.Stop();
        Corpse corpse=null;var died=ClassLootHooks.CreatureDied;var before=ClassLootHooks.BeforeDeath;var retained=ClassLootHooks.RetainOriginal;
        ClassLootHooks.BeforeDeath=ReviveBeforeDeath.Capture;ClassLootHooks.RetainOriginal=ReviveOriginals.Capture;
        ClassLootHooks.CreatureDied=(who,container)=>{died?.Invoke(who,container);if(who==original){corpse=container as Corpse;corpse?.DropItem(new Gold(1));}};
        try{owner.DoHarmful(actor,true);actor.Damage(actor.Hits+1,owner);CombatDamage.Finish(actor,owner);}
        finally{ClassLootHooks.CreatureDied=died;ClassLootHooks.BeforeDeath=before;ClassLootHooks.RetainOriginal=retained;}
        Require(corpse is{Deleted:false}&&ReviveOriginals.Find(corpse)==actor,"Actual original unavailable for activation");corpse.BeginDecay(TimeSpan.FromHours(1));return corpse;
    }
    internal static BaseCreature Activate(PlayerMobile owner,Corpse corpse,BaseCreature expected,int maximum=211)
    {
        uint serial=expected.Serial.Value;var type=expected.GetType();var ai=expected.AI;
        var setup=new ReviveNativeSetup(SkillMechanics.Rank(owner,CombatAbility.Revive),maximum,maximum-17,4,10);
        Require(ReviveNativeActivation.CommitPrepared(owner,corpse,corpse.Location,Ordinary,setup,out var actor,out var issue),"Native activation failed:"+issue);
        Require(actor==expected&&actor.Serial.Value==serial&&actor.GetType()==type&&actor.AI==ai&&!actor.Summoned&&actor.ControlMaster==owner&&actor.Controlled
            &&actor.NoKillAwards&&actor.ControlSlots==0&&owner.AllFollowers.Contains(actor)&&!ReviveOriginals.IsRetained(actor),"Activation cloned/changed original or lost native owner identity");
        Require(PersistentMinions.Behavior(actor)!=null&&actor is not IPersistentMinion&&ClassCombatHooks.PersistentMinion(actor),"Original type did not use persistent behavior adapter");
        Require(actor.Alive&&actor.Blessed==(owner.NetState==null)&&!actor.Hidden&&!actor.Frozen&&!actor.CantWalk&&actor.Hits==maximum-17&&actor.HitsMax==maximum,"Explicit native setup not applied");
        Require(CorpseSkillRules.Profiles.Find(corpse) is{NoSelect:true,UnitReactivated:true}&&corpse.SanctuaryConsumed&&corpse.Animated,"Consumed loot corpse retained a dead source-unit projection");
        var registry=new GenericPetWorldIdentity(41);Require(registry.Resolve(41,1,(int)serial)==serial,"Live original registry is shadowed by consumed corpse identity");
        Require(corpse.CanLoot(owner,null)&&!corpse.IsCriminalAction(owner)&&corpse.GetAmount(typeof(Gold))==1,"Original corpse loot rights/items changed on owner binding");
        Require(!NecromancerCombat.Implemented(CombatAbility.Revive),"Prepared activation enabled unfinished learned spell");return actor;
    }
    private static void Invalid(ReviveNativeState row)
    {
        var before=ReviveNativeActivation.Snapshot();
        foreach(var bad in new[]{new[]{row with{Owner=0}},new[]{row with{Actor=0}},new[]{row with{Order=0}},new[]{row with{Setup=row.Setup with{Life=0}}},new[]{row,row},new[]{row with{Original=row.Original with{BeforeDeathJson=null}}}})
        {
            bool refused=false;try{ReviveNativeActivation.Restore(bad);}catch(InvalidDataException){refused=true;}
            Require(refused&&ReviveNativeActivation.Snapshot().SequenceEqual(before),"Native activation restore was not atomic");
        }
    }
    private static void Check()
    {
        var owner=Owner();var firstCorpse=Body(owner,false,out var first);var before=ReviveOriginals.Snapshot();int followers=owner.Followers;
        var setup=new ReviveNativeSetup(2,211,194,4,10);
        Require(!ReviveNativeActivation.CommitPrepared(owner,firstCorpse,firstCorpse.Location,Ordinary with{Unique=true},setup,out _,out _),"Source unique refusal bypassed");
        Require(!ReviveNativeActivation.CommitPrepared(owner,firstCorpse,new(-1,-1,0),Ordinary,setup,out _,out _),"Invalid native destination accepted");
        Require(!ReviveNativeActivation.CommitPrepared(owner,firstCorpse,firstCorpse.Location,Ordinary,setup with{Life=0},out _,out _),"Zero life setup accepted");
        Require(ReviveOriginals.Snapshot().SequenceEqual(before)&&!CorpseSkillRules.NoSelect(firstCorpse)&&firstCorpse.GetAmount(typeof(Gold))==1&&owner.Followers==followers,"Refused activation changed original storage/resources/loot");
        Activate(owner,firstCorpse,first);
        var secondCorpse=Body(owner,true,out var second);Activate(owner,secondCorpse,second,321);
        var thirdCorpse=Body(owner,false,out var third);Activate(owner,thirdCorpse,third,417);
        Require(first.Deleted&&!second.Deleted&&!third.Deleted&&ReviveNativeActivation.Snapshot().Count(r=>r.Owner==owner.Serial.Value)==2&&owner.Followers==followers,"Per-skill FIFO replacement or zero-slot owner accounting failed");
        Require(!ReviveNativeActivation.CommitPrepared(owner,thirdCorpse,thirdCorpse.Location,Ordinary,setup,out _,out _),"Consumed corpse activated twice");
        // Taking the final loot deletes the old corpse; its original stays alive.
        Require(ClassLoot.Take(owner,secondCorpse.Items.Single(i=>i is Gold))&&secondCorpse.Deleted&&second.Alive&&!second.Deleted,"Corpse cleanup killed detached active original");
        Require(new GenericPetWorldIdentity(41).Resolve(41,1,(int)second.Serial.Value)==second.Serial.Value,"Deleted loot container invalidated live original identity");
        PersistentMinions.Suspend(owner);Require(second.Map==Map.Internal&&third.Map==Map.Internal&&second.ControlMaster==owner&&third.ControlMaster==owner,"Original NPCs failed shared offline suspension");
        var rows=ReviveNativeActivation.Snapshot().Where(r=>r.Owner==owner.Serial.Value).ToArray();foreach(var row in rows){Invalid(row);}
        Require(PersistentMinions.Profiles.Find(second).ExpiresUtc==null&&PersistentMinions.Profiles.Find(third).ExpiresUtc==null&&PersistentMinions.Behavior(second).ExpiresUtc==null,"Native Revive gained an expiration time");
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(owner.Serial.Value,rows,thirdCorpse.Serial.Value,thirdCorpse.Items.Select(i=>i.Serial.Value).Order().ToArray())));owner.Internalize();Report();
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var owner=World.FindMobile((Serial)saved.Owner) as PlayerMobile;
        Require(owner is{Alive:true,Deleted:false},"Native activation owner lost");
        foreach(var row in saved.Actors)
        {
            var actor=World.FindMobile((Serial)row.Actor) as BaseCreature;
            Require(actor is{Alive:true,Deleted:false}&&actor.GetType().FullName==row.Original.RuntimeType&&actor.ControlMaster==owner&&actor.ControlSlots==0
                &&actor.Map==Map.Internal&&ReviveNativeActivation.State(actor)==row&&PersistentMinions.Behavior(actor)!=null,"Actual original/owner/native adapter changed on reload");
            Require(new GenericPetWorldIdentity(41).Resolve(41,1,(int)row.Actor)==row.Actor,"Active registry projection changed on reload");Invalid(row);
        }
        var corpse=World.FindItem((Serial)saved.LootCorpse) as Corpse;
        Require(corpse is{Deleted:false}&&CorpseSkillRules.Profiles.Find(corpse) is{UnitReactivated:true,NoSelect:true}
            &&corpse.Items.Select(i=>i.Serial.Value).Order().SequenceEqual(saved.Loot),"Consumed loot corpse marker/items changed on reload");Report();
    }
    internal static void Attach(NetState network,PlayerMobile owner)
    {
        network.Seeded=network.SentFirstPacket=true;network.CompressionEnabled=false;network.Mobile=owner;owner.NetState=network;
        var state=typeof(NetState).GetField("_protocolState",BindingFlags.Instance|BindingFlags.NonPublic);state.SetValue(network,Enum.Parse(state.FieldType,"GameServer_LoggedIn"));network.ResetMovementState();
    }
    private static void Live(NetState network)
    {
        var owner=Owner();Attach(network,owner);var corpse=Body(owner,true,out var actor);Activate(owner,corpse,actor,321);
        Require(actor.Map==owner.Map&&ClassCombat.Friendly(actor,owner)&&!actor.CanBeHarmful(owner,false),"Connected owner did not resume original or gain allegiance protection");
        var enemy=new Zombie{Frozen=true,CantWalk=true,NoKillAwards=true};enemy.MoveToWorld(new(owner.X+3,owner.Y,owner.Z),owner.Map);enemy.AIObject?.AITimer.Stop();
        Require(actor.CanBeHarmful(enemy,false),"Connected original cannot target an ordinary enemy");
        actor.GetAbility(MonsterAbilityType.FireBreath).Trigger(MonsterAbilityTrigger.CombatAction,actor,enemy);int health=enemy.Hits;
        PersistentMinions.Suspend(owner);
        Require(actor.Map==Map.Internal&&actor.Blessed&&!actor.CanBeHarmful(enemy,false),"Parked original remains offensive");int parkedLife=actor.Hits;actor.Damage(parkedLife+1,enemy);Require(actor.Hits==parkedLife&&!actor.Deleted,"Direct queued incoming damage reached a parked original");
        Log.Information("revive-native-activation-live: ready");
        Timer.StartTimer(TimeSpan.FromSeconds(3),()=>
        {
            try
            {
                Require(enemy.Hits==health&&actor is{Alive:true,Deleted:false},"Queued native attack continued while parked or original expired");
                PersistentMinions.Resume(owner);Require(actor.Map==owner.Map,"Resume failed to return same original");
                ClassCreation.Profiles.SelectHistory(owner,DiabloClass.Sorceress);PersistentMinions.ParkClass(owner,DiabloClass.Necromancer);Require(actor.Map==Map.Internal&&actor.Alive,"Foreign-class history did not park original");
                ClassCreation.Profiles.SelectHistory(owner,DiabloClass.Necromancer);PersistentMinions.Resume(owner);Require(actor.Map==owner.Map&&actor.ControlMaster==owner,"Returning class history lost original");
                Require(PersistentMinions.Profiles.Find(actor).ExpiresUtc==null&&PersistentMinions.Behavior(actor).ExpiresUtc==null&&actor.Alive,"Original gained age expiry");owner.Kill();
                Timer.StartTimer(TimeSpan.FromMilliseconds(150),()=>
                {
                    try{Require(actor.Deleted,"Owner death did not remove native Revive");Log.Information("revive-native-activation-live-result: passed");}
                    catch(Exception e){Log.Error(e,"revive-native-activation-live-result: failed");}
                    finally{network.Mobile=null;owner.NetState=null;actor.Delete();enemy.Delete();corpse.Delete();owner.Corpse?.Delete();owner.Delete();}
                });
            }
            catch(Exception e){Log.Error(e,"revive-native-activation-live-result: failed");network.Mobile=null;owner.NetState=null;actor.Delete();enemy.Delete();corpse.Delete();owner.Delete();}
        });
    }
    private static void Report()=>Log.Information("Prepared native Revive activation:same original actors/types/serials,explicit integral setup,native owner/zero slots,skill-cap FIFO,corpse lease detachment,loot rights and permanent source-identity retirement pass;shared offline/save lifecycle and atomic invalid state checks retained;source stat/passive/FP256/AI/facet/input composition and learned spell remain gated");
}
