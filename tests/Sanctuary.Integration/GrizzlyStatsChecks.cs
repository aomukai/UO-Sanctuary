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

public static class GrizzlyStatsChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(GrizzlyStatsChecks));
    public static void Configure()=>ConsoleInputHandler.RegisterCommand("grizzly-stats-check","Prepared do119 Grizzly birth; actor/BearSmite are separate",_=>Core.LoopContext.Post(()=>
    {try{Check();Log.Information("grizzly-stats-check: passed");}catch(Exception e){Log.Error(e,"grizzly-stats-check: failed");}}));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static SkillValues Values(int level,int rank,int spirit=0,int dire=0,int hard=0)=>new(SkillMechanics.For(CombatAbility.SummonGrizzly),
        key=>key=="Summon Grizzly"?rank:key=="Summon Spirit Wolf"?spirit:key=="Summon Fenris"?dire:0,_=>hard,level);
    private static void Check()
    {
        Require(!DruidCombat.Implemented(CombatAbility.SummonGrizzly),"Grizzly handler enabled before actor acceptance");
        using var data=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"grizzly-creation-source-probe.json")));
        int births=0,sunder=0;
        foreach(var row in data.RootElement.EnumerateArray())
        {
            int N(int i)=>row[i].GetInt32();if(N(8)!=0){continue;}int S(int i)=>row[10][i].GetInt32();
            var values=Values(N(0),N(1),N(4),N(5));var spawn=DruidGrizzlyStats.Spawn((MonsterDifficulty)N(2));
            Require(spawn.Stats==new SummonSpawnStats((N(2)==0?750:650)*256,245,0) && spawn.Regeneration256==(N(2)==0?234:203),"Pinned difficulty noRatio bases");
            var supplied=spawn with{Stats=spawn.Stats with{Life256=spawn.Stats.Life256+N(7)}};
            var actual=DruidGrizzlyStats.Apply(values,(MonsterDifficulty)N(2),N(3)!=0,supplied,
                name=>name=="item_pierce_damage_immunity"?N(6):throw new NotSupportedException(name));
            Require(actual.Rank==N(1) && actual.Level==S(0) && actual.Life256==S(3) && S(3)==S(4)
                && actual.BaseDefense==S(1) && actual.BaseAttack+actual.AttackBonus==S(2) && actual.Regeneration256==S(20)
                && actual.ArmorPercent==S(5) && actual.DamagePercent==S(6) && actual.PhysicalSunder==S(7)
                && actual.FireResistance==S(12) && actual.ColdResistance==S(13) && actual.LightningResistance==S(14)
                && actual.PoisonResistance==S(15) && actual.RequestedEquipmentLevel==N(11) && DruidGrizzlyStats.Limit(values)==N(16),
                "Complete do119/base/four-passive/auras/HP composition");
            births++;if(N(6)!=0){sunder++;}
        }
        Require(births==55152 && sunder==72,"Incomplete Grizzly source capture");
        for(int rank=1;rank<=255;rank++)
        {
            var values=Values(99,rank,28,28);var actual=DruidGrizzlyStats.Birth(values,MonsterDifficulty.Normal,true,_=>0);
            Require(values.Mana256==40*256 && DruidGrizzlyStats.Limit(values)==1
                && actual.Life256==192000+SourcePercentage.Calculate(192000,10*rank+15*28,100)
                && actual.ArmorPercent==280 && actual.AttackBonus==700 && actual.DamagePercent==25+10*(rank-1)
                && actual.FireResistance==Math.Min(5*rank,85) && actual.Regeneration256==234,"All-rank source formulas");
            Require(actual==DruidGrizzlyStats.Birth(Values(99,rank,28,28,20),MonsterDifficulty.Normal,true,_=>0),"Summon synergy used hard points");
        }
        foreach(int rank in new[]{0,256})
        {bool refused=false;try{DruidGrizzlyStats.Birth(Values(99,rank),MonsterDifficulty.Normal,true,_=>0);}catch(NotSupportedException){refused=true;}Require(refused,"Invalid rank accepted");}
        bool wrong=false;try{DruidGrizzlyStats.Birth(new(SkillMechanics.For(CombatAbility.SummonSpiritWolf),_=>1,_=>0,99),MonsterDifficulty.Normal,true,_=>0);}catch(NotSupportedException){wrong=true;}Require(wrong,"Wrong source accepted");
        var observed=DruidGrizzlyStats.Birth(Values(99,28,28,28,20),MonsterDifficulty.Hell,true,_=>0);
        var tooltip=Values(99,28,28,28,20);
        Require(observed.Life256/256==5200 && observed.AttackBonus==700 && observed.ArmorPercent==280
            && observed.DamagePercent==295 && tooltip.Mana256==10240 && DruidGrizzlyStats.Limit(tooltip)==1
            && SourcePercentage.Calculate(tooltip.PhysicalDamage256(false)>>8,100+observed.DamagePercent,100)==1959
            && SourcePercentage.Calculate(tooltip.PhysicalDamage256(true)>>8,100+observed.DamagePercent,100)==2077,"User rank28 tooltip corroboration");
        AttackModes();Actual();Log.Information("Grizzly prepared birth:55152complete source comparisons including72nonzero-sunder transport controls;255ranks,rank28user tooltip,real owner/gear and difficulty-dependent captured rescale. Prepared Grizzly attack-mode stats match6144source contexts; native actor/mode entry,BearSmite and sunder delivery are not claimed");
    }
    private static void AttackModes()
    {
        using var data=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"grizzly-attack-source-probe.json")));int compared=0;
        foreach(var row in data.RootElement.EnumerateArray())
        {
            int N(int i)=>row[i].GetInt32();if(N(5)!=0){continue;}
            var mode=DruidBeastAttackStats.Capture(Values(99,N(1)),(MonsterDifficulty)N(2),N(4));
            Require(mode==new BeastAttackStats(N(1),N(6),N(7),N(8)),"Complete Grizzly owner/getter/table/mode composition");compared++;
        }
        Require(compared==6144,"Incomplete Grizzly attack-mode capture");
        Require(DruidBeastAttackStats.Capture(Values(99,1),MonsterDifficulty.Normal,4) is{Minimum:30,Maximum:60,Attack:600}
            && DruidBeastAttackStats.Capture(Values(99,28),MonsterDifficulty.Normal,4) is{Minimum:496,Maximum:526,Attack:1140}
            && DruidBeastAttackStats.Capture(Values(99,28),MonsterDifficulty.Hell,5) is{Minimum:496,Maximum:526,Attack:840},
            "Live attack rank/table AR remain separate from copied birth passive");
        bool refused=false;try{DruidBeastAttackStats.Capture(Values(99,28),MonsterDifficulty.Normal,16);}catch(NotSupportedException){refused=true;}
        Require(refused,"Unmapped raw monster mode accepted");
    }

    private static void Actual()
    {
        var owner=new PlayerMobile{Body=0x190,Player=true,Name="GrizzlyStatsOwner"};owner.AddItem(new Backpack());
        try
        {
            ClassCreation.Profiles.Assign(owner,DiabloClass.Druid);ClassCreation.Profiles.AdvanceLevel(owner,99);
            void Train(string id)
            {Require(TalentCatalog.TryGet(DiabloClass.Druid,id,out var talent),"Talent");foreach(var prior in talent.Prerequisites){if(ClassCreation.Profiles.GetRank(owner.Serial,prior)==0){Train(prior);}}Require(ClassCreation.Profiles.TryTrain(owner.Serial,id,out _),"Train "+id);}
            Train("druid.summongrizzly");var original=DruidGrizzlyStats.Capture(owner);
            Require(original is{SpiritRank:1,DireRank:1,Expansion:true,Birth:{Rank:1,Level:99,Life256:240000,ArmorPercent:10,AttackBonus:25,DamagePercent:25,Regeneration256:234,PhysicalSunder:0}},"Real rank-one capture");
            var helm=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.Name=="Tarnhelm").Id,99,new Random(173),true);
            Require(owner.EquipItem(helm),"Real Tarnhelm equip");ClassGear.Refresh(owner);var changed=DruidGrizzlyStats.Capture(owner);
            Require(changed is{SpiritRank:2,DireRank:2,Birth:{Rank:2,Life256:288000,ArmorPercent:20,AttackBonus:50,DamagePercent:35}}
                && original.Birth is{Rank:1,Life256:240000},"Soft ranks / immutable birth");
            var next=changed;
            // Reconstruction uses captured ranks, never the owner's subsequently changed gear/skills.
            owner.AddToBackpack(helm);ClassGear.Refresh(owner);
            foreach(var difficulty in new[]{MonsterDifficulty.Normal,MonsterDifficulty.Nightmare,MonsterDifficulty.Hell})
            {
                var rebuilt=DruidGrizzlyStats.Rescale(next,difficulty);
                Require(rebuilt.Birth==DruidGrizzlyStats.Birth(Values(99,2,2,2),difficulty,true,_=>0)
                    && rebuilt.SpiritRank==2 && rebuilt.DireRank==2,"Captured difficulty reconstruction");
                var inherited=next with{Birth=next.Birth with{PhysicalSunder=95}};
                Require(DruidGrizzlyStats.Rescale(inherited,difficulty).Birth.PhysicalSunder==95,"Rescale lost inherited source stat");
            }
            Require(!ClassCombat.Available(owner,CombatAbility.SummonGrizzly),"Prepared Grizzly exposed before actor acceptance");
        }
        finally{owner.Delete();}
    }
}
