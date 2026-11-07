// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class ValkyrieStartupChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(ValkyrieStartupChecks));
    private sealed record Held(uint Owner,uint Body,string Text,string[] Items,string Resources,NpcEquipmentSeed Random,int Life);
    private sealed record Saved(Held[] Bodies,int Generation=0);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"valkyrie-startup-fixtures.json");
    private static string Json<T>(T v)=>JsonSerializer.Serialize(v);
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    public static void Configure(){Register("valkyrie-startup-check",false);Register("valkyrie-startup-reload-check",true);}
    private static void Register(string name,bool reload)=>ConsoleInputHandler.RegisterCommand(name,"Derived initialized Valkyrie startup and full source mode/rate controls",_=>Core.LoopContext.Post(()=>
    {try{Check(reload);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static Held Capture(AmazonValkyrieBody b)=>new(b.ControlMaster.Serial.Value,b.Serial.Value,b.SavedText,ValkyrieEquipment.Originals(b).Select(i=>i.SavedText).ToArray(),Json(GeneratedNpcLiveResources.State(b)),NpcEquipmentRandom.Require(b),SkillHealth.Balance256(b));
    private static PlayerMobile Owner(int rank)
    {
        var p=new PlayerMobile{Name="ValkyrieStartupFixture",Body=0x190,Player=true};p.AddItem(new Backpack());ClassCreation.Profiles.Assign(p,DiabloClass.Amazon);ClassCreation.Profiles.AdvanceLevel(p,99);ClassSkillChecks.Train(p,"amazon.valkyrie",rank);FixtureSpace.Corridor(p,6220,32,3180);return p;
    }
    private static void SourceControls()
    {
        using var oracle=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"valkyrie-startup-source-cases.json")));
        using var raw=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"valkyrie-damage-cases.json")));
        var cases=raw.RootElement.EnumerateArray().Where(c=>c.GetProperty("Critical").GetInt32()==1).ToArray();var owner=Owner(20);int contexts=0,steps=0,compared=0;
        try
        {
            foreach(var c in cases)
            {
                var b=ValkyrieDamageChecks.Body(owner,c);
                try
                {
                    string before=b.SavedText;var random=NpcEquipmentRandom.Require(b);bool refused=false;
                    try{ValkyrieGroundMotion.ActivateInitialized(b,TimedEffects.Now.AddHours(2));}catch(NotSupportedException){refused=true;}
                    Require(refused&&b.SavedText==before&&NpcEquipmentRandom.Require(b)==random,"Supplied birth silently acquired an initialized source startup");
                    foreach(var row in oracle.RootElement.EnumerateArray().Where(r=>r.GetProperty("Rank").GetInt32()==c.GetProperty("Rank").GetInt32()&&r.GetProperty("Difficulty").GetInt32()==c.GetProperty("Difficulty").GetInt32()))
                    {
                        contexts++;
                        foreach(var step in row.GetProperty("Steps").EnumerateArray())
                        {
                            steps++;int mode=step[4].GetInt32();if(mode==1){continue;}
                            int rate=row.GetProperty("BaseAttack").GetInt32(),move=row.GetProperty("BaseMove").GetInt32()+(mode==15?50:0);
                            var plan=ValkyrieEquipmentAnimation.Plan(b,mode,rate,move);
                            Require(plan.Speed==step[8].GetInt32(),"Full mode/start/rate source speed differs");
                            if(mode==4)
                            {
                                var expected=step[15].EnumerateArray().Select(e=>new SourceAnimationEvent(e[0].GetInt32(),e[1].GetInt32(),e[2].GetInt32(),e[3].GetInt32())).ToArray();
                                Require(Json(plan.Events)==Json(expected)&&plan.FrameBonus==2,"Full startup reset lost source action/end events or frame bonus");
                            }
                            else{Require(plan.Velocity==step[9].GetInt32(),"Full mode-entry movement velocity differs");}
                            compared++;
                        }
                    }
                    Require(b.SavedText==before&&NpcEquipmentRandom.Require(b)==random,"Read-only startup/source controls changed original state or RNG");
                }
                finally{b.Delete();}
            }
        }
        finally{owner.Delete();}
        Require(contexts==456&&steps==5016&&compared==4104&&ValkyrieStartup.Rates==(100,75,125),"Missing full startup contexts or derived defaults");
    }
    private static IronGolemWorldState State(AmazonValkyrieBody b)=>b.Ground.State.Controller;
    private static void Through(AmazonValkyrieBody b,int frame,bool actions=true)
    {
        var s=State(b);IronGolemWorld.Advance(b,s.NextUtc.AddMilliseconds(40L*(frame-s.Frame-1)),actions);
        Require(!b.Deleted&&b.Alive&&State(b).Fault==null,"Initialized source controller fault");
    }
    private static void WalkRun(AmazonValkyrieBody b)
    {
        foreach(bool run in new[]{true,false,true,false})
        {
            var rng=NpcEquipmentRandom.Require(b);Require(IronGolemWorld.MovePrepared(b,new(b.X*2+8,b.Y*2),run),"Derived initialized movement path refused");
            var s=State(b);var plan=ValkyrieEquipmentAnimation.Plan(b,run?15:2,0,run?125:75);
            Require(s.Mode==(run?15:2)&&s.Motion.Velocity==plan.Velocity&&ValkyrieGroundMotion.Speed(b,0,run)==(plan.Velocity,plan.Speed),"Derived run modifier stacked or survived walk transition");
            Through(b,s.Frame+1);Require(NpcEquipmentRandom.Require(b)==rng,"Movement-rate transition consumed actor RNG");b.Ground.Store(IronGolemWorld.Park(State(b)));
        }
    }
    private static void Refusals(AmazonValkyrieBody b)
    {
        var original=b.Prepared;string before=b.SavedText;var random=NpcEquipmentRandom.Require(b);var world=NpcWorldAllocation.Capture();
        foreach(var bad in new[]{original with{Initialization=null},original with{GroundMotion=original.GroundMotion with{AttackRate=99}},original with{GroundMotion=original.GroundMotion with{RunVelocityPercent=175}},original with{GroundMotion=original.GroundMotion with{StartupProfile="unknown"}}})
        {
            bool refused=false;try{b.RestorePrepared(bad);}catch(InvalidDataException){refused=true;}
            Require(refused&&b.SavedText==before&&NpcEquipmentRandom.Require(b)==random&&NpcWorldAllocation.Capture()==world,"Corrupt startup provenance changed body/streams before refusal");
        }
    }
    private static void Check(bool reload)
    {
        Require(NpcWorldAllocation.Capture()==NpcWorldAllocationChecks.CheckedContinuation,"Unexpected world draws before initialized startup checks");
        SourceControls();NpcExpandedItemSpellChecks.Packets();var held=new List<Held>();int generation=0;
        if(reload)
        {
            var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));generation=saved.Generation+1;Require(generation<=2&&saved.Bodies.Length==12,"Startup reload count/roster");
            foreach(var h in saved.Bodies)
            {
                var b=World.FindMobile((Serial)h.Body)as AmazonValkyrieBody;Require(b is{Alive:true,Deleted:false}&&b.Map==Map.Internal&&Json(Capture(b))==Json(h),"Initialized startup/items/wounds/resources/RNG changed on load");
                ValkyrieStartup.Validate(b.Prepared);var before=Capture(b);Through(b,State(b).Frame+1,false);Require(SkillHealth.Balance256(b)==before.Life&&NpcEquipmentRandom.Require(b)==before.Random,"Offline loaded startup replayed RNG or healed");held.Add(Capture(b));
            }
        }
        else
        {
            var facets=SummonDifficulties.Facets();
            try
            {
                foreach(int rank in new[]{1,5,10,20})foreach(var difficulty in Enum.GetValues<MonsterDifficulty>())
                {
                    var p=Owner(rank);SummonDifficulties.SetFacets(facets.Where(f=>f.Map!=p.Map.MapID).Append(new(p.Map.MapID,difficulty)).ToArray());var at=TimedEffects.Now.AddHours(2);
                    var world=NpcWorldAllocation.Capture();int count=World.Mobiles.Count;bool refused=false;
                    try{AmazonValkyrieBody.CreateInitializedController(p,new(p.X+1,p.Y,p.Z),DateTime.SpecifyKind(at,DateTimeKind.Unspecified));}catch(ArgumentException){refused=true;}
                    Require(refused&&World.Mobiles.Count==count&&NpcWorldAllocation.Capture()==world,"Invalid initialized startup allocated body/RNG");
                    var b=AmazonValkyrieBody.CreateInitializedController(p,new(p.X+1,p.Y,p.Z),at);var s=State(b);var motion=b.Prepared.GroundMotion;
                    Require(motion is{StartupProfile:ValkyrieStartup.Profile,AttackRate:100,WalkVelocityPercent:75,RunVelocityPercent:125}&&s is{Mode:1,NextThink:20,Frame:0}&&!b.CantWalk,"Creation did not derive source startup/default first think");
                    Require(b.Prepared.Initialization!=null&&b.Prepared.Birth.Rank==rank&&b.Prepared.Difficulty==difficulty&&NpcEquipmentRandom.Require(b).Seed==b.Prepared.Equipment.PetAfter,"Active creation lost original birth/equipment/actor continuation");
                    TimedEffects.PreventHealing(b,TimedEffects.Now);WalkRun(b);Refusals(b);NpcExpandedItemSpellChecks.Flights(b);ValkyrieGroundCombatChecks.Contacts(b,p);
                    SkillHealth.SetBalance256(b,b.MaximumLife256-257);PersistentMinions.Suspend(p);p.Internalize();held.Add(Capture(b));
                }
            }
            finally{SummonDifficulties.SetFacets(facets);}
        }
        NpcWorldAllocationChecks.Checked(NpcWorldAllocation.Capture());File.WriteAllText(Manifest,Json(new Saved(held.ToArray(),generation)));
        Log.Information("Valkyrie startup:24complete source bodies/5016transitions/4104native rate-event comparisons;derived100/75/125from original initialization/current Amazon run state;12actual initialized rank/difficulty creations,unfiltered gear,walk-run/contact/provenance guards;original resources/items/wounds/RNG through two reloads;town/sunder/remaining contact adapters and learned cast remain separate");
    }
}
