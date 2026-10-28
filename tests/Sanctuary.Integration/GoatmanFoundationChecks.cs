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
public static class GoatmanFoundationChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(GoatmanFoundationChecks));
    private sealed record Saved(uint Owner,uint Weapon,uint Helm,bool Female,GoatmanBirth Birth);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"goatman-foundation-fixtures.json");
    private static void Require(bool b,string why){if(!b){throw new InvalidOperationException(why);}}
    private static void Register(string n,Action a)=>ConsoleInputHandler.RegisterCommand(n,"Goatman prepared birth suppliers/source controls",_=>Core.LoopContext.Post(()=>{try{a();Log.Information("{Name}: passed",n);}catch(Exception e){Log.Error(e,"{Name}: failed",n);}}));
    public static void Configure(){Register("goatman-foundation-check",Check);Register("goatman-foundation-reload-check",Reload);}
    private static SkillValues Values(int rank,int mastery=0,int oath=0,int hardMastery=0,int death=0)=>new(SkillMechanics.For(CombatAbility.SummonGoatman),
        k=>k=="Summon Goatman"?rank:k=="Demonic Mastery"?mastery:k=="Blood Oath"?oath:0,k=>k=="Demonic Mastery"?hardMastery:k=="Death Mark"?death:0,47);
    private static int Curve(int rank,int min,int max)=>rank<=0?0:min+(max-min)*Math.Min(100,110*rank/(rank+6))/100;
    private static void Model()
    {
        int count=0;for(int rank=1;rank<=255;rank++)foreach(int mastery in new[]{0,1,17,20,255})foreach(int oath in new[]{0,1,6,20})foreach(int hard in new[]{0,4,5,9,10,20})
        {
            int death=rank%21,sunder=rank%2;var values=Values(rank,mastery,oath,hard,death);var v=GoatmanPrepared.Capture(values,_=>sunder);
            var expected=new GoatmanSuppliers(rank,35*(rank-1)+(oath>0?50+35*(oath-1):0),Math.Min(10+rank-1,25)+(mastery>0?Math.Min(5+mastery-1,25):0),Curve(mastery,5,39),
                140+40*(rank-1)+25*mastery,40*rank+25*mastery,100+20*(rank-1),10*(rank+mastery),1+2*(rank-1),Curve(oath,1,12),5+death,sunder,sunder,hard>=10?3:hard>=5?2:1,
                (rank+29)*128,15,rank>=2?rank:0,rank>=3?rank:0,rank>=4?rank:0,rank>=5?rank:0);
            Require(v==expected,$"Goatman independent raw suppliers {rank}/{mastery}/{oath}/{hard}: {v}");
            foreach(var (difficulty,life,min,max) in new[]{(MonsterDifficulty.Normal,100,3,4),(MonsterDifficulty.Nightmare,175,30,50),(MonsterDifficulty.Hell,275,230,275)})
            {
                var b=GoatmanPrepared.Birth(values,difficulty,47,_=>sunder);int low=(min+v.FlatDamage)*256,high=(max+v.FlatDamage)*256;
                Require(b.MaximumLife256==life*256+(int)((long)life*256*v.LifePercent/100)&&b.MinimumDamage256==low+(int)((long)low*v.DamagePercent/100)&&b.MaximumDamage256==high+(int)((long)high*v.DamagePercent/100)&&b.PetLevel==Math.Min(47,rank+35)&&b.Suppliers==v,"Goatman base/flat-before-ED/FP-life projection");
            }
            count++;
        }
        Require(count==30600&&!WarlockCombat.Implemented(CombatAbility.SummonGoatman),"Incomplete learned Goatman enabled");
        try{Values(4,17).Formula(SkillMechanics.For(CombatAbility.SummonGoatman).Text("passivecalc3"));throw new InvalidOperationException("Generic parser auto-closed a source expression");}catch(InvalidDataException){}
        using(var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"goatman-formula-source-probe.json"))))
        {int cases=0;foreach(var r in doc.RootElement.EnumerateArray()){Require(GoatmanPrepared.Capture(Values(r[1].GetInt32(),r[2].GetInt32()),_=>0).DamagePercent==r[4].GetInt32()&&r[5].GetInt32()==1,"Complete exact Fog compiler/native damage supplier");cases++;}Require(cases==3570,"Goatman formula source cases");}
        using(var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"goatman-creation-source-probe.json"))))
        {
            int source=0,native=0;foreach(var r in doc.RootElement.GetProperty("base").EnumerateArray())
            {source++;if(r[3].GetInt32()!=1){continue;}var b=GoatmanPrepared.Birth(Values(r[1].GetInt32()),(MonsterDifficulty)r[2].GetInt32(),r[0].GetInt32(),_=>0);Require(b.PetLevel==r[5].GetInt32()&&b.LevelArmor==r[6].GetInt32()&&b.LevelAttack==r[7].GetInt32(),"Complete shared base routine/native level additions");native++;}Require(source==7650&&native==3825,"Goatman base source/native counts");
            int casts=0;foreach(var r in doc.RootElement.GetProperty("cast").EnumerateArray())
            {Require(r[0].GetInt32()==1,"Null-owner diagnostic entered admitted source cases");if(r[1].GetInt32()==1&&r[3].GetInt32()==742){Require(GoatmanPrepared.Capture(Values(r[6].GetInt32(),hardMastery:r[7].GetInt32()),_=>0).PetMaximum==r[13].GetInt32(),"Source shared pet cap versus prepared hard-only supplier");}casts++;}Require(casts==3456,"Goatman do56source controls");
        }
        UserTooltip();Log.Information("Goatman:30600raw contexts,3570Fog/7650base/3456do56source controls;current user tooltip matches,GenericPet and learned actor remain gated");
    }
    private static void UserTooltip()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"goatman-tooltip-observation.json")));var o=doc.RootElement;var t=o.GetProperty("tooltip");
        var b=GoatmanPrepared.Birth(Values(o.GetProperty("summonGoatmanEffectiveRank").GetInt32(),o.GetProperty("demonicMasteryEffectiveRank").GetInt32(),o.GetProperty("bloodOathEffectiveRank").GetInt32(),o.GetProperty("demonicMasteryHardRank").GetInt32()),MonsterDifficulty.Nightmare,o.GetProperty("ownerLevel").GetInt32(),_=>0);var v=b.Suppliers;
        Require(b.MaximumLife256==192640&&b.MaximumLife256/256==t.GetProperty("life").GetInt32()&&b.MinimumDamage256==29363&&b.MaximumDamage256==45235&&b.MinimumDamage256/256==t.GetProperty("minimumDamage").GetInt32()&&b.MaximumDamage256/256==t.GetProperty("maximumDamage").GetInt32(),"Observed Nightmare Goatman life/damage versus current source projection");
        Require(v.AttackSpeed==t.GetProperty("attackSpeedPercent").GetInt32()&&v.DisplayAttackBonus==t.GetProperty("attackRatingBonus").GetInt32()&&v.DefenseBonus==t.GetProperty("defenseBonus").GetInt32()&&v.Mana256/256.0==t.GetProperty("manaCost").GetDouble()&&v.AttackBonus==685&&v.DisplayAttackBonus==585&&v.PetMaximum==3,"Observed Goatman speed/AR display versus server bonus/defense/mana");
    }
    private static void Train(PlayerMobile p,string id,int rank=1)
    {Require(TalentCatalog.TryGet(DiabloClass.Warlock,id,out var t),"Goatman talent "+id);foreach(var pre in t.Prerequisites){if(ClassCreation.Profiles.GetRank(p.Serial,pre)==0){Train(p,pre);}}while(ClassCreation.Profiles.GetRank(p.Serial,id)<rank){Require(ClassCreation.Profiles.TryTrain(p.Serial,id,out _),"Goatman train "+id);}}
    private static void Check()
    {
        Model();var saved=new Saved[2];int i=0;foreach(bool female in new[]{false,true})
        {
            var p=new PlayerMobile{Female=female,Body=female?0x191:0x190,Player=true,Name="GoatmanBirthFixture"};p.AddItem(new Backpack());ClassCreation.Profiles.Assign(p,DiabloClass.Warlock);ClassCreation.Profiles.AdvanceLevel(p,47);FixtureSpace.Place(p,new(6100,1580,0),Map.Trammel);
            while(p.RawStr<100){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Strength),"Goatman fixture Strength");}Train(p,"warlock.summongoatman");Train(p,"warlock.demonicmastery",16);Train(p,"warlock.bloodoath",5);
            var weapon=new Broadsword();p.AddToBackpack(weapon);Require(p.EquipItem(weapon),"Goatman fixture weapon");var helm=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey=="Tarnhelm").Id,99,new Random(27),true);p.AddToBackpack(helm);Require(p.EquipItem(helm),"Goatman Tarnhelm");ClassGear.Refresh(p);
            NecromancerEffects.SetMana256(p,p.ManaMax*256);int mana=NecromancerEffects.ManaBalance256(p);var b=GoatmanPrepared.Birth(SkillMechanics.Values(p,CombatAbility.SummonGoatman),MonsterDifficulty.Nightmare,47,_=>0);
            Require(b.Suppliers is{Rank:2,PetMaximum:3,AttackSpeed:32,DamagePercent:190,FlatDamage:3,Mana256:3968,DisplayAttackBonus:505,AttackBonus:605,DefenseBonus:120}&&b.MaximumLife256==161280&&b.MinimumDamage256==24499&&b.MaximumDamage256==39347&&mana==NecromancerEffects.ManaBalance256(p),"Actual trained Goatman supplies with admitted Tarnhelm gear");
            saved[i++]=new(p.Serial.Value,weapon.Serial.Value,helm.Serial.Value,female,b);
        }
        File.WriteAllText(Manifest,JsonSerializer.Serialize(saved));
    }
    private static void Reload()
    {
        Model();foreach(var s in JsonSerializer.Deserialize<Saved[]>(File.ReadAllText(Manifest)))
        {var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);Require(p.Female==s.Female&&World.FindItem((Serial)s.Weapon).Parent==p&&World.FindItem((Serial)s.Helm).Parent==p&&GoatmanPrepared.Birth(SkillMechanics.Values(p,CombatAbility.SummonGoatman),MonsterDifficulty.Nightmare,47,_=>0)==s.Birth,"Retained Goatman owner/gear/prepared projection");}
    }
}
