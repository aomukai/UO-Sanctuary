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

public static class DireWolfStatsChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(DireWolfStatsChecks));
    public static void Configure()=>ConsoleInputHandler.RegisterCommand("dire-wolf-stats-check","Prepared do119 Dire Wolf birth; actor/attack/rage are separate",_=>Core.LoopContext.Post(()=>
    {try{Check();Log.Information("dire-wolf-stats-check: passed");}catch(Exception e){Log.Error(e,"dire-wolf-stats-check: failed");}}));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static SkillValues Values(int level,int rank,int spirit=0,int grizzly=0,int hard=0)=>new(SkillMechanics.For(CombatAbility.SummonDireWolf),
        key=>key=="Summon Fenris"?rank:key=="Summon Spirit Wolf"?spirit:key=="Summon Grizzly"?grizzly:0,_=>hard,level);
    private static void Check()
    {
        Require(!DruidCombat.Implemented(CombatAbility.SummonDireWolf),"Preparation must not enable unfinished Dire Wolf");
        using var data=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"dire-wolf-creation-source-probe.json")));
        int births=0,sunder=0;
        foreach(var row in data.RootElement.EnumerateArray())
        {
            int N(int i)=>row[i].GetInt32();if(N(8)!=0){continue;}int S(int i)=>row[10][i].GetInt32();
            var values=Values(N(0),N(1),N(4),N(5));var spawn=DruidDireWolfStats.Spawn((MonsterDifficulty)N(2));
            Require(spawn is{Stats:{Life256:55296,Defense:116,Attack:0},Regeneration256:54},"Pinned noRatio bases");
            var supplied=spawn with{Stats=spawn.Stats with{Life256=spawn.Stats.Life256+N(7)}};
            var actual=DruidDireWolfStats.Apply(values,(MonsterDifficulty)N(2),N(3)!=0,supplied,
                name=>name=="item_pierce_damage_immunity"?N(6):throw new NotSupportedException(name));
            Require(actual.Rank==N(1) && actual.Level==S(0) && actual.Life256==S(3) && S(3)==S(4)
                && actual.BaseDefense==S(1) && actual.BaseAttack+actual.AttackBonus==S(2) && actual.Regeneration256==S(20)
                && actual.ArmorPercent==S(5) && actual.DamagePercent==S(6) && actual.PhysicalSunder==S(7)
                && actual.FireResistance==S(12) && actual.ColdResistance==S(13) && actual.LightningResistance==S(14)
                && actual.PoisonResistance==S(15) && actual.RequestedEquipmentLevel==N(11) && DruidDireWolfStats.Limit(values)==N(16),
                "Complete do119/base/four-passive/auras/HP composition");
            births++;if(N(6)!=0){sunder++;}
        }
        Require(births==55152 && sunder==72,"Incomplete Dire Wolf source capture");
        for(int rank=1;rank<=255;rank++)
        {
            var values=Values(99,rank,28,28);var actual=DruidDireWolfStats.Birth(values,MonsterDifficulty.Normal,true,_=>0);
            Require(values.Mana256==20*256 && DruidDireWolfStats.Limit(values)==Math.Min(rank,3)
                && actual.Life256==55296+SourcePercentage.Calculate(55296,10*rank+50+15*(rank-1),100)
                && actual.ArmorPercent==4*rank+280 && actual.AttackBonus==700 && actual.DamagePercent==295
                && actual.FireResistance==Math.Min(5*rank,85) && actual.Regeneration256==54,"All-rank source formulas");
            Require(actual==DruidDireWolfStats.Birth(Values(99,rank,28,28,20),MonsterDifficulty.Normal,true,_=>0),"Summon synergy used hard points");
        }
        foreach(int rank in new[]{0,256})
        {bool refused=false;try{DruidDireWolfStats.Birth(Values(99,rank),MonsterDifficulty.Normal,true,_=>0);}catch(NotSupportedException){refused=true;}Require(refused,"Invalid rank accepted");}
        bool wrong=false;try{DruidDireWolfStats.Birth(new(SkillMechanics.For(CombatAbility.SummonSpiritWolf),_=>1,_=>0,99),MonsterDifficulty.Normal,true,_=>0);}catch(NotSupportedException){wrong=true;}Require(wrong,"Wrong source accepted");
        var observed=DruidDireWolfStats.Birth(Values(99,28,28,28,20),MonsterDifficulty.Normal,true,_=>0);
        var tooltip=Values(99,28,28,28,20);
        Require(observed.Life256/256==1803 && observed.AttackBonus==700 && observed.ArmorPercent==392
            && tooltip.Linear(1,2)==455 && tooltip.Mana256==5120 && DruidDireWolfStats.Limit(tooltip)==3
            && SourcePercentage.Calculate(tooltip.PhysicalDamage256(false)>>8,100+observed.DamagePercent,100)==635
            && SourcePercentage.Calculate(tooltip.PhysicalDamage256(true)>>8,100+observed.DamagePercent,100)==762,"User rank28 tooltip corroboration");
        AttackModes();Actual();Log.Information("Dire Wolf prepared birth:55152complete source comparisons including72nonzero-sunder transport controls;255ranks,rank28user tooltip,real owner/gear and captured rescale. Prepared beast attack-mode stats match12288source contexts; native actor/mode entry,rage and sunder delivery are not claimed");
    }
    private static void AttackModes()
    {
        using var data=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"druid-beast-attack-source-probe.json")));int compared=0;
        foreach(var row in data.RootElement.EnumerateArray())
        {
            int N(int i)=>row[i].GetInt32();if(N(5)!=0){continue;}
            var ability=N(0)==0?CombatAbility.SummonSpiritWolf:CombatAbility.SummonDireWolf;
            var values=new SkillValues(SkillMechanics.For(ability),_=>N(1),_=>0,99);
            var mode=DruidBeastAttackStats.Capture(values,(MonsterDifficulty)N(2),N(4));
            Require(mode==new BeastAttackStats(N(1),N(6),N(7),N(8)),"Complete owner/getter/table/mode composition");compared++;
        }
        Require(compared==12288,"Incomplete beast attack-mode capture");
        var before=DruidDireWolfStats.Birth(Values(99,1,1,1),MonsterDifficulty.Normal,true,_=>0);
        Require(DruidBeastAttackStats.Capture(Values(99,1),MonsterDifficulty.Normal,4) is{Minimum:7,Maximum:12,Attack:300}
            && DruidBeastAttackStats.Capture(Values(99,28),MonsterDifficulty.Normal,4) is{Minimum:161,Maximum:193,Attack:840}
            && before is{Rank:1,DamagePercent:25},"Live attack rank remains separate from copied birth passive");
    }
    private static void Actual()
    {
        var owner=new PlayerMobile{Body=0x190,Player=true,Name="DireWolfStatsOwner"};owner.AddItem(new Backpack());
        try
        {
            ClassCreation.Profiles.Assign(owner,DiabloClass.Druid);ClassCreation.Profiles.AdvanceLevel(owner,99);
            void Train(string id)
            {Require(TalentCatalog.TryGet(DiabloClass.Druid,id,out var talent),"Talent");foreach(var prior in talent.Prerequisites){if(ClassCreation.Profiles.GetRank(owner.Serial,prior)==0){Train(prior);}}Require(ClassCreation.Profiles.TryTrain(owner.Serial,id,out _),"Train "+id);}
            Train("druid.summondirewolf");var original=DruidDireWolfStats.Capture(owner);
            Require(original is{SpiritRank:1,GrizzlyRank:0,Expansion:true,Birth:{Rank:1,Level:99,Life256:88473,ArmorPercent:14,AttackBonus:25,DamagePercent:0,Regeneration256:54,PhysicalSunder:0}},"Real rank-one capture");
            var helm=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.Name=="Tarnhelm").Id,99,new Random(173),true);
            Require(owner.EquipItem(helm),"Real Tarnhelm equip");ClassGear.Refresh(owner);var changed=DruidDireWolfStats.Capture(owner);
            Require(changed is{SpiritRank:2,GrizzlyRank:0,Birth:{Rank:2,Life256:102297,ArmorPercent:28,AttackBonus:50,DamagePercent:0}}
                && original.Birth is{Rank:1,Life256:88473},"Soft ranks / immutable birth");
            Train("druid.summongrizzly");var next=DruidDireWolfStats.Capture(owner);
            Require(next is{GrizzlyRank:2,Birth:{DamagePercent:35}} && changed.Birth.DamagePercent==0,"Effective Grizzly / copied damage passive");
            // Reconstruction uses captured ranks, never the owner's subsequently changed gear/skills.
            owner.AddToBackpack(helm);ClassGear.Refresh(owner);
            foreach(var difficulty in new[]{MonsterDifficulty.Normal,MonsterDifficulty.Nightmare,MonsterDifficulty.Hell})
            {
                var rebuilt=DruidDireWolfStats.Rescale(next,difficulty);
                Require(rebuilt.Birth==DruidDireWolfStats.Birth(Values(99,2,2,2),difficulty,true,_=>0)
                    && rebuilt.SpiritRank==2 && rebuilt.GrizzlyRank==2,"Captured difficulty reconstruction");
                var inherited=next with{Birth=next.Birth with{PhysicalSunder=95}};
                Require(DruidDireWolfStats.Rescale(inherited,difficulty).Birth.PhysicalSunder==95,"Rescale lost inherited source stat");
            }
            Require(!ClassCombat.Available(owner,CombatAbility.SummonDireWolf),"Unfinished summon made available");
        }
        finally{owner.Delete();}
    }
}
