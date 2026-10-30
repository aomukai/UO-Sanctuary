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

public static class IronGolemBirthGraphChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(IronGolemBirthGraphChecks));
    private sealed record Saved(uint Owner,uint Pet,uint Item,string Gear,int LifeBeforeWrite,int LifeBaseWrite,int Maximum,int Current,int Armor,int Attack,string[] Omissions);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"iron-golem-birth-graph-fixtures.json");
    public static void Configure(){Register("iron-golem-birth-graph-check",Check);Register("iron-golem-birth-graph-reload-check",Reload);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Iron Golem equipped graph birth writes",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool yes,string why){if(!yes){throw new InvalidOperationException(why);}}
    private static EquipmentStatValue[] Fields(params(int Id,int Value)[] values)=>values.Where(x=>x.Value!=0)
        .Select(x=>new EquipmentStatValue(EquipmentStatKey.Pack(x.Id),x.Value)).OrderBy(x=>x.Packed).ToArray();
    private static SkillValues Skills(int level,int rank,int mastery)=>new(SkillMechanics.For(CombatAbility.IronGolem),
        name=>name=="IronGolem"?rank:name=="Golem Mastery"?mastery:0,
        name=>name is "Clay Golem" or "BloodGolem" or "FireGolem"?20:0,level);
    private sealed class Graph
    {
        internal readonly EquipmentStatUnit Actor,Original;
        internal readonly EquipmentStatValues Values;
        internal readonly List<int[]> Trace=new();
        internal EquipmentStatNode Root=>Actor.Stats;
        private static int Identity(EquipmentStatUnit unit)=>unit?.Entity is int id?id:0;
        internal Graph(object actor,object original,int initialLevel,int hp,int current,int skillLifePercent=50)
        {
            Actor=new(actor,1);Original=new(original,4);
            Actor.Stats=new(){Flags=EquipmentStatNode.Extended,OwnerKind=1,Owner=Actor,
                BaseValues=Fields((7,hp),(6,current),(12,initialLevel),(31,140),(74,hp*3>>12),(76,skillLifePercent))};
            Root.FullValues=(EquipmentStatValue[])Root.BaseValues.Clone();
            Original.Stats=new(){Flags=EquipmentStatNode.Extended,OwnerKind=4,Owner=Original};
            EquipmentStatValues service=null;
            service=new(_=>4,_=>null,_=>throw new NotSupportedException("NPC must not use player coefficients"),(node,source,key,old,next)=>
            {
                Trace.Add([Identity(node.Owner),Identity(source),unchecked((int)key),old,next]);
                if(node==Root&&EquipmentStatKey.Stat(key)==7)
                {
                    int currentLife=EquipmentStatValues.Total(Root,EquipmentStatKey.Pack(6));
                    service.SetBase(Root,EquipmentStatKey.Pack(6),BattleOrders.Rescale256(currentLife,old,next));
                    int recovery=EquipmentStatValues.Total(Root,EquipmentStatKey.Pack(74));
                    service.SetBase(Root,EquipmentStatKey.Pack(74),BattleOrders.RegenerationAfter(old,next,currentLife,3,recovery));
                }
            });Values=service;
        }
        internal void Merge(EquipmentStatNode properties)
        {
            Values.Recalculate(Root,EquipmentStatKey.Pack(7));
            if(properties!=null){EquipmentStatGraph.Post(Original,properties,true,Values);}
            EquipmentStatGraph.Merge(Actor,Original,true,Values);
        }
        internal void Compare(JsonElement stage,int context,string phase)
        {
            EquipmentStatValue[][] arrays=[Root.BaseValues,Root.FullValues,Original.Stats.BaseValues,Original.Stats.FullValues];
            for(int i=0;i<arrays.Length;i++)
            {Require(arrays[i].Select(x=>(x.Packed,x.Value)).SequenceEqual(stage[i].EnumerateArray().Select(x=>(x[0].GetUInt32(),x[1].GetInt32()))),"Source graph differs at "+context+" "+phase+" array "+i);}
        }
    }
    private static void SourceCases()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"iron-golem-birth-graph-source-cases.json")));
        int count=0,levelChanges=0,accruedDiffers=0;
        foreach(var row in doc.RootElement.EnumerateArray())
        {
            int N(int i)=>row[0][i].GetInt32();int hp=(N(2)==0?306:N(2)==1?595:980)*256+17;
            var graph=new Graph(1,2,N(6),hp,N(7)!=0?17:hp);
            var properties=new List<(int,int)>();
            if(N(5) is 1 or 4){properties.Add((7,60*256));properties.Add((16,100));graph.Original.Stats.BaseValues=Fields((31,150));}
            if(N(5) is 2 or 4){properties.Add((76,40));}
            if(N(5) is 3 or 4){properties.Add((216,2560));}
            if(N(5)==4){properties.Add((19,80));properties.Add((119,30));}
            if(N(5)==5){properties.AddRange([(142,10),(144,10),(148,10)]);}
            if(N(5)==6){properties.AddRange([(76,100),(216,768)]);}
            graph.Merge(new(){Flags=0x40,BaseValues=Fields(properties.ToArray())});graph.Compare(row[1][0],count,"equipped");
            int initial=EquipmentStatValues.Total(graph.Root,EquipmentStatKey.Pack(7));
            var result=IronGolemBirthGraph.Apply(Skills(N(0),N(1),N(4)),(MonsterDifficulty)N(2),N(3)!=0,graph.Actor,graph.Original,graph.Values,_=>0,52);
            graph.Compare(row[1][1],count,"born");
            Require(result.LifeBeforePassiveWrite256==row[2][0].GetInt32()&&row[2][1].GetInt32()==1
                &&result.Writes.RequestedEquipmentLevel==row[2][2].GetInt32()&&result.AuraState==36,"Source life read/event/equipment request");
            Require(result.MaximumLife256==EquipmentStatValues.Total(graph.Root,EquipmentStatKey.Pack(7))
                &&result.CurrentLife256==EquipmentStatValues.Total(graph.Root,EquipmentStatKey.Pack(6)),"Final accrued getters mismatch");
            if(initial!=result.LifeBeforePassiveWrite256){levelChanges++;}
            if(result.MaximumLife256!=result.Writes.MaximumLifeBaseWrite256){accruedDiffers++;}
            bool refused=false;try{IronGolemBirthGraph.Apply(Skills(N(0),N(1),N(4)),(MonsterDifficulty)N(2),N(3)!=0,graph.Actor,graph.Original,graph.Values,_=>0,52);}catch(InvalidOperationException){refused=true;}
            Require(refused,"Birth replay duplicated permanent passives");
            EquipmentStatGraph.Detach(graph.Original.Stats,graph.Values);graph.Compare(row[1][2],count,"item removed");
            var expected=row[3].EnumerateArray().Select(x=>x.EnumerateArray().Select(v=>v.GetInt32()).ToArray()).ToArray();
            Require(graph.Trace.Count==expected.Length&&graph.Trace.Zip(expected).All(x=>x.First.SequenceEqual(x.Second)),"Source callback chronology differs at "+count);
            count++;
        }
        Require(count==2016&&levelChanges>0&&accruedDiffers>0&&!NecromancerCombat.Implemented(CombatAbility.IronGolem),"Incomplete graph composition controls or incomplete learned actor enabled");
        Guards();
        Log.Information("Iron Golem birth graph:2016 complete source graph/base/passive/resistance contexts and exact callbacks;{LevelChanges} post-level life changes;{AccruedDiffers} accrued maxima differ from base writes",levelChanges,accruedDiffers);
    }
    private static void Guards()
    {
        var graph=new Graph(1,2,1,306*256,306*256);
        void Reject(Action action)
        {bool rejected=false;try{action();}catch(Exception e)when(e is ArgumentException or InvalidOperationException or NotSupportedException){rejected=true;}Require(rejected,"Unsupported/replayed birth accepted");}
        IronGolemGraphBirth Apply(Func<string,int> accrued)=>IronGolemBirthGraph.Apply(Skills(47,20,20),MonsterDifficulty.Normal,true,graph.Actor,graph.Original,graph.Values,accrued,52);
        string Snapshot()=>JsonSerializer.Serialize(new[]{graph.Root.BaseValues,graph.Root.FullValues});
        string before=Snapshot();Reject(()=>Apply(_=>0));Require(before==Snapshot(),"Unmerged refusal changed actor graph");
        graph.Merge(new(){Flags=0x40});EquipmentStatGraph.ToggleDamage(graph.Actor,graph.Original,false,graph.Values);
        before=Snapshot();Reject(()=>Apply(_=>0));Require(before==Snapshot(),"Dynamic-item refusal changed actor graph");
        EquipmentStatGraph.ToggleDamage(graph.Actor,graph.Original,true,graph.Values);
        graph.Root.OwnerKind=0;before=Snapshot();Reject(()=>Apply(_=>0));Require(before==Snapshot(),"Wrong actor refusal changed graph");graph.Root.OwnerKind=1;
        Reject(()=>Apply(_=>throw new NotSupportedException("Missing accrued-stat binding")));
        Reject(()=>Apply(_=>0)); // A partially written graph must be reconstructed.
    }
    private static Saved Exercise(SkeletonGuardian pet,Item item)
    {
        var state=ClassGear.State(item);string before=JsonSerializer.Serialize(state);var b=ClassGear.Base(item);
        var basis=new EquipmentPropertyBase(b.MinDamage,b.MaxDamage,b.TwoHandMin,b.TwoHandMax,b.MissileMin,b.MissileMax,b.MinDefense,b.MaxDefense);
        var graph=new Graph(pet,item,1,306*256,306*256,0);
        bool weapon=ClassGear.Slot(item)==GearSlot.Weapon;
        graph.Original.Stats.BaseValues=Fields((21,basis.Minimum),(22,basis.Maximum),(23,basis.SecondaryMinimum),(24,basis.SecondaryMaximum),
            (159,basis.ThrowMinimum),(160,basis.ThrowMaximum),(31,weapon?0:state.Defense));
        var omissions=EquipmentRawProperties.Project(item,graph.Original,graph.Values,basis,weapon,!weapon,false).Select(x=>x.Code+":"+x.Reason).ToArray();
        Require(omissions.All(x=>x.StartsWith("sock:",StringComparison.Ordinal)),"Unexpected actual-item numeric omission: "+string.Join(",",omissions));
        // Project already posted the saved property's list. Merge the original
        // without adding a second list or rerolling any property.
        graph.Merge(null);
        var birth=IronGolemBirthGraph.Apply(Skills(47,20,20),MonsterDifficulty.Normal,true,graph.Actor,graph.Original,graph.Values,_=>0,52);
        Require(birth.LifeBeforePassiveWrite256>306*256&&birth.MaximumLife256>birth.Writes.MaximumLifeBaseWrite256
            &&birth.CurrentLife256==birth.Writes.CurrentLifeWrite256,"Actual original life not retained through graph birth");
        Require(item.Parent==pet&&IronGolemItems.State(pet)?.Item==item.Serial.Value&&before==JsonSerializer.Serialize(ClassGear.State(item)),"Graph birth moved/cloned/rerolled original item");
        return new(pet.ControlMaster.Serial.Value,pet.Serial.Value,item.Serial.Value,before,birth.LifeBeforePassiveWrite256,
            birth.Writes.MaximumLifeBaseWrite256,birth.MaximumLife256,birth.CurrentLife256,birth.Armor,birth.Attack,omissions);
    }
    private static void Check()
    {
        SourceCases();var owner=new PlayerMobile{Name="IronGraphOwner",Body=0x190,Player=true};owner.AddItem(new Backpack());
        ClassCreation.Profiles.Assign(owner,DiabloClass.Necromancer);ClassCreation.Profiles.AdvanceLevel(owner,99);
        Require(ClassCreation.Profiles.TryTrain(owner.Serial,"necromancer.raiseskeleton",out _)&&ClassCreation.Profiles.TryTrain(owner.Serial,"necromancer.raiseskeleton",out _),"Storage fixture training");
        FixtureSpace.Place(owner,new(6100,1800,0),Map.Trammel);var saved=new List<Saved>();
        foreach(string source in new[]{"Crown of Thieves","Shaftstop","Spiritforge"})
        {
            var original=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey==source).Id,99,new Random(901),true);
            var pet=new SkeletonGuardian{Name="Iron birth graph [storage fixture]",Frozen=true,CantWalk=true};pet.Initialize(owner,2,0);
            Require(pet.SetControlMaster(owner),"Storage master");PersistentMinions.Profiles.Track(pet);pet.MoveToWorld(owner.Location,owner.Map);pet.AIObject?.AITimer.Stop();
            original.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);Require(IronGolemItems.TryAttach(owner,pet,original,out var why),"Original binding: "+why);
            saved.Add(Exercise(pet,original));pet.Internalize();
        }
        File.WriteAllText(Manifest,JsonSerializer.Serialize(saved));owner.Internalize();
    }
    private static void Reload()
    {
        SourceCases();foreach(var prior in JsonSerializer.Deserialize<Saved[]>(File.ReadAllText(Manifest)))
        {
            var pet=World.FindMobile((Serial)prior.Pet) as SkeletonGuardian;var item=World.FindItem((Serial)prior.Item);
            Require(pet is{Alive:true,Deleted:false}&&pet.ControlMaster?.Serial.Value==prior.Owner&&item is{Deleted:false},"Saved birth original missing");
            var current=Exercise(pet,item);Require(current with{Omissions=prior.Omissions}==prior&&current.Omissions.SequenceEqual(prior.Omissions),"Saved original birth projection changed");pet.AIObject?.AITimer.Stop();
        }
    }
}
