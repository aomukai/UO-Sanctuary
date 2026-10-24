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

public static class AssassinTrapCreationChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(AssassinTrapCreationChecks));
    private sealed record Saved(uint Owner,uint Helm,AssassinTrapCreationStats Stats,WeaponMissileDamage Packet);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"assassin-trap-creation-fixtures.json");
    public static void Configure(){Register("assassin-trap-creation-check",Check);Register("assassin-trap-creation-reload-check",Reload);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Trap creation/packet components only; no learned Blade Sentinel",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static SkillValues Values(int rank,int hard=0)=>new(SkillMechanics.For(CombatAbility.BladeSentinel),
        key=>key=="Blade Sentinel"?rank:73,key=>key=="Blade Fury"?Math.Min(20,hard):key=="Blade Shield"?Math.Max(0,hard-20):0,99);
    private static readonly MissileSourceStats Raw=new(FireMin:10*256,FireMax:31*256,LightningMin:12*256,LightningMax:49*256,
        MagicMin:256,MagicMax:512,ColdMin:7*256,ColdMax:13*256,ColdFrames:15,PoisonMin:257,PoisonMax:513,PoisonFrames:101,PoisonCount:2,LifeDrain:7,ManaDrain:5);
    private static void Differential()
    {
        using var data=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"blade-sentinel-damage-source-probe.json")));
        string[] fields=data.RootElement.GetProperty("fields").EnumerateArray().Select(x=>x.GetString()).ToArray();int count=0;
        foreach(var row in data.RootElement.GetProperty("cases").EnumerateArray())
        {
            int N(int i)=>row[i].GetInt32();int rank=N(0),hard=N(1),gear=N(2),percent=N(3);bool crit=N(4)!=0;
            int min=gear==3?40:gear==4?25:9,max=gear==3?60:gear==4?35:14;
            var packet=BladeSentinelPackets.FromSource(Values(rank,hard),min*256,max*256,gear==1,percent+15+(gear==2?0:118),11,13,crit,Raw);
            var expected=fields.Select((name,i)=>(name,value:row[5][i].GetInt32())).ToDictionary(x=>x.name,x=>x.value);
            Require(packet.Min256==expected["nMinDamage"] && packet.Max256==expected["nMaxDamage"] && packet.Percent==expected["nDamagePercent"],"Full creator/getter physical differential");
            Require(packet.DemonPercent==expected["nDemonDamagePercent"] && packet.UndeadPercent==expected["nUndeadDamagePercent"] && packet.Critical==((expected["nFlags"]&2)!=0),"Species/once-created critical differential");
            var e=packet.Elements;
            var actual=new Dictionary<string,int>{["nFireMinDamage"]=e.FireMin,["nFireMaxDamage"]=e.FireMax,["nLightMinDamage"]=e.LightningMin,["nLightMaxDamage"]=e.LightningMax,
                ["nMagicMinDamage"]=e.MagicMin,["nMagicMaxDamage"]=e.MagicMax,["nColdMinDamage"]=e.ColdMin,["nColdMaxDamage"]=e.ColdMax,["nColdLength"]=e.ColdFrames,
                ["nPoisonMinDamage"]=e.PoisonMin,["nPoisonMaxDamage"]=e.PoisonMax,["nPoisonLength"]=e.PoisonFrames,["nPoisonCount"]=e.PoisonCount,
                ["nLifeDrainMinDamage"]=e.LifeDrain,["nManaDrainMinDamage"]=e.ManaDrain,["nBurningMin"]=e.BurnMin,["nBurningMax"]=e.BurnMax,["nBurnLength"]=e.BurnFrames};
            foreach(var pair in actual){Require(pair.Value==expected[pair.Key],"Full typed packet scaling differential "+pair.Key);}
            Require(e.PoisonImpactFrames==101 && packet.SourcePoisonFrames && packet.FireMastery==0 && packet.LightningMastery==0 && packet.PoisonMastery==0,"Count0/1 duration or invented missile mastery");count++;
        }
        Require(count==960,"Incomplete Blade Sentinel creator fixture");
        var spawn=new SummonSpawnStats(102437,141,250);int calls=0;
        int Captured(string name){Require(name!="item_pierce_cold_immunity","Malformed formula incorrectly auto-closed");calls++;return calls*7-50;}
        foreach(int rank in new[]{1,3,20,21,255})foreach(int difficulty in new[]{0,1,2})foreach(bool expansion in new[]{false,true})
        {
            calls=0;var actual=AssassinTrapCreation.Prepare(Values(rank),99,difficulty,expansion,spawn,Captured);
            var basis=ValkyrieCreation.Base(99,rank,difficulty,expansion,spawn);
            Require(actual.Rank==rank && actual.Level==99 && actual.BaseStatsLevel==Math.Min(99,rank+74) && actual.BaseStatsLevel==basis.Level,"Caster-level overwrite lost preliminary monlvl context");
            Require(actual.Life256==102437 && actual.Defense==basis.Defense && actual.Attack==basis.Attack+rank*5 && actual.DurationFrames==100+12*(rank-1),"Creation additive stats/refill/duration");
            Require(actual.Passives.Length==14 && calls==12 && actual.Passives[0]==new SummonStat("tohit",rank*5) && actual.Passives[5]==new SummonStat("item_pierce_cold_immunity",0),"All current passive slots/legacy malformed policy");
            int index=0;foreach(var passive in actual.Passives.Skip(1)){if(passive.Name=="item_pierce_cold_immunity"){continue;}index++;Require(passive.Value==index*7-50,"Signed accrued stat identity lost");}
            Require(actual.Skills.Length==1 && actual.Skills[0]==new SummonStat("Blade Sentinel",rank),"Effective-rank inherited skill");
            var armed=AssassinTrapCreation.Arm(actual,80,11,17);Require(armed.MissileArmed && armed.Attack==actual.Attack+80 && armed.AttackPercent==28 && !actual.MissileArmed,"Successful missile inherited AR/mastery stage");
            bool rejected=false;try{AssassinTrapCreation.Arm(armed,80,11,17);}catch(InvalidOperationException){rejected=true;}Require(rejected,"Double missile inheritance accepted");
        }
        bool missing=false;try{AssassinTrapCreation.Prepare(Values(3),99,0,true,spawn,_=>throw new NotSupportedException("Missing accrued source stat"));}catch(NotSupportedException){missing=true;}Require(missing,"Missing owner source defaulted silently");
        bool malformed=false;try{Values(3).StatEval("passivecalc6",_=>1);}catch(InvalidDataException){malformed=true;}Require(malformed,"General interpreter auto-repaired malformed function");
        bool wrong=false;try{AssassinTrapCreation.Prepare(new(SkillMechanics.For(CombatAbility.FireBlast),_=>3,_=>1,99),99,0,true,spawn,_=>0);}catch(ArgumentException){wrong=true;}Require(wrong,"Wrong skill context accepted");
        var one=BladeSentinelPackets.FromSource(Values(1),9*256,14*256,false,133,11,13,false,Raw);
        var hit=one.Roll(true,false,false,_=>0);Require(NamedElements.To256(hit.Physical)==7605 && NamedElements.To256(hit.Poison)==19392 && hit.PoisonMs==4040,"Physical ED and exact source poison duration roll");
        Require(!AssassinCombat.Implemented(CombatAbility.BladeSentinel),"Incomplete actor enabled by component");
    }
    private static void Train(PlayerMobile owner,string id,int rank=1)
    {
        Require(TalentCatalog.TryGet(DiabloClass.Assassin,id,out var definition),"Talent "+id);foreach(string pre in definition.Prerequisites){Train(owner,pre);}
        while(ClassCreation.Profiles.GetRank(owner.Serial,id)<rank){Require(ClassCreation.Profiles.TryTrain(owner.Serial,id,out _),"Train "+id);}
    }
    private static void Check()
    {
        Differential();var owner=new PlayerMobile{Body=0x190,Name="AssassinTrapCreationOwner",Player=true};owner.AddItem(new Backpack());
        ClassCreation.Profiles.Assign(owner,DiabloClass.Assassin);ClassCreation.Profiles.AdvanceLevel(owner,99);Train(owner,"assassin.bladesentinel",20);Train(owner,"assassin.bladefury");
        var helm=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey=="Tarnhelm").Id,99,new Random(173),true);Require(owner.EquipItem(helm),"Real soft-rank gear");ClassGear.Refresh(owner);
        var values=SkillMechanics.Values(owner,CombatAbility.BladeSentinel);Require(values.Rank==21,"Actual effective Blade Sentinel rank");
        int Captured(string name)=>name switch{"item_pierce_damage_immunity"=>11,"item_pierce_fire_immunity"=>22,"item_pierce_light_immunity"=>33,"item_pierce_poison_immunity"=>44,
            "passive_fire_mastery"=>55,"passive_ltng_mastery"=>66,"passive_cold_mastery"=>77,"passive_pois_mastery"=>88,"passive_fire_pierce"=>-11,"passive_ltng_pierce"=>-22,"passive_cold_pierce"=>-33,"passive_pois_pierce"=>-44,_=>throw new NotSupportedException(name)};
        var stats=AssassinTrapCreation.Prepare(values,99,0,true,new(25600,100,0),Captured);
        Require(stats.Rank==21 && stats.Level==99 && stats.BaseStatsLevel==95 && stats.DurationFrames==340 && stats.Life256==25600,"Real learned/soft-rank snapshot");
        var packet=BladeSentinelPackets.FromSource(values,9*256,14*256,false,133,11,13,false,Raw);string before=JsonSerializer.Serialize(new{stats,packet});
        owner.AddToBackpack(helm);ClassGear.Refresh(owner);Train(owner,"assassin.bladefury",2);
        Require(JsonSerializer.Serialize(new{stats,packet})==before && SkillMechanics.Rank(owner,CombatAbility.BladeSentinel)==20,"Owner changes mutated captured records");
        var next=AssassinTrapCreation.Prepare(SkillMechanics.Values(owner,CombatAbility.BladeSentinel),99,0,true,new(25600,100,0),_=>0);
        Require(next.Rank==20 && next.BaseStatsLevel==94 && next.DurationFrames==328 && !ClassCombat.Available(owner,CombatAbility.BladeSentinel),"Fresh context/handler gate");
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(owner.Serial.Value,helm.Serial.Value,stats,packet)));
    }
    private static void Reload()
    {
        Differential();var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var owner=World.FindMobile((Serial)saved.Owner) as PlayerMobile;
        Require(owner!=null && World.FindItem((Serial)saved.Helm)?.Parent==owner.Backpack && SkillMechanics.Rank(owner,CombatAbility.BladeSentinel)==20,"Saved learned owner/gear");
        Require(saved.Stats.Rank==21 && saved.Stats.Level==99 && saved.Stats.BaseStatsLevel==95 && saved.Stats.Passives.Length==14 && saved.Stats.Passives[5].Value==0 && saved.Stats.DurationFrames==340,"Component fixture stat round-trip");
        Require(saved.Packet.Min256>0 && saved.Packet.Elements.PoisonFrames==101 && saved.Packet.SourcePoisonFrames && saved.Packet.Critical==false,"Component fixture packet round-trip");
        var current=SkillMechanics.Values(owner,CombatAbility.BladeSentinel);Require(ClassCreation.Profiles.GetRank(owner.Serial,"assassin.bladefury")==2 && current.Rank==20,"Saved hard synergy/effective ranks");
        Require(!ClassCombat.Available(owner,CombatAbility.BladeSentinel),"Component fixture enabled playable skill");
        // JSON round-trip is a component fixture contract, not production blade
        // actor/missile persistence. No such entity is enabled or claimed here.
    }
}
