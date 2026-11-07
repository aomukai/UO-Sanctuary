// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class ValkyrieInitializerChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(ValkyrieInitializerChecks));
    private sealed record Held(uint Owner,uint Body,string State,string[] Items,string Resources,NpcEquipmentSeed Random,int Life,int Maximum);
    private sealed record Saved(Held[] Bodies,NpcWorldAllocationState World,int Generation=0,
        [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]NpcWorldAllocationState WorldAtSave=null);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"valkyrie-initializer-fixtures.json");
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value);
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    public static void Configure()
    {
        Register("valkyrie-initializer-check",false);Register("valkyrie-initializer-reload-check",true);
        ConsoleInputHandler.RegisterCommand("valkyrie-initializer-seal","Seal checked shared world state immediately before the existing save",_=>Core.LoopContext.Post(()=>
        {
            try
            {
                var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var final=NpcWorldAllocation.Capture();
                Require(final==NpcWorldAllocationChecks.CheckedContinuation,"World changed after the last checked family endpoint");
                NpcWorldAllocationChecks.SealSave(final);File.WriteAllText(Manifest,Json(saved with{WorldAtSave=final}));
                Log.Information("valkyrie-initializer-seal: passed");
            }
            catch(Exception e){Log.Error(e,"valkyrie-initializer-seal: failed");}
        }));
    }
    private static void Register(string name,bool reload)=>ConsoleInputHandler.RegisterCommand(name,"Original world unit/life/equipment initialization and saves",_=>Core.LoopContext.Post(()=>
    {try{Check(reload);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void SourceControls()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"valkyrie-initializer-source-cases.json")));int all=0,matched=0;
        foreach(var row in doc.RootElement.EnumerateArray())
        {
            all++;int variant=row[4].GetInt32();if(variant is not(0 or 1)){continue;}
            var result=ValkyrieInitializer.Roll(ulong.Parse(row[0].GetString()),(MonsterDifficulty)row[1].GetInt32());var stats=row[11];
            Require(result.After==ulong.Parse(row[5].GetString())&&result.Life256==stats[13].GetInt32()&&result.RegenerationBase==stats[17].GetInt32()&&row[6].GetInt32()==1,"Initializer differs from complete source body");matched++;
        }
        Require(all==840&&matched==336,"Missing initializer source cases/zero-choice controls");
    }
    private static PlayerMobile Owner(int rank)
    {
        var p=new PlayerMobile{Name="ValkyrieInitializerFixture",Body=0x190,Player=true};p.AddItem(new Backpack());ClassCreation.Profiles.Assign(p,DiabloClass.Amazon);ClassCreation.Profiles.AdvanceLevel(p,99);
        ClassSkillChecks.Train(p,"amazon.valkyrie",rank);FixtureSpace.Corridor(p,6220,32,3340);return p;
    }
    private static string[] Items(AmazonValkyrieBody body)=>ValkyrieEquipment.Originals(body).Select(i=>i.SavedText).ToArray();
    private static Held Capture(AmazonValkyrieBody body)
        =>new(body.Prepared.Owner,body.Serial.Value,body.SavedText,Items(body),Json(GeneratedNpcLiveResources.State(body)),NpcEquipmentRandom.Require(body),SkillHealth.Balance256(body),SkillHealth.Maximum256(body));
    private static void Validate(AmazonValkyrieBody b)
    {
        var state=b.Prepared;var init=state.Initialization;var equipment=state.Equipment;
        Require(init!=null&&equipment!=null&&state.Birth.SpawnLife256==init.SpawnLife256,"Creation did not retain actual initialized life");
        ValkyrieInitializer.Validate(init,state.Birth,equipment);
        ulong world=init.WorldBefore;uint low=SourceRandom.Roll(ref world);ulong own=SourceRandom.InitLow(unchecked((int)low));uint life=SourceRandom.Roll(ref own);
        Require(world==init.WorldAfter&&init.UnitSeed==SourceRandom.InitLow(unchecked((int)low))&&own==init.AfterInitialization
            &&init.SpawnLife256==(400+life%81)*256&&init.RegenerationBase==((init.SpawnLife256*12)>>12),"Birth did not use allocated unit seed/one HP draw");
        Require(equipment.GameBefore==world&&equipment.PetBefore==own&&NpcEquipmentRandom.Require(b).Seed==equipment.PetAfter,"Creation lost world/own stream handoff");
        var resources=GeneratedNpcLiveResources.State(b);Require(resources is{Valkyrie:not null,ItemEvents:not null}&&resources.Valkyrie.RegenerationBase==init.RegenerationBase
            &&resources.Valkyrie.ManaMaximum256==0&&resources.Valkyrie.StaminaMaximum256==0,"Creation guessed resource or regen bases");
        Require(state.EquipmentDefenseProfile==ValkyrieEquipmentDefense.Profile&&state.GroundMotion==null&&state.EquipmentContactProfile==null&&b.CantWalk,"Prepared creation accidentally enabled controller/contact");
    }
    private static void Refusals(PlayerMobile p)
    {
        var world=NpcWorldAllocation.Capture();int count=World.Mobiles.Count;bool refused=false;
        try{AmazonValkyrieBody.CreateInitialized(p,new(p.X+1,p.Y,p.Z),DateTime.SpecifyKind(TimedEffects.Now,DateTimeKind.Unspecified));}catch(ArgumentException){refused=true;}
        Require(refused&&NpcWorldAllocation.Capture()==world&&World.Mobiles.Count==count,"Bad UTC allocated a native body or spent world RNG");
    }
    private static void Check(bool reload)
    {
        Require(NpcWorldAllocation.Capture()==NpcWorldAllocationChecks.CheckedContinuation,"Unexpected world draws between checked fixture families");
        SourceControls();var held=new System.Collections.Generic.List<Held>();int generation=0;
        if(reload)
        {
            var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));generation=saved.Generation+1;Require(generation<=2&&NpcWorldAllocationChecks.LoadedWorld==(saved.WorldAtSave??saved.World),"Initializer boot world differs from sealed save");
            foreach(var old in saved.Bodies)
            {
                var b=World.FindMobile((Serial)old.Body)as AmazonValkyrieBody;Validate(b);var current=Capture(b);
                Require(Json(current)==Json(old)&&b.Map==Map.Internal&&b.ControlMaster?.Serial.Value==old.Owner,"Loaded initialization/items/resources/actor RNG/wounds changed");held.Add(current);
            }
        }
        else
        {
            var facets=SummonDifficulties.Facets();
            try
            {
                foreach(int rank in new[]{1,5,10,20})foreach(var difficulty in Enum.GetValues<MonsterDifficulty>())
                {
                    var p=Owner(rank);SummonDifficulties.SetFacets(facets.Where(f=>f.Map!=p.Map.MapID).Append(new(p.Map.MapID,difficulty)).ToArray());Refusals(p);
                    var beforeWorld=NpcWorldAllocation.Capture();var b=AmazonValkyrieBody.CreateInitialized(p,new(p.X+1,p.Y,p.Z),TimedEffects.Now);Validate(b);
                    Require(b.Prepared.Initialization.WorldBefore==(beforeWorld?.Seed??SourceRandom.InitLow(unchecked((int)NpcWorldAllocation.Capture().InitialLow))),"Creation broke the shared world sequence");
                    Require(b.Prepared.Birth.Rank==rank&&b.Prepared.Initialization.Difficulty==difficulty&&NpcWorldAllocation.Capture().Seed==b.Prepared.Equipment.GameAfter,"Created body lost captured rank/difficulty/world continuation");
                    var before=b.Prepared;string text=b.SavedText;bool refused=false;
                    try{b.RestorePrepared(before with{Initialization=before.Initialization with{AfterInitialization=before.Initialization.AfterInitialization^1}});}catch(InvalidDataException){refused=true;}
                    Require(refused&&b.SavedText==text,"Corrupt initializer changed live body before refusal");
                    SkillHealth.SetBalance256(b,SkillHealth.Maximum256(b)-257);PersistentMinions.Suspend(p);p.Internalize();held.Add(Capture(b));
                }
            }
            finally{SummonDifficulties.SetFacets(facets);}
            Require(held.Count==12,"Missing rank/difficulty initialized creations");
        }
        NpcWorldAllocationChecks.Checked(NpcWorldAllocation.Capture());
        File.WriteAllText(Manifest,Json(new Saved(held.ToArray(),NpcWorldAllocation.Capture(),generation)));
        Log.Information("Valkyrie initializer:840source contexts/336zero-choice native comparisons;12actual rank/difficulty world creations,allocated own HP roll then unfiltered equipment,derived zero resource bases/regen,exact world/own/item streams,atomic preflight/witness guards;original initialized bodies/items/resources/wounds through two reloads;controller/contact/cast and outer current callbacks remain separate");
    }
}
