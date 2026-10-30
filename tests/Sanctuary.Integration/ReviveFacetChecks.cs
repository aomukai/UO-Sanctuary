// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Accounting;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary.Integration;

public static class ReviveFacetChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(ReviveFacetChecks));
    private sealed record Saved(uint Owner,ReviveNativeState[] Actors,MonsterNativeCombat[] Baselines,int[] Life,uint[] Corpses);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"revive-facet-fixtures.json");
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    public static void Configure()
    {
        Register("revive-facet-check",Check);Register("revive-facet-reload-check",Reload);
        Register("revive-facet-live-check",()=>FixtureClock.NextLoop("revive-facet-live-result",()=>FixtureNetwork.Connected("revive-facet-live-result",Live)));
    }
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Actual owner facet rescaling of original Revives",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception error){Log.Error(error,"{Name}: failed",name);}}));
    private static (BaseCreature Actor,Corpse Corpse) Create(PlayerMobile owner,bool nightmare=false)
    {
        var actor=new Skeleton{NoKillAwards=true};if(nightmare){MonsterVariants.Assign(actor,MonsterDifficulty.Nightmare);}
        actor.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);actor.AIObject.AITimer.Stop();
        Corpse corpse=null;var died=ClassLootHooks.CreatureDied;var seed=CorpseBaseSnapshots.SeedLow;
        ClassLootHooks.CreatureDied=(who,container)=>{died?.Invoke(who,container);if(who==actor){corpse=container as Corpse;corpse?.DropItem(new Gold(1));}};
        try{CorpseBaseSnapshots.SeedLow=()=>42;owner.DoHarmful(actor,true);actor.Damage(actor.Hits+1,owner);CombatDamage.Finish(actor,owner);}
        finally{ClassLootHooks.CreatureDied=died;CorpseBaseSnapshots.SeedLow=seed;}
        Require(corpse is{Deleted:false}&&ReviveOriginals.Find(corpse)==actor,"Actual source death/capture failed");
        corpse.BeginDecay(TimeSpan.FromHours(1));owner.Mana=owner.ManaMax;
        Require(ReviveCasts.Plan(owner,corpse,out _,out _,out var issue)&&ReviveCasts.Release(owner,corpse),"Actual learned cast: "+issue);
        actor.AIObject.AITimer.Stop();return(actor,corpse);
    }
    private static void Facets(MonsterDifficulty trammel,MonsterDifficulty felucca)
        =>SummonDifficulties.SetFacets([new(Map.Trammel.MapID,trammel),new(Map.Felucca.MapID,felucca)]);
    private static void Expect(BaseCreature actor,ReviveNativeState birth,int before,int oldMaximum,MonsterDifficulty difficulty)
    {
        int level=difficulty switch{MonsterDifficulty.Normal=>2,MonsterDifficulty.Nightmare=>37,_=>68};
        var original=birth.Setup.LifePlan;
        var life=ReviveLife.Calculate(original.Source,new(actor.Serial.Value,level,difficulty,CorpseBaseSnapshots.Base256(original.Source,difficulty,level),original.Seed),original.CasterLevel,original.MasteryRank);
        var row=ReviveNativeActivation.State(actor);
        Require(row.Original==birth.Original&&row.Setup==birth.Setup&&row.Order==birth.Order&&row.FacetPlan?.Life==life
            &&row.Difficulty==difficulty&&MonsterVariants.Difficulty(actor)==difficulty,"Facet projection rewrote birth/history or chose wrong target context");
        int expectedMaximum=checked((int)(life.Maximum256+ClassResources.Percentage(life.Maximum256,BattleOrders.LifePercent(actor))));
        Require(SkillHealth.Maximum256(actor)==expectedMaximum
            &&SkillHealth.Balance256(actor)==SummonDifficulties.Life256(before,oldMaximum,expectedMaximum),"Actual facet maximum/relative FP256 life");
        Require(CreatureProfiles.Level(actor)==Math.Min(level,original.CasterLevel)&&ReviveCombat.DamagePercent(actor)==birth.Setup.CombatPlan.DamagePercent,
            "Reduced target level or captured mastery was lost");
        var profile=CreatureProfiles.Variant(actor,difficulty);var basis=MonsterVariantBaselines.Find(actor);
        Require(actor.DamageMin==(profile.ApplyStats?profile.Attack1.MinDamage??basis.Minimum:basis.Minimum)
            &&actor.DamageMax==(profile.ApplyStats?profile.Attack1.MaxDamage??basis.Maximum:basis.Maximum)
            &&actor.BaseEnergyResistance==(profile.ApplyStats?profile.Resistances.Lightning??basis.Lightning:basis.Lightning),"Native damage/resistance failed to rescale or restore");
        Require(ClassCombatHooks.SourceWeaponDamage(actor)==profile.ApplyStats,"Native raw source weapon lane did not follow target variant");
        actor.AIObject.AITimer.Stop();
    }
    private static void Move(PlayerMobile owner,BaseCreature[] actors,ReviveNativeState[] births,Map map,MonsterDifficulty difficulty)
    {
        var life=actors.Select(SkillHealth.Balance256).ToArray();var maximum=actors.Select(SkillHealth.Maximum256).ToArray();
        owner.MoveToWorld(new(owner.X+2,owner.Y,owner.Z),map);
        for(int i=0;i<actors.Length;i++){Expect(actors[i],births[i],life[i],maximum[i],difficulty);}
        var rows=actors.Select(ReviveNativeActivation.State).ToArray();var after=actors.Select(SkillHealth.Balance256).ToArray();
        SummonDifficulties.Sync(owner);Require(rows.SequenceEqual(actors.Select(ReviveNativeActivation.State))&&after.SequenceEqual(actors.Select(SkillHealth.Balance256)),"Same-difficulty notification rerolled or rounded again");
    }
    private static void Invalid(BaseCreature actor)
    {
        var rows=ReviveNativeActivation.Snapshot();var row=ReviveNativeActivation.State(actor);bool refused=false;
        try{ReviveNativeActivation.Restore(rows.Select(x=>x.Actor==row.Actor?x with{FacetPlan=x.FacetPlan with{Life=x.FacetPlan.Life with{Seed=x.FacetPlan.Life.Seed+1}}}:x).ToArray());}
        catch(InvalidDataException){refused=true;}
        Require(refused&&rows.SequenceEqual(ReviveNativeActivation.Snapshot()),"Malformed active facet plan was not refused atomically");
        var bases=MonsterVariantBaselines.Snapshot();refused=false;
        try{MonsterVariantBaselines.Restore([..bases,bases[0]]);}catch(InvalidDataException){refused=true;}
        Require(refused&&bases.SequenceEqual(MonsterVariantBaselines.Snapshot()),"Malformed native baseline was not refused atomically");
    }
    private static void Check()
    {
        var previous=SummonDifficulties.Facets();var owner=ReviveNativeActivationChecks.Owner();FixtureSpace.Corridor(owner,6560,32,3100);owner.RawInt=200;
        try
        {
            Facets(MonsterDifficulty.Normal,MonsterDifficulty.Nightmare);
            var first=Create(owner);var second=Create(owner,true);BaseCreature[] actors=[first.Actor,second.Actor];
            var births=actors.Select(ReviveNativeActivation.State).ToArray();
            var supplier=new PlayerMobile{Name="FacetOrdersBoundary",Body=0x190,Player=true};supplier.AddItem(new Backpack());
            ClassCreation.Profiles.Assign(supplier,DiabloClass.Barbarian);ClassCreation.Profiles.AdvanceLevel(supplier,99);
            // Prepared upper-domain control, not an attainable character rank
            // or a claim that the natural buff lasts beyond its normal time.
            BattleOrders.Apply(supplier,second.Actor,255,TimedEffects.Now.AddDays(1));
            for(int i=0;i<actors.Length;i++){SkillHealth.SetBalance256(actors[i],SkillHealth.Maximum256(actors[i])*2/3+17);}
            Move(owner,actors,births,Map.Felucca,MonsterDifficulty.Nightmare);
            Facets(MonsterDifficulty.Normal,MonsterDifficulty.Hell);Move(owner,actors,births,Map.Felucca,MonsterDifficulty.Hell);
            Require(SkillHealth.Maximum256(second.Actor)>65000*256,"Facet life did not exercise the supported large FP256 domain");
            Move(owner,actors,births,Map.Trammel,MonsterDifficulty.Normal);
            SkillHealth.SetBalance256(first.Actor,1);Move(owner,actors,births,Map.Felucca,MonsterDifficulty.Hell);
            Require(SkillHealth.Balance256(first.Actor)>=1,"Living fractional boundary died on rescale");Invalid(first.Actor);
            File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(owner.Serial.Value,actors.Select(ReviveNativeActivation.State).ToArray(),
                actors.Select(MonsterVariantBaselines.Find).ToArray(),actors.Select(SkillHealth.Balance256).ToArray(),[first.Corpse.Serial.Value,second.Corpse.Serial.Value])));
            owner.Internalize();LegacyBaseline();Report();
        }
        finally{SummonDifficulties.SetFacets(previous);}
    }
    private static void LegacyBaseline()
    {
        Facets(MonsterDifficulty.Nightmare,MonsterDifficulty.Hell);
        var owner=ReviveNativeActivationChecks.Owner();FixtureSpace.Corridor(owner,6680,24,3100);owner.RawInt=200;
        var original=Create(owner,true);var before=ReviveNativeActivation.State(original.Actor);
        var bases=MonsterVariantBaselines.Snapshot();int life=SkillHealth.Balance256(original.Actor);
        MonsterVariantBaselines.Restore(bases.Where(x=>x.Actor!=original.Actor.Serial.Value).ToArray());
        Facets(MonsterDifficulty.Normal,MonsterDifficulty.Hell);owner.MoveToWorld(new(owner.X+2,owner.Y,owner.Z),owner.Map);
        Require(ReviveNativeActivation.State(original.Actor)==before&&SkillHealth.Balance256(original.Actor)==life
            &&original.Actor.Map==Map.Internal&&ReviveFacets.Issue(original.Actor)?.Contains("baseline")==true,
            "Missing legacy native baseline was guessed or left fighting instead of explicitly parked");
        MonsterVariantBaselines.Restore(bases);owner.MoveToWorld(new(owner.X+2,owner.Y,owner.Z),owner.Map);
        Require(ReviveNativeActivation.State(original.Actor).Difficulty==MonsterDifficulty.Normal&&ReviveFacets.Issue(original.Actor)==null,
            "Supported rescaling did not recover after restoring the required baseline");
        original.Actor.Delete();original.Corpse.Delete();owner.Delete();
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));
        for(int i=0;i<saved.Actors.Length;i++)
        {
            var row=saved.Actors[i];var actor=(BaseCreature)World.FindMobile((Serial)row.Actor);
            Require(actor is{Alive:true,Deleted:false}&&actor.ControlMaster?.Serial.Value==saved.Owner&&actor.Map==Map.Internal
                &&ReviveNativeActivation.State(actor)==row&&MonsterVariantBaselines.Find(actor)==saved.Baselines[i]
                &&SkillHealth.Balance256(actor)==saved.Life[i],"Actual reload lost native baseline/active facet/relative life");
            Require(World.FindItem((Serial)saved.Corpses[i]) is Corpse body&&body.GetAmount(typeof(Gold))==1,"Actual reload lost independent corpse loot");
        }
        Invalid((BaseCreature)World.FindMobile((Serial)saved.Actors[0].Actor));Report();
    }
    private static void Live(NetState network)
    {
        var previous=SummonDifficulties.Facets();var owner=ReviveNativeActivationChecks.Owner();int floor=FixtureSpace.Corridor(owner,6620,32,3100);owner.RawInt=200;
        for(int dx=-4;dx<=32;dx++)for(int dy=-4;dy<=4;dy++){new Static(floor).MoveToWorld(new(owner.X+dx,owner.Y+dy,owner.Z),Map.Felucca);}
        var account=new Account("revive-facets-"+Guid.NewGuid().ToString("N"),Guid.NewGuid().ToString("N"));account[0]=owner;network.Account=account;ReviveNativeActivationChecks.Attach(network,owner);
        BaseCreature actor=null;Corpse corpse=null;BaseCreature enemy=null;
        try
        {
            Facets(MonsterDifficulty.Normal,MonsterDifficulty.Nightmare);var created=Create(owner);actor=created.Actor;corpse=created.Corpse;
            var birth=ReviveNativeActivation.State(actor);SkillHealth.SetBalance256(actor,SkillHealth.Maximum256(actor)*2/3);
            Move(owner,[actor],[birth],Map.Felucca,MonsterDifficulty.Nightmare);
            Require(actor.Map==owner.Map&&!actor.Blessed&&network.Running,"Connected original did not follow owner into scaled facet");
            Facets(MonsterDifficulty.Hell,MonsterDifficulty.Nightmare);Move(owner,[actor],[birth],Map.Trammel,MonsterDifficulty.Hell);
            Require(actor.Map==owner.Map&&!actor.Blessed,"Connected Hell projection remained parked");
            enemy=new Skeleton{NoKillAwards=true};enemy.MoveToWorld(new(owner.X+3,owner.Y,owner.Z),owner.Map);enemy.AIObject.AITimer.Stop();
            int before=SkillHealth.Balance256(actor);AOS.Damage(actor,enemy,100,false,0,0,0,0,100,weaponDamage:false);
            Require(SkillHealth.Balance256(actor)==before,"Actual native lightning hit ignored projected Hell immunity");
            Facets(MonsterDifficulty.Normal,MonsterDifficulty.Nightmare);Move(owner,[actor],[birth],Map.Trammel,MonsterDifficulty.Normal);
            Require(corpse.GetAmount(typeof(Gold))==1,"Facet travel changed separate loot");
            Log.Information("revive-facet-live-result: passed");
        }
        catch(Exception error){Log.Error(error,"revive-facet-live-result: failed");}
        finally
        {
#pragma warning disable CS0618
            network.Dispose();
#pragma warning restore CS0618
            enemy?.Delete();actor?.Delete();corpse?.Delete();owner.Delete();SummonDifficulties.SetFacets(previous);
        }
    }
    private static void Report()=>Log.Information("Revive facets:actual source-born Normal/Nightmare originals,owner map hooks,target levels/native stats,relative FP256 life,immutable birth/loot,same-area idempotence and atomic malformed payloads;live travel/immunity and actual persistence checked separately");
}
