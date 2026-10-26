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

public static class SpiritWolfStatsChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(SpiritWolfStatsChecks));
    public static void Configure()=>ConsoleInputHandler.RegisterCommand("spirit-wolf-stats-check","Prepared do119 Spirit Wolf birth/cold; no learned actor",_=>Core.LoopContext.Post(()=>
    {try{Check();Log.Information("spirit-wolf-stats-check: passed");}catch(Exception e){Log.Error(e,"spirit-wolf-stats-check: failed");}}));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static SkillValues Values(int level,int rank,int dire=0,int bear=0,int hard=0)=>new(SkillMechanics.For(CombatAbility.SummonSpiritWolf),
        key=>key=="Summon Spirit Wolf"?rank:key=="Summon Fenris"?dire:key=="Summon Grizzly"?bear:255,_=>hard,level);
    private static void Check()
    {
        Require(DruidCombat.Implemented(CombatAbility.SummonSpiritWolf),"Learned wolf route missing");
        using var data=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"spirit-wolf-creation-source-probe.json")));int cold=0,birth=0;
        foreach(var row in data.RootElement.GetProperty("cold").EnumerateArray())
        {
            int N(int i)=>row[i].GetInt32();if(N(3)!=0){continue;}var values=Values(99,N(0),0,N(1));
            var actual=DruidSpiritWolfStats.Cold(values);
            Require(actual.Minimum==N(7) && actual.Maximum==N(8) && actual.Frames==N(6),"Complete unmastered getter / whole passive copy");cold++;
        }
        foreach(var row in data.RootElement.GetProperty("birth").EnumerateArray())
        {
            int N(int i)=>row[i].GetInt32();if(N(7)!=0){continue;}int S(int i)=>row[9][i].GetInt32();
            var values=Values(N(0),N(1),N(4),N(5));var spawn=DruidSpiritWolfStats.Spawn((MonsterDifficulty)N(2));
            Require(spawn.Stats is{Life256:33280,Defense:67,Attack:0} && spawn.Regeneration256==32,"Pinned noRatio supplied bases");
            var supplied=spawn with{Stats=spawn.Stats with{Life256=spawn.Stats.Life256+N(6)}};
            var actual=DruidSpiritWolfStats.Apply(values,(MonsterDifficulty)N(2),N(3)!=0,supplied,_=>0);
            Require(actual.Rank==N(1) && actual.Level==S(0) && actual.Life256==S(3) && S(3)==S(4)
                && actual.BaseDefense==S(1) && actual.BaseAttack+actual.AttackBonus==S(2) && actual.Regeneration256==S(20)
                && actual.ArmorPercent==S(5) && actual.ColdMinimum==S(6) && actual.ColdMaximum==S(7) && actual.ColdFrames==S(8)
                && actual.FireResistance==S(12) && actual.ColdResistance==S(13) && actual.LightningResistance==S(14)
                && actual.PoisonResistance==S(15) && actual.RequestedEquipmentLevel==N(10) && DruidSpiritWolfStats.Limit(values)==N(15),
                "Complete do119/base/five-passive/auras/HP composition");birth++;
        }
        Require(cold==3825 && birth==45900,"Incomplete Spirit Wolf source capture");
        var observed=DruidSpiritWolfStats.Birth(Values(99,28,28,28,20),MonsterDifficulty.Normal,true,_=>0);
        Require(observed is{Life256:266240,ColdMinimum:343,ColdMaximum:359,AttackBonus:725,ArmorPercent:516}
            && Values(99,28).Linear(5,6)==320,"User28/20 tooltip readings / separate armor passive");
        var effectiveOnly=DruidSpiritWolfStats.Birth(Values(99,28,28,28,0),MonsterDifficulty.Normal,true,_=>0);
        Require(effectiveOnly==observed,"Summon synergies incorrectly used hard points");
        var inherited=DruidSpiritWolfStats.Birth(Values(99,1),MonsterDifficulty.Hell,true,name=>name=="item_pierce_cold_immunity"?17:throw new NotSupportedException(name));
        Require(inherited.ColdSunder==17,"Current sixth passive data omitted");
        bool refused=false;try{DruidSpiritWolfStats.Cold(Values(99,0));}catch(NotSupportedException){refused=true;}Require(refused,"Unlearned prepared wolf accepted");
        Actual();Log.Information("Spirit Wolf prepared stats:3825 complete unmastered cold getters and45900 complete do119/base/passive contexts match; sixth slot is data only, actual actor pending");
    }
    private static void Train(PlayerMobile owner,string id,int rank=1)
    {
        Require(TalentCatalog.TryGet(DiabloClass.Druid,id,out var definition),"Wolf talent");
        foreach(string prerequisite in definition.Prerequisites){Train(owner,prerequisite);}
        while(ClassCreation.Profiles.GetRank(owner.Serial,id)<rank){Require(ClassCreation.Profiles.TryTrain(owner.Serial,id,out _),"Train "+id);}
    }
    private static void Actual()
    {
        var owner=new PlayerMobile{Body=0x190,Player=true,Name="SpiritWolfStatsOwner"};owner.AddItem(new Backpack());
        ClassCreation.Profiles.Assign(owner,DiabloClass.Druid);ClassCreation.Profiles.AdvanceLevel(owner,99);Train(owner,"druid.summonspiritwolf");
        var values=SkillMechanics.Values(owner,CombatAbility.SummonSpiritWolf);
        var captured=DruidSpiritWolfStats.Birth(values,MonsterDifficulty.Normal,true,_=>0);
        Require(captured is{Rank:1,Level:99,Life256:36608,ColdMinimum:2,ColdMaximum:6,ColdFrames:31,Regeneration256:32},"Actual rank1 absent-Grizzly birth");
        var helm=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.Name=="Tarnhelm").Id,99,new Random(173),true);
        Require(owner.EquipItem(helm),"Real Tarnhelm equip");ClassGear.Refresh(owner);
        var changed=DruidSpiritWolfStats.Birth(SkillMechanics.Values(owner,CombatAbility.SummonSpiritWolf),MonsterDifficulty.Hell,true,_=>0);
        Require(changed is{Rank:2,Level:99,Life256:39936,ColdMinimum:3,ColdMaximum:7,Regeneration256:32}
            && captured is{Rank:1,Life256:36608,ColdMinimum:2},"Soft rank and immutable prior birth");
        Train(owner,"druid.summongrizzly");var next=DruidSpiritWolfStats.Birth(SkillMechanics.Values(owner,CombatAbility.SummonSpiritWolf),MonsterDifficulty.Normal,true,_=>0);
        Require(next.ColdMinimum==4 && next.ColdMaximum==9 && changed.ColdMinimum==3,"Effective Grizzly synergy / retained snapshot");
        Require(ClassCombat.Available(owner,CombatAbility.SummonSpiritWolf),"Learned summon unavailable");owner.Delete();
    }
}
