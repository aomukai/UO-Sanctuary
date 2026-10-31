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

public static class MageBirthChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(MageBirthChecks));
    private sealed record Saved(uint Owner,uint Mage,Point3D Point,int Map,int Life256,int Maximum256,int Rate,int Rank,int Mastery,int MissileRank,int Element,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"mage-birth-fixtures.json");
    public static void Configure(){Register("mage-birth-check",Check);Register("mage-birth-reload-check",Reload);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Mage fixed-point birth, recovery, facet, buff and persistence",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static void Train(PlayerMobile p,CombatAbility ability,int rank)
    {
        string id=SkillMechanics.For(ability).TalentId;
        void Add(string key,int count)
        {
            TalentCatalog.TryGet(DiabloClass.Necromancer,key,out var row);
            foreach(string prior in row.Prerequisites){if(ClassCreation.Profiles.GetRank(p.Serial,prior)==0){Add(prior,1);}}
            while(ClassCreation.Profiles.GetRank(p.Serial,key)<count){Require(ClassCreation.Profiles.TryTrain(p.Serial,key,out _),"Train "+key);}
        }
        Add(id,rank);
    }
    private static SkillValues Values(int level,int rank,int mastery)=>new(SkillMechanics.For(CombatAbility.RaiseSkeletalMage),
        key=>key=="Raise Skeletal Mage"?rank:key=="Skeleton Mastery"?mastery:0,_=>0,level);
    private static void Source()
    {
        using var json=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"mage-birth-source-cases.json")));int count=0;
        foreach(var row in json.RootElement.EnumerateArray())
        {
            int V(int i)=>row[i].GetInt32();var actual=NecromancerMageBirth.Prepare(Values(V(0),V(1),V(3)),V(0),(MonsterDifficulty)V(2));
            Require(actual==new MageBirthStats(V(5),V(6),V(8),V(9),V(10),V(11),V(12))&&V(7)==V(6),"Mage source base/passive/MAXHP case "+count);
            // The source probe also executes resistance writes with supplied
            // bonuses. The live rank7 control below verifies our real supplier.
            Require(Enumerable.Range(13,4).All(i=>V(i)==V(4)),"Source elemental resistance slots");count++;
        }
        Require(count==11475,"Incomplete mage source cases");
    }
    private static Corpse Corpse(PlayerMobile p)
    {
        var mob=new GraveyardZombie{NoKillAwards=true};mob.MoveToWorld(new(p.X+2,p.Y,p.Z),p.Map);mob.Damage(10000,p);if(mob.Alive){mob.Kill();}
        var corpse=mob.Corpse as Corpse;Require(SkeletonPets.Usable(p,corpse),"Actual killed monster did not leave usable corpse");return corpse;
    }
    private static (PlayerMobile Owner,NecromancerMinion Mage) Cast()
    {
        var p=PoisonDaggerChecks.Owner();p.Name="MageBirthFixture";Train(p,CombatAbility.RaiseSkeletalMage,4);Train(p,CombatAbility.SummonResist,7);
        Require(SkillMechanics.Rank(p,CombatAbility.SkeletonMastery)==0,"Unexpected Mastery prerequisite");
        p.Mana=p.ManaMax;var corpse=Corpse(p);int mana=NecromancerEffects.ManaBalance256(p),cost=SkillMechanics.Values(p,CombatAbility.RaiseSkeletalMage).Mana256;
        Require(NecromancerCombat.Release(p,CombatAbility.RaiseSkeletalMage,corpse.Serial,corpse.Location),"Learned mage release refused");
        var mage=p.AllFollowers.OfType<NecromancerMinion>().Single(x=>x.IsMage);mage.AIObject.AITimer.Stop();mage.SuspendActions();
        Require(NecromancerEffects.ManaBalance256(p)==mana-cost&&(corpse.Deleted||corpse.SanctuaryConsumed),"Mage did not consume corpse/pay once");
        Require(!NecromancerCombat.Release(p,CombatAbility.RaiseSkeletalMage,corpse.Serial,mage.Location)&&NecromancerEffects.ManaBalance256(p)==mana-cost,"Consumed corpse recast or paid again");
        Require(mage.Rank==4&&mage.Mastery==0&&mage.SourceLife256==17177&&SkillHealth.Maximum256(mage)==17177&&SkillHealth.Balance256(mage)==17177&&mage.HitsMax==68
            &&mage.SourceRegeneration256==16&&BattleOrders.Regeneration256(mage)==16,"Fractional mage birth lost life or source callback regeneration");
        Require(mage.FireResistance==52&&mage.ColdResistance==52&&mage.EnergyResistance==52&&mage.PoisonResistance==52&&mage.PhysicalResistance==0&&mage.SummonMagicResistance==0,"Real Summon Resist supplier/elements");
        corpse.Delete();return(p,mage);
    }
    private static void Recovery(NecromancerMinion mage)
    {
        long now=Core.TickCount+10000;int max=SkillHealth.Maximum256(mage);SkillHealth.SetBalance256(mage,max-100);mage.ResetLifeRecovery();
        mage.RecoverLife(now);mage.RecoverLife(now+39);Require(SkillHealth.Balance256(mage)==max-100,"Recovery ran before40ms");
        mage.RecoverLife(now+40);Require(SkillHealth.Balance256(mage)==max-84,"Recovery lost16FP source rate");
        SkillHealth.SetBalance256(mage,max-5);mage.RecoverLife(now+80);mage.RecoverLife(now+120);
        Require(SkillHealth.Balance256(mage)==max&&mage.Hits==68&&SkillHealth.Remainder256(mage)==231,"Recovery discarded fractional full-life remainder");
        SkillHealth.SetBalance256(mage,max-100);var map=mage.Map;var point=mage.Location;mage.Internalize();mage.RecoverLife(now+160);
        mage.MoveToWorld(point,map);mage.RecoverLife(now+200);Require(SkillHealth.Balance256(mage)==max-84,"Parked interval repaid recovery");
        TimedEffects.PreventHealing(mage,TimedEffects.Now);int before=SkillHealth.Balance256(mage);mage.RecoverLife(now+240);mage.RecoverLife(now+1240);
        Require(SkillHealth.Balance256(mage)==before,"Prevent Monster Heal did not stop mage recovery");
        TimedEffects.Load(TimedEffects.Snapshot().Where(x=>x.Owner!=mage.Serial.Value).ToArray());mage.ResetLifeRecovery();
    }
    private static void Facets(PlayerMobile p,NecromancerMinion mage)
    {
        var original=SummonDifficulties.Facets();var point=p.Location;var map=p.Map;int mana=NecromancerEffects.ManaBalance256(p);
        var actions=(ClassCombat.For(p).NextAction,p.NextSkillTime,p.NextSpellTime);var location=mage.Location;
        try
        {
            SummonDifficulties.SetFacets([new(Map.Trammel.MapID,MonsterDifficulty.Normal),new(Map.Felucca.MapID,MonsterDifficulty.Nightmare),new(Map.Ilshenar.MapID,MonsterDifficulty.Hell)]);
            foreach(var next in new[]{Map.Felucca,Map.Ilshenar,Map.Trammel})
            {
                int old=SkillHealth.Maximum256(mage),life=SkillHealth.Balance256(mage);p.MoveToWorld(point,next);int max=SkillHealth.Maximum256(mage);
                int expected=(int)((long)life*max/old);Require(SkillHealth.Balance256(mage)==expected&&mage.ControlMaster==p&&mage.Location==location&&mage.Map==map,"Facet exact ratio or actor identity");
                p.MoveToWorld(point,next);Require(SkillHealth.Balance256(mage)==expected,"Same-facet notification rerounded life");
            }
            Require(NecromancerEffects.ManaBalance256(p)==mana&&actions==(ClassCombat.For(p).NextAction,p.NextSkillTime,p.NextSpellTime),"Facet rescaling recast/charged mana");
        }
        finally{p.MoveToWorld(point,map);SummonDifficulties.SetFacets(original);}
    }
    private static void Buff(PlayerMobile p,NecromancerMinion mage)
    {
        var barb=new PlayerMobile{Player=true,Body=0x190};ClassCreation.Profiles.Assign(barb,DiabloClass.Barbarian);
        try
        {
            int basis=SkillHealth.Maximum256(mage),before=SkillHealth.Balance256(mage);BattleOrders.Apply(barb,mage,1,TimedEffects.Now);
            int percent=BattleOrders.Percent(mage),max=basis+SourcePercentage.Calculate(basis,percent,100);
            Require(percent>0&&SkillHealth.Maximum256(mage)==max&&SkillHealth.Balance256(mage)==(int)(before/(double)basis*max)
                &&BattleOrders.Regeneration256(mage)==4*(max>>8)>>4,"Battle Orders did not preserve exact pool/ratio or maxHP callback");
            int buffed=SkillHealth.Balance256(mage);BattleOrders.Clear(mage);
            Require(SkillHealth.Maximum256(mage)==basis&&SkillHealth.Balance256(mage)==(int)(buffed/(double)max*basis)
                &&BattleOrders.Regeneration256(mage)==4*(basis>>8)>>4,"Battle Orders removal rounded exact mage pool");
        }
        finally{BattleOrders.Clear(mage);barb.Delete();}
    }
    private static void Check()
    {
        Source();var(p,mage)=Cast();bool saved=false;
        try
        {
            Recovery(mage);SkillHealth.SetBalance256(mage,9013);Facets(p,mage);Buff(p,mage);
            long maximum=mage.SourceLife256;int missile=mage.MageMissileRank;Train(p,CombatAbility.SkeletonMastery,1);PoisonDaggerChecks.Named(p,"Wormskull");
            Require(mage.Rank==4&&mage.Mastery==0&&mage.SourceLife256==maximum&&mage.MageMissileRank==missile&&mage.FireResistance==52,"Existing mage reread owner talents/gear");
            var fresh=new NecromancerMinion();try{fresh.Prepare(p,CombatAbility.RaiseSkeletalMage);Require(fresh.Rank>mage.Rank&&fresh.Mastery>mage.Mastery&&fresh.SourceLife256>maximum,"Fresh mage missed changed owner ranks");}finally{fresh.Delete();}
            SkillHealth.SetBalance256(mage,9013);var point=p.Location;int map=p.Map.MapID;PersistentMinions.Suspend(p);p.Internalize();
            File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,mage.Serial.Value,point,map,9013,SkillHealth.Maximum256(mage),BattleOrders.Regeneration256(mage),mage.Rank,mage.Mastery,mage.MageMissileRank,mage.MageElement,0)));saved=true;
            Log.Information("Mage birth:11475source cases;paid learned corpse cast,17177FP life,16FP recovery,PMH,facet ratios,Battle Orders and captured ranks pass");
        }
        finally{if(!saved){mage.Delete();p.Delete();}}
    }
    private static void Reload()
    {
        Source();var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);var m=(NecromancerMinion)World.FindMobile((Serial)s.Mage);
        Require(m.ControlMaster==p&&m.Map==Map.Internal&&m.Rank==s.Rank&&m.Mastery==s.Mastery&&m.MageMissileRank==s.MissileRank&&m.MageElement==s.Element
            &&SkillHealth.Balance256(m)==s.Life256&&SkillHealth.Maximum256(m)==s.Maximum256&&BattleOrders.Regeneration256(m)==s.Rate&&m.FireResistance==52,"Reload changed mage birth snapshot/exact resources");
        p.MoveToWorld(s.Point,Map.Maps[s.Map]);p.ClaimAutoStabledPets();m.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);m.AIObject.AITimer.Stop();m.ResetLifeRecovery();
        int life=SkillHealth.Balance256(m);long now=Core.TickCount+10000;m.RecoverLife(now);Require(SkillHealth.Balance256(m)==life,"Offline recovery replayed on load");m.RecoverLife(now+40);
        Require(SkillHealth.Balance256(m)==life+s.Rate,"Loaded source regeneration failed");PersistentMinions.Suspend(p);p.Internalize();
        File.WriteAllText(Manifest,JsonSerializer.Serialize(s with{Life256=life+s.Rate,Generation=s.Generation+1}));
    }
}
