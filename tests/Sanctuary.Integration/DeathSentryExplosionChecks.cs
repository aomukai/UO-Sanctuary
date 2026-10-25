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

public static class DeathSentryExplosionChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(DeathSentryExplosionChecks));
    private sealed record Saved(uint Owner,uint Actor,uint Corpse,CorpseSkillRecord Row,int Rank,int Shots,Point3D Origin,int Map);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"death-sentry-explosion-fixtures.json");
    public static void Configure(){Register("death-explosion-check",Check);Register("death-explosion-reload-check",Reload);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Death Sentry corpse RNG/area engine; learned AI dispatch remains disabled",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool condition,string message){if(!condition){throw new InvalidOperationException(message);}}
    internal static void Train(PlayerMobile owner,CombatAbility ability,int rank)
    {
        var row=SkillMechanics.For(ability);Require(TalentCatalog.TryGet(DiabloClass.Assassin,row.TalentId,out var talent),"Missing Assassin talent");
        foreach(string prerequisite in talent.Prerequisites){Train(owner,SkillMechanics.All.Single(x=>x.TalentId==prerequisite).Ability,1);}
        while(ClassCreation.Profiles.GetRank(owner.Serial,row.TalentId)<rank){Require(ClassCreation.Profiles.TryTrain(owner.Serial,row.TalentId,out _),"Train "+row.TalentId);}
    }
    internal static Corpse Body(PlayerMobile owner,Point3D point)
    {
        var victim=new GraveyardSkeleton{Frozen=true,CantWalk=true,NoKillAwards=true};MonsterVariants.Assign(victim,MonsterDifficulty.Hell);
        victim.MoveToWorld(point,owner.Map);victim.AIObject.AITimer.Stop();Corpse corpse=null;
        var prior=ClassLootHooks.CreatureDied;var seed=CorpseBaseSnapshots.SeedLow;
        ClassLootHooks.CreatureDied=(actor,container)=>{if(actor!=victim){prior?.Invoke(actor,container);return;}CorpseSkillRules.Capture(actor,container);corpse=container as Corpse;corpse?.DropItem(new Gold(1));};
        CorpseBaseSnapshots.SeedLow=()=>31337;
        try{owner.DoHarmful(victim,true);victim.Damage(victim.Hits+1,owner);CombatDamage.Finish(victim,owner);}
        finally{ClassLootHooks.CreatureDied=prior;CorpseBaseSnapshots.SeedLow=seed;}
        Require(corpse is{Deleted:false} && CorpseSkillRules.Profiles.Find(corpse)?.BaseLife?.Seed==SourceRandom.InitLow(31337),"Actual death did not retain supplied native seed");
        corpse.BeginDecay(TimeSpan.FromHours(1));return corpse;
    }
    internal static BladeMissileTarget Target(Corpse corpse,int dx,int dy=0)
    {
        var target=new BladeMissileTarget{Name="DeathSentryAreaFixture",NoKillAwards=true};target.MoveToWorld(new(corpse.X+dx,corpse.Y+dy,corpse.Z),corpse.Map);target.AIObject.AITimer.Stop();
        target.SetHits(100000);target.Hits=target.HitsMax;foreach(var resistance in new[]{ResistanceType.Physical,ResistanceType.Fire}){target.SetResistance(resistance,0);}return target;
    }
    private static CorpseExplosionPacket Expected(AssassinSentryBody actor,CorpseSkillRecord row,out ulong advanced)
    {
        var c=actor.Prepared.Creation;var v=SentryNpcSkills.Values(312,c.Rank,c.Level,c.Skill);ulong stream=row.BaseLife.Seed.Value;
        var packet=CorpseExplosionPackets.Create(row.BaseLife.Base256,c.Level,row.BaseLife.Level,(int)v.Eval("aurarangecalc"),(int)v.Eval("calc1"),(int)v.Eval("calc2"),(int)v.Eval("calc3"),bound=>(int)SourceRandom.Limited(ref stream,bound));
        advanced=stream;return packet;
    }
    private static void Area(PlayerMobile owner,AssassinSentryBody actor,Corpse corpse)
    {
        var inner=Target(corpse,2);var edge=Target(corpse,4);var outside=Target(corpse,5);
        var physicalImmune=Target(corpse,1,1);physicalImmune.SetResistance(ResistanceType.Physical,100);
        var fireImmune=Target(corpse,1,-1);fireImmune.SetResistance(ResistanceType.Fire,100);
        int hp=SkillHealth.Balance256(inner),ehp=SkillHealth.Balance256(edge),ohp=SkillHealth.Balance256(outside);
        int php=SkillHealth.Balance256(physicalImmune),fhp=SkillHealth.Balance256(fireImmune),mana=NecromancerEffects.ManaBalance256(owner);
        var before=CorpseSkillRules.Profiles.Find(corpse);var packet=Expected(actor,before,out ulong advanced);
        Require(packet.InnerSquared==49 && packet.OuterRange==8 && actor.TrapTarget(owner,inner),"Rank6 split-radius/actual allegiance control");
        var result=AssassinDeathSentryCorpses.Explode(actor,corpse);
        int pierce=actor.Prepared.Creation.Passive("passive_fire_pierce");
        int physical=NamedElements.Resolve256(actor,inner,packet.Physical256,"physical",enhance:false,pierceOverride:0).Damage256;
        int fire=NamedElements.Resolve256(actor,inner,packet.Fire256,"fire",enhance:false,pierceOverride:pierce).Damage256;
        Require(result.Status==DeathExplosionStatus.Success && result.Targets==4 && result.Packet==packet && result.AdvancedSeed==advanced
            && SkillHealth.Balance256(inner)==hp-physical-fire && SkillHealth.Balance256(edge)==ehp-fire && SkillHealth.Balance256(outside)==ohp
            && SkillHealth.Balance256(physicalImmune)==php-fire && SkillHealth.Balance256(fireImmune)==fhp-physical
            && NecromancerEffects.ManaBalance256(owner)==mana,"Native shared packet/typed immunity reductions/physical-only edge clip or NPC mana isolation");
        var row=CorpseSkillRules.Profiles.Find(corpse);
        Require(row.NoSelect && row.BaseLife.Seed==advanced && corpse.SanctuaryConsumed && corpse.Animated && corpse.GetAmount(typeof(Gold))==1,"Atomic seed/reservation/consumption or retained loot");
        Require(AssassinDeathSentryCorpses.Explode(actor,corpse).Status==DeathExplosionStatus.NotEligible && SkillHealth.Balance256(inner)==hp-physical-fire,"Consumed corpse fired twice");
        inner.Delete();edge.Delete();outside.Delete();physicalImmune.Delete();fireImmune.Delete();
    }
    private static void Check()
    {
        DeathSentrySourceGoldens.Run();var owner=new PlayerMobile{Body=0x190,Player=true,Name="DeathSentryCorpseFixture"};owner.AddItem(new Backpack());
        ClassCreation.Profiles.Assign(owner,DiabloClass.Assassin);ClassCreation.Profiles.AdvanceLevel(owner,99);owner.RawStr=owner.RawDex=owner.RawInt=200;FixtureSpace.Corridor(owner,6060,30,2000);
        Train(owner,CombatAbility.DeathSentry,1);Train(owner,CombatAbility.FireBlast,10);
        var staff=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey=="Mang Song's Lesson").Id,99,new Random(173),true);
        Require(owner.EquipItem(staff),"Actual +5/pierce staff");ClassGear.Refresh(owner);
        var actor=AssassinSentryBody.CreatePrepared(owner,new(owner.X+2,owner.Y,owner.Z),TimedEffects.Now,SourceRandom.InitLow(23),CombatAbility.DeathSentry);
        var c=actor.Prepared.Creation;Require(c.Rank==6 && c.Passives.Length==7 && c.Skill("Fire Trauma")==10 && AssassinSentryCreation.Shots(c)==8
            && actor.Prepared.DeathControl.Ai.RemainingShots==-1 && actor.Prepared.Control==null && actor.Prepared.ControllerFault==null,"Actual Death Sentry creation/passives/hard-only shots/distinct AI context");
        // Owner equipment/location changes must not replace the captured trap
        // rank/pierce or drag its corpse targeting into the new facet.
        owner.AddToBackpack(staff);ClassGear.Refresh(owner);
        Require(SkillMechanics.Rank(owner,CombatAbility.DeathSentry)==1 && actor.Prepared.Creation.Rank==6,"Actual captured soft-rank contrast");
        var corpse=Body(owner,new(owner.X+8,owner.Y,owner.Z));var origin=owner.Location;var map=owner.Map;
        owner.MoveToWorld(origin,Map.Felucca);try{Area(owner,actor,corpse);}finally{owner.MoveToWorld(origin,map);}
        var pending=Body(owner,new(owner.X+8,owner.Y,owner.Z));var retained=CorpseSkillRules.Profiles.Find(pending);
        var missing=retained with{BaseLife=null};CorpseSkillRules.Profiles.Store(missing);
        Require(AssassinDeathSentryCorpses.Explode(actor,pending).Status==DeathExplosionStatus.MissingSnapshot && !pending.SanctuaryConsumed,"Missing history invented a packet");
        CorpseSkillRules.Profiles.Store(retained with{BaseLife=retained.BaseLife with{Seed=null}});
        Require(AssassinDeathSentryCorpses.Explode(actor,pending).Status==DeathExplosionStatus.MissingSeed && !pending.SanctuaryConsumed,"Missing old seed invented RNG");
        CorpseSkillRules.Profiles.Store(retained);
        var marker=Target(pending,1);var selected=AssassinDeathSentryCorpses.Select(actor,owner,marker,c.Rank);
        Require(selected.Corpse==pending && selected.OriginalGuid==(int)retained.BaseLife.OriginalMonsterSerial,"Native corpse search lost original monster identity");marker.Delete();
        // Save the actor and an unused corpse: RNG must resume from this exact
        // seed, without replaying the preceding consumed corpse.
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(owner.Serial.Value,actor.Serial.Value,pending.Serial.Value,retained,c.Rank,AssassinSentryCreation.Shots(c),owner.Location,owner.Map.MapID)));
        Require(!AssassinCombat.Implemented(CombatAbility.DeathSentry),"Prepared engine enabled an unfinished learned talent");
    }
    private static void Reload()
    {
        DeathSentrySourceGoldens.Run();var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));
        var owner=(PlayerMobile)World.FindMobile((Serial)saved.Owner);var actor=(AssassinSentryBody)World.FindMobile((Serial)saved.Actor);var corpse=(Corpse)World.FindItem((Serial)saved.Corpse);
        if(owner.Map==Map.Internal){owner.MoveToWorld(saved.Origin,Map.Maps[saved.Map]);}
        Require(actor.SummonMaster==owner && actor.Prepared.Creation.Rank==saved.Rank && AssassinSentryCreation.Shots(actor.Prepared.Creation)==saved.Shots
            && CorpseSkillRules.Profiles.Find(corpse)==saved.Row && !corpse.SanctuaryConsumed,"Saved source actor/skills/unused corpse RNG changed");
        Area(owner,actor,corpse);
        var pending=Body(owner,new(owner.X+8,owner.Y,owner.Z));
        File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{Corpse=pending.Serial.Value,Row=CorpseSkillRules.Profiles.Find(pending)}));
    }
}
