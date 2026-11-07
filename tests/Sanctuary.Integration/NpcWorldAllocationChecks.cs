// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class NpcWorldAllocationChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(NpcWorldAllocationChecks));
    private sealed record Saved(uint Owner,Point3D Point,uint Body,string BodyState,uint EquippedBody,string EquippedState,uint Item,string ItemState,
        NpcWorldAllocationState World,NpcEquipmentSeed Actor,ChainFlight Flight,uint[] Targets,int[] Lives,int Generation=0,
        [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]NpcWorldAllocationState WorldAtSave=null);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"npc-world-allocation-fixtures.json");
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    internal static NpcWorldAllocationState LoadedWorld{get;private set;}
    internal static NpcWorldAllocationState CheckedContinuation{get;private set;}
    // Server.Initialize runs after World.Load. Keep this immutable boot witness
    // separate from subsequent legitimate draws by other fixture families.
    public static void Initialize(){LoadedWorld=CheckedContinuation=NpcWorldAllocation.Capture();}
    internal static void Checked(NpcWorldAllocationState state)
    {Require(NpcWorldAllocation.Capture()==state,"Checked world endpoint differs from live continuation");CheckedContinuation=state;}
    internal static void SealSave(NpcWorldAllocationState final)
    {
        Require(NpcWorldAllocation.Capture()==final&&CheckedContinuation==final,"Unexpected allocation after the checked family endpoints");
        if(!File.Exists(Manifest)){return;}
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));
        File.WriteAllText(Manifest,Json(saved with{WorldAtSave=final}));
    }
    public static void Configure(){Register("npc-world-allocation-check",false);Register("npc-world-allocation-reload-check",true);}
    private static void Register(string name,bool reload)=>ConsoleInputHandler.RegisterCommand(name,"Persistent shared world item/root/child allocation",_=>Core.LoopContext.Post(()=>
    {try{Check(reload);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static string Json<T>(T x)=>JsonSerializer.Serialize(x);
    private static ulong Advance(ulong seed,int count){for(int i=0;i<count;i++){SourceRandom.Roll(ref seed);}return seed;}
    private static void SourceControls()
    {
        var old=NpcWorldAllocation.Capture();
        try
        {
            using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"npc-world-allocation-source-cases.json")));int contexts=0;
            int[][] sequences=[[3],[3,3,3],[4,3,4,3],[3,4,3,4,3]];
            foreach(var c in doc.RootElement.GetProperty("allocations").EnumerateArray())
            {
                ulong game=ulong.Parse(c[0].GetString());bool world=game!=0;
                if(world){NpcWorldAllocation.Restore(new(0,NpcWorldAllocation.Profile,1,game));}
                int index=0;
                foreach(var row in c[6].EnumerateArray())
                {
                    int kind=sequences[c[1].GetInt32()][index++];if(c[5].GetInt32()!=0){Require(row.ValueKind==JsonValueKind.Null,"Source failure row");continue;}
                    if(kind==3)
                    {
                        uint low=SourceRandom.Roll(ref game);ulong seed=world?NpcWorldAllocation.UnitSeed():SourceRandom.InitLow(unchecked((int)low));
                        Require(seed==ulong.Parse(row[3].GetString())&&unchecked((uint)seed)==low,"Persistent unit seed differs from complete source");
                    }
                    else
                    {
                        var a=NpcItemAllocations.Take(ref game);var actual=world?NpcItemAllocations.Take(ref NpcWorldAllocation.RequireStream().Value):a;
                        Require(actual==a&&a.UnitStart==row[2].GetUInt32()&&a.ItemStart==row[4].GetUInt32(),"Shared item draw order differs from source");
                    }
                }
                Require(game==ulong.Parse(c[7].GetString())&&(!world||NpcWorldAllocation.Capture().Seed==game),"Mixed world continuation differs from source");contexts++;
            }
            Require(contexts==960,"Missing complete mixed allocation contexts");
            // A retained suite may already have allocated through this real
            // service. Check fresh-world bootstrap in a scoped empty context.
            var seedLow=NpcWorldAllocation.SeedLow;
            try
            {
                int calls=0;NpcWorldAllocation.Restore(null);NpcWorldAllocation.SeedLow=()=>{calls++;return 7;};
                Require(NpcWorldAllocation.Capture()==null,"Unused world eagerly initialized");
                var first=NpcWorldAllocation.RequireStream();Require(calls==1&&first.Value==SourceRandom.InitLow(7)&&ReferenceEquals(first,NpcWorldAllocation.RequireStream())&&calls==1,"World bootstrap ran more than once");
            }
            finally{NpcWorldAllocation.SeedLow=seedLow;}
            NpcWorldAllocation.Restore(new(0,NpcWorldAllocation.Profile,7,SourceRandom.InitLow(7)));var before=NpcWorldAllocation.Capture();var stream=NpcWorldAllocation.RequireStream();
            foreach(var bad in new[]{before with{Version=1},before with{Profile="unknown"},before with{Seed=0}})
            {
                bool refused=false;try{NpcWorldAllocation.Restore(bad);}catch(InvalidDataException){refused=true;}
                Require(refused&&NpcWorldAllocation.Capture()==before&&ReferenceEquals(stream,NpcWorldAllocation.RequireStream()),"Bad world state changed the live stream");
            }
        }
        finally{NpcWorldAllocation.Restore(old);}
    }
    private static void Spells(AmazonValkyrieBody b,BaseCreature target)
    {
        foreach(int skill in new[]{36,38,44,48,53})
        {
            var before=NpcWorldAllocation.Capture();var actor=NpcEquipmentRandom.Require(b);int count=skill is 44 or 48?64:skill==38?5:1;DateTime at=TimedEffects.Now.AddDays(2);
            Require(NpcItemSpellCasts.CastOriginal(b,NpcItemTarget.Capture(target),skill,3,1,at,r=>NpcOriginalItemProcs.Allocator(b,r,at)),"Actual root did not cast through installed provider");
            Require(NpcWorldAllocation.Capture().Seed==Advance(before.Seed,count)&&NpcEquipmentRandom.Require(b)==actor,"Actual roots borrowed actor RNG or consumed extra world draws");
            SorceressSpellMissiles.Cancel(b);ChainLightning.Cancel(b);
        }
        var state=NpcWorldAllocation.Capture();bool refused=false;
        try{NpcWorldAllocation.Root(b,new(58,36,0,b.X*2,b.Y*2,target.X*2,target.Y*2,33,0,0),TimedEffects.Now);}catch(NotSupportedException){refused=true;}
        Require(refused&&NpcWorldAllocation.Capture()==state,"Bad root spent world RNG");
        DateTime time=TimedEffects.Now.AddDays(2);
        Require(NpcItemSpellCasts.CastOriginalPosition(b,new(b.X+60,b.Y,b.Z),36,3,1,time,r=>NpcOriginalItemProcs.Allocator(b,r,time))&&NpcWorldAllocation.Capture()==state
            &&!SorceressSpellMissiles.Snapshot().Flights.Any(f=>f.Npc?.Actor==b.Serial.Value),"Range factory refusal changed shared attempt return,allocated RNG or published a flight");
    }
    private static ChainFlight Hop(AmazonValkyrieBody b,ChainFlight flight)
    {
        int ordinal=flight.OriginalNpc.Ordinal;var before=NpcWorldAllocation.Capture();
        for(int i=0;i<26;i++)
        {
            ChainLightning.Tick(flight.NextUtc,true,flight.Cast);
            flight=ChainLightning.Snapshot().SingleOrDefault(f=>f.Cast==flight.Cast);
            Require(flight!=null,"World chain ended before its next child");
            if(flight.OriginalNpc.Ordinal==ordinal){Require(NpcWorldAllocation.Capture()==before,"World stream advanced before a child allocation");continue;}
            Require(flight.OriginalNpc.Ordinal==ordinal+1&&NpcWorldAllocation.Capture().Seed==Advance(before.Seed,1),"Child did not consume exactly one world draw");
            ulong copy=before.Seed;uint low=SourceRandom.Roll(ref copy);Require(flight.OriginalNpc.Seed==SourceRandom.InitLow(unchecked((int)low)),"Child lost allocated seed");return flight;
        }
        throw new InvalidOperationException("World chain did not reach a real contact/child");
    }
    private static void Check(bool reload)
    {
        SourceControls();PlayerMobile p;AmazonValkyrieBody b,equipped;GeneratedNpcItem item;BaseCreature[] targets;int generation=0;ChainFlight flight;Point3D point;
        var oldSeed=NpcWorldAllocation.SeedLow;
        try
        {
            if(reload)
            {
                var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));generation=saved.Generation+1;Require(generation<=2&&LoadedWorld==(saved.WorldAtSave??saved.World)&&NpcWorldAllocation.Capture()==CheckedContinuation,"World persistence/checked boot continuation differs");
                NpcWorldAllocation.SeedLow=()=>throw new InvalidOperationException("Reload reseeded world");
                p=World.FindMobile((Serial)saved.Owner)as PlayerMobile;b=World.FindMobile((Serial)saved.Body)as AmazonValkyrieBody;equipped=World.FindMobile((Serial)saved.EquippedBody)as AmazonValkyrieBody;item=World.FindItem((Serial)saved.Item)as GeneratedNpcItem;point=saved.Point;
                Require(b?.SavedText==saved.BodyState&&equipped?.SavedText==saved.EquippedState&&item?.SavedText==saved.ItemState&&item.Parent==equipped&&NpcEquipmentRandom.Require(b)==saved.Actor,"World reload recreated original body/item/actor RNG");
                flight=ChainLightning.Snapshot().Single(f=>f.Cast==saved.Flight.Cast);Require(Json(flight)==Json(saved.Flight),"Saved world-allocated child changed");
                targets=saved.Targets.Select(s=>World.FindMobile((Serial)s)as BaseCreature).ToArray();Require(targets.Select(SkillHealth.Balance256).SequenceEqual(saved.Lives),"Saved chain wounds changed");
                p.MoveToWorld(point,Map.Trammel);NpcItemChainChecks.Place(b,p);flight=Hop(b,flight);
            }
            else
            {
                bool fresh=NpcWorldAllocation.Capture()==null;int calls=0;NpcWorldAllocation.SeedLow=()=>{calls++;return 0x12345678;};
                var game=NpcWorldAllocation.RequireStream();Require(calls==(fresh?1:0)&&ReferenceEquals(game,NpcWorldAllocation.RequireStream()),"World initialized more than once");
                p=new PlayerMobile{Name="NpcWorldAllocationFixture",Body=0x190,Player=true};p.AddItem(new Backpack());ClassCreation.Profiles.Assign(p,DiabloClass.Amazon);ClassCreation.Profiles.AdvanceLevel(p,99);ClassSkillChecks.Train(p,"amazon.valkyrie",20);FixtureSpace.Corridor(p,6250,32,3200);point=p.Location;
                using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"valkyrie-damage-cases.json")));b=ValkyrieDamageChecks.Body(p,doc.RootElement.EnumerateArray().First(c=>c.GetProperty("Critical").GetInt32()==1));NpcItemChainChecks.Place(b,p);
                equipped=AmazonValkyrieBody.CreatePrepared(p,new(p.X-1,p.Y,p.Z),400*256,TimedEffects.Now.AddDays(2));
                var before=NpcWorldAllocation.Capture();Require(GeneratedNpcEquipment.TryCreateWorldAllocated(equipped,p,EquipMonster.Other,"cap",20,2,0,1,out item),"World allocated item did not attach");
                Require(NpcWorldAllocation.Capture().Seed==Advance(before.Seed,2)&&item.Snapshot.Allocation.GameBefore==before.Seed&&item.Parent==equipped,"Item borrowed another allocation stream");
                targets=Enumerable.Range(0,5).Select(i=>(BaseCreature)NpcItemChainChecks.Target(b,4+i*2)).ToArray();Spells(b,targets[0]);
                before=NpcWorldAllocation.Capture();flight=ChainLightning.PrepareValkyrie(b,targets[0].Location,3,TimedEffects.Now.AddDays(2),ChainLightning.OriginalNpcAllocator);ChainLightning.CommitValkyrie(flight);
                Require(NpcWorldAllocation.Capture().Seed==Advance(before.Seed,1),"Chain root did not use world stream");flight=Hop(b,flight);Require(calls==(fresh?1:0),"Item/root/child generation reseeded world");
            }
            var actor=NpcEquipmentRandom.Require(b);var world=NpcWorldAllocation.Capture();Checked(world);var held=new Saved(p.Serial.Value,point,b.Serial.Value,b.SavedText,equipped.Serial.Value,equipped.SavedText,item.Serial.Value,item.SavedText,world,actor,flight,targets.Select(t=>t.Serial.Value).ToArray(),targets.Select(SkillHealth.Balance256).ToArray(),generation);
            Require(flight.OriginalNpc.Ordinal==generation+1&&flight.Remaining==4-generation&&targets.Any(t=>SkillHealth.Balance256(t)<65000*256),"World child did not damage a real recipient");
            PersistentMinions.Suspend(p);p.Internalize();File.WriteAllText(Manifest,Json(held));
            Log.Information("World allocation:960complete mixed source contexts;one persistent world stream,independent actor/item/property/missile streams,actual equipped item/five root spells/natural path child beforeHP,range/domain guards,lazy host entropy once,exact world/item/child/HP continuation through two reloads;no historical global chronology or learned initializer/AI claim");
        }
        finally{NpcWorldAllocation.SeedLow=oldSeed;}
    }
}
