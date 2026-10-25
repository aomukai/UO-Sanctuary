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
public static class CorpseExplosionCastChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(CorpseExplosionCastChecks));
    private sealed record Saved(uint Owner,uint Corpse,CorpseSkillRecord Row,Point3D Origin,int Map,int Mana);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"corpse-explosion-cast-fixtures.json");
    public static void Configure(){Register("corpse-cast-check",Check);Register("corpse-cast-reload-check",Reload);}
    private static void Register(string name,Action run)=>ConsoleInputHandler.RegisterCommand(name,"Player74 shared corpse packets, orphan metadata, defenses/potions and persistence",_=>Core.LoopContext.Post(()=>
    {try{run();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool condition,string why){if(!condition){throw new InvalidOperationException(why);}}
    private static void Train(PlayerMobile owner,CombatAbility ability,int rank)
    {
        var row=SkillMechanics.For(ability);Require(TalentCatalog.TryGet(row.CharacterClass,row.TalentId,out var talent),"Missing Necromancer talent");
        foreach(var prerequisite in talent.Prerequisites){Train(owner,SkillMechanics.All.Single(x=>x.TalentId==prerequisite).Ability,1);}
        while(ClassCreation.Profiles.GetRank(owner.Serial,row.TalentId)<rank){bool learned=ClassCreation.Profiles.TryTrain(owner.Serial,row.TalentId,out string reason);Require(learned,"Train "+row.TalentId+": "+reason);}
    }
    private static void Goldens()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"necro-corpse-explosion-source-probe.json")));int cases=0;
        foreach(var row in doc.RootElement.EnumerateArray())
        {
            int V(int i)=>row[i].GetInt32();if(V(0)!=15){continue;}
            int range=8+V(1)-1,roll=V(4),dx=range/2+V(5)-1;
            var packet=CorpseExplosionPackets.Create(CorpseExplosionPackets.MonsterBase256(V(2),V(2)+3),50,V(3),range,70,120,50,
                bound=>roll==0?0:roll==1?bound/2:bound-1);
            var hit=packet.Recipient(dx*dx);
            Require(packet.OuterRange==V(8) && hit.Physical256==V(10) && hit.Fire256==V(11),"Player74 complete do55 source comparison");cases++;
        }
        Require(cases==540,"Player corpse source inventory incomplete");Log.Information("Corpse cast:540applicable player74 complete source vectors passed");
    }
    private static Corpse Body(PlayerMobile owner)
    {
        var corpse=DeathSentryExplosionChecks.Body(owner,new(owner.X+6,owner.Y,owner.Z));
        // Retain historical source data after the original unit is gone.
        corpse.Owner?.Delete();return corpse;
    }
    private static CorpseExplosionPacket Expected(PlayerMobile owner,Corpse corpse,out ulong advanced)
    {
        var v=SkillMechanics.Values(owner,CombatAbility.CorpseExplosion);var life=CorpseSkillRules.Profiles.Find(corpse).BaseLife;ulong seed=life.Seed.Value;
        var packet=CorpseExplosionPackets.Create(life.Base256,ClassStats.Level(owner),life.Level,(int)v.Eval("aurarangecalc"),(int)v.Eval("calc1"),(int)v.Eval("calc2"),(int)v.Eval("calc3"),bound=>(int)SourceRandom.Limited(ref seed,bound));advanced=seed;return packet;
    }
    private static void Area(PlayerMobile owner,Corpse corpse,bool potion=false)
    {
        var inner=DeathSentryExplosionChecks.Target(corpse,1);var edge=DeathSentryExplosionChecks.Target(corpse,2,1);var outside=DeathSentryExplosionChecks.Target(corpse,3);
        var physicalImmune=DeathSentryExplosionChecks.Target(corpse,1,1);physicalImmune.SetResistance(ResistanceType.Physical,100);
        var fireImmune=DeathSentryExplosionChecks.Target(corpse,1,-1);fireImmune.SetResistance(ResistanceType.Fire,100);
        var targets=new[]{inner,edge,outside,physicalImmune,fireImmune};
        foreach(var target in targets){foreach(var r in new[]{ResistanceType.Cold,ResistanceType.Energy,ResistanceType.Poison}){target.SetResistance(r,0);}}
        var before=targets.Select(SkillHealth.Balance256).ToArray();var packet=Expected(owner,corpse,out ulong advanced);var v=SkillMechanics.Values(owner,CombatAbility.CorpseExplosion);
        Require(v.Rank==2 && packet.InnerSquared==16 && packet.OuterRange==5,"Rank2 odd-edge scene");
        int mana=NecromancerEffects.ManaBalance256(owner);
        Require(NecromancerCombat.ValidTarget(owner,CombatAbility.CorpseExplosion,corpse.Serial,corpse.Location)
            && NecromancerCombat.Release(owner,CombatAbility.CorpseExplosion,corpse.Serial,corpse.Location),"Learned release rejected saved orphan corpse");
        for(int i=0;i<targets.Length;i++)
        {
            int expected=0;
            if(i!=2)
            {
                int physical=i is 1 or 3?0:packet.Physical256,fire=i==4?0:packet.Fire256;expected=physical+fire;
                if(potion)
                {
                    double added=(i==1?packet.Fire256:packet.Physical256+packet.Fire256)/256.0*.1;
                    foreach(var channel in new[]{"fire","cold","ltng","pois"})
                    {if(i==4 && channel=="fire"){continue;}expected+=NamedElements.To256(NamedElements.Enhance(owner,added,channel));}
                }
            }
            Require(before[i]-SkillHealth.Balance256(targets[i])==expected,"Shared fractional packet/edge/immunity/mastery/potion mismatch at "+i);
        }
        Require(NecromancerEffects.ManaBalance256(owner)==mana-v.Mana256 && corpse.SanctuaryConsumed
            && CorpseSkillRules.Profiles.Find(corpse) is{NoSelect:true,BaseLife.Seed:not null} row && row.BaseLife.Seed==advanced
            && corpse.Items.OfType<Gold>().Sum(x=>x.Amount)==1,"Once-paid seed/consumption/retained loot");
        mana=NecromancerEffects.ManaBalance256(owner);Require(!NecromancerCombat.Release(owner,CombatAbility.CorpseExplosion,corpse.Serial,corpse.Location)
            && NecromancerEffects.ManaBalance256(owner)==mana,"Consumed corpse charged twice");
        foreach(var target in targets){target.Delete();}corpse.Delete();
    }
    private static void Mastery(PlayerMobile scene)
    {
        var caster=new PlayerMobile{Body=0x190,Player=true,Name="CorpsePayloadMasteryFixture"};caster.AddItem(new Backpack());ClassCreation.Profiles.Assign(caster,DiabloClass.Sorceress);ClassCreation.Profiles.AdvanceLevel(caster,99);caster.RawStr=caster.RawDex=caster.RawInt=200;caster.MoveToWorld(new(scene.X,scene.Y+3,scene.Z),scene.Map);
        Train(caster,CombatAbility.FireMastery,20);
        var orb=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey=="Eschuta's temper").Id,99,new Random(71),true);Require(caster.EquipItem(orb),"Actual available mastery orb");ClassGear.Refresh(caster);
        var corpse=Body(scene);var target=DeathSentryExplosionChecks.Target(corpse,1);foreach(var r in new[]{ResistanceType.Cold,ResistanceType.Energy,ResistanceType.Poison}){target.SetResistance(r,0);}
        Require(NamedElements.MasteryPercent(caster,"fire")>0 && NamedElements.MasteryPercent(caster,"ltng")>0,"Real skill/item masteries missing");
        int before=SkillHealth.Balance256(target);SkillCombat.HitParts(caster,target,new(100,100,0,0,0),true,spell:true,enhanceBase:false);
        Require(before-SkillHealth.Balance256(target)==200*256,"Prepared corpse payload inherited fire mastery");
        before=SkillHealth.Balance256(target);SkillCombat.HitParts(caster,target,new(100,100,0,0,0),true,spell:true);
        Require(before-SkillHealth.Balance256(target)>200*256,"Default spell mastery control was removed");
        Require(CombatPotions.Apply(caster,3),"Mastered spell potion");int expected=200*256;
        foreach(var channel in new[]{"fire","cold","ltng","pois"}){expected+=NamedElements.To256(NamedElements.Enhance(caster,20,channel));}
        before=SkillHealth.Balance256(target);SkillCombat.HitParts(caster,target,new(100,100,0,0,0),true,spell:true,enhanceBase:false);
        Require(before-SkillHealth.Balance256(target)==expected,"Prepared packet changed approved mastered potion additions");
        target.Delete();corpse.Delete();caster.Delete();
    }
    private static void Save(PlayerMobile owner)
    {
        owner.Mana=owner.ManaMax;var corpse=Body(owner);var row=CorpseSkillRules.Profiles.Find(corpse);
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(owner.Serial.Value,corpse.Serial.Value,row,owner.Location,owner.Map.MapID,NecromancerEffects.ManaBalance256(owner))));
    }
    private static void Check()
    {
        Goldens();var owner=new PlayerMobile{Body=0x190,Player=true,Name="CorpseCastFixture"};owner.AddItem(new Backpack());ClassCreation.Profiles.Assign(owner,DiabloClass.Necromancer);ClassCreation.Profiles.AdvanceLevel(owner,99);
        FixtureSpace.Corridor(owner,6860,30,2000);Train(owner,CombatAbility.CorpseExplosion,1);
        var robe=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey=="Tarnhelm").Id,99,new Random(71),true);Require(owner.EquipItem(robe),"Actual Tarnhelm soft rank");ClassGear.Refresh(owner);owner.Mana=owner.ManaMax;
        var pending=Body(owner);var original=CorpseSkillRules.Profiles.Find(pending);int mana=NecromancerEffects.ManaBalance256(owner);
        foreach(var row in new[]{original with{BaseLife=null},original with{BaseLife=original.BaseLife with{Seed=null}}})
        {CorpseSkillRules.Profiles.Store(row);Require(!CorpseExplosionCasts.Supported(pending) && !NecromancerCombat.Release(owner,CombatAbility.CorpseExplosion,pending.Serial,pending.Location)
            && !pending.SanctuaryConsumed && NecromancerEffects.ManaBalance256(owner)==mana,"Unsupported old metadata invented base life/seed or charged mana");}
        foreach(var row in new[]{original with{Owned=true},original with{Selectable=false},original with{Shattered=true},original with{NoSelect=true}})
        {CorpseSkillRules.Profiles.Store(row);Require(!NecromancerCombat.ValidTarget(owner,CombatAbility.CorpseExplosion,pending.Serial,pending.Location)
            && !NecromancerCombat.Release(owner,CombatAbility.CorpseExplosion,pending.Serial,pending.Location)
            && !pending.SanctuaryConsumed && NecromancerEffects.ManaBalance256(owner)==mana,"Historical corpse eligibility was lost with its original owner or charged mana");}
        CorpseSkillRules.Profiles.Store(original);Area(owner,pending);Require(CombatPotions.Apply(owner,3),"Approved spell potion");Area(owner,Body(owner),true);TimedEffects.Clear(owner);Mastery(owner);Save(owner);
    }
    private static void Reload()
    {
        Goldens();var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var owner=(PlayerMobile)World.FindMobile((Serial)saved.Owner);var corpse=(Corpse)World.FindItem((Serial)saved.Corpse);
        if(owner.Map==Map.Internal){owner.MoveToWorld(saved.Origin,Map.Maps[saved.Map]);}
        Require(CorpseSkillRules.Profiles.Find(corpse)==saved.Row && corpse.Owner==null && NecromancerEffects.ManaBalance256(owner)==saved.Mana,"Saved orphan source data/RNG/resource changed: row="+(CorpseSkillRules.Profiles.Find(corpse)==saved.Row)+", owner="+(corpse.Owner?.Serial.Value.ToString()??"null")+", mana="+NecromancerEffects.ManaBalance256(owner)+"/"+saved.Mana+", max="+owner.ManaMax);Area(owner,corpse);Save(owner);
    }
}
