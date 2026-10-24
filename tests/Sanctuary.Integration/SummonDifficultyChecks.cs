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

public static class SummonDifficultyChecks
{
    private static readonly ILogger Log = LogFactory.GetLogger(typeof(SummonDifficultyChecks));
    private sealed record Pet(uint Serial, MonsterProfile Stats, int Life256, int Maximum, int Rank, int Mastery);
    private sealed record Saved(uint Owner, Pet[] Pets, int Mana256, uint LegacyOwner, Pet Legacy);
    private static string Manifest => Path.Join(Core.BaseDirectory, "summon-difficulty-fixtures.json");
    private static void Require(bool ok, string why) { if (!ok) { throw new InvalidOperationException(why); } }
    public static void Configure()
    { Register("summon-difficulty-check", Check); Register("summon-difficulty-reload-check", Reload); }
    private static void Register(string name, Action action) => ConsoleInputHandler.RegisterCommand(name, "Facet summon stat transition and life-ratio persistence", _ => Core.LoopContext.Post(() =>
    { try { action(); Log.Information("{Name}: passed", name); } catch (Exception error) { Log.Error(error, "{Name}: failed", name); } }));
    private static FacetDifficulty[] Designations => [new(Map.Trammel.MapID, MonsterDifficulty.Normal), new(Map.Felucca.MapID, MonsterDifficulty.Nightmare), new(Map.Ilshenar.MapID, MonsterDifficulty.Hell)];
    private static void Enter(PlayerMobile owner, Map map)
    {
        // A location/map callback, rather than a direct call to Sync, must apply
        // the new stats. Test coordinates don't assert playable terrain there.
        owner.MoveToWorld(new(5450,1160,0), map);
    }
    private static Pet Snapshot(SkeletonGuardian pet) => new(pet.Serial.Value, pet.CombatProfile, SkillHealth.Balance256(pet), pet.HitsMax, pet.Rank, pet.Mastery);
    private static SkeletonGuardian Add(PlayerMobile p, CombatAbility kind)
    {
        SkeletonGuardian pet;
        if (kind == CombatAbility.RaiseSkeleton) { pet = new SkeletonGuardian(); pet.Initialize(p, 1, 0); }
        else { var minion = new NecromancerMinion(); minion.Prepare(p, kind); pet = minion; }
        Require(pet.SetControlMaster(p), "Native summon ownership");
        PersistentMinions.Profiles.Track(pet); pet.Internalize(); pet.AIObject?.AITimer.Stop();
        return pet;
    }
    private static void Move(PlayerMobile owner, SkeletonGuardian[] pets, Map map, MonsterDifficulty difficulty)
    {
        var before = pets.Select(Snapshot).ToArray(); int mana = NecromancerEffects.ManaBalance256(owner);
        var deadlines = (ClassCombat.For(owner).NextAction, owner.NextSpellTime, owner.NextSkillTime);
        Enter(owner, map);
        for (int i = 0; i < pets.Length; i++)
        {
            var pet = pets[i]; var old = before[i];
            Require(pet.SummonDifficulty == difficulty && MonsterVariants.Difficulty(pet) == difficulty, "Facet transition was not observed");
            Require(SkillHealth.Balance256(pet) == (long)old.Life256 * pet.HitsMax / old.Maximum, "Injured summon lost fractional life percentage");
            Require(pet.ControlMaster == owner && pet.Map == Map.Internal && pet.Rank == old.Rank && pet.Mastery == old.Mastery, "Transition moved/respawned/retrained parked summon");
            int life = SkillHealth.Balance256(pet); var profile = pet.CombatProfile;
            Enter(owner, map); Require(SkillHealth.Balance256(pet) == life && pet.CombatProfile == profile, "Repeated notification rerounded/healed summon");
        }
        Require(mana == NecromancerEffects.ManaBalance256(owner) && deadlines == (ClassCombat.For(owner).NextAction, owner.NextSpellTime, owner.NextSkillTime), "Facet rescale recast or charged/reset action state");
    }
    private static void Guards()
    {
        Require(SummonDifficulties.Life256(1,3,2)==1 && SummonDifficulties.Life256(1,3,5)==1
            && SummonDifficulties.Life256(2,3,5)==3 && SummonDifficulties.Life256(3,3,5)==5
            && SummonDifficulties.Life256(0,3,5)==0 && SummonDifficulties.Life256(999,3,5)==5, "FP256 rounding/full/empty bounds");
        var good=SummonDifficulties.Facets();
        foreach(var bad in new[]{new[]{new FacetDifficulty(Map.Internal.MapID,MonsterDifficulty.Hell)},new[]{new FacetDifficulty(-1,MonsterDifficulty.Hell)},
            new[]{new FacetDifficulty(Map.Felucca.MapID,(MonsterDifficulty)3)},new[]{new FacetDifficulty(Map.Felucca.MapID,MonsterDifficulty.Hell),new FacetDifficulty(Map.Felucca.MapID,MonsterDifficulty.Normal)}})
        {
            bool rejected=false;try{SummonDifficulties.SetFacets(bad);}catch(InvalidDataException){rejected=true;}
            Require(rejected && SummonDifficulties.Facets().SequenceEqual(good),"Invalid designation mutated live rules");
        }
    }
    private static void Check()
    {
        var original=SummonDifficulties.Facets();Require(original.Length==0,"Test requires shipped unassigned facets");
        PlayerMobile p=null;
        try
        {
            SummonDifficulties.SetFacets(Designations);Guards();p=ClassSkillChecks.Player();p.Name="SummonDifficultyFixture";p.AddItem(new Backpack());
            var pets=new[]{CombatAbility.RaiseSkeleton,CombatAbility.RaiseSkeletalMage,CombatAbility.ClayGolem,CombatAbility.BloodGolem,CombatAbility.FireGolem}.Select(kind=>Add(p,kind)).ToArray();
            Require(pets[0].CombatProfile is{Life:21,AttackRating:1088,Defense:614,MinDamage:1,MaxDamage:2},"Pinned Normal level99/rank1 skeleton baseline");
            foreach(var pet in pets){SkillHealth.SetBalance256(pet,pet.HitsMax*97-13);}
            var unchanged=BattleOrders.Capture(pets[0]);pets[0].RefreshDifficulty();
            BattleOrders.Change(pets[0],unchanged,preserveSummonLifeRatio:true);
            Require(SkillHealth.Balance256(pets[0])==unchanged.Life256,"Equal-maximum stat refresh healed an injured summon");
            var ordinary=new Dog();ordinary.SetControlMaster(p);ordinary.Internalize();ordinary.Hits=Math.Max(1,ordinary.HitsMax/2);int ordinaryLife=ordinary.Hits,ordinaryMax=ordinary.HitsMax;
            Move(p,pets,Map.Felucca,MonsterDifficulty.Nightmare);
            Require(pets[0].CombatProfile is{Life:30,AttackRating:1891,Defense:1304},"Nightmare skeleton columns/level additions");
            var born=Add(p,CombatAbility.RaiseSkeleton);Require(born.SummonDifficulty==MonsterDifficulty.Nightmare && born.HitsMax==30 && born.Hits==30,"New summon ignored owner facet");born.Delete();
            Move(p,pets,Map.Ilshenar,MonsterDifficulty.Hell);
            Require(pets[0].CombatProfile is{Life:42,AttackRating:3961,Defense:1919},"Hell skeleton columns/level additions");
            Move(p,pets,Map.Trammel,MonsterDifficulty.Normal);
            Require(ordinary.Hits==ordinaryLife && ordinary.HitsMax==ordinaryMax && MonsterVariants.Difficulty(ordinary)==MonsterDifficulty.Normal,"Ordinary pet was rescaled");ordinary.Delete();
            // The buff keeps its supplier/rank/deadline while both old and new
            // maxima include its existing percentage. This is not a buff recast.
            BattleOrders.Apply(p,pets[0],1,DateTime.UtcNow);var orders=BattleOrders.State(pets[0]);
            Move(p,pets,Map.Felucca,MonsterDifficulty.Nightmare);Require(BattleOrders.State(pets[0])==orders,"Facet refreshed Battle Orders");BattleOrders.Clear(pets[0]);
            // Capture parked records and injuries in Hell, then disconnect.
            foreach(var pet in pets)
            {var state=PersistentMinions.Profiles.Find(pet);PersistentMinions.Profiles.Store(pet,state with{Parked=true});}
            Move(p,pets,Map.Ilshenar,MonsterDifficulty.Hell);
            var legacyOwner=ClassSkillChecks.Player();legacyOwner.Name="SummonDifficultyLegacyFixture";
            var legacy=Add(legacyOwner,CombatAbility.RaiseSkeleton);SkillHealth.SetBalance256(legacy,1337);
            SummonDifficulties.Profiles.States.Remove(legacy.Serial);legacyOwner.Internalize();
            var saved=new Saved(p.Serial.Value,pets.Select(Snapshot).ToArray(),NecromancerEffects.ManaBalance256(p),legacyOwner.Serial.Value,Snapshot(legacy));
            p.Internalize();Require(pets.All(x=>x.SummonDifficulty==MonsterDifficulty.Hell),"Internal map overwrote saved difficulty");
            File.WriteAllText(Manifest,JsonSerializer.Serialize(saved));p=null;
            Log.Information("Facet summons: native map hook, five current summon families, Normal/Nightmare/Hell stats, fractional ratio, unchanged repeated notifications/ordinary pets/cast resources and Battle Orders pass");
        }
        finally{if(p!=null){PersistentMinions.Dismiss(p);p.Delete();}SummonDifficulties.SetFacets(original);}
    }
    private static void Reload()
    {
        var original=SummonDifficulties.Facets();
        try
        {
            var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);
            var pets=saved.Pets.Select(x=>(SkeletonGuardian)World.FindMobile((Serial)x.Serial)).ToArray();
            for(int i=0;i<pets.Length;i++)
            {
                var pet=pets[i];var before=saved.Pets[i];
                Require(pet.SummonDifficulty==MonsterDifficulty.Hell && pet.CombatProfile==before.Stats && pet.HitsMax==before.Maximum
                    && SkillHealth.Balance256(pet)==before.Life256 && pet.Rank==before.Rank && pet.Mastery==before.Mastery
                    && pet.ControlMaster==p && pet.Map==Map.Internal && PersistentMinions.Profiles.Find(pet).Parked,"Reload lost difficulty/profile/fraction/parked identity");
            }
            Require(NecromancerEffects.ManaBalance256(p)==saved.Mana256,"Reload changed owner mana");
            var legacy=(SkeletonGuardian)World.FindMobile((Serial)saved.Legacy.Serial);
            Require(legacy.ControlMaster?.Serial.Value==saved.LegacyOwner && legacy.SummonDifficulty==MonsterDifficulty.Normal
                && legacy.CombatProfile==saved.Legacy.Stats && SkillHealth.Balance256(legacy)==saved.Legacy.Life256
                && !SummonDifficulties.Profiles.States.ContainsKey(legacy.Serial),"Legacy summon without a difficulty record failed Normal migration");
            SummonDifficulties.SetFacets(Designations);Move(p,pets,Map.Felucca,MonsterDifficulty.Nightmare);Move(p,pets,Map.Ilshenar,MonsterDifficulty.Hell);p.Internalize();
            File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{Pets=pets.Select(Snapshot).ToArray()}));
            Log.Information("Facet summons reload: five retained creatures, difficulty-derived stats, FP256 injured life, owner/ranks/parking and resumed map transitions pass");
        }
        finally{SummonDifficulties.SetFacets(original);}
    }
}
