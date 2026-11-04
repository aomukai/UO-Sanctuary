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
namespace Sanctuary.Integration;

public static class ReviveGraphChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(ReviveGraphChecks));
    private sealed record Fixture(uint Actor,bool Graph,int[][] Expected);
    private sealed record Saved(uint Owner,Fixture[] Actors,int[] Life,int[] Mana,int[] Stamina,int Generation=0);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"revive-graph-fixtures.json");
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    public static void Configure(){Register("revive-graph-check",Check);Register("revive-graph-reload-check",Reload);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Frozen item base/ownership graph,derived resources/armor/events",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static Item Named(string name)=>NamedGear.Create(NamedGear.Catalog.Items.Single(r=>r.SourceKey==name).Id,99,new Random(173),true);
    private static Fixture Create(PlayerMobile owner,int kind,bool enabled)
    {
        var b=new Skeleton{Name="PoisonDaggerFixtureReviveGraph",NoKillAwards=true};foreach(var item in b.Items.Where(i=>i is not Backpack&&i.Layer!=Layer.Bank).ToArray()){item.Delete();}
        string[] names=kind==1?["The Gnasher","Duriel's Shell","Bloodfist","Raven Frost","Razortail","Goblin Toe"]
            :enabled?["The Gnasher","Harlequin Crest","Frostburn","Dwarf Star","Razortail","Goblin Toe"]
            :["The Gnasher","Harlequin Crest","Frostburn","Dwarf Star","Goblin Toe"];
        var gear=names.Select(Named).ToArray();foreach(var item in gear){b.AddItem(item);}Require(gear.All(i=>i.Parent==b),"Graph fixture slots collided");
        var expected=Enumerable.Range(1,99).Select(level=>
        {
            var contexts=gear.Select(i=>new ItemStatContext(i,level)).ToArray();int Bonus(string code)=>contexts.Sum(c=>c.Bonus(code));
            int[] Resource(string code,int stat)=>[contexts.Sum(c=>c.ResourceInput(code,stat).Flat256),contexts.Sum(c=>c.ResourceInput(code,stat).LevelCoefficientRaw)];
            var hp=Resource("hp",216);var mana=Resource("mana",217);var stam=Resource("stam",242);
            return new[]{hp[0],hp[1],Bonus("hp%"),mana[0],mana[1],Bonus("mana%"),stam[0],stam[1],Bonus("dex"),contexts.Sum(c=>c.Defense),Bonus("thorns"),Bonus("openwounds"),Bonus("crush")};
        }).ToArray();
        b.RawInt=70;b.ManaMaxSeed=70;b.StamMaxSeed=55;b.MoveToWorld(new(owner.X+2,owner.Y,owner.Z),owner.Map);b.AIObject?.AITimer.Stop();Corpse corpse=null;var died=ClassLootHooks.CreatureDied;
        ClassLootHooks.CreatureDied=(who,item)=>{died?.Invoke(who,item);if(who==b){corpse=item as Corpse;corpse?.DropItem(new Gold(1));}};
        try{owner.DoHarmful(b,true);b.Damage(b.Hits+1,owner);CombatDamage.Finish(b,owner);}finally{ClassLootHooks.CreatureDied=died;}
        Require(corpse is{Deleted:false}&&ReviveOriginals.Find(corpse)==b,"Graph original was not retained");corpse.BeginDecay(FixtureSpace.Retention);
        var original=ReviveOriginals.State(corpse);var before=ReviveBeforeDeath.Decode(original.BeforeDeathJson);var frozen=ReviveBeforeDeath.Equipment(before);
        foreach(var snapshot in frozen)
        {
            var live=gear.Single(i=>i.Serial.Value==snapshot.OriginalItem);var captured=NpcItemStatCapture.Capture(live);var stored=FrozenEquipmentEffects.Bind(snapshot);
            Require(stored.GraphInput(1).Body==captured.Body&&stored.GraphInput(1).ItemBase.SequenceEqual(captured.ItemBase)&&stored.NumericProperties(1).SequenceEqual(captured.Properties),"Frozen graph changed original base/property arrays");
        }
        var setup=ReviveLife.Setup(owner,corpse,before.Mana,before.Stamina)with{RegenerationRate=ReviveRegeneration.Rate("skeleton1")};setup=enabled?ReviveEquipmentGraph.Prepare(setup,original):ReviveEquipmentIncoming.Prepare(setup,original);
        Require(ReviveNativeActivation.CommitPrepared(owner,corpse,corpse.Location,before.AiStatus.Value,setup,out var actual,out var issue)&&actual==b,"Graph activation failed: "+issue);
        foreach(var item in gear){Require(item.Parent==corpse,"Graph original gear was not independent loot");owner.AddToBackpack(item);item.Delete();}return new(b.Serial.Value,enabled,expected);
    }
    private static int Resource(int channel,int level,int source,int[] row,int orders,int vigor=0)
        =>NpcResourceValues.Calculate(channel switch
        {
            0=>new(1,0,level,source,row[0],row[1],row[2],orders),
            1=>new(1,1,level,70*256,row[3],row[4],row[5],orders),
            _=>new(1,2,level,55*256,row[6],row[7],orders+vigor,0)
        });
    private static void Expect(Fixture fixture)
    {
        var actor=World.FindMobile((Serial)fixture.Actor)as BaseCreature;var state=ReviveNativeActivation.State(actor);int level=(state.FacetPlan?.Combat??state.Setup.CombatPlan).Level;
        int[] row=fixture.Expected[level-1];int orders=BattleOrders.Percent(actor),source=(state.FacetPlan?.Life??state.Setup.LifePlan).Maximum256;
        Require(ReviveEquipmentGraph.Handles(actor)==fixture.Graph,"Graph opt-in changed an older incoming profile");
        Require(SkillHealth.Maximum256(actor)==Resource(0,level,source,row,orders)&&NecromancerEffects.ManaMaximum256(actor)==Resource(1,level,source,row,orders)
            &&BattleOrders.StaminaMaximum256(actor)==Resource(2,level,source,row,orders,PaladinVigor.StaminaPercent(actor)),"Graph resource totals differ from independent existing NPC resource oracle");
        Require(NpcEquipmentCombat.Defense(actor)==row[9]+row[8]/4,"Attached item-local/per-level armor or pooled Dexterity differs from original live suppliers");
        if(!fixture.Graph){return;}
        var graph=ReviveEquipmentGraph.Current(state);int life=SkillHealth.Balance256(actor),mana=NecromancerEffects.ManaBalance256(actor),stam=BattleOrders.Stamina256(actor);
        foreach(var item in graph.Items){Require(EquipmentStatGraph.Owner(item,out bool damage)==graph.Actor&&damage&&item.Stats.Recipient==graph.Actor,"Frozen graph lost actual item ownership/recipient");}
        Require(graph.Actor.Kind==1&&graph.Actor.Stats.OwnerKind==1&&graph.Read(12)==level&&graph.Read(78)==row[10]&&graph.Read(135)==row[11]&&graph.Read(136)==row[12],"Kind1 graph used owner level or lost derived event totals");
        Require(NpcEquipmentRetaliation.Flat(actor)==row[10]&&NpcOpenWounds.Chance(actor)==row[11]&&NpcCrushing.Chance(actor)==row[12],"World event readers ignored attached graph");
        for(int n=0;n<3;n++){_=SkillHealth.Maximum256(actor);_=ReviveEquipmentGraph.Current(state);_=NpcCombatEvents.ForSource(actor);}
        Require(SkillHealth.Balance256(actor)==life&&NecromancerEffects.ManaBalance256(actor)==mana&&BattleOrders.Stamina256(actor)==stam,"Graph reconstruction/query healed or reset resources");
        var items=ReviveEquipmentLife.Items(state);
        foreach(int at in new[]{1,3,21,99})
        {
            var projection=new FrozenNpcStatGraph(items,at,source,70*256,55*256,orders,17);var expected=fixture.Expected[at-1];
            Require(projection.Read(7)==Resource(0,at,source,expected,orders)&&projection.Read(9)==Resource(1,at,source,expected,orders)&&projection.Read(11)==Resource(2,at,source,expected,orders,17)
                &&projection.Read(31)==expected[9]&&projection.Read(78)==expected[10],"Projected per-level resources/armor/derived retaliation mismatch");
        }
    }
    private static void Refusals(Fixture fixture,Fixture legacy)
    {
        var actor=World.FindMobile((Serial)fixture.Actor)as BaseCreature;var state=ReviveNativeActivation.State(actor);var rows=ReviveNativeActivation.Snapshot();var before=ReviveBeforeDeath.Decode(state.Original.BeforeDeathJson);
        var saved=ReviveBeforeDeath.Equipment(before);var payload=FrozenEquipmentEffects.Decode<FrozenCombatSnapshot>(saved[0].CombatJson);bool refused=false;
        var time=saved.ToArray();time[0]=time[0]with{CombatJson=JsonSerializer.Serialize(payload with{Numeric=payload.Numeric.Append(new EquipmentStatValue(EquipmentStatKey.Pack(268),1)).ToArray()})};
        try{_=ReviveEquipmentGraph.Prepare(state.Setup,state.Original with{BeforeDeathJson=JsonSerializer.Serialize(before with{EquipmentJson=JsonSerializer.Serialize(time)})});}catch(NotSupportedException){refused=true;}Require(refused,"World-time coefficient silently used a guessed clock");
        var oldState=ReviveNativeActivation.State(World.FindMobile((Serial)legacy.Actor)as BaseCreature);var oldBefore=ReviveBeforeDeath.Decode(oldState.Original.BeforeDeathJson);
        var historical=ReviveBeforeDeath.Equipment(oldBefore).Select(s=>s with{CombatJson=JsonSerializer.Serialize(FrozenEquipmentEffects.Decode<FrozenCombatSnapshot>(s.CombatJson)with{Graph=null})}).ToArray();
        var oldOriginal=oldState.Original with{BeforeDeathJson=JsonSerializer.Serialize(oldBefore with{EquipmentJson=JsonSerializer.Serialize(historical)})};refused=false;
        try{_=ReviveEquipmentGraph.Prepare(oldState.Setup,oldOriginal);}catch(NotSupportedException){refused=true;}Require(refused,"Historical missing item bases were invented");
        Require(ReviveEquipmentIncoming.Prepare(oldState.Setup,oldOriginal)==oldState.Setup,"Historical old profile stopped working");
        int life=SkillHealth.Balance256(actor);refused=false;
        try{ReviveNativeActivation.Restore(rows.Select(r=>r.Actor==state.Actor?r with{Setup=r.Setup with{EquipmentGraphProfile="unknown"}}:r).ToArray());}catch(InvalidDataException){refused=true;}
        Require(refused&&ReviveNativeActivation.Snapshot().SequenceEqual(rows)&&SkillHealth.Balance256(actor)==life,"Unknown graph profile replaced roster or health");
    }
    private static void NativeTick(Mobile actor,string name)
    {
        var type=typeof(Mobile).GetNestedType(name,BindingFlags.NonPublic)??throw new InvalidOperationException("Missing pinned native resource timer");
        var timer=Activator.CreateInstance(type,actor);type.GetMethod("OnTick",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(timer,null);
    }
    private static void Recovery(PlayerMobile owner,Fixture[] fixtures)
    {
        foreach(var fixture in fixtures)
        {
            var actor=World.FindMobile((Serial)fixture.Actor)as BaseCreature;int mana=NecromancerEffects.ManaBalance256(actor),stamina=BattleOrders.Stamina256(actor);
            Require(!actor.CanRegenStam&&!actor.CanRegenMana,"Suspended original still permits native resource recovery");
            for(int tick=0;tick<12;tick++){NativeTick(actor,"StamTimer");NativeTick(actor,"ManaTimer");}
            Require(NecromancerEffects.ManaBalance256(actor)==mana&&BattleOrders.Stamina256(actor)==stamina,"Forced native timer recovered suspended resources");
        }
        var ordinary=new Skeleton{NoKillAwards=true};ordinary.ManaMaxSeed=100;ordinary.StamMaxSeed=100;ordinary.Mana=10;ordinary.Stam=10;
        try{Require(ordinary.CanRegenMana&&ordinary.CanRegenStam,"Ordinary NPC recovery was disabled");NativeTick(ordinary,"ManaTimer");NativeTick(ordinary,"StamTimer");Require(ordinary.Mana==11&&ordinary.Stam==11,"Ordinary native recovery no longer runs");}
        finally{ordinary.Delete();}
    }
    private static void Exercise(PlayerMobile owner,Fixture[] fixtures)
    {
        owner.MoveToWorld(new(6600,3600,0),Map.Trammel);foreach(var f in fixtures){Expect(f);}Refusals(fixtures[0],fixtures[2]);
        foreach(var f in fixtures)
        {
            var actor=World.FindMobile((Serial)f.Actor)as BaseCreature;SkillHealth.SetBalance256(actor,SkillHealth.Maximum256(actor)*2/3+17);
            NecromancerEffects.SetMana256(actor,NecromancerEffects.ManaMaximum256(actor)-257);BattleOrders.SetStamina256(actor,BattleOrders.StaminaMaximum256(actor)-513);Expect(f);
        }
        owner.Internalize();Recovery(owner,fixtures);
    }
    private static void Check()
    {
        var owner=PoisonDaggerChecks.Owner();owner.Name="PoisonDaggerFixtureReviveGraphOwner";MagePetBuffChecks.Train(owner,"necromancer.revive",20);MagePetBuffChecks.Train(owner,"necromancer.skeletonmastery",20);FixtureSpace.Corridor(owner,6600,24,3600);
        var fixtures=new[]{Create(owner,0,true),Create(owner,1,true),Create(owner,0,false)};
        var supplier=new PlayerMobile{Name="ReviveGraphOrders",Body=0x190,Player=true};supplier.AddItem(new Backpack());ClassCreation.Profiles.Assign(supplier,DiabloClass.Barbarian);ClassCreation.Profiles.AdvanceLevel(supplier,99);
        foreach(var f in fixtures){BattleOrders.Apply(supplier,World.FindMobile((Serial)f.Actor),3,TimedEffects.Now.Add(FixtureSpace.Retention));}Exercise(owner,fixtures);
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(owner.Serial.Value,fixtures,fixtures.Select(f=>SkillHealth.Balance256(World.FindMobile((Serial)f.Actor))).ToArray(),fixtures.Select(f=>NecromancerEffects.ManaBalance256(World.FindMobile((Serial)f.Actor))).ToArray(),fixtures.Select(f=>BattleOrders.Stamina256(World.FindMobile((Serial)f.Actor))).ToArray())));
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var owner=World.FindMobile((Serial)saved.Owner)as PlayerMobile;
        for(int i=0;i<saved.Actors.Length;i++)
        {var actor=World.FindMobile((Serial)saved.Actors[i].Actor);Require(actor is{Alive:true,Deleted:false}&&actor.Map==Map.Internal&&SkillHealth.Balance256(actor)==saved.Life[i]&&NecromancerEffects.ManaBalance256(actor)==saved.Mana[i]&&BattleOrders.Stamina256(actor)==saved.Stamina[i],"Saved graph/legacy original resource balances changed: "+JsonSerializer.Serialize(new{i,saved.Actors[i].Graph,Alive=actor?.Alive,Map=actor?.Map?.MapIndex,ExpectedLife=saved.Life[i],ActualLife=actor==null?-1:SkillHealth.Balance256(actor),ExpectedMana=saved.Mana[i],ActualMana=actor==null?-1:NecromancerEffects.ManaBalance256(actor),ExpectedStamina=saved.Stamina[i],ActualStamina=actor==null?-1:BattleOrders.Stamina256(actor),LifeMaximum=actor==null?-1:SkillHealth.Maximum256(actor),ManaMaximum=actor==null?-1:NecromancerEffects.ManaMaximum256(actor),StaminaMaximum=actor==null?-1:BattleOrders.StaminaMaximum256(actor),Orders=actor==null?-1:BattleOrders.Percent(actor)}));}
        Exercise(owner,saved.Actors);File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{Life=saved.Actors.Select(f=>SkillHealth.Balance256(World.FindMobile((Serial)f.Actor))).ToArray(),Mana=saved.Actors.Select(f=>NecromancerEffects.ManaBalance256(World.FindMobile((Serial)f.Actor))).ToArray(),Stamina=saved.Actors.Select(f=>BattleOrders.Stamina256(World.FindMobile((Serial)f.Actor))).ToArray(),Generation=saved.Generation+1}));
    }
}
