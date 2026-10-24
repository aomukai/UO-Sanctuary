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

public static class ValkyrieCreationChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(ValkyrieCreationChecks));
    private sealed record Saved(uint Owner,uint Helm,ValkyrieCreationStats Snapshot);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"valkyrie-creation-fixtures.json");
    public static void Configure(){Register("valkyrie-creation-check",Check);Register("valkyrie-creation-reload-check",Reload);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Valkyrie creation component; no learned summon handler",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static int Stat(SummonStat[] stats,string name)=>stats.SingleOrDefault(x=>x.Name.Equals(name,StringComparison.OrdinalIgnoreCase))?.Value??0;
    private static string Render(ValkyrieCreationStats stats)=>JsonSerializer.Serialize(stats);
    private static SkillValues Values(int rank,int decoy,int inherit,int level)=>new(SkillMechanics.For(CombatAbility.Valkyrie),
        key=>key.Equals("Valkyrie",StringComparison.OrdinalIgnoreCase)?rank:99,
        key=>key.Equals("Dopplezon",StringComparison.OrdinalIgnoreCase)?decoy:inherit==0?0:key.ToLowerInvariant() switch{"dodge"=>1,"avoid"=>3,"evade"=>6,"critical strike"=>20,_=>0},level);
    private static void Differential()
    {
        using var data=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"valkyrie-creation-source-probe.json")));int cases=0;
        foreach(var row in data.RootElement.EnumerateArray())
        {
            int N(int i)=>row[i].GetInt32();var values=Values(N(1),N(5),N(7),N(0));
            var actual=ValkyrieCreation.Prepare(values,N(0),N(2),N(3)!=0,new(N(4),141,250),_=>N(6));
            Require(actual.Level==N(10) && actual.Life256==N(11) && N(11)==N(12) && actual.EquipmentItemLevel==N(13),"Full helper level/life/item-level differential");
            var stats=row[14];Require(actual.Defense==stats[1].GetInt32() && actual.Attack==stats[2].GetInt32(),"Full base difficulty/game monlvl additions");
            Require(actual.Passives.Length==7 && actual.Auras.Length==6,"Current passive/aura slots lost");
            for(int i=0;i<5;i++){Require(actual.Passives[i].Value==stats[i+5].GetInt32(),"Old complete helper passive stat differential");}
            for(int i=0;i<6;i++){Require(actual.Auras[i].Value==stats[i+12].GetInt32(),"Complete helper aura stat differential");}
            // These slots are current table goldens, outside the old five-slot proof.
            Require(Stat(actual.Passives,"item_pierce_poison_immunity")==N(6) && Stat(actual.Passives,"item_pierce_damage_immunity")==N(6),"Current additional inherited stat slots");
            foreach(var pair in new[]{("dodge",0),("avoid",1),("Evade",2),("Critical Strike",3)})
            {Require(Stat(actual.Skills,pair.Item1)==row[15][pair.Item2].GetInt32(),"Hard-only inherited skill differential");}
            cases++;
        }
        Require(cases==5184,"Incomplete full-helper fixture");
        var value=Values(3,1,1,99);var spawn=new SummonSpawnStats(102437,141,250);
        bool rejected=false;try{ValkyrieCreation.Prepare(value,99,0,true,spawn,_=>throw new NotSupportedException("Missing actual accrued source stat"));}catch(NotSupportedException){rejected=true;}
        Require(rejected,"Missing accrued source context silently defaulted");
        rejected=false;try{ValkyrieCreation.Prepare(new(SkillMechanics.For(CombatAbility.Decoy),_=>3,_=>1,99),99,0,true,spawn,_=>0);}catch(ArgumentException){rejected=true;}
        Require(rejected,"Wrong skill values accepted");
        rejected=false;try{ValkyrieCreation.Base(99,3,3,true,spawn);}catch(ArgumentOutOfRangeException){rejected=true;}
        Require(rejected,"Invalid difficulty accepted");
        Require(!AmazonCombat.Implemented(CombatAbility.Valkyrie),"Creation component enabled incomplete skill");
    }
    private static void Train(PlayerMobile owner,string id,int rank=1)
    {
        Require(TalentCatalog.TryGet(DiabloClass.Amazon,id,out var definition),"Talent "+id);
        foreach(string prerequisite in definition.Prerequisites){Train(owner,prerequisite);}
        while(ClassCreation.Profiles.GetRank(owner.Serial,id)<rank){Require(ClassCreation.Profiles.TryTrain(owner.Serial,id,out _),"Train "+id);}
    }
    private static void Check()
    {
        Differential();var owner=new PlayerMobile{Body=0x190,Name="ValkyrieCreationOwner",Player=true};owner.AddItem(new Backpack());
        ClassCreation.Profiles.Assign(owner,DiabloClass.Amazon);ClassCreation.Profiles.AdvanceLevel(owner,99);
        foreach(var pair in new[]{("amazon.valkyrie",1),("amazon.decoy",3),("amazon.dodge",2),("amazon.avoid",3),("amazon.evade",4),("amazon.criticalstrike",5)}){Train(owner,pair.Item1,pair.Item2);}
        var helm=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey=="Tarnhelm").Id,99,new Random(173),true);Require(owner.EquipItem(helm),"Real soft-rank gear");ClassGear.Refresh(owner);
        var values=SkillMechanics.Values(owner,CombatAbility.Valkyrie);Require(values.Rank==2,"Actual Valkyrie soft rank");
        var spawn=new SummonSpawnStats(102437,141,250);
        // Distinct synthetic accrued source channels, not guessed native item properties.
        int Captured(string stat)=>stat switch{"item_pierce_cold_immunity"=>11,"item_pierce_fire_immunity"=>22,"item_pierce_light_immunity"=>33,"item_pierce_poison_immunity"=>44,"item_pierce_damage_immunity"=>55,_=>throw new InvalidOperationException("Unexpected source stat")};
        var snapshot=ValkyrieCreation.Prepare(values,99,0,true,spawn,Captured);
        Require(snapshot.Rank==2 && snapshot.Level==76 && snapshot.EquipmentItemLevel==28 && snapshot.Life256==184386,"Real learned/soft rank creation snapshot");
        Require(Stat(snapshot.Auras,"strength")==50 && Stat(snapshot.Auras,"dexterity")==24 && Stat(snapshot.Auras,"fireresist")==10,"Hard Decoy and rank aura values");
        Require(new[]{"dodge","avoid","Evade","Critical Strike"}.Select(k=>Stat(snapshot.Skills,k)).SequenceEqual(new[]{2,3,4,5}),"Item soft ranks leaked into inherited skills");
        Require(snapshot.Passives.Skip(2).Select(x=>x.Value).SequenceEqual(new[]{11,22,33,44,55}),"Accrued stat channel identity lost");
        string before=Render(snapshot);owner.AddToBackpack(helm);ClassGear.Refresh(owner);Train(owner,"amazon.decoy",4);Train(owner,"amazon.dodge",3);
        Require(Render(snapshot)==before && Render(ValkyrieCreation.Prepare(values,99,0,true,spawn,Captured))==before,"Later owner changes mutated captured SkillValues or snapshot");
        var next=ValkyrieCreation.Prepare(SkillMechanics.Values(owner,CombatAbility.Valkyrie),99,0,true,spawn,_=>0);
        Require(next.Rank==1 && next.EquipmentItemLevel==25 && Stat(next.Skills,"dodge")==3 && Stat(next.Auras,"fireresist")==10 && next.Life256==184386,"Fresh cast did not read current hard ranks/gear");
        Require(!ClassCombat.Available(owner,CombatAbility.Valkyrie),"Incomplete skill made available");
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(owner.Serial.Value,helm.Serial.Value,snapshot)));
    }
    private static void Reload()
    {
        Differential();var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var owner=World.FindMobile((Serial)saved.Owner) as PlayerMobile;
        Require(owner!=null && World.FindItem((Serial)saved.Helm)?.Parent==owner.Backpack && SkillMechanics.Rank(owner,CombatAbility.Valkyrie)==1,"Saved owner/real gear removal");
        Require(saved.Snapshot.Rank==2 && saved.Snapshot.EquipmentItemLevel==28 && Stat(saved.Snapshot.Skills,"dodge")==2 && Stat(saved.Snapshot.Passives,"item_pierce_damage_immunity")==55,"Fixture snapshot round-trip");
        var current=ValkyrieCreation.Prepare(SkillMechanics.Values(owner,CombatAbility.Valkyrie),99,0,true,new(102437,141,250),_=>0);
        Require(current.Rank==1 && Stat(current.Skills,"dodge")==3 && Stat(current.Auras,"fireresist")==10,"Reloaded current hard-point snapshot");
        // The saved creation record is a fixture contract; no Valkyrie entity or
        // new production persistence format exists or is claimed here.
    }
}
