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
public static class EquipmentPropertyWriterChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(EquipmentPropertyWriterChecks));
    private static string Manifest=>Path.Join(Core.BaseDirectory,"item-property-writers-fixtures.json");
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private sealed record Saved(uint Owner,uint Pet,uint Item,string Gear,int Life256,int Defense,int LifeBonus256,int Armor,int Minimum,int Maximum,string[] Omissions);
    private sealed class Fixture:EquipmentPropertyServices
    {
        private readonly EquipmentPropertyBase Base;private readonly bool IsWeapon,Throws,Allocate;
        internal readonly Dictionary<uint,int> BaseStats=new(),List=new();internal readonly List<int[]> Trace=new();internal int Draws;
        internal override EquipmentPropertyBase Basis=>Base;internal override bool Weapon=>IsWeapon;internal override bool Armor=>!IsWeapon;internal override bool Throwable=>Throws;
        internal Fixture(EquipmentPropertyBase basis,int b,bool allocate)
        {
            Base=basis;IsWeapon=b!=3;Throws=b==2;Allocate=allocate;
            foreach(var p in new[]{(21,basis.Minimum),(22,basis.Maximum),(23,basis.SecondaryMinimum),(24,basis.SecondaryMaximum),(159,basis.ThrowMinimum),(160,basis.ThrowMaximum),(31,basis.MinimumArmor)})
            {if(p.Item2!=0){BaseStats.Add(EquipmentStatKey.Pack(p.Item1),p.Item2);}}
        }
        internal override int BaseValue(int stat)=>BaseStats.GetValueOrDefault(EquipmentStatKey.Pack(stat));
        internal override void SetBase(int stat,int value){BaseStats[EquipmentStatKey.Pack(stat)]=value;Trace.Add([2,stat,value,0]);}
        internal override bool GetOrCreateList(int state,int flags){Trace.Add([3,state,flags,Allocate?1:0]);return Allocate;}
        internal override int ListValue(int stat,int layer)=>List.GetValueOrDefault(EquipmentStatKey.Pack(stat,layer));
        internal override void SetList(int stat,int value,int layer){List[EquipmentStatKey.Pack(stat,layer)]=value;Trace.Add([4,stat,value,layer]);}
        internal override void AddList(int stat,int value,int layer){uint key=EquipmentStatKey.Pack(stat,layer);List[key]=checked(List.GetValueOrDefault(key)+value);Trace.Add([5,stat,value,layer]);}
        internal override int Roll(int bound){int value=Draws++%bound;Trace.Add([6,bound,value,0]);return value;}
        internal override void SetEthereal()=>Trace.Add([1,0,1,0]);
    }
    public static void Configure(){Register("item-property-writers-check",Check);Register("item-property-writers-reload-check",Reload);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Original-item numeric property writers",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void SourceCases()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"item-property-writers-source-probe.json")));var root=doc.RootElement;
        var plans=root.GetProperty("plans").EnumerateArray().ToDictionary(v=>v.GetProperty("Ordinal").GetInt32(),v=>EquipmentPropertyWriter.Plan(v.GetProperty("Code").GetString()));
        var bases=root.GetProperty("bases").EnumerateArray().Select(v=>v.GetProperty("Values").EnumerateArray().Select(x=>x.GetInt32()).ToArray()).Select(v=>new EquipmentPropertyBase(v[0],v[1],v[2],v[3],v[4],v[5],v[6],v[7])).ToArray();int count=0;
        foreach(var row in root.GetProperty("cases").EnumerateArray())
        {
            var i=row[0].EnumerateArray().Select(v=>v.GetInt32()).ToArray();var f=new Fixture(bases[i[1]],i[1],i[5]!=0);
            EquipmentPropertyWriter.Apply(plans[i[0]],new(i[3],i[3],8),i[2],f);if(i[4]!=0){EquipmentPropertyWriter.Ethereality(f);}
            bool Same(Dictionary<uint,int> actual,JsonElement expected)=>actual.OrderBy(p=>p.Key).Select(p=>(p.Key,p.Value)).SequenceEqual(expected.EnumerateArray().Select(v=>(v[0].GetUInt32(),v[1].GetInt32())));
            Require(Same(f.BaseStats,row[1])&&Same(f.List,row[2])&&f.Draws==row[3].GetInt32(),"Source writer base/list/draw result differs at "+count);
            var trace=row[4].EnumerateArray().Select(v=>v.EnumerateArray().Select(x=>x.GetInt32()).ToArray()).ToArray();
            Require(f.Trace.Count==trace.Length&&f.Trace.Zip(trace).All(p=>p.First.SequenceEqual(p.Second)),"Source writer service order differs at "+count);count++;
        }
        Require(count==45840&&plans.Count==284&&plans.Values.Count(p=>p.Supported)==191,"Numeric property scope missing or incomplete golem enabled");
    }
    private static EquipmentPropertyBase Basis(Item item)
    {var b=ClassGear.Base(item);return new(b.MinDamage,b.MaxDamage,b.TwoHandMin,b.TwoHandMax,b.MissileMin,b.MissileMax,b.MinDefense,b.MaxDefense);}
    private static Saved Exercise(BaseCreature pet,Item item)
    {
        var state=ClassGear.State(item);var b=Basis(item);int life=SkillHealth.Balance256(pet),defense=ClassStats.Defense(pet);string gear=JsonSerializer.Serialize(state);
        bool weapon=ClassGear.Slot(item)==GearSlot.Weapon;var actor=new EquipmentStatUnit(pet,1);var original=new EquipmentStatUnit(item,4);
        actor.Stats=new(){Flags=EquipmentStatNode.Extended,OwnerKind=1,Owner=actor};original.Stats=new(){Flags=EquipmentStatNode.Extended,Owner=original};
        var values=new EquipmentStatValues(_=>IronGolemItems.Metadata(state.BaseCode).Body1,_=>null,_=>throw new NotSupportedException("No player metadata"),(_,_,_,_,_)=>{});
        foreach(var p in new[]{(7,life),(31,defense),(12,37)}){values.SetBase(actor.Stats,EquipmentStatKey.Pack(p.Item1),p.Item2);}
        foreach(var p in new[]{(21,b.Minimum),(22,b.Maximum),(23,b.SecondaryMinimum),(24,b.SecondaryMaximum),(159,b.ThrowMinimum),(160,b.ThrowMaximum),(31,weapon?0:state.Defense)})
        {values.SetBase(original.Stats,EquipmentStatKey.Pack(p.Item1),p.Item2);}
        var omissions=EquipmentRawProperties.Project(item,original,values,b,weapon,!weapon,false).Select(o=>o.Code+":"+o.Reason).ToArray();
        Require(omissions.All(o=>o.StartsWith("sock:",StringComparison.Ordinal)),"Unexpected native fixture property omission");
        if(!weapon)
        {
            Require(omissions.Length==1&&EquipmentStatValues.Base(original.Stats,EquipmentStatKey.Pack(31),false)==b.MaximumArmor+1,"Armor reset or explicit socket omission missing");
            Require(original.Stats.Last is{IsExtended:false} propertyList&&EquipmentStatValues.Raw(propertyList.BaseValues,EquipmentStatKey.Pack(216))==2560,"Raw per-level life coefficient was flattened or lost");
        }
        else{Require(omissions.Length==0,"Plain sword acquired omissions");}
        EquipmentStatGraph.Merge(actor,original,true,values);var c=IronGolemItems.Equipment(pet,37);
        int hp=EquipmentStatValues.Total(actor.Stats,EquipmentStatKey.Pack(7))-life,ac=EquipmentStatValues.Total(actor.Stats,EquipmentStatKey.Pack(31))-defense;
        int minimum=EquipmentStatValues.Total(original.Stats,EquipmentStatKey.Pack(21)),maximum=EquipmentStatValues.Total(original.Stats,EquipmentStatKey.Pack(22));
        Require(hp==c.LifeBonus256&&ac==c.Defense&&(!weapon||(minimum,maximum)==c.WeaponDamage),"Original raw properties did not reach recipient numeric graph");
        EquipmentStatGraph.Detach(original.Stats,values);Require(EquipmentStatValues.Total(actor.Stats,EquipmentStatKey.Pack(7))==life&&EquipmentStatValues.Total(actor.Stats,EquipmentStatKey.Pack(31))==defense,"Raw original removal left contributions");
        Require(item.Parent==pet&&!item.Visible&&!item.Movable&&JsonSerializer.Serialize(ClassGear.State(item))==gear&&SkillHealth.Balance256(pet)==life&&ClassStats.Defense(pet)==defense,"Raw projection changed original item/live creature");
        return new(pet.ControlMaster.Serial.Value,pet.Serial.Value,item.Serial.Value,gear,life,defense,hp,ac,minimum,maximum,omissions);
    }
    private static void Check()
    {
        SourceCases();var owner=new PlayerMobile{Name="RawPropertyOwner",Body=0x190,Player=true};owner.AddItem(new Backpack());ClassCreation.Profiles.Assign(owner,DiabloClass.Necromancer);ClassCreation.Profiles.AdvanceLevel(owner,47);
        for(int n=0;n<2;n++){Require(ClassCreation.Profiles.TryTrain(owner.Serial,"necromancer.raiseskeleton",out _),"Raw holder training failed");}FixtureSpace.Place(owner,new(6100,1750,0),Map.Trammel);
        Item[] items=[NamedGear.Create(NamedGear.Catalog.Items.Single(r=>r.SourceKey=="Spiritforge").Id,99,new Random(71),true),new Broadsword()];ClassGear.Profiles.Ensure(items[1]);var saved=new Saved[2];var pets=new SkeletonGuardian[2];
        for(int n=0;n<2;n++)
        {
            var pet=new SkeletonGuardian{Name="Raw item properties [persistent storage fixture]",Frozen=true,CantWalk=true};pet.Initialize(owner,2,0);Require(pet.SetControlMaster(owner),"Raw holder ownership failed");PersistentMinions.Profiles.Track(pet);pet.MoveToWorld(owner.Location,owner.Map);pet.AIObject?.AITimer.Stop();pets[n]=pet;
            items[n].MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);Require(IronGolemItems.TryAttach(owner,pet,items[n],out var why),"Raw original attach failed: "+why);saved[n]=Exercise(pet,items[n]);
        }
        owner.AutoStablePets();Require(pets.All(p=>p.ControlMaster==owner&&!p.IsStabled),"Raw holders entered stabling");File.WriteAllText(Manifest,JsonSerializer.Serialize(saved));foreach(var pet in pets){pet.Internalize();}owner.Internalize();
        Log.Information("Item property writers:45840complete-source dispatch/base-reset/ethereal contexts and exact write traces pass;actual original armor/sword project saved raw properties without reroll,retain FP256life-per-level and explicit socket omission;live NPC effects remain gated");
    }
    private static void Reload()
    {
        SourceCases();foreach(var saved in JsonSerializer.Deserialize<Saved[]>(File.ReadAllText(Manifest)))
        {
            var pet=World.FindMobile((Serial)saved.Pet) as BaseCreature;var item=World.FindItem((Serial)saved.Item);
            Require(pet is{Alive:true,Deleted:false}&&pet.ControlMaster?.Serial.Value==saved.Owner&&item is{Deleted:false},"Saved raw original missing");
            var current=Exercise(pet,item);Require(current with{Omissions=saved.Omissions}==saved&&current.Omissions.SequenceEqual(saved.Omissions),"Saved raw original/roll/projection changed");pet.AIObject?.AITimer.Stop();
        }
        Log.Information("Item property writers:saved original armor/sword retain identity,private ownership,complete rolls and raw numeric projection through actual reload;socket writer remains explicitly omitted");
    }
}
