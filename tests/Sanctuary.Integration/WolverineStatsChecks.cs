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

public static class WolverineStatsChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(WolverineStatsChecks));
    public static void Configure()=>ConsoleInputHandler.RegisterCommand("wolverine-stats-check","Prepared Heart of Wolverine creation/aura arithmetic; no learned actor",_=>Core.LoopContext.Post(()=>
    {try{Check();Log.Information("wolverine-stats-check: passed");}catch(Exception e){Log.Error(e,"wolverine-stats-check: failed");}}));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static SkillValues Values(int level,int rank)=>new(SkillMechanics.For(CombatAbility.HeartOfWolverine),_=>rank,_=>0,level);
    private static void Check()
    {
        using var data=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"heart-of-wolverine-source-probe.json")));
        int compared=0;
        foreach(var row in data.RootElement.EnumerateArray())
        {
            int N(int i)=>row[i].GetInt32();if(N(5)!=0){continue;}
            var spawn=DruidSpiritStats.Spawn(CombatAbility.HeartOfWolverine,(MonsterDifficulty)N(2));
            Require(spawn is{Stats:{Life256:34816,Defense:123,Attack:0},Regeneration256:25},"Current noRatio bases");
            var fractional=spawn with{Stats=spawn.Stats with{Life256=spawn.Stats.Life256+N(4)}};
            var actual=DruidSpiritStats.Apply(Values(N(0),N(1)),fractional,(MonsterDifficulty)N(2),N(3)!=0);
            Require(actual.Rank==N(1) && actual.Level==N(7) && actual.Life256==N(8) && N(8)==N(9)
                && actual.Defense==N(10) && actual.Attack==N(11) && actual.Regeneration256==N(12)
                && actual.PoisonResistance==N(13) && actual.MaximumPoisonResistance==N(14)
                && actual.AuraRank==N(15) && N(16)==1 && actual.RequestedEquipmentLevel==N(17),
                "Complete do119/base/passive source composition");compared++;
        }
        Require(compared==180,"Incomplete Heart of Wolverine composition fixture");
        for(int rank=1;rank<=255;rank++)
        {
            var values=Values(99,rank);var birth=DruidSpiritStats.Birth(values,MonsterDifficulty.Normal);
            var aura=DruidWolverineStats.Aura(rank);
            Require(values.Mana256==(19+rank)*256 && birth.Level==99 && birth.Life256==34816+34816*25*(rank-1)/100
                && birth.AuraRank==rank && aura==new WolverineAura(rank,30+2*(rank-1),25+7*(rank-1),20+7*(rank-1)),"Pinned rank formulas / aura source units");
            // Player-row tooltip mirrors must agree with the separate NPC row.
            Require(aura.Radius==values.Linear(7,8) && aura.AttackPercent==values.Linear(3,4) && aura.DamagePercent==values.Linear(5,6),"Summon tooltip/NPC aura drift");
        }
        foreach(int rank in new[]{0,256})
        {
            bool rejected=false;try{DruidSpiritStats.Birth(Values(99,rank),MonsterDifficulty.Normal);}catch(NotSupportedException){rejected=true;}
            Require(rejected,"Unsupported summon rank accepted");
            rejected=false;try{DruidWolverineStats.Aura(rank);}catch(ArgumentOutOfRangeException){rejected=true;}
            Require(rejected,"Unsupported aura rank accepted");
        }
        bool wrong=false;try{DruidSpiritStats.Birth(new(SkillMechanics.For(CombatAbility.Raven),_=>1,_=>0,99),MonsterDifficulty.Normal);}catch(NotSupportedException){wrong=true;}
        Require(wrong,"Wrong skill accepted");Actual();
        Log.Information("Heart of Wolverine numeric preparation:180complete source creation compositions,255rank aura/cost/tooltip controls and real owner gear snapshot; no actor/aura lease claimed");
    }
    private static void Actual()
    {
        var owner=new PlayerMobile{Body=0x190,Player=true,Name="WolverineStatsFixture"};owner.AddItem(new Backpack());
        try
        {
            ClassCreation.Profiles.Assign(owner,DiabloClass.Druid);ClassCreation.Profiles.AdvanceLevel(owner,99);
            void Train(string id)
            {Require(TalentCatalog.TryGet(DiabloClass.Druid,id,out var talent),"Talent");foreach(var prior in talent.Prerequisites){if(ClassCreation.Profiles.GetRank(owner.Serial,prior)==0){Train(prior);}}Require(ClassCreation.Profiles.TryTrain(owner.Serial,id,out _),"Train "+id);}
            Train("druid.heartofwolverine");
            var born=DruidSpiritStats.Capture(owner,CombatAbility.HeartOfWolverine);
            Require(born is{Rank:1,Level:99,Life256:34816,Regeneration256:25,PoisonResistance:100,MaximumPoisonResistance:100,AuraRank:1}
                && SkillMechanics.Values(owner,CombatAbility.HeartOfWolverine).Mana256==5120,"Real rank-one owner capture");
            var helm=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.Name=="Tarnhelm").Id,99,new Random(173),true);
            Require(owner.EquipItem(helm),"Real Tarnhelm equip");ClassGear.Refresh(owner);var fresh=DruidSpiritStats.Capture(owner,CombatAbility.HeartOfWolverine);
            Require(fresh is{Rank:2,Level:99,Life256:43520,Regeneration256:25,AuraRank:2} && born.Rank==1 && born.Life256==34816
                && SkillMechanics.Values(owner,CombatAbility.HeartOfWolverine).Mana256==5376,"Soft rank / immutable old snapshot");
            foreach(var difficulty in new[]{MonsterDifficulty.Normal,MonsterDifficulty.Nightmare,MonsterDifficulty.Hell})
            {Require(DruidSpiritStats.Rescale(fresh,difficulty,CombatAbility.HeartOfWolverine)==DruidSpiritStats.Birth(SkillMechanics.Values(owner,CombatAbility.HeartOfWolverine),difficulty),"Captured owner/rank difficulty reconstruction");}
            Require(!SkillBindings.Instance.Assign(owner,true,CombatAbility.HeartOfWolverine),"Oak incorrectly bound to left attack");
        }
        finally{owner.Delete();}
    }
}
