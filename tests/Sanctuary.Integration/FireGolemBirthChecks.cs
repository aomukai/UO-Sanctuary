// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Server;
using Server.Logging;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary.Integration;

public static class FireGolemBirthChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(FireGolemBirthChecks));
    private sealed record Saved(uint Owner,uint Actor,uint Historical,uint HistoricalOwner,int Life,int Maximum,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"fire-golem-birth-fixtures.json");
    public static void Configure()
    {
        Register("fire-golem-birth-check",Check);Register("fire-golem-birth-reload-check",Reload);
        foreach(bool quick in new[]{false,true}){bool q=quick;Register(q?"fire-golem-birth-quick-check":"fire-golem-birth-right-check",()=>FixtureNetwork.Connected(q?"fire-golem-birth-quick-result":"fire-golem-birth-right-result",n=>Input(n,q)));}
    }
    private static void Register(string name,Action run)=>ConsoleInputHandler.RegisterCommand(name,"Fire Golem exact birth/resources and retained historical actors",_=>Core.LoopContext.Post(()=>
    {try{run();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool yes,string why){if(!yes){throw new InvalidOperationException(why);}}
    private static SkillValues Values(int owner,int rank,int gm,int blood,int clay,int iron)=>new(SkillMechanics.For(CombatAbility.FireGolem),
        key=>key=="FireGolem"?rank:key=="Golem Mastery"?gm:0,key=>key=="BloodGolem"?blood:key=="Clay Golem"?clay:key=="IronGolem"?iron:0,owner);
    private static void Source()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"fire-golem-birth-source-cases.json")));int count=0,independent=0;
        foreach(var row in doc.RootElement.EnumerateArray())
        {
            int V(int i)=>row[i].GetInt32();var values=Values(V(0),V(1),V(3),V(4),V(5),V(6));
            var b=NecromancerFireBirth.Prepare(values,V(0),(MonsterDifficulty)V(2));
            Require(V(8)==0&&b==new FireBirthStats(V(9),V(10),V(12),V(13),V(14),V(15),V(16),V(17),V(18),V(19),V(20),V(21))&&V(10)==V(11),"Complete Fire birth/callback "+count);
            Require(V(22)==0&&V(23)==1&&Enumerable.Range(24,3).All(i=>V(i)==V(7))&&b.FireResistance+b.FireAbsorb==200,"Fire aura selection/absorb/Summon Resist exclusion");
            if(V(3) is 8 or 2){independent++;}count++;
        }
        Require(count==16065&&independent==4590,"Incomplete Fire source matrix");
        Require(NecromancerFireBirth.Prepare(Values(99,4,0,0,0,0),99,MonsterDifficulty.Nightmare) is{Life256:156928,Regeneration256:114,FireResistance:142,FireAbsorb:58,HolyFireRank:10},"Fire rank4Nightmare fixed noRatio bases");
        NecromancerFireBirth.Preflight(Values(99,255,255,20,20,20),99);
        bool refused=false;try{NecromancerFireBirth.Preflight(Values(99,256,0,0,0,0),99);}catch(NotSupportedException){refused=true;}Require(refused,"Out-of-range Fire birth domain was not refused");
    }
    private static void Train(PlayerMobile p,string id,int rank)
    {
        TalentCatalog.TryGet(DiabloClass.Necromancer,id,out var row);
        foreach(string prior in row.Prerequisites){if(ClassCreation.Profiles.GetRank(p.Serial,prior)==0){Train(p,prior,1);}}
        while(ClassCreation.Profiles.GetRank(p.Serial,id)<rank){Require(ClassCreation.Profiles.TryTrain(p.Serial,id,out _),"Train "+id);}
    }
    private static PlayerMobile Owner()
    {
        var p=PoisonDaggerChecks.Owner();p.Name="FireGolemBirthFixture";
        Train(p,"necromancer.firegolem",4);
        Require(SkillBindings.Instance.Assign(p,false,CombatAbility.FireGolem)&&SkillBindings.Instance.AssignQuick(p,7,CombatAbility.FireGolem),"Fire right/quick bindings");
        return p;
    }
    private static FireGolemBody Cast(PlayerMobile p)
    {
        p.Mana=p.ManaMax;int mana=NecromancerEffects.ManaBalance256(p),cost=SkillMechanics.Values(p,CombatAbility.FireGolem).Mana256;
        Require(NecromancerCombat.Release(p,CombatAbility.FireGolem,Serial.Zero,new(p.X+1,p.Y,p.Z)),"Learned Fire release");
        var b=NecromancerGolems.For(p).OfType<FireGolemBody>().Single();b.AIObject.AITimer.Stop();b.SuspendActions();
        Require(NecromancerEffects.ManaBalance256(p)==mana-cost&&SkillHealth.Balance256(b)==84134&&SkillHealth.Maximum256(b)==84134&&b.SourceRegeneration256==61,"Fire paid once/birth328.6484375HP/source61FP recovery");
        Require(b.PhysicalResistance==0&&b.ColdResistance==0&&b.EnergyResistance==0&&b.PoisonResistance==0&&b.FireResistance==142&&b.FireAbsorb==58,"Current Fire innate resistances");return b;
    }
    private static void Recovery(FireGolemBody b)
    {
        long now=Core.TickCount+10000;int max=SkillHealth.Maximum256(b);SkillHealth.SetBalance256(b,max-100);b.ResetLifeRecovery();
        b.RecoverLife(now);b.RecoverLife(now+39);Require(SkillHealth.Balance256(b)==max-100,"Fire recovered before40ms");
        b.RecoverLife(now+40);Require(SkillHealth.Balance256(b)==max-39,"Fire lost61FP recovery rate");
        b.RecoverLife(now+80);b.RecoverLife(now+120);Require(SkillHealth.Balance256(b)==max,"Fire recovery overflow");
        SkillHealth.SetBalance256(b,max-257);TimedEffects.PreventHealing(b,TimedEffects.Now);b.RecoverLife(now+160);b.RecoverLife(now+1160);
        Require(SkillHealth.Balance256(b)==max-257,"Fire PMH recovery");TimedEffects.Load(TimedEffects.Snapshot().Where(x=>x.Owner!=b.Serial.Value).ToArray());b.ResetLifeRecovery();
    }
    private static void Facets(PlayerMobile p,FireGolemBody b)
    {
        var original=SummonDifficulties.Facets();var map=p.Map;var other=map==Map.Felucca?Map.Trammel:Map.Felucca;var point=p.Location;
        int mana=NecromancerEffects.ManaBalance256(p);
        try
        {
            SummonDifficulties.SetFacets([new(map.MapID,MonsterDifficulty.Normal),new(other.MapID,MonsterDifficulty.Nightmare),new(Map.Ilshenar.MapID,MonsterDifficulty.Hell)]);
            foreach(var next in new[]{other,Map.Ilshenar,map})
            {
                int before=SkillHealth.Balance256(b),old=SkillHealth.Maximum256(b);p.MoveToWorld(point,next);int max=SkillHealth.Maximum256(b);
                Require(SkillHealth.Balance256(b)==(int)((long)before*max/old),"Fire facet did not preserve relative fractional life");
                if(next==other){Require(max==164774&&b.HitsMax==644&&b.SourceRegeneration256==120,"Nightmare Fire fractional maximum/regeneration");}
                int life=SkillHealth.Balance256(b);p.MoveToWorld(point,next);Require(SkillHealth.Balance256(b)==life,"Repeated Fire facet notification healed/rounded again");
            }
            Require(NecromancerEffects.ManaBalance256(p)==mana,"Fire facet rescaling recast/paid mana");
        }
        finally{p.MoveToWorld(point,map);SummonDifficulties.SetFacets(original);}
    }
    private static void Buff(FireGolemBody b)
    {
        var p=new PlayerMobile{Player=true,Body=0x190};ClassCreation.Profiles.Assign(p,DiabloClass.Barbarian);
        try
        {
            int max=SkillHealth.Maximum256(b),life=SkillHealth.Balance256(b);BattleOrders.Apply(p,b,1,TimedEffects.Now);int buffed=SkillHealth.Maximum256(b);
            Require(buffed==max+SourcePercentage.Calculate(max,BattleOrders.LifePercent(b),100)&&SkillHealth.Balance256(b)==(int)(life/(double)max*buffed)
                &&BattleOrders.Regeneration256(b)==3*(buffed>>8)>>4,"Fire Battle Orders maximum/ratio/callback");
            BattleOrders.Clear(b);Require(SkillHealth.Maximum256(b)==max,"Fire Battle Orders removal maximum");
        }
        finally{BattleOrders.Clear(b);p.Delete();}
    }
    private static void Check()
    {
        Source();var p=Owner();var b=Cast(p);
        NecromancerEffects.SetMana256(p,0);Require(!NecromancerCombat.Release(p,CombatAbility.FireGolem,Serial.Zero,new(p.X+2,p.Y,p.Z))
            &&!b.Deleted&&NecromancerGolems.For(p).Single()==b&&NecromancerEffects.ManaBalance256(p)==0,"Insufficient mana replaced the existing golem or charged");
        var mod=new ResistanceMod(ResistanceType.Fire,"FireBirthFixture",-17);b.AddResistanceMod(mod);
        Require(b.FireResistance==125,"Fire birth seed lost native resistance modifiers");b.RemoveResistanceMod(mod);Require(b.FireResistance==142,"Fire modifier removal changed captured seed");
        Recovery(b);
        var enemy=PoisonDaggerChecks.Target(p);int prior=SkillHealth.Balance256(b);
        try{SkillCombat.Hit(enemy,b,100,"fire");Require(SkillHealth.Balance256(b)==prior,"Raw Fire birth immunity failed native fire delivery");}finally{enemy.Delete();}
        Facets(p,b);Buff(b);
        var before=(b.SourceLife256,b.CombatProfile,b.SourceRegeneration256);PoisonDaggerChecks.Named(p,"Wormskull");
        Require(before==(b.SourceLife256,b.CombatProfile,b.SourceRegeneration256),"Later owner soft ranks rewrote captured Fire birth");
        foreach(var pair in new[]{("necromancer.golemmastery",7),("necromancer.claygolem",4),("necromancer.irongolem",3),("necromancer.bloodgolem",2),("necromancer.summonresist",7)}){Train(p,pair.Item1,pair.Item2);}
        p.Mana=p.ManaMax;var previous=b;int mana=NecromancerEffects.ManaBalance256(p),cost=SkillMechanics.Values(p,CombatAbility.FireGolem).Mana256;
        Require(NecromancerCombat.Release(p,CombatAbility.FireGolem,Serial.Zero,new(p.X+2,p.Y,p.Z)),"Learned synergy/gear Fire recast");b=NecromancerGolems.For(p).OfType<FireGolemBody>().Single();b.AIObject.AITimer.Stop();b.SuspendActions();
        Require(previous.Deleted&&NecromancerEffects.ManaBalance256(p)==mana-cost&&b.Rank==5&&b.SourceLife256==216345&&b.SourceRegeneration256==158&&b.VelocityPercent==24,"Actual soft Mastery/hard Clay synergy birth or replacement/payment");
        var expected=NecromancerFireBirth.Prepare(b.CapturedValues,99,MonsterDifficulty.Normal);
        Require(expected.FireMinimum==46&&expected.FireMaximum==67&&expected.HolyFireRank==11&&b.CombatProfile.Defense==779&&expected.Armor==779&&b.CombatProfile.AttackRating==1248&&expected.Attack==1248&&b.FireResistance==138&&b.PoisonResistance==54&&b.FireAbsorb==62,"Independent hard Clay/Iron/Fire or effective Mastery/SummonResist passive capture");
        var historicalOwner=Owner();var legacy=new NecromancerMinion();legacy.Prepare(historicalOwner,CombatAbility.FireGolem);Require(legacy.SetControlMaster(historicalOwner),"Historical Fire owner");legacy.Internalize();legacy.AIObject.AITimer.Stop();PersistentMinions.Profiles.Track(legacy);PersistentMinions.Suspend(historicalOwner);historicalOwner.Internalize();
        SkillHealth.SetBalance256(b,SkillHealth.Maximum256(b)-257);PersistentMinions.Suspend(p);p.Internalize();
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,b.Serial.Value,legacy.Serial.Value,historicalOwner.Serial.Value,SkillHealth.Balance256(b),SkillHealth.Maximum256(b),0)));
        Log.Information("Fire birth:16065safe complete-source contexts/4590independent synergies;fire stat/Holy Fire rank/Summon Resist exclusion captured;learned once-only mana,exact fractional life,source regeneration/PMH,facet ratios,Battle Orders,captured ranks and historical type persistence pass");
    }
    private static void Reload()
    {
        Source();var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);var b=(FireGolemBody)World.FindMobile((Serial)s.Actor);var old=World.FindMobile((Serial)s.Historical);
        Require(b.ControlMaster==p&&b.Map==Map.Internal&&SkillHealth.Balance256(b)==s.Life&&SkillHealth.Maximum256(b)==s.Maximum&&b.SourceRegeneration256==158,"Saved exact Fire resources/owner/parked type");
        Require(old?.GetType()==typeof(NecromancerMinion)&&((NecromancerMinion)old).Kind==CombatAbility.FireGolem&&((NecromancerMinion)old).ControlMaster?.Serial.Value==s.HistoricalOwner,"Historical Fire save type was migrated silently");
        File.WriteAllText(Manifest,JsonSerializer.Serialize(s with{Generation=s.Generation+1}));
    }
    private static void Input(NetState network,bool quick)
    {
        var p=Owner();PoisonDaggerChecks.Ready(p);NecromancerEffects.SetMana256(p,p.ManaMax*256);
        int mana=NecromancerEffects.ManaBalance256(p),cost=SkillMechanics.Values(p,CombatAbility.FireGolem).Mana256;
        var point=new Point3D(p.X+1,p.Y,p.Z);string label=quick?"fire-golem-birth-quick":"fire-golem-birth-right";
        network.Seeded=network.SentFirstPacket=true;network.CompressionEnabled=false;network.Mobile=p;p.NetState=network;
        var field=typeof(NetState).GetField("_protocolState",BindingFlags.Instance|BindingFlags.NonPublic);field.SetValue(network,Enum.Parse(field.FieldType,"GameServer_LoggedIn"));network.ResetMovementState();network.SendMobileUpdate(p);
        bool began=false;long deadline=Core.TickCount+6000;
        void Clean(){foreach(var pet in NecromancerGolems.For(p)){pet.Delete();}p.Delete();}
        void Inspect()
        {
            try
            {
                Require(Core.TickCount<deadline,"Fire input timeout");var session=ClassCombat.For(p);
                if(session.PendingAbility==CombatAbility.FireGolem&&!began){Require(NecromancerEffects.ManaBalance256(p)==mana&&NecromancerGolems.For(p).Length==0,"Fire input created/paid during windup");began=true;Log.Information("{Label}: windup",label);}
                var pet=NecromancerGolems.For(p).OfType<FireGolemBody>().SingleOrDefault();
                if(pet==null){Timer.StartTimer(TimeSpan.FromMilliseconds(20),Inspect);return;}
                Require(began&&pet.SourceLife256==84134&&SkillHealth.Balance256(pet)==84134&&pet.SourceRegeneration256==61&&pet.FireResistance==142
                    &&NecromancerEffects.ManaBalance256(p)==mana-cost,"Actual Fire input/resource/payment");Clean();Log.Information("{Label}-result: passed",label);
            }
            catch(Exception e){Clean();Log.Error(e,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("{Label}: ready ability={Ability} class={Class} target=0 x={X} y={Y} z={Z}",label,(int)CombatAbility.FireGolem,(int)DiabloClass.Necromancer,point.X,point.Y,point.Z);Inspect();});
    }

}
