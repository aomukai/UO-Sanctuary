// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class ShadowBirthChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(ShadowBirthChecks));
    private sealed record Held(CombatAbility Ability,ShadowCreationStats Writes);
    private sealed record Saved(uint Owner,uint Helm,Dictionary<string,int> OwnerStats,Held[] Plans,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"shadow-birth-fixtures.json");
    private static string Json<T>(T x)=>JsonSerializer.Serialize(x);
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    public static void Configure()
    {Register("shadow-birth-check",()=>Check(false));Register("shadow-birth-reload-check",()=>Check(true));}
    private static void Register(string name,Action run)=>ConsoleInputHandler.RegisterCommand(name,"Current Shadow birth writes;actor/cast remain unavailable",_=>Core.LoopContext.Post(()=>
    {try{run();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private sealed class Source(int life,bool allocated,int profile,int modifier):ShadowBirthServices
    {
        internal readonly List<object[]> Trace=[];
        internal override int LifePercentPerRank(){Trace.Add(["skill",268]);return 15;}
        internal override int MaximumLife256(){Trace.Add(["life"]);return life;}
        internal override void UnitStat(int stat,int value)=>Trace.Add(["unit",stat,value]);
        internal override bool AllocateList(){Trace.Add(["allocate"]);return allocated;}
        internal override void PostList()=>Trace.Add(["post"]);
        internal override int StatCount=>512;
        internal override int AuraStat(int slot)=>profile==2||profile==1&&slot==1?-1:new[]{0,19,171,200,511,510}[slot];
        internal override int PassiveStat(int slot)=>profile==2?-1:profile==1&&slot==2?512:310+slot;
        internal override int OwnerAura(int slot){Trace.Add(["formula","owner",100+slot]);return 10100+slot;}
        internal override int OwnerPassive(int slot){Trace.Add(["formula","owner",200+slot]);return 10200+slot;}
        internal override void ListStat(int stat,int value)=>Trace.Add(["stat",stat,value]);
        internal override int PetModifier(){Trace.Add(["formula","pet",900]);return modifier;}
        internal override void AssignModifier(int value)=>Trace.Add(["modifier",value]);
    }
    private static readonly string[] Sunder=["item_pierce_cold_immunity","item_pierce_fire_immunity","item_pierce_light_immunity","item_pierce_poison_immunity","item_pierce_damage_immunity"];
    private static SkillValues Values(CombatAbility ability,int rank,int level=99)
    {var d=SkillMechanics.For(ability);return new(d,key=>key==d.SourceKey?rank:0,_=>0,level);}
    private static void Model()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"shadow-birth-source-cases.json")));int count=0;
        foreach(var row in doc.RootElement.EnumerateArray())
        {
            var c=row.GetProperty("case");var s=new Source(c[1].GetInt32(),c[3].GetBoolean(),c[4].GetInt32(),c[5].GetInt32());
            ShadowBirth.Apply(c[0].GetInt32(),s);
            Require(Json(s.Trace)==row.GetProperty("trace").GetRawText(),"Complete current helper trace differs at "+count);count++;
        }
        Require(count==3456,"Incomplete captured helper controls");
        int[] ranks=[1,2,10,20,40];int[] life=[256000,294400,601600,985600,1753600];
        int[] warriorResistance=[4,8,40,75,75],masterResistance=[17,27,62,76,85];
        foreach(var ability in new[]{CombatAbility.ShadowWarrior,CombatAbility.ShadowMaster})
        for(int i=0;i<ranks.Length;i++)
        {
            int rank=ranks[i];var owner=Values(ability,rank);var pet=Values(ability,rank,1);
            int Stats(string name)=>Array.IndexOf(Sunder,name)is>=0 and var at?(at+1)*11:throw new NotSupportedException(name);
            var p=ShadowCreation.Prepare(owner,pet,256000,true,Stats,_=>throw new InvalidOperationException("Literal modifier read pet stats"));
            Require(p.MaximumLifeWrite256==life[i]&&p.CurrentLifeWrite256==life[i]&&p.Modifier==42&&p.ListAllocated,"Pinned Shadow life/modifier writes");
            var expected=ability==CombatAbility.ShadowWarrior?new SummonStat[]{new("tohit",40*rank),new("skill_armor_percent",12*rank),new("strength",10*rank),new("dexterity",10*rank)}
                :new SummonStat[]{new("tohit",40*rank),new("strength",10*rank),new("dexterity",10*rank)};
            Require(p.Auras.SequenceEqual(expected)&&p.Passives.Length==9,"Pinned distinct aura indices/passive slots");
            Require(p.Passives.Take(4).All(s=>s.Value==(ability==CombatAbility.ShadowWarrior?warriorResistance[i]:masterResistance[i]))
                &&p.Passives.Skip(4).Select(s=>s.Value).SequenceEqual(new[]{11,22,33,44,55}),"Pinned resistance/explicit owner sunder snapshot");
            var noList=ShadowCreation.Prepare(owner,pet,256000,false,_=>throw new InvalidOperationException("Failed allocation read owner stats"),_=>throw new InvalidOperationException("Failed allocation read pet stats"));
            Require(noList.MaximumLifeWrite256==life[i]&&noList.CurrentLifeWrite256==life[i]&&!noList.ListAllocated&&noList.Auras.Length==0&&noList.Passives.Length==0&&noList.Modifier==null,"List failure changed source order");
            var zero=ShadowCreation.Prepare(owner,pet,256000,true,_=>0,_=>0);Require(zero.Passives.Length==9&&zero.Passives.Skip(4).All(s=>s.Value==0),"Zero-valued source writes were dropped");
        }
        foreach(int id in Enumerable.Range(0,EquipmentStatValues.Count))
        {var d=EquipmentStatValues.Definition(id);Require(EquipmentStatValues.Definition(d.Name)==d,"Shared stat name lookup mismatch");}
        Require(EquipmentStatValues.Count==368&&EquipmentStatValues.Definition("not-a-source-stat")==null,"Shared catalogue count/unknown stat policy");
        void Reject(Action run){try{run();}catch(Exception e)when(e is NotSupportedException or ArgumentException){return;}throw new InvalidOperationException("Invalid Shadow birth accepted");}
        Reject(()=>ShadowCreation.Prepare(Values(CombatAbility.ShadowWarrior,1),Values(CombatAbility.ShadowMaster,1),256000,true,_=>0,_=>0));
        Reject(()=>ShadowCreation.Prepare(Values(CombatAbility.ShadowWarrior,1),Values(CombatAbility.ShadowWarrior,2),256000,true,_=>0,_=>0));
        Reject(()=>ShadowCreation.Prepare(Values(CombatAbility.ShadowWarrior,1),Values(CombatAbility.ShadowWarrior,1),0,true,_=>0,_=>0));

    }
    private static ShadowCreationStats Plan(PlayerMobile owner,CombatAbility ability,Dictionary<string,int> stats)
    {
        var values=SkillMechanics.Values(owner,ability);return ShadowCreation.Prepare(values,Values(ability,values.Rank),256000,true,
            name=>stats.TryGetValue(name,out int value)?value:throw new NotSupportedException(name),_=>throw new NotSupportedException("Undeclared pet stats"));
    }
    private static void Check(bool reload)
    {
        Model();var world=NpcWorldAllocation.Capture();PlayerMobile owner;Item helm;Dictionary<string,int> stats;Held[] plans;int generation=0;
        if(reload)
        {
            var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));generation=saved.Generation+1;Require(generation<=2,"Shadow birth reload count");
            owner=World.FindMobile((Serial)saved.Owner)as PlayerMobile;helm=World.FindItem((Serial)saved.Helm);stats=saved.OwnerStats;plans=saved.Plans;
            Require(owner is{Alive:true,Deleted:false}&&helm is{Deleted:false}&&helm.Parent==owner,"Original Shadow fixture owner/gear lost");
            foreach(var held in plans){Require(Json(Plan(owner,held.Ability,stats))==Json(held.Writes),"Original prepared birth writes changed after reload");}
            owner.AddToBackpack(helm);ClassGear.Refresh(owner);
            foreach(var held in plans)
            {var next=Plan(owner,held.Ability,stats);Require(next.Rank==held.Writes.Rank-1&&next.MaximumLifeWrite256<held.Writes.MaximumLifeWrite256,"New birth did not capture changed effective rank");}
            Require(owner.EquipItem(helm),"Original Shadow gear re-equip");ClassGear.Refresh(owner);
        }
        else
        {
            owner=ClassSkillChecks.Player(DiabloClass.Assassin);owner.Name="ShadowBirthFixture";owner.AddItem(new Backpack());
            ClassSkillChecks.Train(owner,"assassin.shadowwarrior",9);ClassSkillChecks.Train(owner,"assassin.shadowmaster",20);
            helm=PoisonDaggerChecks.Named(owner,"Tarnhelm");ClassGear.Refresh(owner);stats=Sunder.Select((n,i)=>(n,v:(i+1)*11)).ToDictionary(p=>p.n,p=>p.v);
            plans=new[]{CombatAbility.ShadowWarrior,CombatAbility.ShadowMaster}.Select(a=>new Held(a,Plan(owner,a,stats))).ToArray();
            Require(plans.Select(p=>p.Writes.Rank).SequenceEqual(new[]{10,21}),"Actual trained/Tarnhelm Shadow ranks");
        }
        string snapshot=Json(plans);var changed=new Dictionary<string,int>(stats){[Sunder[0]]=999};
        foreach(var held in plans){Require(Plan(owner,held.Ability,changed).Passives.Single(s=>s.Name==Sunder[0]).Value==999,"Fresh birth owner-stat capture");}
        Require(Json(plans)==snapshot&&NpcWorldAllocation.Capture()==world,"Prepared snapshot mutated or borrowed world allocation RNG");
        File.WriteAllText(Manifest,Json(new Saved(owner.Serial.Value,helm.Serial.Value,new(stats),plans,generation)));
        Log.Information("Shadow birth:3456complete current-helper traces,10pinned skill/rank plans,368shared stat names;explicit owner/pet contexts,allocation failure and zero writes;actual gear ranks and immutable prepared numeric snapshots through two reloads;no live Shadow actor/cast or dispatch certificate");
    }
}
