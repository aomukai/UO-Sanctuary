// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class ShadowInitializerChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(ShadowInitializerChecks));
    private sealed record Held(uint Actor,ShadowInitialization Initialization,int Rank,uint[] Items,string[] Payloads,
        EquipmentStatValue[] Numeric,ShadowCreationStats Writes,int NativeLife,int NativeMaximum,ulong PetAfter,ulong WorldAfter);
    private sealed record Saved(uint Owner,Held[] Holders,int Generation=0);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"shadow-initializer-fixtures.json");
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value);
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static void Reject(Action action)
    {try{action();}catch(Exception e)when(e is ArgumentException or InvalidDataException or NotSupportedException or InvalidOperationException){return;}throw new Exception("Unsupported Shadow context accepted");}
    public static void Configure()
    {
        Register("shadow-initializer-check",false);Register("shadow-initializer-reload-check",true);
        EventSink.WorldLoad+=()=>
        {
            if(!File.Exists(Manifest)){return;}
            foreach(var held in JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest)).Holders)
            {if(World.FindMobile((Serial)held.Actor)is BaseCreature b){b.AIObject?.AITimer.Stop();}}
        };
    }
    private static void Register(string name,bool reload)=>ConsoleInputHandler.RegisterCommand(name,"Prepared Shadow initialization/numeric original-item graphs;no Shadow actor/cast",_=>Core.LoopContext.Post(()=>
    {try{Check(reload);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static SkillValues Values(CombatAbility ability,int rank,int level)
    {var d=SkillMechanics.For(ability);return new(d,key=>key==d.SourceKey?rank:0,_=>0,level);}
    private static void SourceControls()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"shadow-initializer-source-cases.json")));int count=0;
        foreach(var row in doc.RootElement.EnumerateArray())
        {
            int monster=row[0].GetInt32();var ability=monster==417?CombatAbility.ShadowWarrior:CombatAbility.ShadowMaster;
            var actual=ShadowInitializer.Roll(ability,ulong.Parse(row[1].GetString()),(MonsterDifficulty)row[2].GetInt32());
            Require(actual.After==ulong.Parse(row[6].GetString())&&row[7].GetInt32()==2,"Whole source initializer RNG ordering");
            Require(Json(actual.Stats.Select(s=>s.Value))==row[12].GetRawText()&&Json(actual.Components)==row[13].GetRawText(),"Whole source initializer base writes/components");
            Require(Json(actual.Skills.Select(s=>new[]{s.Id,s.Rank,s.Mode}))==row[14].GetRawText(),"Whole source initializer skill/mode requests");
            Require(actual.Skills.Select(s=>s.Sequence).SequenceEqual(new[]{49,56,49,49,53,49,-1}),"Current initial sequence metadata");count++;
        }
        Require(count==1344,"Missing complete Shadow initializer contexts");
        // Returned arrays never expose the lazy table's canonical profile.
        var first=ShadowInitializer.Roll(CombatAbility.ShadowWarrior,1,MonsterDifficulty.Normal);first.Components[1]=9;first.Skills[0]=new(0,99,0,0);first.Stats[0]=new("level",99);
        var next=ShadowInitializer.Roll(CombatAbility.ShadowWarrior,1,MonsterDifficulty.Normal);
        Require(next.Components.All(n=>n==0)&&next.Skills[0]==new ShadowInitialSkill(259,1,14,49)&&next.Stats[0]==new SummonStat("level",0),"Initializer leaked mutable source profiles");
    }
    private static (GeneratedNpcStatGraph Graph,ShadowCreationStats Writes) Build(BaseCreature actor,ShadowInitialization init,int rank)
    {
        ShadowCreationStats writes=null;var owner=Values(init.Ability,rank,99);var pet=Values(init.Ability,rank,99);
        var graph=new GeneratedNpcStatGraph(actor,ShadowInitializer.Basis(init,99),_=>null,(_,_,_,_,_)=>{},false,_=>{},_=>{},
            beforeReplay:g=>writes=ShadowBirthGraph.Apply(owner,pet,g,_=>0,_=>0));
        Require(graph.Read(12)==99&&EquipmentStatValues.Base(graph.Actor.Stats,EquipmentStatKey.Pack(31))==196
            &&EquipmentStatValues.Base(graph.Actor.Stats,EquipmentStatKey.Pack(19))==0,"Shadow borrowed Valkyrie level-based AC/AR additions");
        Require(writes.Modifier==42&&graph.Read(36)==40,"Shadow source modifier request/physical resistance lost");
        Require(graph.Actor.Stats.BaseValues.Single(v=>v.Stat==7).Value==96256+(int)(96256L*15*(rank-1)/100)
            &&writes.MaximumLifeWrite256==writes.CurrentLifeWrite256,"Source initialized life did not feed current birth helper");
        Require(!GeneratedNpcLiveResources.Handles(actor),"Prepared numeric graph activated unqualified live resources");
        return(graph,writes);
    }
    private static Held Capture(BaseCreature actor,ShadowInitialization init,int rank,ulong pet,ulong world)
    {
        int life=actor.Hits,max=actor.HitsMax;var result=Build(actor,init,rank);var items=GeneratedNpcEquipment.Items(actor);
        Require(result.Graph.ActiveItems.SequenceEqual(items.Select(i=>i.Serial.Value)),"Posted birth list confused original active inventory order");
        Require(actor.Hits==life&&actor.HitsMax==max,"Prepared numeric graph changed native balances/maxima");
        return new(actor.Serial.Value,init,rank,items.Select(i=>i.Serial.Value).ToArray(),items.Select(i=>i.SavedText).ToArray(),result.Graph.Stats,result.Writes,life,max,pet,world);
    }
    private static Held Create(PlayerMobile owner,CombatAbility ability,int rank,MonsterDifficulty difficulty)
    {
        var before=NpcWorldAllocation.Capture();var init=ShadowInitializer.Allocate(ability,difficulty);ShadowInitializer.Validate(init);
        ulong expected=init.WorldBefore;uint low=SourceRandom.Roll(ref expected);ulong own=SourceRandom.InitLow(unchecked((int)low));SourceRandom.Roll(ref own);SourceRandom.Roll(ref own);
        Require(init.WorldBefore==(before?.Seed??SourceRandom.InitLow(unchecked((int)NpcWorldAllocation.Capture().InitialLow)))
            &&expected==init.WorldAfter&&own==init.AfterInitialization&&init.SpawnLife256==96256&&init.RegenerationBase==(ability==CombatAbility.ShadowWarrior?70:47),"Actual world allocation/own two-draw handoff");
        var b=new SkeletonGuardian{Name="Shadow numeric storage [test fixture]",Frozen=true,CantWalk=true,ControlSlots=0};b.Initialize(owner,2,0);Require(b.SetControlMaster(owner),"Shadow fixture owner");PersistentMinions.Profiles.Track(b);b.AIObject?.AITimer.Stop();b.Internalize();
        var empty=Build(b,init,rank);
        Require(empty.Graph.Read(19)==rank*40&&empty.Graph.Read(0)==rank*10&&empty.Graph.Read(2)==rank*10&&empty.Graph.Read(7)==empty.Writes.MaximumLifeWrite256,"Current birth graph missing aura/vital writes");
        Require(empty.Graph.Read(39)==empty.Writes.Passives.Single(s=>s.Name=="fireresist").Value,"Current resistance list was not applied");
        Reject(()=>ShadowBirthGraph.Apply(Values(ability,rank,99),Values(ability,rank,99),empty.Graph,_=>0,_=>0));
        var unknown=new EquipmentStatNode{OwnerKind=1};EquipmentStatGraph.Post(empty.Graph.Actor,unknown,true,empty.Graph.Values);
        Reject(()=>{_=empty.Graph.ActiveItems;});EquipmentStatGraph.Detach(unknown,empty.Graph.Values);
        var pet=new SourceSeedStream(init.AfterInitialization);var game=NpcWorldAllocation.RequireStream();var attempts=new List<SummonEquipmentRequest>();
        ShadowEquipment.Select(ability,rank,ref pet.Value,r=>
        {
            attempts.Add(r);Require(r.NoSockets&&r.NoEthereal&&r.NoSell&&r.RestoreDurability,"Shadow item flags");
            return GeneratedNpcEquipment.TryCreateAllocated(b,owner,ability==CombatAbility.ShadowWarrior?EquipMonster.ShadowWarrior:EquipMonster.ShadowMaster,
                r.Code,r.ItemLevel,r.Quality,(int)difficulty,r.Slot,game,out _);
        },slot=>slot switch{1=>"cap",3=>"qui",_=>throw new NotSupportedException("Unprovided fixture owner base")});
        var items=GeneratedNpcEquipment.Items(b);
        Require(items.Length>=2&&items.Count(i=>i.Snapshot.Equipment.Body is 4 or 5)==2&&items.All(i=>!i.Snapshot.Graph.Ethereal),"Original Shadow claws/equipment were not delivered");
        ulong replay=init.AfterInitialization;var expectedRequests=new List<SummonEquipmentRequest>();
        ShadowEquipment.Select(ability,rank,ref replay,r=>{expectedRequests.Add(r);return items.Any(i=>i.Snapshot.Equipment.Body==r.Slot);},slot=>slot==1?"cap":"qui");
        Require(replay==pet.Value&&attempts.SequenceEqual(expectedRequests)&&game.Value!=init.WorldAfter,"Original loadout stream/order continuation");
        b.Hits=Math.Max(1,b.HitsMax-3);return Capture(b,init,rank,pet.Value,game.Value);
    }
    private static void Check(bool reload)
    {
        Require(NpcWorldAllocation.Capture()==NpcWorldAllocationChecks.CheckedContinuation,"Unexpected world draws before Shadow fixtures");
        SourceControls();var held=new List<Held>();PlayerMobile owner;int generation=0;
        if(reload)
        {
            var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));generation=saved.Generation+1;Require(generation<=2,"Shadow reload generation");
            owner=World.FindMobile((Serial)saved.Owner)as PlayerMobile;Require(owner is{Alive:true,Deleted:false},"Original fixture owner lost");
            foreach(var old in saved.Holders)
            {
                var actor=World.FindMobile((Serial)old.Actor)as BaseCreature;Require(actor is{Alive:true,Deleted:false}&&actor.ControlMaster==owner&&actor.Map==Map.Internal,"Original storage actor/owner changed");
                var current=Capture(actor,old.Initialization,old.Rank,old.PetAfter,old.WorldAfter);
                Require(Json(current)==Json(old),"Original items/seed witness/numeric graph/native wounds changed on reload");held.Add(current);
            }
        }
        else
        {
            owner=new PlayerMobile{Name="ShadowInitializerOwnerFixture",Body=0x190,Player=true};ClassCreation.Profiles.Assign(owner,DiabloClass.Necromancer);ClassCreation.Profiles.AdvanceLevel(owner,99);ClassSkillChecks.Train(owner,"necromancer.raiseskeleton",2);
            foreach(var ability in new[]{CombatAbility.ShadowWarrior,CombatAbility.ShadowMaster})foreach(var difficulty in Enum.GetValues<MonsterDifficulty>())foreach(int rank in new[]{1,10,20,40})
            {held.Add(Create(owner,ability,rank,difficulty));}
            Require(held.Count==24,"Missing Shadow skill/rank/difficulty original item fixtures");owner.Internalize();
        }
        var world=NpcWorldAllocation.Capture();Reject(()=>ShadowInitializer.Allocate(CombatAbility.BasicAttack,MonsterDifficulty.Normal));Reject(()=>ShadowInitializer.Allocate(CombatAbility.ShadowWarrior,(MonsterDifficulty)3));
        Require(NpcWorldAllocation.Capture()==world,"Invalid initializer spent a shared world draw");
        var sample=held[0].Initialization;Reject(()=>ShadowInitializer.Validate(sample with{AfterInitialization=sample.AfterInitialization^1}));Reject(()=>ShadowInitializer.Basis(sample,0));
        Require(!AssassinCombat.Implemented(CombatAbility.ShadowWarrior)&&!AssassinCombat.Implemented(CombatAbility.ShadowMaster),"Prepared initialization enabled incomplete learned Shadows");
        NpcWorldAllocationChecks.Checked(NpcWorldAllocation.Capture());File.WriteAllText(Manifest,Json(new Saved(owner.Serial.Value,held.ToArray(),generation)));
        Log.Information("Shadow initializer:1344whole older-source contexts with current tables;24actual unit allocations and unfiltered original-item storage fixtures;current birth helper applied before item replay;two own draws,40physical resistance,196AC/zeroAR bases,initial skill ranks1/4/8;original native items/numeric graph and untouched native wounds through two reloads;no live Shadow actor/resources/AI/UMod death action or learned cast");
    }
}
