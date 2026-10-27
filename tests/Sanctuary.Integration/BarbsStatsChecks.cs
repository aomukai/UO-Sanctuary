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

public static class BarbsStatsChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(BarbsStatsChecks));
    public static void Configure()=>ConsoleInputHandler.RegisterCommand("barbs-stats-check","Prepared Spirit of Barbs creation/aura arithmetic; no learned actor",_=>Core.LoopContext.Post(()=>
    {try{Check();Log.Information("barbs-stats-check: passed");}catch(Exception e){Log.Error(e,"barbs-stats-check: failed");}}));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static SkillValues Values(int level,int rank)=>new(SkillMechanics.For(CombatAbility.SpiritOfBarbs),_=>rank,_=>0,level);
    private static int Flat(int rank)=>32+Enumerable.Range(2,Math.Max(0,rank-1)).Sum(i=>i<=8?9:i<=16?18:i<=22?27:i<=28?36:45);
    private static void Check()
    {
        using var data=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"spirit-of-barbs-source-probe.json")));
        int compared=0;
        foreach(var row in data.RootElement.EnumerateArray())
        {
            int N(int i)=>row[i].GetInt32();if(N(5)!=0){continue;}
            var spawn=DruidSpiritStats.Spawn(CombatAbility.SpiritOfBarbs,(MonsterDifficulty)N(2));
            Require(spawn is{Stats:{Life256:54528,Defense:196,Attack:0},Regeneration256:39},"Current noRatio bases");
            var fractional=spawn with{Stats=spawn.Stats with{Life256=spawn.Stats.Life256+N(4)}};
            var actual=DruidSpiritStats.Apply(Values(N(0),N(1)),fractional,(MonsterDifficulty)N(2),N(3)!=0);
            Require(actual.Rank==N(1) && actual.Level==N(7) && actual.Life256==N(8) && N(8)==N(9)
                && actual.Defense==N(10) && actual.Attack==N(11) && actual.Regeneration256==N(12)
                && actual.PoisonResistance==N(13) && actual.MaximumPoisonResistance==N(14)
                && actual.AuraRank==N(15) && N(16)==1 && actual.RequestedEquipmentLevel==N(17),
                "Complete do119/base/passive source composition");compared++;
        }
        Require(compared==180,"Incomplete Spirit of Barbs composition fixture");
        for(int rank=1;rank<=255;rank++)
        {
            var values=Values(99,rank);var birth=DruidSpiritStats.Birth(values,MonsterDifficulty.Normal);
            var aura=DruidBarbsStats.Aura(rank);
            Require(values.Mana256==(24+rank)*256 && birth.Level==99 && birth.Life256==54528+54528*25*(rank-1)/100
                && birth.AuraRank==rank && aura==new BarbsAura(rank,30+2*(rank-1),Flat(rank)),"Pinned rank formulas / aura source units");
            // The player physical minimum mirrors the NPC flat stat; radius belongs to the NPC row.
            Require(aura.FlatDamage==values.PhysicalDamage256(false)/256,"Summon tooltip/NPC aura drift");
        }
        foreach(int rank in new[]{0,256})
        {
            bool rejected=false;try{DruidSpiritStats.Birth(Values(99,rank),MonsterDifficulty.Normal);}catch(NotSupportedException){rejected=true;}
            Require(rejected,"Unsupported summon rank accepted");
            rejected=false;try{DruidBarbsStats.Aura(rank);}catch(ArgumentOutOfRangeException){rejected=true;}
            Require(rejected,"Unsupported aura rank accepted");
        }
        bool wrong=false;try{DruidSpiritStats.Birth(new(SkillMechanics.For(CombatAbility.Raven),_=>1,_=>0,99),MonsterDifficulty.Normal);}catch(NotSupportedException){wrong=true;}
        Require(wrong,"Wrong skill accepted");Actual();
        Log.Information("Spirit of Barbs numeric preparation:180complete source creation compositions,255rank aura/cost/tooltip controls and real owner gear snapshot; no actor/aura lease claimed");
    }
    private static void Actual()
    {
        var owner=new PlayerMobile{Body=0x190,Player=true,Name="BarbsStatsFixture"};owner.AddItem(new Backpack());
        try
        {
            ClassCreation.Profiles.Assign(owner,DiabloClass.Druid);ClassCreation.Profiles.AdvanceLevel(owner,99);
            void Train(string id)
            {Require(TalentCatalog.TryGet(DiabloClass.Druid,id,out var talent),"Talent");foreach(var prior in talent.Prerequisites){if(ClassCreation.Profiles.GetRank(owner.Serial,prior)==0){Train(prior);}}Require(ClassCreation.Profiles.TryTrain(owner.Serial,id,out _),"Train "+id);}
            Train("druid.spiritofbarbs");
            var born=DruidSpiritStats.Capture(owner,CombatAbility.SpiritOfBarbs);
            Require(born is{Rank:1,Level:99,Life256:54528,Regeneration256:39,PoisonResistance:100,MaximumPoisonResistance:100,AuraRank:1}
                && SkillMechanics.Values(owner,CombatAbility.SpiritOfBarbs).Mana256==6400,"Real rank-one owner capture");
            var helm=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.Name=="Tarnhelm").Id,99,new Random(173),true);
            Require(owner.EquipItem(helm),"Real Tarnhelm equip");ClassGear.Refresh(owner);var fresh=DruidSpiritStats.Capture(owner,CombatAbility.SpiritOfBarbs);
            Require(fresh is{Rank:2,Level:99,Life256:68160,Regeneration256:39,AuraRank:2} && born.Rank==1 && born.Life256==54528
                && SkillMechanics.Values(owner,CombatAbility.SpiritOfBarbs).Mana256==6656,"Soft rank / immutable old snapshot");
            foreach(var difficulty in new[]{MonsterDifficulty.Normal,MonsterDifficulty.Nightmare,MonsterDifficulty.Hell})
            {Require(DruidSpiritStats.Rescale(fresh,difficulty,CombatAbility.SpiritOfBarbs)==DruidSpiritStats.Birth(SkillMechanics.Values(owner,CombatAbility.SpiritOfBarbs),difficulty),"Captured owner/rank difficulty reconstruction");}
            Require(!SkillBindings.Instance.Assign(owner,true,CombatAbility.SpiritOfBarbs),"Oak incorrectly bound to left attack");
        }
        finally{owner.Delete();}
    }
}
