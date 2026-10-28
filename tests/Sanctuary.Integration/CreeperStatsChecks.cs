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

public static class CreeperStatsChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(CreeperStatsChecks));
    public static void Configure()=>ConsoleInputHandler.RegisterCommand("creeper-stats-check","Prepared Creeper numeric components; no learned actor",_=>Core.LoopContext.Post(()=>
    {try{Check();Log.Information("creeper-stats-check: passed");}catch(Exception e){Log.Error(e,"creeper-stats-check: failed");}}));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static SkillValues Values(int level,int rank,int hard=0)=>new(SkillMechanics.For(CombatAbility.PoisonCreeper),
        key=>key=="Plague Poppy"?rank:255,key=>key=="Rabies"?hard:0,level);
    private static void Check()
    {
        Require(!DruidCombat.Implemented(CombatAbility.PoisonCreeper),"Numeric preparation enabled incomplete learned skill");
        using var data=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"creeper-components-source-probe.json")));
        var fields=data.RootElement.GetProperty("fields").EnumerateArray().Select(x=>x.GetString()).ToArray();int damage=0,birth=0;
        foreach(var row in data.RootElement.GetProperty("damage").EnumerateArray())
        {
            int N(int i)=>row[i].GetInt32();int Field(string name)=>row[6][Array.IndexOf(fields,name)].GetInt32();
            // The explicit MissileSkill override is a source control, not this trail's route.
            if(N(5)!=0){continue;}
            var actual=DruidCreeperStats.Poison(Values(99,N(0),N(1)),N(2));
            Require(actual.Minimum256==Field("nPoisonMinDamage") && actual.Maximum256==Field("nPoisonMaxDamage")
                && actual.Frames==Field("nPoisonLength") && actual.Count==N(9) && N(12)==222,
                "Explicit Creeper skill/source-zero poison creator comparison");damage++;
            if(N(2)==0)
            {Require(DruidCreeperPoison.Damage(N(0),new(99,N(1)),100)==actual,"Live Creeper packet disagrees with compiled source endpoints");}
        }
        foreach(var row in data.RootElement.GetProperty("birth").EnumerateArray())
        {
            int N(int i)=>row[i].GetInt32();if(N(5)!=0){continue;}
            var basis=DruidCreeperStats.Spawn((MonsterDifficulty)N(2));
            Require(basis.Stats.Life256==12800 && basis.Stats.Defense==25 && basis.Stats.Attack==0 && basis.Regeneration256==9,"Pinned noRatio spawn bases");
            // Source supplies a fractional fixed-point delta as an independent percentage control.
            var supplied=basis with{Stats=basis.Stats with{Life256=basis.Stats.Life256+N(4)}};
            var actual=DruidCreeperStats.Apply(Values(N(0),N(1)),supplied,name=>name=="item_pierce_poison_immunity"?N(3):throw new NotSupportedException(name));
            Require(actual.Rank==N(1) && actual.Level==N(7) && actual.Life256==N(8) && N(8)==N(9)
                && actual.Defense==N(10) && actual.Attack==N(11) && actual.Regeneration256==N(12)
                && actual.Pierce==N(13) && actual.VineAttackRank==N(14) && actual.RequestedEquipmentLevel==N(15),
                "Complete do115/passive percentage/explicit-level composition comparison");birth++;
        }
        Require(damage==3636 && birth==9180,"Incomplete Creeper numeric capture");
        Require(DruidCreeperStats.Birth(Values(99,255),MonsterDifficulty.Hell,_=>0).Level==329,"Explicit level incorrectly clamped to monster lookup");
        Refused(()=>DruidCreeperStats.Poison(Values(99,0),0));
        Refused(()=>DruidCreeperStats.Poison(new SkillValues(SkillMechanics.For(CombatAbility.Raven),_=>1,_=>0,99),0));
        bool malformed=false;try{DruidCreeperStats.Apply(Values(99,1),new(new(0,25,0),9),_=>0);}catch(ArgumentException){malformed=true;}
        Require(malformed,"Invalid spawn accepted");
        Vines();Actual();Log.Information("Creeper numeric preparation:3636 complete poison creator and9180 do115/passive source comparisons pass; actor, AI, trail and owner-stat transport pending");
    }
    private static void Refused(Action action)
    {bool rejected=false;try{action();}catch(NotSupportedException){rejected=true;}Require(rejected,"Unsupported route accepted");}
    private static void Vines()
    {
        using var data=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"creeper-vines-age-source-probe.json")));int heads=0,emissions=0,contacts=0;
        foreach(var row in data.RootElement.GetProperty("launch").EnumerateArray())
        {
            int N(int i)=>row[i].GetInt32();if(N(1)!=0){continue;}
            var actual=DruidCreeperVines.Heads(N(0),40,70);var requests=row[4];Require(actual.Length==requests.GetArrayLength(),"Vine Attack count");
            for(int i=0;i<actual.Length;i++)
            {var expected=requests[i];var at=actual[i];Require(at.X==expected[0].GetInt32() && at.Y==expected[1].GetInt32()
                && at.OffsetX==expected[2].GetInt32() && at.OffsetY==expected[3].GetInt32() && at.InitializerIndex==expected[8].GetInt32(),"Vine Attack cardinal initializer request");}
            heads++;
        }
        foreach(var row in data.RootElement.GetProperty("head").EnumerateArray())
        {
            int N(int i)=>row[i].GetInt32();if(N(2)!=5){continue;}
            Require(DruidCreeperVines.EmitsTrail(N(0),N(1))==(N(4)!=0),"Head elapsed-age modulo source comparison");
            if(N(1)==9){Require(DruidCreeperVines.EmitsTrail(N(0))==(N(4)!=0),"Pinned head parameter");}emissions++;
        }
        foreach(var row in data.RootElement.GetProperty("trail").EnumerateArray())
        {
            int N(int i)=>row[i].GetInt32();uint last=N(4)!=0?10u:0u;
            var actual=DruidCreeperVines.Contact(N(0),N(1),last,N(2)!=0?10u:0u,N(5)!=0,N(6)!=0,N(3)!=0);
            Require(actual.Damage==(N(8)!=0) && actual.Remove==(N(7)==2) && actual.HitEvent==(N(11)!=0)
                && actual.LastCollision==(N(10)<0?0u:(uint)N(10)),"Complete hit50/router/history comparison");contacts++;
        }
        uint previous=0;
        foreach(var row in data.RootElement.GetProperty("sequence").EnumerateArray())
        {
            int N(int i)=>row[i].GetInt32();var actual=DruidCreeperVines.Contact(125,N(0),previous,(uint)N(1),true,true,false);
            Require(actual.Damage==(N(3)!=0) && !actual.Remove && actual.LastCollision==(uint)N(4),"Sequential early/alternating/repeated LastCollide");previous=actual.LastCollision;
        }
        Require(heads==255 && emissions==75 && contacts==864,"Incomplete prepared vine callback coverage");
        var first=DruidCreeperVines.Heads(1,-5,9);first[0]=default;
        Require(DruidCreeperVines.Heads(1,-5,9)[0] is{X:-5,Y:9,OffsetX:0,OffsetY:1},"Request array leaked state");
        Log.Information("Creeper prepared vines:255 launch,75 emission,864 complete router and6 sequential history source comparisons pass; factory/actor/current engine remain unverified");
    }
    private static void Actual()
    {
        var owner=new PlayerMobile{Body=0x190,Player=true,Name="CreeperComponentFixture"};owner.AddItem(new Backpack());
        ClassCreation.Profiles.Assign(owner,DiabloClass.Druid);ClassCreation.Profiles.AdvanceLevel(owner,99);
        Require(ClassCreation.Profiles.TryTrain(owner.Serial,"druid.poisoncreeper",out _),"Train Creeper fixture");
        var first=SkillMechanics.Values(owner,CombatAbility.PoisonCreeper);
        var born=DruidCreeperStats.Birth(first,MonsterDifficulty.Normal,_=>0);var poison=DruidCreeperStats.Poison(first,0);
        Require(born is{Rank:1,Level:75,Life256:12800,Defense:25,Attack:0,Regeneration256:9} && first.Mana256==2048
            && poison is{Minimum256:32,Maximum256:48,Frames:100,Count:0} && poison.Minimum256*poison.Frames/256==12
            && poison.Maximum256*poison.Frames/256==18,"Real owner snapshot / user rank-one tooltip preview");
        var helm=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.Name=="Tarnhelm").Id,99,new Random(173),true);
        Require(owner.EquipItem(helm),"Real Tarnhelm equip");ClassGear.Refresh(owner);var changed=SkillMechanics.Values(owner,CombatAbility.PoisonCreeper);
        Require(DruidCreeperStats.Birth(changed,MonsterDifficulty.Hell,_=>0) is{Rank:2,Level:76,Life256:19200,Regeneration256:9}
            && DruidCreeperStats.Poison(changed,0) is{Minimum256:64,Maximum256:80,Frames:100}
            && born.Rank==1 && born.Life256==12800 && poison.Minimum256==32,"Gear rank, source regeneration and immutable prior snapshot");
        Require(NecromancerSummons.Formula("plague vines trail","Range",first)==125
            && NecromancerSummons.Formula("plague vines trail","Range",changed)==150,"Raw trail Range expression retained");
        owner.Delete();
    }
}
