// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Logging;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class ClayGolemBirthChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(ClayGolemBirthChecks));
    private sealed record Saved(uint Owner,uint Actor,uint Historical,uint HistoricalOwner,int Life,int Maximum,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"clay-golem-birth-fixtures.json");
    public static void Configure(){Register("clay-golem-birth-check",Check);Register("clay-golem-birth-reload-check",Reload);}
    private static void Register(string name,Action run)=>ConsoleInputHandler.RegisterCommand(name,"Clay Golem exact birth/resources and retained historical actors",_=>Core.LoopContext.Post(()=>
    {try{run();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool yes,string why){if(!yes){throw new InvalidOperationException(why);}}
    private static SkillValues Values(int owner,int rank,int gm,int blood,int iron,int fire)=>new(SkillMechanics.For(CombatAbility.ClayGolem),
        key=>key=="Clay Golem"?rank:key=="Golem Mastery"?gm:0,key=>key=="BloodGolem"?blood:key=="IronGolem"?iron:key=="FireGolem"?fire:0,owner);
    private static void Source()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"clay-golem-birth-source-cases.json")));int safe=0,unsafeCount=0;
        foreach(var row in doc.RootElement.EnumerateArray())
        {
            int V(int i)=>row[i].GetInt32();var values=Values(V(0),V(1),V(3),V(4),V(5),V(6));
            try
            {
                var b=NecromancerClayBirth.Prepare(values,V(0),(MonsterDifficulty)V(2));
                Require(V(8)==0&&b==new ClayBirthStats(V(9),V(10),V(12),V(13),V(14),V(15),V(16),V(17),V(18))&&V(10)==V(11),"Complete Clay birth/callback context "+safe);
                Require(V(19)==1&&Enumerable.Range(20,4).All(i=>V(i)==V(7)),"Source Clay event registration/resistance writes");safe++;
            }
            catch(NotSupportedException){Require(V(8)==1,"Unexpected safe Clay birth refusal");unsafeCount++;}
        }
        Require(safe==10377&&unsafeCount==1098,"Incomplete safe/unsafe Clay source contexts");
        Require(NecromancerClayBirth.Prepare(Values(99,4,0,0,0,0),99,MonsterDifficulty.Nightmare) is{Life256:91840,Regeneration256:67},"Rank4Nightmare must preserve358.75HP and67FP recovery");
        var cross=Values(99,255,20,20,20,20);Require(NecromancerClayBirth.Prepare(cross,99,MonsterDifficulty.Normal).Life256>0,"Cross-facet control must start safe in Normal");
        bool refused=false;try{NecromancerClayBirth.Preflight(cross,99);}catch(NotSupportedException){refused=true;}Require(refused,"Normal-safe/Hell-unsafe Clay cast was not preflighted");
    }
    private static void Train(PlayerMobile p,string id,int rank)
    {
        TalentCatalog.TryGet(DiabloClass.Necromancer,id,out var row);
        foreach(string prior in row.Prerequisites){if(ClassCreation.Profiles.GetRank(p.Serial,prior)==0){Train(p,prior,1);}}
        while(ClassCreation.Profiles.GetRank(p.Serial,id)<rank){Require(ClassCreation.Profiles.TryTrain(p.Serial,id,out _),"Train "+id);}
    }
    private static PlayerMobile Owner()
    {
        var p=PoisonDaggerChecks.Owner();p.Name="ClayGolemBirthFixture";
        while(ClassCreation.Profiles.GetRank(p.Serial,"necromancer.claygolem")<4){Require(ClassCreation.Profiles.TryTrain(p.Serial,"necromancer.claygolem",out _),"Train Clay4");}
        return p;
    }
    private static ClayGolemBody Cast(PlayerMobile p)
    {
        p.Mana=p.ManaMax;int mana=NecromancerEffects.ManaBalance256(p),cost=SkillMechanics.Values(p,CombatAbility.ClayGolem).Mana256;
        Require(NecromancerCombat.Release(p,CombatAbility.ClayGolem,Serial.Zero,new(p.X+1,p.Y,p.Z)),"Learned Clay release");
        var b=NecromancerGolems.For(p).OfType<ClayGolemBody>().Single();b.AIObject.AITimer.Stop();b.SuspendActions();
        Require(NecromancerEffects.ManaBalance256(p)==mana-cost&&SkillHealth.Balance256(b)==52480&&SkillHealth.Maximum256(b)==52480&&b.SourceRegeneration256==38,"Clay paid once/birth205HP/source38FP recovery");
        Require(b.PhysicalResistance==25&&b.ColdResistance==50&&b.EnergyResistance==20,"Current Clay innate resistances");return b;
    }
    private static void Recovery(ClayGolemBody b)
    {
        long now=Core.TickCount+10000;int max=SkillHealth.Maximum256(b);SkillHealth.SetBalance256(b,max-100);b.ResetLifeRecovery();
        b.RecoverLife(now);b.RecoverLife(now+39);Require(SkillHealth.Balance256(b)==max-100,"Clay recovered before40ms");
        b.RecoverLife(now+40);Require(SkillHealth.Balance256(b)==max-62,"Clay lost38FP recovery rate");
        b.RecoverLife(now+80);b.RecoverLife(now+120);Require(SkillHealth.Balance256(b)==max,"Clay recovery overflow");
        SkillHealth.SetBalance256(b,max-257);TimedEffects.PreventHealing(b,TimedEffects.Now);b.RecoverLife(now+160);b.RecoverLife(now+1160);
        Require(SkillHealth.Balance256(b)==max-257,"Clay PMH recovery");TimedEffects.Load(TimedEffects.Snapshot().Where(x=>x.Owner!=b.Serial.Value).ToArray());b.ResetLifeRecovery();
    }
    private static void Facets(PlayerMobile p,ClayGolemBody b)
    {
        var original=SummonDifficulties.Facets();var map=p.Map;var other=map==Map.Felucca?Map.Trammel:Map.Felucca;var point=p.Location;
        int mana=NecromancerEffects.ManaBalance256(p);
        try
        {
            SummonDifficulties.SetFacets([new(map.MapID,MonsterDifficulty.Normal),new(other.MapID,MonsterDifficulty.Nightmare),new(Map.Ilshenar.MapID,MonsterDifficulty.Hell)]);
            foreach(var next in new[]{other,Map.Ilshenar,map})
            {
                int before=SkillHealth.Balance256(b),old=SkillHealth.Maximum256(b);p.MoveToWorld(point,next);int max=SkillHealth.Maximum256(b);
                Require(SkillHealth.Balance256(b)==(int)((long)before*max/old),"Clay facet did not preserve relative fractional life");
                if(next==other){Require(max==91840&&b.HitsMax==359&&b.SourceRegeneration256==67,"Nightmare Clay fractional maximum/regeneration");}
                int life=SkillHealth.Balance256(b);p.MoveToWorld(point,next);Require(SkillHealth.Balance256(b)==life,"Repeated Clay facet notification healed/rounded again");
            }
            Require(NecromancerEffects.ManaBalance256(p)==mana,"Clay facet rescaling recast/paid mana");
        }
        finally{p.MoveToWorld(point,map);SummonDifficulties.SetFacets(original);}
    }
    private static void Buff(ClayGolemBody b)
    {
        var p=new PlayerMobile{Player=true,Body=0x190};ClassCreation.Profiles.Assign(p,DiabloClass.Barbarian);
        try
        {
            int max=SkillHealth.Maximum256(b),life=SkillHealth.Balance256(b);BattleOrders.Apply(p,b,1,TimedEffects.Now);int buffed=SkillHealth.Maximum256(b);
            Require(buffed==max+SourcePercentage.Calculate(max,BattleOrders.LifePercent(b),100)&&SkillHealth.Balance256(b)==(int)(life/(double)max*buffed)
                &&BattleOrders.Regeneration256(b)==3*(buffed>>8)>>4,"Clay Battle Orders maximum/ratio/callback");
            BattleOrders.Clear(b);Require(SkillHealth.Maximum256(b)==max,"Clay Battle Orders removal maximum");
        }
        finally{BattleOrders.Clear(b);p.Delete();}
    }
    private static void Check()
    {
        Source();var p=Owner();var b=Cast(p);Recovery(b);Facets(p,b);Buff(b);
        var before=(b.SourceLife256,b.CombatProfile,b.SourceRegeneration256);PoisonDaggerChecks.Named(p,"Wormskull");
        Require(before==(b.SourceLife256,b.CombatProfile,b.SourceRegeneration256),"Later owner soft ranks rewrote captured Clay birth");
        foreach(var pair in new[]{("necromancer.golemmastery",7),("necromancer.bloodgolem",2),("necromancer.irongolem",3),("necromancer.firegolem",4),("necromancer.summonresist",7)}){Train(p,pair.Item1,pair.Item2);}
        p.Mana=p.ManaMax;var previous=b;int mana=NecromancerEffects.ManaBalance256(p),cost=SkillMechanics.Values(p,CombatAbility.ClayGolem).Mana256;
        Require(NecromancerCombat.Release(p,CombatAbility.ClayGolem,Serial.Zero,new(p.X+2,p.Y,p.Z)),"Learned synergy/gear Clay recast");b=NecromancerGolems.For(p).OfType<ClayGolemBody>().Single();b.AIObject.AITimer.Stop();b.SuspendActions();
        Require(previous.Deleted&&NecromancerEffects.ManaBalance256(p)==mana-cost&&b.Rank==5&&b.SourceLife256==648*256&&b.SourceRegeneration256==121&&b.VelocityPercent==24,"Actual soft Mastery/hard Blood synergy birth or replacement/payment");
        var expected=NecromancerClayBirth.Prepare(b.CapturedValues,99,MonsterDifficulty.Normal);
        Require(expected.DamagePercent==164&&b.CombatProfile.Defense==expected.Armor&&b.CombatProfile.AttackRating==expected.Attack&&b.FireResistance==54&&b.PoisonResistance==54,"Hard Iron/Fire or effective Mastery/SummonResist passive capture");
        var historicalOwner=Owner();var legacy=new NecromancerMinion();legacy.Prepare(historicalOwner,CombatAbility.ClayGolem);Require(legacy.SetControlMaster(historicalOwner),"Historical Clay owner");legacy.Internalize();legacy.AIObject.AITimer.Stop();PersistentMinions.Profiles.Track(legacy);PersistentMinions.Suspend(historicalOwner);historicalOwner.Internalize();
        SkillHealth.SetBalance256(b,SkillHealth.Maximum256(b)-257);PersistentMinions.Suspend(p);p.Internalize();
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,b.Serial.Value,legacy.Serial.Value,historicalOwner.Serial.Value,SkillHealth.Balance256(b),SkillHealth.Maximum256(b),0)));
        Log.Information("Clay birth:10377safe complete-source matches/1098unsafe refusals;learned once-only mana,exact fractional life,source regeneration/PMH,facet ratios,Battle Orders,captured ranks and historical type persistence pass");
    }
    private static void Reload()
    {
        Source();var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);var b=(ClayGolemBody)World.FindMobile((Serial)s.Actor);var old=World.FindMobile((Serial)s.Historical);
        Require(b.ControlMaster==p&&b.Map==Map.Internal&&SkillHealth.Balance256(b)==s.Life&&SkillHealth.Maximum256(b)==s.Maximum&&b.SourceRegeneration256==121,"Saved exact Clay resources/owner/parked type");
        Require(old?.GetType()==typeof(NecromancerMinion)&&((NecromancerMinion)old).Kind==CombatAbility.ClayGolem&&((NecromancerMinion)old).ControlMaster?.Serial.Value==s.HistoricalOwner,"Historical Clay save type was migrated silently");
        File.WriteAllText(Manifest,JsonSerializer.Serialize(s with{Generation=s.Generation+1}));
    }
}
