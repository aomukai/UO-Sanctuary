// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
namespace Sanctuary.Integration;

// Isolate exact recovery/save assertions from unrelated background player
// regeneration. This class is confined to disposable integration worlds.
public sealed class RecyclerFixturePlayer:PlayerMobile
{
    public RecyclerFixturePlayer(){}
    public RecyclerFixturePlayer(Serial serial):base(serial){}
    public override bool CanRegenHits=>false;
    public override bool CanRegenMana=>false;
    public override void Serialize(IGenericWriter writer){base.Serialize(writer);writer.Write(0);}
    public override void Deserialize(IGenericReader reader){base.Deserialize(reader);if(reader.ReadInt()!=0){throw new InvalidDataException("Invalid recycler fixture version");}}
}

public static class RecyclerVineChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(RecyclerVineChecks));
    private sealed record Saved(uint Owner,uint Carrion,uint Solar,RecyclerBodyState CarrionState,RecyclerBodyState SolarState,
        int CarrionLife,int SolarLife,int Map,Point3D Point);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"recycler-body-fixtures.json");
    public static void Configure()
    {Register("recycler-model-check",Models);Register("recycler-body-check",Bodies);Register("recycler-body-reload-check",Reload);}
    private static void Register(string name,Action check)=>ConsoleInputHandler.RegisterCommand(name,"Prepared recycler components and saved body",_=>Core.LoopContext.Post(()=>
    {try{check();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool b,string why){if(!b){throw new InvalidOperationException(why);}}
    private static CombatAbility Ability(int kind)=>kind==0?CombatAbility.CarrionVine:CombatAbility.SolarCreeper;
    private static SkillValues Values(CombatAbility ability,int rank,int level)
    {var row=SkillMechanics.For(ability);return new(row,key=>key==row.SourceKey?rank:0,_=>0,level);}
    private static void Models()
    {
        Require(!DruidCombat.Implemented(CombatAbility.CarrionVine)&&!DruidCombat.Implemented(CombatAbility.SolarCreeper),"Prepared components enabled learned skills");
        using var birth=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"recycler-vines-birth-source-probe.json")));int births=0;
        foreach(var row in birth.RootElement.EnumerateArray())
        {
            int N(int i)=>row[i].GetInt32();if(N(5)!=0){continue;}var expected=row[6];var ability=Ability(N(0));
            var basis=DruidRecyclerStats.Spawn(ability,(MonsterDifficulty)N(3));
            var supplied=basis with{Stats=basis.Stats with{Life256=basis.Stats.Life256+N(4)},Regeneration256=(basis.Stats.Life256+N(4))*3>>12};
            var actual=DruidRecyclerStats.Apply(Values(ability,N(2),N(1)),supplied);
            Require(actual.Level==expected[1].GetInt32()&&actual.Life256==expected[2].GetInt32()&&expected[2].GetInt32()==expected[3].GetInt32()
                &&actual.Defense==expected[4].GetInt32()&&actual.Attack==expected[5].GetInt32()&&actual.Regeneration256==expected[6].GetInt32()
                &&actual.VineAttackRank==expected[7].GetInt32()&&actual.RecyclerRank==expected[8].GetInt32()&&actual.RequestedEquipmentLevel==expected[9].GetInt32(),"Complete recycler do115/passive birth comparison");births++;
        }
        Require(births==9180,"Incomplete recycler birth coverage");Ai();Recovery();
        Log.Information("Recycler native models:9180complete shared birth cases,4705supported AI111decisions and1140valid delayed-resource callbacks pass; Solar current-slot inference remains separate");
    }
    private static void Ai()
    {
        using var data=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"recycler-vines-ai-source-probe.json")));int compared=0;
        foreach(var row in data.RootElement.EnumerateArray())
        {
            var c=row[0];int N(int i)=>c[i].GetInt32();if(N(7)==0){continue;}
            var actions=new List<int[]>();
            void Add(int a,int b=0,int d=0,int e=0,int f=0)=>actions.Add([a,b,d,e,f]);
            var result=DruidRecyclerAi.Think(new(N(7)==1?CombatAbility.CarrionVine:CombatAbility.SolarCreeper,N(0)!=0,N(1),N(19)+N(18),N(14),N(11)!=0,N(12)!=0,N(13),
                N(16)==0?0:3,N(16)==2,N(17)!=0,N(8)!=0,N(9)!=0),DruidCreeperAi.Initialize(new(777,N(19))),
                new(r=>{switch(r.Kind){case CreeperAiKind.Idle:Add(7,r.A);break;case CreeperAiKind.Walk:Add(6,r.A,r.B);break;case CreeperAiKind.Escape:Add(5,r.A,r.B,r.C);break;default:throw new InvalidOperationException("Unexpected recycler request");}},
                    (target,melee,flag,leash)=>{Add(3,target,melee?1:0,flag,leash);return N(6)!=0;},
                    (mode,run,speed,flag)=>{Add(1,mode,run?1:0,speed,flag);return N(2)!=0;},
                    range=>{Add(2,range);return N(3)!=0?new RecyclerCorpse(4,N(4),N(5)!=0):default;},
                    ()=>{Add(8,N(10));return N(10);},
                    request=>{Add(4,request.A,request.B,request.C);return N(15)!=0;}));
            var expected=row[2].EnumerateArray().Select(a=>a.EnumerateArray().Select(n=>n.GetInt32()).ToArray()).ToArray();
            Require(result.Parameter0==row[1][0].GetInt32()&&result.LastSkillFrame==row[1][1].GetInt32()
                &&actions.Count==expected.Length&&actions.Zip(expected).All(p=>p.First.SequenceEqual(p.Second)),"Complete AI111ordered-service/strict-delay comparison");compared++;
        }
        Require(compared==4705,"Incomplete supported AI111coverage");
    }
    private static void Recovery()
    {
        using var data=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"recycler-vines-source-probe.json")));int compared=0;
        foreach(var row in data.RootElement.EnumerateArray())
        {
            if(row[0].GetInt32()!=1){continue;}var c=row[1];int N(int i)=>c[i].GetInt32();if(N(5)!=0){continue;}
            var expected=row[2];var ability=Ability(N(0));
            var actual=DruidRecyclerRecovery.Gain(ability,N(1),N(2),N(3),100*256+211,N(4)!=0,N(4)!=2,RecyclerRecoveryModel.LegacySource);
            Require(actual.Changed==(expected[4].GetInt32()!=0)&&actual.After256==expected[N(0)==0?1:2].GetInt32(),"Complete source do29/do33whole/fractional resource comparison");compared++;
        }
        Require(compared==1140,"Incomplete valid source callback coverage");
        // Current table puts Solar's intended amount in calc2. Keep this named
        // provisional control separate from the unmodified legacy zero route.
        for(int rank=1;rank<=255;rank++)
        {
            var values=DruidNpcSkills.Recycler(CombatAbility.SolarCreeper,rank);
            Require(values.Eval("calc1")==0&&values.Eval("calc2")==rank+3,"Solar formula-slot conflict disappeared without review");
            var actual=DruidRecyclerRecovery.Gain(CombatAbility.SolarCreeper,rank,45,9*256+17,100*256+211,true,true,RecyclerRecoveryModel.CurrentTableSolarProvisional);
            Require(actual.Changed&&actual.After256==Math.Min(100,9+rank+3)*256,"Explicit current-table Solar inference");
        }
    }
    private static void Train(PlayerMobile p,string id)
    {
        Require(TalentCatalog.TryGet(DiabloClass.Druid,id,out var row),"Druid talent");foreach(var prerequisite in row.Prerequisites){Train(p,prerequisite);}
        if(ClassCreation.Profiles.GetRank(p.Serial,id)==0){Require(ClassCreation.Profiles.TryTrain(p.Serial,id,out _),"Train "+id);}
    }
    internal static PlayerMobile Fresh()
    {
        var p=new RecyclerFixturePlayer{Body=0x190,Player=true,Name="RecyclerVineFixture"};p.AddItem(new Backpack());ClassCreation.Profiles.Assign(p,DiabloClass.Druid);
        ClassCreation.Profiles.AdvanceLevel(p,99);Train(p,"druid.carrionvine");Train(p,"druid.solarcreeper");FixtureSpace.Corridor(p,6790,25,2100);return p;
    }
    private static void Bodies()
    {
        var p=Fresh();var at=TimedEffects.Now.AddDays(2);var point=p.Location;
        var carrion=DruidRecyclerBody.CreatePrepared(p,CombatAbility.CarrionVine,new(p.X+1,p.Y,p.Z),at);
        var solar=DruidRecyclerBody.CreatePrepared(p,CombatAbility.SolarCreeper,new(p.X+2,p.Y,p.Z),at);
        Require(carrion.MaximumLife256==24320&&solar.MaximumLife256==42240&&ClassStats.Level(carrion)==75&&ClassStats.Level(solar)==75
            &&ClassStats.Defense(carrion)==92&&ClassStats.Defense(solar)==165&&CreatureProfiles.NativeAttack(carrion)==0
            &&BattleOrders.Regeneration256(carrion)==17&&BattleOrders.Regeneration256(solar)==30,"Actual recycler body stats copied another summon");
        Require(carrion.ControlMaster==p&&solar.ControlMaster==p&&carrion.Permitted(p)&&solar.Permitted(p)&&carrion.CanBeDamaged()
            &&!carrion.Blessed&&carrion.PoisonImmune==null&&!carrion.BleedImmune&&!carrion.CanRegenHits&&carrion.ControlSlots==0,"Recycler ownership/immunity route");
        SkillHealth.SetBalance256(carrion,12007);SkillHealth.SetBalance256(solar,21007);
        carrion.AdvanceRecovery(at.AddMilliseconds(40),true);solar.AdvanceRecovery(at.AddMilliseconds(40),true);
        Require(SkillHealth.Balance256(carrion)==12024&&SkillHealth.Balance256(solar)==21037,"Actual17/30FP regeneration");
        var old=carrion.Prepared.Snapshot;var helm=NamedGear.Create(NamedGear.Catalog.Items.Single(i=>i.Name=="Tarnhelm").Id,99,new Random(943),true);
        Require(p.EquipItem(helm),"Recycler actual +skill helm");ClassGear.Refresh(p);
        Require(carrion.Prepared.Snapshot==old&&DruidRecyclerStats.Capture(p,CombatAbility.CarrionVine).Birth is{Rank:2,Life256:30400}
            &&DruidRecyclerStats.Capture(p,CombatAbility.SolarCreeper).Birth is{Rank:2,Life256:50688},"Immutable birth/fresh effective rank");
        var facets=SummonDifficulties.Facets();
        try
        {
            SummonDifficulties.SetFacets([new(Map.Trammel.MapID,MonsterDifficulty.Normal),new(Map.Felucca.MapID,MonsterDifficulty.Hell)]);
            int life=SkillHealth.Balance256(carrion);p.MoveToWorld(p.Location,Map.Felucca);
            Require(carrion.Prepared.Difficulty==MonsterDifficulty.Hell&&carrion.MaximumLife256==24320&&SkillHealth.Balance256(carrion)==life,"Facet rescale changed injury or invented a noRatio multiplier");
            p.MoveToWorld(point,Map.Trammel);
        }
        finally{SummonDifficulties.SetFacets(facets);}
        var valid=carrion.Prepared;string before=carrion.SavedText;
        foreach(var invalid in new[]{valid with{Owner=0},valid with{Profile="unknown"},valid with{Snapshot=valid.Snapshot with{Birth=valid.Snapshot.Birth with{Life256=1}}}})
        {bool rejected=false;try{carrion.RestorePrepared(invalid);}catch(InvalidDataException){rejected=true;}Require(rejected&&carrion.SavedText==before,"Invalid recycler restore changed valid bytes");}
        PersistentMinions.Suspend(p);
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,carrion.Serial.Value,solar.Serial.Value,carrion.Prepared,solar.Prepared,
            SkillHealth.Balance256(carrion),SkillHealth.Balance256(solar),p.Map.MapID,p.Location)));
        Log.Information("Recycler bodies: current95/165HP,92/165defense,17/30FP regeneration,actual Tarnhelm rank capture,facet injury and atomic restore pass; vulnerable placeholders saved,controller/exposure pending");
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);
        var carrion=(DruidRecyclerBody)World.FindMobile((Serial)saved.Carrion);var solar=(DruidRecyclerBody)World.FindMobile((Serial)saved.Solar);
        Require(carrion.ControlMaster==p&&solar.ControlMaster==p&&carrion.SavedText==JsonSerializer.Serialize(saved.CarrionState)&&solar.SavedText==JsonSerializer.Serialize(saved.SolarState)
            &&SkillHealth.Balance256(carrion)==saved.CarrionLife&&SkillHealth.Balance256(solar)==saved.SolarLife,"Actual recycler reload changed state/owner/fractional life");
        p.MoveToWorld(saved.Point,Map.Maps[saved.Map]);
        carrion.AdvanceRecovery(carrion.Prepared.NextRecoveryUtc,false);solar.AdvanceRecovery(solar.Prepared.NextRecoveryUtc,false);
        Require(SkillHealth.Balance256(carrion)==saved.CarrionLife&&SkillHealth.Balance256(solar)==saved.SolarLife,"Loaded recycler replayed recovery");
        carrion.AdvanceRecovery(carrion.Prepared.NextRecoveryUtc,true);solar.AdvanceRecovery(solar.Prepared.NextRecoveryUtc,true);
        Require(SkillHealth.Balance256(carrion)==saved.CarrionLife+17&&SkillHealth.Balance256(solar)==saved.SolarLife+30,"Resumed recycler did not heal exactly one frame");
        PersistentMinions.Suspend(p);File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{CarrionState=carrion.Prepared,SolarState=solar.Prepared,
            CarrionLife=SkillHealth.Balance256(carrion),SolarLife=SkillHealth.Balance256(solar)}));
    }
}
