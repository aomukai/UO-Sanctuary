// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class BladeSpawnChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(BladeSpawnChecks));
    private sealed record Body(uint Actor,BladeBodyState State);
    private sealed record Saved(uint Owner,Point3D Origin,int Map,Body[] Bodies,AssassinTrapEntry[] Roster,int Mana256);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"blade-spawn-fixtures.json");
    private static void Require(bool ok,string reason){if(!ok){throw new InvalidOperationException(reason);}}
    public static void Configure(){Register("blade-spawn-check",Check);Register("blade-spawn-reload-check",Reload);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Actual blade spawn/owner capture and shared trap FIFO persistence",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception error){Log.Error(error,"{Name}: failed",name);}}));
    private static int Passive(AssassinTrapCreationStats stats,string name)=>stats.Passives.Single(x=>x.Name==name).Value;
    private static Item Named(PlayerMobile owner,string source)
    {
        var item=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey==source).Id,99,new Random(173),true);
        owner.AddToBackpack(item);Require(owner.EquipItem(item),"Actual equipment: "+source);ClassGear.Refresh(owner);return item;
    }
    private static void Collect()
    {
        var p=BladeBodyChecks.Fresh();var original=SummonDifficulties.Facets();
        try
        {
            while(p.RawStr<200){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Strength),"Strength allocation");}
            var helm=Named(p,"Tarnhelm");var capture=BladeSentinelSummons.Capture(p);
            Require(capture.Creation.Rank==2 && capture.Creation.BaseStatsLevel==76 && capture.Creation.Level==99
                && capture.Creation.Life256==25600 && capture.Context.Speed==2304 && capture.Creation.DurationFrames==112,"Real soft-rank/source spawn/velocity context");
            foreach(var difficulty in new[]{MonsterDifficulty.Normal,MonsterDifficulty.Nightmare,MonsterDifficulty.Hell})
            {
                SummonDifficulties.SetFacets([new(p.Map.MapID,difficulty)]);var current=BladeSentinelSummons.Capture(p);
                Require(current.Context.Difficulty==difficulty && current.Creation.Life256==25600
                    && current.Creation.Attack==NecromancerSummons.Source("level-76","L-TH",difficulty)+10
                    && current.Creation.Defense==100+NecromancerSummons.Source("level-76","L-AC",difficulty),"Actual facet difficulty source columns/preliminary level");
            }
            using(var probe=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"blade-spawn-source-probe.json"))))
            {
                int friendly=0,spawn=0;
                foreach(var row in probe.RootElement.GetProperty("playerCounts").EnumerateArray())
                {
                    if(row[0].GetInt32()!=1 || row[3].GetInt32()!=1){continue;}
                    Require(row[4].GetInt32()==0 && row[5].GetInt32()==1 && row[6].GetInt32()==0 && row[7].GetInt32()==0,"Source friendly player-count controls");friendly++;
                }
                foreach(var row in probe.RootElement.GetProperty("spawnStats").EnumerateArray())
                {
                    if(row[1].GetInt32()!=1 || row[2].GetInt32()!=0 || row[3].GetInt32()!=1){continue;}
                    var difficulty=(MonsterDifficulty)row[0].GetInt32();SummonDifficulties.SetFacets([new(p.Map.MapID,difficulty)]);
                    var actual=BladeSentinelSummons.Capture(p);
                    Require(row[4].GetInt32()==1 && actual.Creation.Life256==row[5].GetInt32()*256
                        && actual.Creation.Defense==row[7].GetInt32()+NecromancerSummons.Source("level-76","L-AC",difficulty)
                        && actual.Creation.Attack==row[9].GetInt32()+NecromancerSummons.Source("level-76","L-TH",difficulty)+10,"Complete source spawn-table/native collection comparison");spawn++;
                }
                Require(friendly==18 && spawn==3,"Incomplete friendly/spawn source selection");
            }
            SummonDifficulties.SetFacets(original);p.AddToBackpack(helm);var eye=Named(p,"Griffon's Eye");var elemental=BladeSentinelSummons.Capture(p);
            Require(Passive(elemental.Creation,"passive_ltng_mastery")>0 && Passive(elemental.Creation,"passive_ltng_pierce")>0
                && Passive(elemental.Creation,"passive_ltng_mastery")==ClassGear.Bonus(p,"extra-ltng")
                && Passive(elemental.Creation,"passive_ltng_pierce")==ClassGear.Bonus(p,"pierce-ltng"),"Real Griffon accrued mastery/pierce");
            string retained=JsonSerializer.Serialize(elemental);p.AddToBackpack(eye);ClassGear.Refresh(p);
            Require(JsonSerializer.Serialize(elemental)==retained && Passive(BladeSentinelSummons.Capture(p).Creation,"passive_ltng_mastery")==0,"Birth passives followed later equipment");
            // The equipment catalogue already refuses these item properties.
            // Test the additional inheritance guard directly, without bypassing
            // that validator or pretending a sunder item is obtainable.
            bool unsupported=false;try{BladeSentinelSummons.InheritedValue("item_pierce_fire_immunity",1);}catch(NotSupportedException){unsupported=true;}
            Require(unsupported && AssassinTrapRoster.For(p).Length==0,"Unsupported nonzero sunder silently admitted/registered");
            bool clock=false;try{BladeSentinelSummons.Create(p,new(p.X+3,p.Y,p.Z),DateTime.Now);}catch(ArgumentException){clock=true;}
            Require(clock && AssassinTrapRoster.For(p).Length==0,"Non-UTC factory clock admitted");
        }
        finally{SummonDifficulties.SetFacets(original);p.Delete();}
    }
    private static BladeSentinelBody Create(PlayerMobile p,DateTime at)=>BladeSentinelSummons.Create(p,new(p.X+8,p.Y,p.Z),at);
    private static void Save(PlayerMobile p)
    {
        var roster=AssassinTrapRoster.For(p);var bodies=roster.Select(x=>(BladeSentinelBody)World.FindMobile((Serial)x.Actor)).ToArray();
        foreach(var body in bodies)
        {Require(body.Prepared.Spawn is{Difficulty:MonsterDifficulty.Normal,Speed:2304} && body.Prepared.ControllerFault==null,"Missing actual source spawn context/controller fault");}
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,p.Location,p.Map.MapID,
            bodies.Select(x=>new Body(x.Serial.Value,x.Prepared)).ToArray(),roster,NecromancerEffects.ManaBalance256(p))));
    }
    private static void Check()
    {
        Collect();var p=BladeBodyChecks.Fresh();var seed=BladeSentinelSummons.SeedLow;BladeSentinelSummons.SeedLow=()=>19;
        try
        {
            int mana=NecromancerEffects.ManaBalance256(p);DateTime held=DateTime.UtcNow.AddDays(1);var bodies=Enumerable.Range(0,7).Select(_=>Create(p,held)).ToArray();
            var roster=AssassinTrapRoster.For(p);Require(bodies.Take(2).All(x=>x.Deleted) && bodies.Skip(2).All(x=>!x.Deleted)
                && roster.Select(x=>x.Actor).SequenceEqual(bodies.Skip(2).Select(x=>x.Serial.Value)),"Shared cap5 did not replace exactly the two oldest traps");
            Require(bodies.Skip(2).All(x=>x.Location==p.Location && x.HitsMax==100 && x.SummonMaster==p && !x.BlocksFit && x.ControlSlots==0)
                && NecromancerEffects.ManaBalance256(p)==mana,"Actual birth position/HP/owner/footprint or unrequested payment");
            // Real factory actor starts at the caster. Its first think creates
            // the real following missile; subsequent updates move on the floor.
            var moving=bodies[2];var origin=moving.Location;
            for(int i=0;i<8;i++){moving.AdvanceController(moving.Prepared.NextFrameUtc);}
            Require(!moving.Deleted && moving.Prepared.ControllerFault==null && moving.Prepared.Creation.MissileArmed
                && moving.Prepared.Control.Creates==1 && moving.Prepared.Frames>0 && moving.Location!=origin
                && BladeSentinelMissiles.Snapshot().Missiles.Count(x=>x.Owner==moving.Serial.Value)==1,"Actual factory did not enter live movement/once-only following creation");
            Save(p);Require(!AssassinCombat.Implemented(CombatAbility.BladeSentinel),"Factory prematurely enabled learned input");
            Log.Information("Blade spawn: actual source100HP/2304 speed, effective ranks and inherited real Griffon stats, three difficulty columns, sunder refusal, cap5/oldest2 replacement and live caster-origin movement/following creation pass");
        }
        finally{BladeSentinelSummons.SeedLow=seed;}
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);
        Require(AssassinTrapRoster.For(p).SequenceEqual(saved.Roster),"Reload lost shared trap insertion order");
        foreach(var old in saved.Bodies)
        {
            var body=(BladeSentinelBody)World.FindMobile((Serial)old.Actor);
            Require(body!=null && !body.Deleted && body.SummonMaster==p
                && JsonSerializer.Serialize(body.Prepared)==JsonSerializer.Serialize(old.State),"Actual birth/armed/clock/pose context changed on reload");
        }
        p.MoveToWorld(saved.Origin,Map.Maps[saved.Map]);Require(NecromancerEffects.ManaBalance256(p)==saved.Mana256,"Reload paid summon mana");
        var oldest=saved.Roster[0];var fresh=Create(p,DateTime.UtcNow.AddDays(1));var roster=AssassinTrapRoster.For(p);
        Require(World.FindMobile((Serial)oldest.Actor)==null && roster.Length==5 && roster.Take(4).SequenceEqual(saved.Roster.Skip(1))
            && roster[^1].Actor==fresh.Serial.Value && roster[^1].Order>saved.Roster[^1].Order,"Reload replaced the wrong oldest trap or reused registration order");
        Save(p);Require(!AssassinCombat.Implemented(CombatAbility.BladeSentinel),"Reload enabled unfinished input");
        Log.Information("Blade spawn reload: five actual entities retain source/weapon/controller state and shared FIFO order; next creation replaces oldest without replaying earlier casts");
    }
}
