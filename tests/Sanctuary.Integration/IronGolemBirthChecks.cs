// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Logging;
namespace Sanctuary.Integration;

public static class IronGolemBirthChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(IronGolemBirthChecks));
    public static void Configure()=>ConsoleInputHandler.RegisterCommand("iron-golem-birth-check","Iron Golem post-equip birth composition; not a learned cast",_=>Core.LoopContext.Post(()=>
    {try{Check();Log.Information("iron-golem-birth-check: passed");}catch(Exception e){Log.Error(e,"iron-golem-birth-check: failed");}}));
    private static void Require(bool yes,string why){if(!yes){throw new InvalidOperationException(why);}}
    private static SkillValues Values(int level,int rank,int mastery=0,int clay=0,int blood=0,int fire=0,int effectiveSynergy=0)
        =>new(SkillMechanics.For(CombatAbility.IronGolem),
            key=>key=="IronGolem"?rank:key=="Golem Mastery"?mastery:effectiveSynergy,
            key=>key=="Clay Golem"?clay:key=="BloodGolem"?blood:key=="FireGolem"?fire:0,level);
    private static void Check()
    {
        using var data=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"iron-golem-birth-source-cases.json")));
        int births=0,resists=0;
        foreach(var row in data.RootElement.GetProperty("birth").EnumerateArray())
        {
            int N(int index)=>row[index].GetInt32();
            var result=IronGolemBirth.Prepare(Values(N(0),N(1),N(4),N(5),N(6),N(7)),(MonsterDifficulty)N(2),N(3)!=0,
                new(N(8),157,13),_=>0,0,_=>throw new InvalidOperationException("Zero resistance must not query absorption"));
            int P(string name)=>result.Passives.Single(x=>x.Name==name).Value;
            Require(N(9)==1&&N(10)==1&&result.Level==N(11)&&result.MaximumLifeBaseWrite256==N(12)
                &&result.CurrentLifeWrite256==N(13)&&157+result.BaseDefenseAddition+P("armorclass")==N(14)
                &&13+result.BaseAttackAddition+P("tohit")==N(15)
                &&P("velocitypercent")==N(16)&&P("damagepercent")==N(17)
                &&result.Auras.Single(x=>x.Name=="item_attackertakesdamage").Value==N(18)
                &&result.Auras.Single(x=>x.Name=="fade").Value==N(19)&&result.RequestedEquipmentLevel==N(20)
                &&N(21)==1&&N(22)==29&&result.Passives.Length==10&&result.Resistances.Length==0,
                "Iron Golem complete source base/passive numeric writes at case "+births);
            births++;
        }
        foreach(var row in data.RootElement.GetProperty("resistance").EnumerateArray())
        {
            int N(int index)=>row[index].GetInt32();
            var actual=IronGolemBirth.Prepare(Values(47,1),MonsterDifficulty.Normal,true,new(306*256,140,0),_=>0,N(0),
                name=>name switch{"item_absorbfire_percent"=>N(1),"item_absorblight_percent"=>N(2),"item_absorbcold_percent"=>N(3),_=>throw new NotSupportedException(name)});
            string[] names=["fireresist","lightresist","coldresist","poisonresist"];
            int mask=0;
            for(int i=0;i<names.Length;i++)
            {
                var entry=actual.Resistances.SingleOrDefault(x=>x.Name==names[i]);
                if(entry!=null){mask|=1<<i;}
                Require((entry?.Value??0)==N(5+i),"Summon Resist post-equip absorb/poison rule");
            }
            Require(mask==N(4),"Summon Resist allocation/write presence");resists++;
        }
        Require(births==36720&&resists==81,"Incomplete Iron Golem source fixture");
        var definition=SkillMechanics.For(CombatAbility.IronGolem);
        var inherited=IronGolemBirth.Prepare(Values(99,20,28,20,20,20,255),MonsterDifficulty.Hell,true,
            new(980*256+17,157,13),name=>Enumerable.Range(5,6).Single(i=>definition.Text("passivestat"+i)==name),52,_=>0);
        Require(inherited.Passives.Skip(4).Select(x=>x.Value).SequenceEqual(Enumerable.Range(5,6)),"Current ten-slot data lost/reordered");
        var hardOnly=IronGolemBirth.Prepare(Values(99,20,28,20,20,20,0),MonsterDifficulty.Hell,true,
            new(980*256+17,157,13),_=>0,52,_=>0);
        Require(inherited.MaximumLifeBaseWrite256==hardOnly.MaximumLifeBaseWrite256&&inherited.BaseAttackAddition==hardOnly.BaseAttackAddition
            &&inherited.Passives.Single(x=>x.Name=="tohit").Value==hardOnly.Passives.Single(x=>x.Name=="tohit").Value
            &&inherited.Passives.Single(x=>x.Name=="damagepercent").Value==120,"Hard synergy accidentally uses item ranks");
        foreach(var action in new Action[]{
            ()=>IronGolemBirth.Prepare(Values(99,0),MonsterDifficulty.Normal,true,new(1,0,0),_=>0,0,_=>0),
            ()=>IronGolemBirth.Prepare(Values(0,1),MonsterDifficulty.Normal,true,new(1,0,0),_=>0,0,_=>0),
            ()=>IronGolemBirth.Prepare(Values(99,1),(MonsterDifficulty)3,true,new(1,0,0),_=>0,0,_=>0),
            ()=>IronGolemBirth.Prepare(Values(99,1),MonsterDifficulty.Normal,true,new(0,0,0),_=>0,0,_=>0)})
        {
            bool rejected=false;try{action();}catch(Exception e)when(e is ArgumentException or NotSupportedException){rejected=true;}
            Require(rejected,"Invalid prepared Iron Golem context accepted");
        }
        Require(!NecromancerCombat.Implemented(CombatAbility.IronGolem),"Prepared birth must not enable an incomplete learned cast");
        Log.Information("Iron Golem birth:36720 complete source base/passive numeric comparisons;81 complete resistance contexts;ten current passives retained as data. Item graph writes/events and actor remain pending.");
    }
}
