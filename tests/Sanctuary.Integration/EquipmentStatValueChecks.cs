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
public static class EquipmentStatValueChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(EquipmentStatValueChecks));
    private static string Manifest=>Path.Join(Core.BaseDirectory,"item-stat-values-fixtures.json");
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private sealed record Saved(uint Owner,uint Pet,uint Item,string Gear,int Life256,int Defense,int ItemLife256,int ItemDefense,int ItemMaximum);
    private sealed class Fixture
    {
        internal readonly EquipmentStatUnit Actor,Item;
        internal EquipmentStatNode Root=>Actor.Stats;internal EquipmentStatNode Node=>Item.Stats;
        internal readonly EquipmentStatValues Values;internal readonly List<int[]> Trace=new();
        private static int Id(EquipmentStatUnit u)=>u==null?0:(int)u.Entity;
        internal Fixture(int kind,int level,int dynamic,int clock,int time)
        {
            Actor=new(1,kind);Item=new(2,4);Actor.Stats=new(){Flags=EquipmentStatNode.Extended,Owner=Actor,OwnerKind=kind};Item.Stats=new(){Flags=EquipmentStatNode.Extended,Owner=Item};
            Root.BaseValues=ArrayOf((0,100),(1,20),(2,50),(3,30),(7,256000),(9,76800),(11,25600),(12,level),(21,7),(22,14),(31,80));Root.FullValues=(EquipmentStatValue[])Root.BaseValues.Clone();
            Node.BaseValues=ArrayOf((21,59),(22,94),(31,167));
            Node.Last=new(){Flags=EquipmentStatNode.Extended|(dynamic!=0?EquipmentStatNode.Dynamic:0),Parent=Node,
                FullValues=ArrayOf((7,8192),(31,30),(17,200),(18,200),(216,320),(218,5),(219,20),(214,4),(269,(300<<12)|(256<<2)),(272,(280<<12)|(256<<2)),(273,(270<<12)|(256<<2)),(0,3),(1,5),(2,7),(3,9))};
            Values=new(_=>4,_=>clock!=0?time:null,_=>new(4,4,16),(n,s,id,old,v)=>Trace.Add([Id(n.Owner),Id(s),(int)id,old,v]));
        }
        internal static EquipmentStatValue[] ArrayOf(params (int Id,int Value)[] values)=>values.Where(v=>v.Value!=0).Select(v=>new EquipmentStatValue(EquipmentStatKey.Pack(v.Id),v.Value)).OrderBy(v=>v.Packed).ToArray();
        internal long[][] Snapshot()=>[[Root.Flags,Node.Flags],Flatten(Root.BaseValues),Flatten(Root.FullValues),Flatten(Node.BaseValues),Flatten(Node.FullValues)];
        private static long[] Flatten(EquipmentStatValue[] values)=>values.SelectMany(v=>new[]{(long)v.Packed,v.Value}).ToArray();
        internal void Compare(JsonElement state,int context,int phase)
        {
            var expected=new List<long[]>{new long[]{state[0].GetUInt32(),state[1].GetUInt32()}};
            for(int a=2;a<6;a++){expected.Add(state[a].EnumerateArray().SelectMany(v=>new[]{v[0].GetInt64(),v[1].GetInt64()}).ToArray());}
            Require(Snapshot().Zip(expected).All(p=>p.First.SequenceEqual(p.Second)),"Numeric source arrays/flags differ at "+context+" phase "+phase);
        }
    }
    public static void Configure(){Register("item-stat-values-check",Check);Register("item-stat-values-reload-check",Reload);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Recursive original-equipment stat values",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void SourceCases()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"item-stat-values-source-probe.json")));int queryCount=0,updateCount=0;
        foreach(var row in doc.RootElement.GetProperty("queries").EnumerateArray())
        {
            var i=row[0].EnumerateArray().Select(v=>v.GetInt32()).ToArray();var f=new Fixture(i[0],i[1],i[2],i[3],i[4]);f.Node.Recipient=f.Actor;
            for(int id=0;id<368;id++)
            {Require(f.Values.Query(f.Node,EquipmentStatKey.Pack(id))==row[1][id].GetInt32()&&f.Values.Query(f.Root,EquipmentStatKey.Pack(id))==row[2][id].GetInt32(),"Complete recursive query differs at "+queryCount+" stat "+id);}
            queryCount++;
        }
        foreach(var row in doc.RootElement.GetProperty("updates").EnumerateArray())
        {
            int[] i=row[0].EnumerateArray().Select(v=>v.GetInt32()).ToArray();var f=new Fixture(1,i[0],0,0,91);
            foreach(uint id in new uint[]{7,17,18,21,22,31,214,216,218,219}){f.Values.Recalculate(f.Node,EquipmentStatKey.Pack((int)id));}
            f.Trace.Clear();f.Compare(row[1][0],updateCount,0);
            EquipmentStatGraph.Merge(f.Actor,f.Item,i[1]!=0,f.Values);f.Compare(row[1][1],updateCount,1);
            bool changed=f.Values.SetBase(f.Node,EquipmentStatKey.Pack(i[2],i[4]),i[3],f.Item);f.Compare(row[1][2],updateCount,2);
            bool repeated=f.Values.SetBase(f.Node,EquipmentStatKey.Pack(i[2],i[4]),i[3],f.Item);f.Compare(row[1][3],updateCount,3);
            f.Values.SetBase(f.Root,EquipmentStatKey.Pack(12),i[0]+1,f.Actor);f.Compare(row[1][4],updateCount,4);
            EquipmentStatGraph.Detach(f.Node,f.Values);f.Compare(row[1][5],updateCount,5);
            Require(changed==(row[2][0].GetInt32()!=0)&&repeated==(row[2][1].GetInt32()!=0),"Source base-set return differs");
            var trace=row[3].EnumerateArray().Select(v=>v.EnumerateArray().Select(n=>n.GetInt32()).ToArray()).ToArray();
            Require(f.Trace.Count==trace.Length&&f.Trace.Zip(trace).All(p=>p.First.SequenceEqual(p.Second)),"Source value-change callback order differs at "+updateCount);updateCount++;
        }
        Require(queryCount==144&&updateCount==480&&!NecromancerCombat.Implemented(CombatAbility.IronGolem),"Numeric source scope missing or incomplete actor enabled");
    }
    private static Saved Exercise(BaseCreature pet,Item item)
    {
        var c=IronGolemItems.Equipment(pet,37);int life=SkillHealth.Balance256(pet),defense=ClassStats.Defense(pet);string gear=JsonSerializer.Serialize(ClassGear.State(item));
        var u=IronGolemItems.StatUnits(pet,37,Fixture.ArrayOf((7,life),(31,defense),(12,37)),[],Fixture.ArrayOf((7,c.LifeBonus256),(31,c.Defense),(22,c.WeaponDamage.Max)));
        int changes=0;var values=new EquipmentStatValues(_=>IronGolemItems.Metadata(ClassGear.State(item).BaseCode).Body1,_=>null,_=>throw new NotSupportedException("No NPC character coefficients"),(n,s,id,old,v)=>{Require(n==u.Recipient.Stats&&s?.Entity==item,"Native numeric callback lost original identities");changes++;});
        EquipmentStatGraph.Merge(u.Recipient,u.Item,true,values);
        Require(EquipmentStatValues.Total(u.Recipient.Stats,EquipmentStatKey.Pack(7))==life+c.LifeBonus256&&EquipmentStatValues.Total(u.Recipient.Stats,EquipmentStatKey.Pack(31))==defense+c.Defense&&EquipmentStatValues.Total(u.Recipient.Stats,EquipmentStatKey.Pack(22))==c.WeaponDamage.Max,"Native original suppliers did not enter numeric graph");
        int prior=changes;EquipmentStatGraph.Merge(u.Recipient,u.Item,true,values);Require(changes==prior,"Repeated original attachment duplicated callback");
        EquipmentStatGraph.ToggleDamage(u.Recipient,u.Item,false,values);Require(EquipmentStatValues.Total(u.Recipient.Stats,EquipmentStatKey.Pack(22))==0&&EquipmentStatValues.Total(u.Recipient.Stats,EquipmentStatKey.Pack(7))==life+c.LifeBonus256,"Numeric damage exclusion altered life or retained damage");
        EquipmentStatGraph.ToggleDamage(u.Recipient,u.Item,true,values);Require(EquipmentStatValues.Total(u.Recipient.Stats,EquipmentStatKey.Pack(22))==c.WeaponDamage.Max,"Numeric damage restoration lost maximum");
        EquipmentStatGraph.Detach(u.Item.Stats,values);
        Require(EquipmentStatValues.Total(u.Recipient.Stats,EquipmentStatKey.Pack(7))==life&&EquipmentStatValues.Total(u.Recipient.Stats,EquipmentStatKey.Pack(31))==defense&&EquipmentStatValues.Total(u.Recipient.Stats,EquipmentStatKey.Pack(22))==0,"Numeric removal left original contribution");
        Require(item.Parent==pet&&!item.Visible&&!item.Movable&&JsonSerializer.Serialize(ClassGear.State(item))==gear&&SkillHealth.Balance256(pet)==life&&ClassStats.Defense(pet)==defense,"Transient numeric service changed original item/live actor");
        return new(pet.ControlMaster.Serial.Value,pet.Serial.Value,item.Serial.Value,gear,life,defense,c.LifeBonus256,c.Defense,c.WeaponDamage.Max);
    }
    private static void Check()
    {
        SourceCases();var owner=new PlayerMobile{Name="ItemStatValuesOwner",Body=0x190,Player=true};owner.AddItem(new Backpack());ClassCreation.Profiles.Assign(owner,DiabloClass.Necromancer);ClassCreation.Profiles.AdvanceLevel(owner,47);
        for(int n=0;n<2;n++){Require(ClassCreation.Profiles.TryTrain(owner.Serial,"necromancer.raiseskeleton",out _),"Numeric holder training failed");}FixtureSpace.Place(owner,new(6100,1730,0),Map.Trammel);
        Item[] items=[NamedGear.Create(NamedGear.Catalog.Items.Single(r=>r.SourceKey=="Spiritforge").Id,99,new Random(81),true),new Broadsword()];ClassGear.Profiles.Ensure(items[1]);var saved=new Saved[2];var pets=new SkeletonGuardian[2];
        for(int n=0;n<2;n++)
        {
            var pet=new SkeletonGuardian{Name="Numeric graph [persistent storage fixture]",Frozen=true,CantWalk=true};pet.Initialize(owner,2,0);Require(pet.SetControlMaster(owner),"Numeric holder owner failed");PersistentMinions.Profiles.Track(pet);pet.MoveToWorld(owner.Location,owner.Map);pet.AIObject?.AITimer.Stop();pets[n]=pet;
            items[n].MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);Require(IronGolemItems.TryAttach(owner,pet,items[n],out var why),"Numeric original attach failed: "+why);saved[n]=Exercise(pet,items[n]);
        }
        owner.AutoStablePets();Require(pets.All(p=>p.ControlMaster==owner&&!p.IsStabled),"Numeric holders entered stabling");File.WriteAllText(Manifest,JsonSerializer.Serialize(saved));foreach(var pet in pets){pet.Internalize();}owner.Internalize();
        Log.Information("Item stat values:105984complete-source query results/480six-stage updates match numeric arrays,flags,base-set returns and ordered callbacks;actual original armor/sword feed numeric graph and remove cleanly;live NPC setters remain gated");
    }
    private static void Reload()
    {
        SourceCases();foreach(var saved in JsonSerializer.Deserialize<Saved[]>(File.ReadAllText(Manifest)))
        {
            var pet=World.FindMobile((Serial)saved.Pet) as BaseCreature;var item=World.FindItem((Serial)saved.Item);
            Require(pet is{Alive:true,Deleted:false}&&pet.ControlMaster?.Serial.Value==saved.Owner&&item is{Deleted:false}&&Exercise(pet,item)==saved,"Saved original numeric graph fixture changed");pet.AIObject?.AITimer.Stop();
        }
        Log.Information("Item stat values:saved original armor/sword retain identity,private parents,full rolls and numeric suppliers;fresh numeric graphs pass after actual reload without changing live NPC stats");
    }
}
