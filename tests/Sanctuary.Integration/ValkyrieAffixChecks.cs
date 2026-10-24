// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class ValkyrieAffixChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(ValkyrieAffixChecks));
    private sealed record Saved(uint Owner,uint Helm,SummonEquipmentRequest[] Requests,ulong Seed);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"valkyrie-affix-fixtures.json");
    public static void Configure(){Register("valkyrie-affix-check",Check);Register("valkyrie-affix-reload-check",Reload);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Valkyrie equipment requests and current affix selection; no items or handler enabled",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static void Differential()
    {
        using var fixture=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"valkyrie-affix-source-probe.json")));int cases=0,unsupported=0;
        foreach(var row in fixture.RootElement.EnumerateArray())
        {
            string code=row[0].GetString();int level=row[1].GetInt32();var quality=row[2].GetInt32()==4?GearRarity.Magic:GearRarity.Rare;
            var kind=row[3].GetInt32()==0?AffixKind.Suffix:AffixKind.Prefix;ulong initial=ulong.Parse(row[4].GetString()),seed=initial;string previous=row[6].GetString();
            string[] existing=string.IsNullOrEmpty(previous)?[]:[previous];bool force=row[12].GetInt32()!=0;
            var selected=ValkyrieAffixes.Select(code,level,quality,kind,ref seed,force,existing);
            Require((selected?.Definition.Id??"")==row[8].GetString() && seed==ulong.Parse(row[9].GetString()),"Full source selected row/final seed case "+cases+" "+code+"/"+level+"/"+kind);
            Require(ValkyrieAffixes.Level(code,level)==row[7].GetInt32(),"Full source raw/affix level distinction");
            ulong preview=initial;bool inhibited=ValkyrieAffixes.Next(ref preview,2)==0 && !force;
            if(!inhibited)
            {
                var pool=ValkyrieAffixes.Pool(code,level,quality,kind,existing);var expected=row[11];Require(pool.Length==expected.GetArrayLength(),"Full source candidate pool length case "+cases);
                for(int i=0;i<pool.Length;i++)
                {Require(pool[i].Definition.Id==expected[i][0].GetString() && ValkyrieAffixes.Weight(pool[i],code)==expected[i][1].GetInt32(),"Full source ordered pool/weight case "+cases+" row "+i);}
                Require(pool.Sum(x=>ValkyrieAffixes.Weight(x,code))==row[10].GetInt32(),"Full source total weight");
            }
            if(selected!=null && !ClassGear.Implemented(selected.Definition)){unsupported++;}
            cases++;
        }
        Require(cases==5184 && unsupported>0,"Incomplete selection fixture or player implementation filter leaked into NPC pool");
        using var equipment=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"valkyrie-equipment-source-probe.json")));int plans=0;
        foreach(var row in equipment.RootElement.GetProperty("equipment").EnumerateArray())
        {
            int rank=row[0].GetInt32();bool occupied=row[1].GetInt32()!=0;ulong seed=(666UL<<32)|173;var actual=new List<SummonEquipmentRequest>();
            ValkyrieAffixes.Equip(rank,25+3*(rank-1),ref seed,x=>{actual.Add(x);return true;},occupied?[4]:[]);var expected=row[2];Require(actual.Count==expected.GetArrayLength(),"Full source equipment request count");
            for(int i=0;i<actual.Count;i++)
            {
                int raw=expected[i][1].GetInt32();string code=new string(Enumerable.Range(0,4).Select(b=>(char)(((uint)raw>>(b*8))&255)).ToArray()).TrimEnd();
                Require(actual[i].Slot==expected[i][0].GetInt32() && actual[i].Code==code && actual[i].Quality==expected[i][2].GetInt32() && actual[i].ItemLevel==expected[i][3].GetInt32(),"Full source requested base/quality/slot/level");
                Require(actual[i].NoSockets && actual[i].NoEthereal && actual[i].NoSell && actual[i].RestoreDurability,"Source item-wrapper request flags");
            }
            ulong expectedSeed=(666UL<<32)|173;
            foreach(var draw in row[3].EnumerateArray()){Require(draw.GetInt32()==1,"Unexpected source Valkyrie row choice count");ValkyrieAffixes.Next(ref expectedSeed,1);}
            Require(seed==expectedSeed,"Occupied/singleton row pet-seed draw consumption");plans++;
        }
        Require(plans==80,"Missing complete equipment selection cases");
        ulong random=173;var requests=new List<SummonEquipmentRequest>();
        ValkyrieAffixes.Equip(17,73,ref random,x=>{requests.Add(x);return x.Code!="7p7";});
        Require(requests.Any(x=>x.Code=="7p7") && requests.Any(x=>x.Code=="9p9") && !requests.Any(x=>x.Code=="spr"),"Failed high-rank weapon creation did not fall through to the next source row");
        requests.Clear();ValkyrieAffixes.Equip(40,142,ref random,x=>{requests.Add(x);return false;});Require(requests.Count==19,"Failed item creation wrongly filled source slots");
        requests.Clear();ValkyrieAffixes.Equip(255,787,ref random,x=>{requests.Add(x);return true;});Require(requests.All(x=>x.ItemLevel==787) && ValkyrieAffixes.Level("7p7",787)==99,"Raw item level silently clamped to player item limit");
        ulong before=random;Require(ValkyrieAffixes.Next(ref random,0)==0 && random==before,"Zero bound advanced source seed");
        Require(ValkyrieAffixes.All.Count==478 && ValkyrieAffixes.All.Any(x=>x.FirstPropertySockets) && !ValkyrieAffixes.Pool("ci2",99,GearRarity.Magic,AffixKind.Prefix).Any(x=>x.FirstPropertySockets),"Bounded legacy socket-first-property rule");
        Require(!AmazonCombat.Implemented(CombatAbility.Valkyrie),"Equipment selection component enabled unfinished summon");
    }
    private static void Train(PlayerMobile owner,string id,int rank=1)
    {
        Require(TalentCatalog.TryGet(DiabloClass.Amazon,id,out var definition),"Talent "+id);
        foreach(string prerequisite in definition.Prerequisites){Train(owner,prerequisite);}
        while(ClassCreation.Profiles.GetRank(owner.Serial,id)<rank){Require(ClassCreation.Profiles.TryTrain(owner.Serial,id,out _),"Train "+id);}
    }
    private static SummonEquipmentRequest[] Plan(PlayerMobile owner,out ulong seed)
    {
        var values=SkillMechanics.Values(owner,CombatAbility.Valkyrie);seed=(666UL<<32)|173;var result=new List<SummonEquipmentRequest>();
        ValkyrieAffixes.Equip(values.Rank,(int)values.Eval("calc2"),ref seed,x=>{result.Add(x);return true;});return result.ToArray();
    }
    private static void Check()
    {
        Differential();var owner=new PlayerMobile{Body=0x190,Name="ValkyrieAffixOwner",Player=true};owner.AddItem(new Backpack());ClassCreation.Profiles.Assign(owner,DiabloClass.Amazon);ClassCreation.Profiles.AdvanceLevel(owner,99);Train(owner,"amazon.valkyrie",16);
        var helm=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey=="Tarnhelm").Id,99,new Random(173),true);Require(owner.EquipItem(helm),"Actual soft-rank helm");ClassGear.Refresh(owner);
        Require(SkillMechanics.Rank(owner,CombatAbility.Valkyrie)==17,"Real effective summon rank");var plan=Plan(owner,out ulong seed);
        Require(plan.Single(x=>x.Slot==4).Code=="7p7" && plan.All(x=>x.ItemLevel==73),"Effective rank17 source equipment threshold/item level");
        owner.AddToBackpack(helm);ClassGear.Refresh(owner);var next=Plan(owner,out _);
        Require(next.Single(x=>x.Slot==4).Code=="9p9" && next.All(x=>x.ItemLevel==70) && plan.Single(x=>x.Slot==4).Code=="7p7","Actual gear removal mutated earlier request or failed to affect later plan");
        Require(!ClassCombat.Available(owner,CombatAbility.Valkyrie),"Incomplete skill became available");
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(owner.Serial.Value,helm.Serial.Value,plan,seed)));
    }
    private static void Reload()
    {
        Differential();var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var owner=World.FindMobile((Serial)saved.Owner) as PlayerMobile;
        Require(owner!=null && World.FindItem((Serial)saved.Helm)?.Parent==owner.Backpack && SkillMechanics.Rank(owner,CombatAbility.Valkyrie)==16,"Saved owner/gear/current rank");
        Require(saved.Requests.Single(x=>x.Slot==4).Code=="7p7" && saved.Requests.All(x=>x.ItemLevel==73),"Captured request fixture round-trip");
        var next=Plan(owner,out ulong seed);Require(next.Single(x=>x.Slot==4).Code=="9p9" && next.All(x=>x.ItemLevel==70) && seed!=saved.Seed,"Reloaded live rank source plan/pet-seed branch");
        // No NPC, generated item, applied property or production save format is
        // introduced or claimed by these fixture request snapshots.
    }
}
