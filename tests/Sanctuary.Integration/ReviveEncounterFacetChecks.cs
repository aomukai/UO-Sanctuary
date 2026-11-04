// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary.Integration;

public static class ReviveEncounterFacetChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(ReviveEncounterFacetChecks));
    private sealed record Spawn(string Profile,int Level,MonsterDifficulty Difficulty,EncounterGrade Grade,int Life,int Attack,int Defense,string Modifiers);
    private sealed record Saved(uint Owner,ReviveNativeState[] Actors,Spawn[] Spawns,int[] Life,uint[] Corpses,int Mana);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"revive-encounter-facet-fixtures.json");
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    public static void Configure()
    {
        Register("revive-encounter-facet-check",Check);Register("revive-encounter-facet-reload-check",Reload);
        Register("revive-encounter-facet-live-check",()=>FixtureClock.NextLoop("revive-encounter-facet-live-result",()=>FixtureNetwork.Connected("revive-encounter-facet-live-result",Live)));
    }
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Original encounter Revives follow facet difficulty without rerolling",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static Spawn Identity(EncounterCreature b)=>new(b.ProfileId,b.BaseLevel,b.Difficulty,b.Grade,b.RolledLife,b.RolledAttack,b.RolledDefense,string.Join(',',b.Modifiers));
    private static PlayerMobile Owner()
    {
        var p=PoisonDaggerChecks.Owner();p.Name="PoisonDaggerFixtureReviveEncounter";p.RawInt=200;
        MagePetBuffChecks.Train(p,"necromancer.revive",20);MagePetBuffChecks.Train(p,"necromancer.skeletonmastery",20);
        FixtureSpace.Corridor(p,7040,40,3700);return p;
    }
    private static (EncounterCreature Actor,Corpse Corpse,Spawn Spawn) Create(PlayerMobile p,MonsterDifficulty difficulty,bool minion)
    {
        var profile=EncounterProfiles.Profile("crypt-sentinel-90");var b=new EncounterCreature();
        b.Assign(profile,profile.MinimumLevel,difficulty,minion?EncounterGrade.Minion:EncounterGrade.Ordinary,
            ChampionKind.Champion,minion?[EliteModifier.ExtraStrong]:[],EliteAura.None,null,new Random(173));
        var spawn=Identity(b);b.NoKillAwards=true;b.MoveToWorld(new(p.X+2,p.Y,p.Z),p.Map);b.AIObject?.AITimer.Stop();
        Corpse corpse=null;var died=ClassLootHooks.CreatureDied;var seed=CorpseBaseSnapshots.SeedLow;
        ClassLootHooks.CreatureDied=(who,container)=>{died?.Invoke(who,container);if(who==b){corpse=container as Corpse;corpse?.DropItem(new Gold(1));}};
        try{CorpseBaseSnapshots.SeedLow=()=>42;p.DoHarmful(b,true);b.Damage(b.Hits+1,p);CombatDamage.Finish(b,p);}
        finally{ClassLootHooks.CreatureDied=died;CorpseBaseSnapshots.SeedLow=seed;}
        Require(corpse is{Deleted:false}&&ReviveOriginals.Find(corpse)==b,"Encounter original death/capture missing");
        corpse.BeginDecay(FixtureSpace.Retention);PoisonDaggerChecks.Ready(p);NecromancerEffects.SetMana256(p,p.ManaMax*256);
        Require(ReviveCasts.Plan(p,corpse,out _,out _,out var issue),"Encounter learned Revive refused: "+issue);
        int mana=NecromancerEffects.ManaBalance256(p),cost=SkillMechanics.Values(p,CombatAbility.Revive).Mana256;
        Require(NecromancerCombat.Begin(p,CombatAbility.Revive,corpse.Serial,corpse.Location,Core.TickCount),"Encounter learned windup refused");long at=ClassCombat.For(p).EffectAt;
        Require(!ClassCombat.ResolveAction(p,at-1)&&NecromancerEffects.ManaBalance256(p)==mana&&ClassCombat.ResolveAction(p,at)
            &&NecromancerEffects.ManaBalance256(p)==mana-cost&&!ClassCombat.ResolveAction(p,at),"Encounter early/once-only release/payment");
        Require(Identity(b)==spawn&&ReviveNativeActivation.State(b)?.Actor==b.Serial.Value,"Revive changed original encounter identity");
        b.AIObject?.AITimer.Stop();return(b,corpse,spawn);
    }
    private static void Facets(MonsterDifficulty other)=>SummonDifficulties.SetFacets([new(Map.Trammel.MapID,MonsterDifficulty.Normal),new(Map.Felucca.MapID,other)]);
    private static void Expect(EncounterCreature b,ReviveNativeState birth,Spawn spawn,int before,int oldMaximum,MonsterDifficulty difficulty)
    {
        var row=ReviveNativeActivation.State(b);var source=birth.Setup.LifePlan;
        var life=ReviveLife.Calculate(source.Source,new(b.Serial.Value,source.OriginalLevel,difficulty,
            CorpseBaseSnapshots.Base256(source.Source,difficulty,source.OriginalLevel),source.Seed),source.CasterLevel,source.MasteryRank);
        Require(ReviveFacets.Issue(b)==null&&row.Difficulty==difficulty&&row.FacetPlan?.Life==life,"Encounter Revive did not acquire requested facet projection");
        Require(row.Original==birth.Original&&row.Setup==birth.Setup&&row.Order==birth.Order&&Identity(b)==spawn,"Facet change rerolled source or rewrote immutable release/history");
        Require(MonsterVariants.SpawnDifficulty(b)==spawn.Difficulty&&MonsterVariants.Difficulty(b)==difficulty,"Spawn and live difficulty were conflated");
        Require(SkillHealth.Maximum256(b)==life.Maximum256&&SkillHealth.Balance256(b)==SummonDifficulties.Life256(before,oldMaximum,life.Maximum256),"Encounter relative FP256 health was not preserved");
        var active=CreatureProfiles.For(b);var profile=CreatureProfiles.Variant(b,difficulty);
        Require(profile==active&&b.DamageMin==profile.Attack1.MinDamage&&b.DamageMax==profile.Attack1.MaxDamage
            &&b.BasePhysicalResistance==profile.Resistances.Physical&&b.BaseEnergyResistance==profile.Resistances.Lightning
            &&b.ActiveMoveSpeed==profile.Movement.WalkMs/1000.0,"Encounter live/native damage,resistance or movement diverged");
        int expected= b.Basis.MinDamage*(100+(spawn.Grade==EncounterGrade.Minion?EncounterProfiles.ExtraStrong(difficulty,true).DamagePercent:0))/100;
        Require(b.CombatProfile.MinDamage==expected&&b.DamageMin==expected&&CreatureProfiles.Level(b)==Math.Min(source.OriginalLevel,source.CasterLevel),"Encounter cache retained another difficulty or lost captured level reduction");
        // Explicit preview of the spawn difficulty cannot alter active values.
        _=CreatureProfiles.Variant(b,spawn.Difficulty);Require(CreatureProfiles.For(b)==active&&b.CombatProfile.MinDamage==expected,"Explicit encounter preview contaminated active profile");
    }
    private static void Move(PlayerMobile p,EncounterCreature[] actors,ReviveNativeState[] births,Spawn[] spawns,Map map,MonsterDifficulty difficulty)
    {
        var life=actors.Select(SkillHealth.Balance256).ToArray();var max=actors.Select(SkillHealth.Maximum256).ToArray();
        p.MoveToWorld(new(p.X+1,p.Y,p.Z),map);
        for(int i=0;i<actors.Length;i++){Expect(actors[i],births[i],spawns[i],life[i],max[i],difficulty);}
        var rows=actors.Select(ReviveNativeActivation.State).ToArray();var after=actors.Select(SkillHealth.Balance256).ToArray();
        SummonDifficulties.Sync(p);Require(rows.SequenceEqual(actors.Select(ReviveNativeActivation.State))&&after.SequenceEqual(actors.Select(SkillHealth.Balance256)),"Repeated notification rerolled or rounded encounter Revives");
    }
    private static void Check()
    {
        var previous=SummonDifficulties.Facets();var p=Owner();Facets(MonsterDifficulty.Nightmare);
        try
        {
            var scenes=(from d in Enum.GetValues<MonsterDifficulty>()from minion in new[]{false,true}select Create(p,d,minion)).ToArray();
            var actors=scenes.Select(s=>s.Actor).ToArray();var spawns=scenes.Select(s=>s.Spawn).ToArray();var births=actors.Select(ReviveNativeActivation.State).ToArray();
            foreach(var b in actors){SkillHealth.SetBalance256(b,SkillHealth.Maximum256(b)*2/3+17);}
            Move(p,actors,births,spawns,Map.Felucca,MonsterDifficulty.Nightmare);
            Facets(MonsterDifficulty.Hell);Move(p,actors,births,spawns,Map.Felucca,MonsterDifficulty.Hell);
            Move(p,actors,births,spawns,Map.Trammel,MonsterDifficulty.Normal);
            SkillHealth.SetBalance256(actors[0],1);Move(p,actors,births,spawns,Map.Felucca,MonsterDifficulty.Hell);
            NecromancerEffects.SetMana256(p,22784);Require(NecromancerEffects.ManaBalance256(p)==22784,"Saved owner mana preparation");p.Internalize();
            File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,actors.Select(ReviveNativeActivation.State).ToArray(),spawns,
                actors.Select(SkillHealth.Balance256).ToArray(),scenes.Select(s=>s.Corpse.Serial.Value).ToArray(),22784)));
        }
        finally{SummonDifficulties.SetFacets(previous);}
    }
    private static void Reload()
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=World.FindMobile((Serial)s.Owner)as PlayerMobile;
        Require(p!=null,"Original owner missing on reload");
        Require(NecromancerEffects.ManaBalance256(p)==s.Mana,$"Original owner mana changed on reload:{s.Mana}->{NecromancerEffects.ManaBalance256(p)}");
        for(int i=0;i<s.Actors.Length;i++)
        {
            var row=s.Actors[i];var b=World.FindMobile((Serial)row.Actor)as EncounterCreature;
            Require(b is{Alive:true,Deleted:false,Map:not null}&&b.Map==Map.Internal&&b.ControlMaster==p
                &&ReviveNativeActivation.State(b)==row&&Identity(b)==s.Spawns[i]&&SkillHealth.Balance256(b)==s.Life[i],"Saved original encounter projection/identity/wounds changed");
            Expect(b,row,s.Spawns[i],s.Life[i],SkillHealth.Maximum256(b),row.Difficulty);
            Require(World.FindItem((Serial)s.Corpses[i])is Corpse corpse&&corpse.GetAmount(typeof(Gold))==1,"Independent encounter loot changed");
        }
    }
    private static void Live(NetState network)
    {
        var previous=SummonDifficulties.Facets();var p=Owner();int floor=FixtureSpace.Corridor(p,7100,24,3700);
        for(int dx=-4;dx<=24;dx++)for(int dy=-4;dy<=4;dy++){new Static(floor).MoveToWorld(new(p.X+dx,p.Y+dy,p.Z),Map.Felucca);}
        FixtureNetwork.Authenticate(network,p,"revive-encounter-");EncounterCreature b=null;Corpse corpse=null;Skeleton enemy=null;
        try
        {
            Facets(MonsterDifficulty.Nightmare);var scene=Create(p,MonsterDifficulty.Normal,true);b=scene.Actor;corpse=scene.Corpse;
            var birth=ReviveNativeActivation.State(b);SkillHealth.SetBalance256(b,SkillHealth.Maximum256(b)*2/3+17);
            Move(p,[b],[birth],[scene.Spawn],Map.Felucca,MonsterDifficulty.Nightmare);
            Require(b.Map==p.Map&&!b.Blessed&&network.Running,"Connected encounter stayed parked after Nightmare travel");
            Facets(MonsterDifficulty.Hell);Move(p,[b],[birth],[scene.Spawn],Map.Felucca,MonsterDifficulty.Hell);
            enemy=new Skeleton{NoKillAwards=true,Frozen=true};enemy.MoveToWorld(new(p.X+3,p.Y,p.Z),p.Map);enemy.AIObject?.AITimer.Stop();
            int before=SkillHealth.Balance256(b);AOS.Damage(b,enemy,100,false,100,0,0,0,0,weaponDamage:false);
            Require(before-SkillHealth.Balance256(b)==25600,"Connected encounter source/native physical damage delivery");
            Move(p,[b],[birth],[scene.Spawn],Map.Trammel,MonsterDifficulty.Normal);
            Require(b.Map==p.Map&&!b.Blessed&&corpse.GetAmount(typeof(Gold))==1,"Connected Normal return lost original follow/loot");
            Log.Information("revive-encounter-facet-live-result: passed");
        }
        catch(Exception e){Log.Error(e,"revive-encounter-facet-live-result: failed");}
        finally
        {
#pragma warning disable CS0618
            network.Dispose();
#pragma warning restore CS0618
            enemy?.Delete();b?.Delete();corpse?.Delete();p.Delete();SummonDifficulties.SetFacets(previous);
        }
    }
}
