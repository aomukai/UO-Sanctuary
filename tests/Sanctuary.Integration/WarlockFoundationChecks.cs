// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
namespace Sanctuary.Integration;
public static class WarlockFoundationChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(WarlockFoundationChecks));
    private sealed record Saved(uint Owner,uint Helm,bool Female);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"warlock-foundation-fixtures.json");
    private static void Require(bool b,string why){if(!b){throw new InvalidOperationException(why);}}
    public static void Configure()
    {Register("warlock-foundation-check",Check);Register("warlock-foundation-reload-check",Reload);}
    private static void Register(string n,Action run)=>ConsoleInputHandler.RegisterCommand(n,"Pinned Warlock extended formula indices",_=>Core.LoopContext.Post(()=>
    {try{run();Log.Information("{Name}: passed",n);}catch(Exception e){Log.Error(e,"{Name}: failed",n);}}));
    private static void Linear()
    {
        using var data=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"warlock-foundation-source-probe.json")));int count=0;
        foreach(var alias in data.RootElement.GetProperty("aliases").EnumerateArray())foreach(var row in data.RootElement.GetProperty("linearControls").EnumerateArray())
        {
            string suffix=alias.GetProperty("suffix").GetString();int a=alias.GetProperty("first").GetInt32(),b=alias.GetProperty("second").GetInt32(),rank=row[1].GetInt32();
            var parameters=Enumerable.Range(1,20).ToDictionary(i=>i.ToString(CultureInfo.InvariantCulture),i=>12345+i*37);parameters[a.ToString(CultureInfo.InvariantCulture)]=row[2].GetInt32();parameters[b.ToString(CultureInfo.InvariantCulture)]=row[3].GetInt32();
            var definition=SkillMechanics.For(CombatAbility.DemonicMastery) with{SourceParameters=parameters};var values=new SkillValues(definition,_=>rank,_=>0,99);
            Require(values.Formula("ln"+suffix)==row[4].GetInt32(),"Complete linear-source operation/current alias index differs "+suffix+" rank="+rank);count++;
            for(int i=1;i<=20;i++){Require(values.Formula(i<10?"par"+i:"pa"+i)==parameters[i.ToString(CultureInfo.InvariantCulture)],"Direct parameter alias "+i);}
        }
        Require(count==23130,"Missing current curve alias controls");
        var unsupported=new SkillValues(SkillMechanics.For(CombatAbility.DemonicMastery),_=>1,_=>0,99);
        try{unsupported.Formula("ln13");throw new InvalidOperationException("Unregistered alias invented a curve");}catch(NotSupportedException){}
        Log.Information("Warlock foundation:23130complete linear-source/current-index controls and462600direct parameter lookups pass; diminishing arithmetic unchanged");
    }
    private static void Train(PlayerMobile p,string id)
    {Require(TalentCatalog.TryGet(DiabloClass.Warlock,id,out var row),"Warlock talent "+id);foreach(var pre in row.Prerequisites){if(ClassCreation.Profiles.GetRank(p.Serial,pre)==0){Train(p,pre);}}if(ClassCreation.Profiles.GetRank(p.Serial,id)==0){Require(ClassCreation.Profiles.TryTrain(p.Serial,id,out _),"Warlock train "+id);}}
    private static void Values(PlayerMobile p)
    {
        Require(SkillMechanics.Rank(p,CombatAbility.DemonicMastery)==2&&SkillMechanics.Rank(p,CombatAbility.BindDemon)==2,"Saved soft ranks");
        Require(SkillMechanics.Values(p,CombatAbility.DemonicMastery).Formula("ln21")==20,"Demonic Mastery parameters11/12");
        Require(SkillMechanics.Values(p,CombatAbility.SummonGoatman).Formula("ln21")==11,"Goatman own attack speed parameters11/12");
        Require(SkillMechanics.Values(p,CombatAbility.BindDemon).Eval("passivecalc9")==75,"Bind Demon dropped parameters13/14");
        foreach(var row in SkillMechanics.All){Require(row.SourceParameters.Count==20,"Player parameter projection still truncated");}
        Require(SkillMechanics.Values(p,CombatAbility.LevitationMastery).Eval("passivecalc2")==-4,"Effective rank2requirement percentage");
    }
    private static void Check()
    {
        Linear();var saved=new List<Saved>();
        foreach(bool female in new[]{false,true})
        {
            var p=new PlayerMobile{Name=female?"WitchFormulaFixture":"WarlockFormulaFixture",Female=female,Body=female?0x191:0x190,Player=true};p.AddItem(new Backpack());ClassCreation.Profiles.Assign(p,DiabloClass.Warlock);ClassCreation.Profiles.AdvanceLevel(p,99);
            foreach(var skill in new[]{"warlock.summongoatman","warlock.demonicmastery","warlock.binddemon","warlock.levitationmastery"}){Train(p,skill);}
            FixtureSpace.Place(p,new(6110,female?1720:1680,0),Map.Trammel);
            while(p.RawStr<50){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Strength),"Fixture strength");}
            var helm=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey=="Tarnhelm").Id,99,new Random(173),true);p.AddToBackpack(helm);Require(p.EquipItem(helm),"Warlock mastery helm");Values(p);saved.Add(new(p.Serial.Value,helm.Serial.Value,female));
        }
        File.WriteAllText(Manifest,JsonSerializer.Serialize(saved));
    }
    private static void Reload()
    {
        Linear();foreach(var s in JsonSerializer.Deserialize<Saved[]>(File.ReadAllText(Manifest)))
        {Require(World.FindMobile((Serial)s.Owner) is PlayerMobile{Deleted:false} p&&p.Female==s.Female&&World.FindItem((Serial)s.Helm)?.Parent==p,"Warlock/Witch saved owner/gear");Values((PlayerMobile)World.FindMobile((Serial)s.Owner));}
    }
}
