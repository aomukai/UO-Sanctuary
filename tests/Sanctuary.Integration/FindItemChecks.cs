// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using Server.Network;

namespace Sanctuary.Integration;

public static class FindItemChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(FindItemChecks));
    private sealed record Saved(uint Owner,uint Used,uint Unused,uint Pending,Point3D Origin,int Map,int Mana256,CorpseSkillRecord UsedState,CorpseSkillRecord UnusedState,uint[] UsedItems,uint[] PendingItems);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"find-item.json");
    private static bool _creating;
    public static void Configure(){Rates();Register("find-item-check",Check);Register("find-item-reload-check",Reload);Register("find-item-snapshot-check",Snapshot);Register("find-item-decay-check",Decay);Register("find-item-inspect",Inspect);if(Environment.GetEnvironmentVariable("SANCTUARY_FIND_ITEM_INSPECT_WORLD")=="1"){EventSink.WorldLoad+=Inspect;}}
    [CallPriority(int.MaxValue)] public static void Initialize()=>Rates();
    private static void Rates()
    {
        var prior=Mobile.ManaRegenRateHandler;
        Mobile.ManaRegenRateHandler=actor=>_creating || actor.Name=="FindItemFixture"?TimeSpan.FromHours(1):prior?.Invoke(actor)??Mobile.DefaultManaRate;
    }
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Find Item source corpse/payment/loot/persistence",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static void Later(int ms,Action action)=>Timer.StartTimer(TimeSpan.FromMilliseconds(Math.Max(1,ms)),()=>{try{action();}catch(Exception e){Log.Error(e,"find-item-result: failed");}});
    private static PlayerMobile Fresh()
    {
        PlayerMobile owner;_creating=true;try{owner=new PlayerMobile{Body=0x190,Name="FindItemFixture",Player=true};}finally{_creating=false;}
        owner.AddItem(new Backpack());ClassCreation.Profiles.Assign(owner,DiabloClass.Barbarian);ClassCreation.Profiles.AdvanceLevel(owner,99);
        for(int x=2700;x<3050;x+=50)for(int y=1000;y<1350;y+=50)
        {
            Point3D point;try{FixtureSpace.Place(owner,new(x,y,0),Map.Felucca);point=ClassSkillChecks.Corridor(owner,x,y);}catch(InvalidOperationException){continue;}
            bool occupied=false;foreach(var mobile in owner.Map.GetMobilesInRange<Mobile>(point,20)){if(mobile!=owner){occupied=true;break;}}
            if(occupied || !Server.Spells.SpellHelper.CheckTown(point,owner)){continue;}
            owner.MoveToWorld(point,owner.Map);Train(owner,CombatAbility.FindItem,1);return owner;
        }
        throw new InvalidOperationException("No Find Item corridor");
    }
    private static void Train(PlayerMobile owner,CombatAbility ability,int rank)
    {
        var row=SkillMechanics.For(ability);Require(TalentCatalog.TryGet(row.CharacterClass,row.TalentId,out var talent),"Missing talent");foreach(string id in talent.Prerequisites){Train(owner,SkillMechanics.All.Single(x=>x.TalentId==id).Ability,1);}
        while(ClassCreation.Profiles.GetRank(owner.Serial,row.TalentId)<rank){Require(ClassCreation.Profiles.TryTrain(owner.Serial,row.TalentId,out var why),"Train: "+why);}
    }
    private static void Ready(PlayerMobile owner){ClassCombat.Stop(owner);ClassCombat.For(owner).NextAction=ClassCombat.For(owner).RecoveryUntil=Core.TickCount;}
    private static Corpse Body(PlayerMobile owner,MonsterDifficulty difficulty=MonsterDifficulty.Normal,bool noRewards=false)
    {
        var victim=new GraveyardSkeleton{Frozen=true,CantWalk=true,NoKillAwards=noRewards};if(difficulty!=MonsterDifficulty.Normal){MonsterVariants.Assign(victim,difficulty);}victim.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);victim.AIObject.AITimer.Stop();
        Corpse corpse=null;var prior=ClassLootHooks.CreatureDied;
        ClassLootHooks.CreatureDied=(actor,container)=>
        {
            if(actor!=victim){prior?.Invoke(actor,container);return;}
            // This fixture tests a retained corpse and the later Find Item
            // roll. Keep its unrelated ordinary-death treasure deterministic;
            // the real capture/reward consumers still run. Random affix failure
            // is tracked separately rather than caught or changed in gameplay.
            int seed=0;
            while(seed<10000 && CreatureGold.Select(typeof(GraveyardSkeleton),new Random(seed),difficulty)!="gld"){seed++;}
            Require(seed<10000,"Ordinary gold treasure seed");
            CorpseSkillRules.Capture(actor,container);RewardLife.Process(actor,container,new Random(seed));
            corpse=container as Corpse;var bag=new Bag{Name="FindItemOriginal"};bag.DropItem(new Gold(7));corpse?.DropItem(bag);
        };
        try{owner.DoHarmful(victim,true);victim.Damage(victim.Hits+1,owner);CombatDamage.Finish(victim,owner);}
        finally{ClassLootHooks.CreatureDied=prior;}
        Require(corpse is{Deleted:false} && ClassLoot.HasContents(corpse),"Real corpse with retained loot");
        if(!noRewards){Require(CorpseSkillRules.Profiles.Find(corpse).Treasure is{Level:>0} context && context.Difficulty==difficulty && context.Source==CreatureGold.Policy(typeof(GraveyardSkeleton),difficulty).Reference,"Captured source/difficulty/resolved TC/level");}
        return corpse;
    }
    private static uint[] Items(Corpse body)=>body.Items.Select(x=>x.Serial.Value).OrderBy(x=>x).ToArray();
    private static int Gold(Corpse body)=>body.GetAmount(typeof(Gold));
    private static void Retained(Corpse body,uint[] original)=>Require(original.All(id=>World.FindItem((Serial)id) is{Deleted:false} item && item.Parent==body),"Original loot removed/reparented");
    private static void Start(PlayerMobile owner,Corpse body,bool quick=false)
    {
        Ready(owner);int mana=NecromancerEffects.ManaBalance256(owner);Require(quick?SkillBindingNetwork.Use(owner,DiabloClass.Barbarian,0,CombatAbility.FindItem,body.Serial,body.Location):ClassCombat.BeginCorpse(owner,CombatAbility.FindItem,body.Serial,body.Location,Core.TickCount),"Find Item begin");
        var action=ClassCombat.For(owner);Require(NecromancerEffects.ManaBalance256(owner)==mana-1792 && action.FindItemTarget==body.Serial && !CorpseSkillRules.NoSelect(body),"Start payment/token or early attempt reservation");
        Require(!ClassCombat.ResolveAction(owner,action.EffectAt-1),"Early Find Item event");
    }
    private static bool Finish(PlayerMobile owner)
    {
        var action=ClassCombat.For(owner);int mana=NecromancerEffects.ManaBalance256(owner);bool result=ClassCombat.ResolveAction(owner,action.EffectAt);Require(!ClassCombat.ResolveAction(owner,action.EffectAt) && action.FindItemTarget==Serial.Zero && NecromancerEffects.ManaBalance256(owner)==mana,"Repeated event/free token/impact debit");return result;
    }
    private static int SeedFor(Corpse body,Func<string,bool> match)
    {
        var context=CorpseSkillRules.Profiles.Find(body).Treasure;for(int seed=0;seed<10000;seed++){if(match(CreatureGold.SelectTreasureClass(context.TreasureClass,new Random(seed),true))){return seed;}}throw new InvalidOperationException("No deterministic drop seed");
    }
    private static void Goldens()
    {
        var row=SkillMechanics.For(CombatAbility.FindItem);
        using var source=JsonDocument.Parse(File.ReadAllText(Path.Join(Environment.GetEnvironmentVariable("SANCTUARY_PROJECT_ROOT"),"tools/reference/find-item-engine-evidence.json")));
        int rank=0;foreach(var expected in source.RootElement.GetProperty("historicalPublishedBaseChance").GetProperty("values").EnumerateArray())
        {
            rank++;foreach(int hard in new[]{0,1,20})
            {var v=new SkillValues(row,key=>key==row.SourceKey?rank:0,key=>key=="Find Potion"?hard:0,99);Require(v.Mana256==1792 && v.Eval("calc1")==expected.GetInt32()+hard,"Historical base chance/current hard Find Potion synergy and7mana");}
        }
        var old=JsonSerializer.Deserialize<CorpseSkillRecord>("{\"Corpse\":1073741825,\"Source\":\"skeleton1\",\"Selectable\":true,\"Owned\":false,\"Shattered\":false}");Require(old is{NoSelect:false,Treasure:null},"Old corpse JSON defaults");
    }
    private static Corpse Synchronous(PlayerMobile owner)
    {
        Require(ClassAbilities.CorpseTarget(CombatAbility.FindItem) && ClassCombat.CorpseCast(CombatAbility.FindItem),"Shared corpse targeting metadata/dispatch");Require(!SkillBindings.Instance.Assign(owner,true,CombatAbility.FindItem) && SkillBindings.Instance.Assign(owner,false,CombatAbility.FindItem) && SkillBindings.Instance.AssignQuick(owner,0,CombatAbility.FindItem),"Find Item right/quick bindings");
        owner.Mana=owner.ManaMax;var body=Body(owner);var original=Items(body);int mana=NecromancerEffects.ManaBalance256(owner);
        Require(!BarbarianFindItem.Release(owner,body.Serial) && !CorpseSkillRules.NoSelect(body) && NecromancerEffects.ManaBalance256(owner)==mana,"Free release without paid start");
        NecromancerEffects.SetMana256(owner,1791);Ready(owner);Require(!ClassCombat.BeginCorpse(owner,CombatAbility.FindItem,body.Serial,body.Location,Core.TickCount) && !CorpseSkillRules.NoSelect(body),"Fractionally insufficient start");owner.Mana=owner.ManaMax;
        var raiser=new GraveyardLich{Frozen=true,CantWalk=true,NoKillAwards=true};raiser.MoveToWorld(new(owner.X,owner.Y+1,owner.Z),owner.Map);raiser.AIObject.AITimer.Stop();Require(raiser.CanRaise(body) && CorpseRecoveryHooks.CanAnimate(body),"Unsearched corpse resurrection fixture");
        var roll=BarbarianFindItem.Roll;var loot=BarbarianFindItem.LootRandom;
        try
        {
            int calls=0,chance=(int)SkillMechanics.Values(owner,CombatAbility.FindItem).Eval("calc1");BarbarianFindItem.Roll=_=>{calls++;return chance;};int art=body.ItemID,hue=body.Hue;Start(owner,body,true);Require(!Finish(owner) && calls==1 && CorpseSkillRules.NoSelect(body) && !body.Animated && !body.SanctuaryConsumed && body.ItemID==art && body.Hue==hue,"Failed chance attempt/return/RNG/presentation");
            Retained(body,original);Require(!CorpseSkillRules.Usable(owner,body,8) && !SkeletonPets.Usable(owner,body) && !raiser.CanRaise(body) && !CorpseRecoveryHooks.CanAnimate(body),"Shared player/NPC/legacy resurrection exclusions");
            Ready(owner);mana=NecromancerEffects.ManaBalance256(owner);Require(!SkillBindingNetwork.Use(owner,DiabloClass.Barbarian,0,CombatAbility.FindItem,body.Serial,body.Location) && calls==1 && NecromancerEffects.ManaBalance256(owner)==mana,"Repeated attempt cost/RNG");
            var dummy=new GraveyardSkeleton{NoKillAwards=true};CorpseSkillRules.Capture(dummy,body);dummy.Delete();Require(CorpseSkillRules.NoSelect(body),"Repeated capture erased reservation");
            foreach(int interruption in new[]{0,1,2})
            {
                var interrupted=Body(owner);original=Items(interrupted);Start(owner,interrupted);mana=NecromancerEffects.ManaBalance256(owner);var origin=owner.Location;
                if(interruption==0){ClassCombat.Stop(owner);}else if(interruption==1){owner.Paralyzed=true;}else{owner.MoveToWorld(new(owner.X,owner.Y+1,owner.Z),owner.Map);}
                Require(!Finish(owner) && !CorpseSkillRules.NoSelect(interrupted) && NecromancerEffects.ManaBalance256(owner)==mana,"Interrupted start refund/attempt/output");owner.Paralyzed=false;owner.MoveToWorld(origin,owner.Map);Retained(interrupted,original);interrupted.Delete();
            }
            var competed=Body(owner);Start(owner,competed);mana=NecromancerEffects.ManaBalance256(owner);NecromancerCombat.Consume(competed);Require(!Finish(owner) && NecromancerEffects.ManaBalance256(owner)==mana,"Competing corpse consumer/refund");competed.Delete();
            var unsupported=Body(owner,noRewards:true);Ready(owner);mana=NecromancerEffects.ManaBalance256(owner);Require(!ClassCombat.BeginCorpse(owner,CombatAbility.FindItem,unsupported.Serial,unsupported.Location,Core.TickCount) && NecromancerEffects.ManaBalance256(owner)==mana,"Unknown reward context silently invented");unsupported.Delete();
            var missing=Body(owner);var state=CorpseSkillRules.Profiles.Find(missing);CorpseSkillRules.Profiles.Store(state with{Treasure=null});Ready(owner);Require(!ClassCombat.BeginCorpse(owner,CombatAbility.FindItem,missing.Serial,missing.Location,Core.TickCount),"Old/missing context fallback invented");missing.Delete();
            var raced=Body(owner);Start(owner,raced);mana=NecromancerEffects.ManaBalance256(owner);raced.Delete();Require(!Finish(owner) && NecromancerEffects.ManaBalance256(owner)==mana,"Deleted corpse refund/output");
            foreach(var difficulty in Enum.GetValues<MonsterDifficulty>())
            {
                var success=Body(owner,difficulty);original=Items(success);var context=CorpseSkillRules.Profiles.Find(success).Treasure;int seed=SeedFor(success,s=>s=="gld");var expected=new Random(seed);Require(CreatureGold.SelectTreasureClass(context.TreasureClass,expected,true)=="gld","Gold seed");int amount=context.Level+expected.Next(5*context.Level),before=Gold(success);
                calls=0;BarbarianFindItem.Roll=_=>{calls++;return 0;};BarbarianFindItem.LootRandom=()=>new Random(seed);owner.Mana=owner.ManaMax;Start(owner,success);NecromancerEffects.SetMana256(owner,0);Require(Finish(owner) && Gold(success)-before==amount && calls==2 && NecromancerEffects.ManaBalance256(owner)==0,"Difficulty/source level/secondary draw/zero-mana impact");Retained(success,original);success.Delete();
            }
            Train(owner,CombatAbility.FindItem,20);Train(owner,CombatAbility.FindPotion,20);var gold=Body(owner);var goldContext=CorpseSkillRules.Profiles.Find(gold).Treasure;int goldSeed=SeedFor(gold,s=>s=="gld");BarbarianFindItem.Roll=_=>0;BarbarianFindItem.LootRandom=()=>new Random(goldSeed);owner.Mana=owner.ManaMax;Start(owner,gold);
            var helm=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey=="Tarnhelm").Id,99,new Random(173),true);Require(owner.EquipItem(helm),"Real Tarnhelm");ClassGear.Refresh(owner);Require(SkillMechanics.Rank(owner,CombatAbility.FindItem)==21 && ClassCreation.Profiles.GetRank(owner.Serial,SkillMechanics.For(CombatAbility.FindPotion).TalentId)==20 && CreatureGold.GoldFind(owner)==75,"Actual soft rank/hard synergy/gold find");
            var random=new Random(goldSeed);CreatureGold.SelectTreasureClass(goldContext.TreasureClass,random,true);int raw=goldContext.Level+random.Next(5*goldContext.Level),beforeGold=Gold(gold);Require(Finish(owner) && Gold(gold)-beforeGold==raw*175/100,"Find Item retained killer/start gear instead of current caster gold find");gold.Delete();helm.Delete();ClassGear.Refresh(owner);
            var potion=Body(owner);int potionSeed=SeedFor(potion,s=>s=="hp1");BarbarianFindItem.LootRandom=()=>new Random(potionSeed);original=Items(potion);owner.Mana=owner.ManaMax;Start(owner,potion);Require(Finish(owner) && potion.Items.Any(x=>!original.Contains(x.Serial.Value) && x is RestorationPotion),"Existing potion factory output");Retained(potion,original);potion.Delete();
            var equipment=Body(owner);int gearSeed=SeedFor(equipment,s=>GearDrops.SelectBase(s,new Random(173)) is{Variants.Length:>0});BarbarianFindItem.LootRandom=()=>new Random(gearSeed);original=Items(equipment);owner.Mana=owner.ManaMax;Start(owner,equipment);Require(Finish(owner) && equipment.Items.Any(x=>!original.Contains(x.Serial.Value) && ClassGear.Profiles.Find(x.Serial) is{} gear && gear.Roll.ItemLevel==CorpseSkillRules.Profiles.Find(equipment).Treasure.Level),"Existing equipment factory/source item level");Retained(equipment,original);equipment.Delete();
        }
        finally{raiser.Delete();BarbarianFindItem.Roll=roll;BarbarianFindItem.LootRandom=loot;}
        owner.Mana=owner.ManaMax;return body;
    }
    private static void Attach(PlayerMobile owner)
    {
        var network=NetState.Instances.Single(x=>x.Mobile==null && IPAddress.IsLoopback(x.Address));network.Seeded=true;network.CompressionEnabled=false;network.Mobile=owner;owner.NetState=network;
        var protocol=typeof(NetState).GetField("_protocolState",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);Require(protocol!=null,"Find Item network bootstrap");protocol.SetValue(network,Enum.Parse(protocol.FieldType,"GameServer_LoggedIn"));network.ResetMovementState();network.SendMobileUpdate(owner);
    }
    private static void Natural(PlayerMobile owner,Corpse used,Corpse target)
    {
        var oldRoll=BarbarianFindItem.Roll;var oldLoot=BarbarianFindItem.LootRandom;int seed=SeedFor(target,s=>s=="gld"),gold=Gold(target);var original=Items(target);BarbarianFindItem.Roll=_=>0;BarbarianFindItem.LootRandom=()=>new Random(seed);owner.Mana=owner.ManaMax;Start(owner,target,true);NecromancerEffects.SetMana256(owner,0);var action=ClassCombat.For(owner);
        Later((int)(action.EffectAt-Core.TickCount+120),()=>
        {
            BarbarianFindItem.Roll=oldRoll;BarbarianFindItem.LootRandom=oldLoot;Require(action.PendingAbility==CombatAbility.None && action.FindItemTarget==Serial.Zero && CorpseSkillRules.NoSelect(target) && Gold(target)>gold && NecromancerEffects.ManaBalance256(owner)==0,"Natural connected40ms cast/payment/attempt/output");Retained(target,original);target.Delete();Seed(owner,used);Log.Information("find-item-result: passed");
        });
    }
    private static void Seed(PlayerMobile owner,Corpse used)
    {
        owner.Mana=owner.ManaMax;var unused=Body(owner);var pending=Body(owner);Start(owner,pending);ClassCombat.For(owner).EffectAt=ClassCombat.For(owner).NextAction=Core.TickCount+FixtureSpace.RetentionMilliseconds; // Inspection only; actual timing was checked above.
        NecromancerEffects.SetMana256(owner,NecromancerEffects.ManaBalance256(owner)-17);
        // These are parked persistence specimens, after the real cast checks.
        // Do not let the unrelated native seven-minute corpse timer bound the suite.
        foreach(var body in new[]{used,unused,pending}){body.BeginDecay(TimeSpan.FromHours(1));}
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(owner.Serial.Value,used.Serial.Value,unused.Serial.Value,pending.Serial.Value,owner.Location,owner.Map.MapID,NecromancerEffects.ManaBalance256(owner),CorpseSkillRules.Profiles.Find(used),CorpseSkillRules.Profiles.Find(unused),Items(used),Items(pending))));
    }
    private static void Inspect()
    {
        if(!File.Exists(Manifest)){return;}
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));
        var rows=new[]{saved.Used,saved.Unused,saved.Pending}.Select(serial=>
        {
            var body=World.FindItem((Serial)serial) as Corpse;
            return new{Serial=serial,Exists=body!=null,Deleted=body?.Deleted,Bones=body?.IsBones,Rule=CorpseSkillRules.Profiles.Find(body)};
        }).ToArray();
        Log.Information("Find Item saved-world inspection: {Rows}",JsonSerializer.Serialize(rows));
    }
    private static void Snapshot()
    {
        if(!File.Exists(Manifest)){return;}
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var used=World.FindItem((Serial)s.Used) as Corpse;
        var unused=World.FindItem((Serial)s.Unused) as Corpse;var pending=World.FindItem((Serial)s.Pending) as Corpse;
        Require(used!=null&&unused!=null&&pending!=null&&CorpseSkillRules.Profiles.Find(used)==s.UsedState
            &&CorpseSkillRules.Profiles.Find(unused)==s.UnusedState,"Parked Find Item corpse/metadata changed before save");
        Retained(used,s.UsedItems);Retained(pending,s.PendingItems);
    }
    private static void Decay()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));
        // Decay needs a real corpse, not a second unoccupied combat arena.
        var owner=new PlayerMobile{Body=0x190,Player=true,Name="FindItemDecayFixture"};
        ClassCreation.Profiles.Assign(owner,DiabloClass.Barbarian);ClassCreation.Profiles.AdvanceLevel(owner,99);
        owner.MoveToWorld(saved.Origin,Map.Maps[saved.Map]);
        var body=Body(owner);Require(CorpseSkillRules.Profiles.Find(body)!=null,"Decay control has no source metadata");
        body.BeginDecay(TimeSpan.FromMilliseconds(80));
        Later(250,()=>
        {
            try{Require(body.Deleted&&World.FindItem(body.Serial)==null&&CorpseSkillRules.Profiles.Find(body)==null,"Real corpse expiry or metadata cleanup disabled");Log.Information("find-item-decay-result: passed");}
            finally{body.Delete();owner.Delete();}
        });
    }
    private static void Check(){Goldens();var owner=Fresh();var used=Synchronous(owner);Attach(owner);Natural(owner,used,Body(owner));}
    private static void Reload()
    {
        Goldens();var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var owner=(PlayerMobile)World.FindMobile((Serial)saved.Owner);owner.MoveToWorld(saved.Origin,Map.Maps[saved.Map]);var used=(Corpse)World.FindItem((Serial)saved.Used);var unused=(Corpse)World.FindItem((Serial)saved.Unused);var pending=(Corpse)World.FindItem((Serial)saved.Pending);
        Require(CorpseSkillRules.Profiles.Find(used)==saved.UsedState && CorpseSkillRules.Profiles.Find(unused)==saved.UnusedState && saved.UsedState.NoSelect && !saved.UnusedState.NoSelect,$"Saved source/drop context/attempt state: usedExists={used!=null}, unusedExists={unused!=null}, actualUsed={JsonSerializer.Serialize(CorpseSkillRules.Profiles.Find(used))}, expectedUsed={JsonSerializer.Serialize(saved.UsedState)}, actualUnused={JsonSerializer.Serialize(CorpseSkillRules.Profiles.Find(unused))}, expectedUnused={JsonSerializer.Serialize(saved.UnusedState)}");Retained(used,saved.UsedItems);Retained(pending,saved.PendingItems);Require(!CorpseSkillRules.NoSelect(pending) && NecromancerEffects.ManaBalance256(owner)==saved.Mana256 && ClassCombat.For(owner).PendingAbility==CombatAbility.None && ClassCombat.For(owner).FindItemTarget==Serial.Zero,"Transient action replay or paid/fractional mana loss");
        Require(!BarbarianFindItem.Release(owner,pending.Serial) && !CorpseSkillRules.NoSelect(pending) && !CorpseRecoveryHooks.CanAnimate(used) && CorpseRecoveryHooks.CanAnimate(unused),"Restored noselect/native animation hook/free release");pending.Delete();Attach(owner);Natural(owner,used,unused);
    }
}
