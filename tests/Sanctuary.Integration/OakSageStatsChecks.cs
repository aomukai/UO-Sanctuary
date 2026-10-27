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

public static class OakSageStatsChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(OakSageStatsChecks));
    public static void Configure()=>ConsoleInputHandler.RegisterCommand("oak-sage-stats-check","Prepared Oak Sage creation/aura arithmetic; no learned actor",_=>Core.LoopContext.Post(()=>
    {try{Check();Log.Information("oak-sage-stats-check: passed");}catch(Exception e){Log.Error(e,"oak-sage-stats-check: failed");}}));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static SkillValues Values(int level,int rank)=>new(SkillMechanics.For(CombatAbility.OakSage),_=>rank,_=>0,level);
    private static void Check()
    {
        using var data=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"oak-sage-source-probe.json")));
        int compared=0;
        foreach(var row in data.RootElement.EnumerateArray())
        {
            int N(int i)=>row[i].GetInt32();if(N(5)!=0){continue;}
            var spawn=DruidOakStats.Spawn((MonsterDifficulty)N(2));
            Require(spawn is{Stats:{Life256:15360,Defense:49,Attack:0},Regeneration256:11},"Current noRatio bases");
            var fractional=spawn with{Stats=spawn.Stats with{Life256=spawn.Stats.Life256+N(4)}};
            var actual=DruidOakStats.Apply(Values(N(0),N(1)),fractional,(MonsterDifficulty)N(2),N(3)!=0);
            Require(actual.Rank==N(1) && actual.Level==N(7) && actual.Life256==N(8) && N(8)==N(9)
                && actual.Defense==N(10) && actual.Attack==N(11) && actual.Regeneration256==N(12)
                && actual.PoisonResistance==N(13) && actual.MaximumPoisonResistance==N(14)
                && actual.AuraRank==N(15) && N(16)==1 && actual.RequestedEquipmentLevel==N(17),
                "Complete do119/base/passive source composition");compared++;
        }
        Require(compared==180,"Incomplete Oak Sage composition fixture");
        for(int rank=1;rank<=255;rank++)
        {
            var values=Values(99,rank);var birth=DruidOakStats.Birth(values,MonsterDifficulty.Normal);
            var aura=DruidOakStats.Aura(rank);
            Require(values.Mana256==(14+rank)*256 && birth.Level==99 && birth.Life256==15360+15360*30*(rank-1)/100
                && birth.AuraRank==rank && aura==new OakAura(rank,30+2*(rank-1),30+5*(rank-1)),"Pinned rank formulas / aura source units");
            // Player-row tooltip mirrors must agree with the separate NPC row.
            Require(aura.Radius==values.Linear(7,8) && aura.LifePercent==values.Linear(1,2),"Summon tooltip/NPC aura drift");
        }
        foreach(int rank in new[]{0,256})
        {
            bool rejected=false;try{DruidOakStats.Birth(Values(99,rank),MonsterDifficulty.Normal);}catch(NotSupportedException){rejected=true;}
            Require(rejected,"Unsupported summon rank accepted");
            rejected=false;try{DruidOakStats.Aura(rank);}catch(ArgumentOutOfRangeException){rejected=true;}
            Require(rejected,"Unsupported aura rank accepted");
        }
        bool wrong=false;try{DruidOakStats.Birth(new(SkillMechanics.For(CombatAbility.Raven),_=>1,_=>0,99),MonsterDifficulty.Normal);}catch(NotSupportedException){wrong=true;}
        Require(wrong,"Wrong skill accepted");Actual();
        Log.Information("Oak Sage numeric preparation:180complete source creation compositions,255rank aura/cost/tooltip controls and real owner gear snapshot; no actor/aura lease claimed");
    }
    private static void Actual()
    {
        var owner=new PlayerMobile{Body=0x190,Player=true,Name="OakSageComponentFixture"};owner.AddItem(new Backpack());
        try
        {
            ClassCreation.Profiles.Assign(owner,DiabloClass.Druid);ClassCreation.Profiles.AdvanceLevel(owner,99);
            Require(ClassCreation.Profiles.TryTrain(owner.Serial,"druid.oaksage",out _),"Train Oak Sage");
            var born=DruidOakStats.Capture(owner);
            Require(born is{Rank:1,Level:99,Life256:15360,Regeneration256:11,PoisonResistance:100,MaximumPoisonResistance:100,AuraRank:1}
                && SkillMechanics.Values(owner,CombatAbility.OakSage).Mana256==3840,"Real rank-one owner capture");
            var helm=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.Name=="Tarnhelm").Id,99,new Random(173),true);
            Require(owner.EquipItem(helm),"Real Tarnhelm equip");ClassGear.Refresh(owner);var fresh=DruidOakStats.Capture(owner);
            Require(fresh is{Rank:2,Level:99,Life256:19968,Regeneration256:11,AuraRank:2} && born.Rank==1 && born.Life256==15360
                && SkillMechanics.Values(owner,CombatAbility.OakSage).Mana256==4096,"Soft rank / immutable old snapshot");
            foreach(var difficulty in new[]{MonsterDifficulty.Normal,MonsterDifficulty.Nightmare,MonsterDifficulty.Hell})
            {Require(DruidOakStats.Rescale(fresh,difficulty)==DruidOakStats.Birth(SkillMechanics.Values(owner,CombatAbility.OakSage),difficulty),"Captured owner/rank difficulty reconstruction");}
            Require(!SkillBindings.Instance.Assign(owner,true,CombatAbility.OakSage),"Oak incorrectly bound to left attack");
        }
        finally{owner.Delete();}
    }
}
