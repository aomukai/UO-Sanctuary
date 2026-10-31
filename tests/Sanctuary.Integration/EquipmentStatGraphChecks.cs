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
public static class EquipmentStatGraphChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(EquipmentStatGraphChecks));
    private static string Manifest=>Path.Join(Core.BaseDirectory,"item-stat-attachment-fixtures.json");
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private sealed record Saved(uint Owner,uint Pet,uint Item,string Gear,int Life256,int Defense,int ItemLife256,int ItemDefense,int ItemMaximum);
    // The frozen attachment proof uses abstract IDs0/1/2; translate only its
    // trace keys while exercising canonical native packed keys here.
    private sealed class Fixture:EquipmentStatServices
    {
        internal EquipmentStatUnit A,B,Item;internal EquipmentStatNode Node;internal int Body;
        internal readonly int[,] Sums=new int[3,3];internal readonly List<int[]> Trace=new();
        private static int Id(EquipmentStatUnit unit)=>unit==null?0:(int)unit.Entity;
        internal override int BodyLocation(EquipmentStatUnit u){Trace.Add([1,Id(u),Body,0,0]);return Body;}
        internal override EquipmentStatCost? Cost(ushort id)=>id<3?new(id!=0,id==2):null;
        internal override void Recalculate(EquipmentStatNode n,uint id)=>Trace.Add([2,Id(n.Recipient),(int)EquipmentStatKey.Stat(id),0,0]);
        internal override void Propagate(EquipmentStatNode n,uint id,int value,EquipmentStatUnit owner)
        {int target=n==A.Stats?1:2;Sums[target,EquipmentStatKey.Stat(id)]+=value;Trace.Add([3,target,(int)EquipmentStatKey.Stat(id),value,Id(owner)]);}
        internal Fixture(int[] i)
        {
            A=new(1,1){Stats=new(){Flags=i[5]!=0?EquipmentStatNode.Extended:0}};B=new(2,1){Stats=new(){Flags=EquipmentStatNode.Extended}};Item=new(29,i[0]);Body=i[1];
            A.Stats.Owner=A;B.Stats.Owner=B;
            uint flags=(i[6]!=0?EquipmentStatNode.Extended:0)|(i[8]%2!=0?EquipmentStatNode.Set:0)|((i[8]&2)!=0?EquipmentStatNode.Dynamic:0)|((i[8]&4)!=0?EquipmentStatNode.Permanent:0)|((i[8]&8)!=0?EquipmentStatNode.TempOnly:0);
            Node=new(){Flags=flags,State=13,Owner=Item,BaseValues=[new(EquipmentStatKey.Pack(0),10),new(EquipmentStatKey.Pack(1),20),new(EquipmentStatKey.Pack(2),30)],FullValues=[new(EquipmentStatKey.Pack(0),12),new(EquipmentStatKey.Pack(1),24),new(EquipmentStatKey.Pack(2),36)]};
            Node.Removed=(u,state,n)=>Trace.Add([4,Id(u),state,n.Parent!=null?1:0,n.Recipient!=null?1:0]);if(i[4]!=0){Item.Stats=Node;}
            if(i[7]!=0)
            {
                Node.Parent=B.Stats;Node.Recipient=B;
                if(Node.Is(EquipmentStatNode.Set)){B.Stats.Sets=Node;}
                else{B.Stats.Last=Node;foreach(var v in Node.Values){if(!Node.Is(EquipmentStatNode.Dynamic)||v.Stat==0){Sums[2,v.Stat]=v.Value;}}}
            }
        }
        internal long[] Snapshot(EquipmentStatUnit source)
        {
            var owner=EquipmentStatGraph.Owner(source,out bool full);
            return[Node.Flags,A.Stats.Flags,Node.Parent==A.Stats?1:Node.Parent==B.Stats?2:0,Id(Node.Recipient),Sums[1,0],Sums[1,1],Sums[1,2],Sums[2,0],Sums[2,1],Sums[2,2],Id(owner),full?1:0];
        }
    }
    public static void Configure(){Register("item-stat-attachment-check",Check);Register("item-stat-attachment-reload-check",Reload);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Original equipment stat-list ownership/update ordering",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void SourceCases()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"item-stat-attachment-source-probe.json")));int count=0;
        foreach(var row in doc.RootElement.EnumerateArray())
        {
            int[] i=row[0].EnumerateArray().Select(v=>v.GetInt32()).ToArray();var f=new Fixture(i);var target=i[2]!=0?f.A:null;var source=i[3]!=0?f.Item:null;var states=new List<long[]>{f.Snapshot(source)};
            EquipmentStatGraph.Merge(target,source,i[9]!=0,f);states.Add(f.Snapshot(source));EquipmentStatGraph.Merge(target,source,i[9]!=0,f);states.Add(f.Snapshot(source));
            bool changed=EquipmentStatGraph.ToggleDamage(target,source,i[9]==0,f);states.Add(f.Snapshot(source));EquipmentStatGraph.Detach(f.Node,f);states.Add(f.Snapshot(source));
            var expected=row[1].EnumerateArray().Select(s=>s.EnumerateArray().Select(v=>v.GetInt64()).ToArray()).ToArray();
            Require(states.Zip(expected).All(p=>p.First.SequenceEqual(p.Second))&&changed==(row[2].GetInt32()!=0),"Source attachment topology/owner/flags/deltas differ at "+count);
            var trace=row[3].EnumerateArray().Select(s=>s.EnumerateArray().Select(v=>v.GetInt32()).ToArray()).ToArray();
            Require(f.Trace.Count==trace.Length&&f.Trace.Zip(trace).All(p=>p.First.SequenceEqual(p.Second)),"Source attachment service order differs at "+count);
            Require(f.Node.Parent==null&&f.Node.Recipient==null&&f.Node.Previous==null&&f.Node.Next==null,"Detached item retains graph links");count++;
        }
        Require(count==12288,"Attachment source scope missing or incomplete golem enabled");
    }
    // Explicit adapter fixture, not a complete stat projection or live NPC setter.
    private sealed class NativeServices:EquipmentStatServices
    {
        internal readonly Dictionary<uint,int> Totals=new();
        internal EquipmentStatNode Root;internal Item Original;internal int Updates,Removed;
        internal override int BodyLocation(EquipmentStatUnit u)
        {Require(u.Entity==Original,"Body-location query lost original item");return IronGolemItems.Metadata(ClassGear.State(Original).BaseCode).Body1;}
        internal override EquipmentStatCost? Cost(ushort id)=>id switch{7 or 31=>new(false,false),22=>new(true,false),_=>throw new NotSupportedException("Undeclared fixture stat")};
        internal override void Recalculate(EquipmentStatNode n,uint id)=>throw new NotSupportedException("No numeric operation-recalculation service in this adapter fixture");
        internal override void Propagate(EquipmentStatNode n,uint id,int delta,EquipmentStatUnit source)
        {Require(n==Root&&source.Entity==Original,"Delta lost recipient or original owner");uint stat=EquipmentStatKey.Stat(id);Totals[stat]=checked(Totals.GetValueOrDefault(stat)+delta);Updates++;}
    }
    private static Saved Exercise(BaseCreature pet,Item item)
    {
        var c=IronGolemItems.Equipment(pet,37);int life=SkillHealth.Balance256(pet),defense=ClassStats.Defense(pet);
        string gear=JsonSerializer.Serialize(ClassGear.State(item));object parent=item.Parent;
        EquipmentStatValue[] basis=[new(EquipmentStatKey.Pack(7),life),new(EquipmentStatKey.Pack(31),defense),new(EquipmentStatKey.Pack(22),0)];
        EquipmentStatValue[] full=[new(EquipmentStatKey.Pack(7),c.LifeBonus256),new(EquipmentStatKey.Pack(31),c.Defense),new(EquipmentStatKey.Pack(22),c.WeaponDamage.Max)];
        var units=IronGolemItems.StatUnits(pet,37,basis,[],full);var node=units.Item.Stats;
        Require(units.Recipient.Entity==pet&&units.Item.Entity==item&&node.Owner==units.Item,"Native wrappers substituted original entities");
        var service=new NativeServices{Root=units.Recipient.Stats,Original=item};foreach(var v in basis){service.Totals.Add(v.Stat,v.Value);}
        // Inputs are copied; later caller edits must not mutate a posted list.
        basis[0]=new(EquipmentStatKey.Pack(7),-1);full[0]=new(EquipmentStatKey.Pack(7),-1);
        Require(units.Recipient.Stats.FullValues[0].Value==life&&node.FullValues[0].Value==c.LifeBonus256,"Stat arrays alias caller input");
        node.Removed=(u,state,n)=>{Require(u.Entity==pet&&n.Parent==null&&n.Recipient==null&&n.Previous==null&&n.Next==null,"Removal callback saw linked original");service.Removed++;};
        void Totals(bool attached,bool damage)
        {
            Require(service.Totals[7]==life+(attached?c.LifeBonus256:0)&&service.Totals[31]==defense+(attached?c.Defense:0)
                &&service.Totals[22]==(attached&&damage?c.WeaponDamage.Max:0),"Explicit fixture delta aggregate differs");
        }
        EquipmentStatGraph.Merge(units.Recipient,units.Item,true,service);Totals(true,true);
        Require(EquipmentStatGraph.Owner(units.Item,out bool included)==units.Recipient&&included&&node.Recipient==units.Recipient,"Original item graph ownership differs");
        int updates=service.Updates;EquipmentStatGraph.Merge(units.Recipient,units.Item,true,service);Totals(true,true);Require(service.Updates==updates,"Repeat merge duplicated item stats");
        Require(EquipmentStatGraph.ToggleDamage(units.Recipient,units.Item,false,service),"Damage exclusion unchanged");Totals(true,false);
        Require(EquipmentStatGraph.Owner(units.Item,out included)==units.Recipient&&!included,"Damage exclusion changed ownership");
        Require(EquipmentStatGraph.ToggleDamage(units.Recipient,units.Item,true,service),"Damage restoration unchanged");Totals(true,true);
        EquipmentStatGraph.Detach(node,service);Totals(false,false);
        Require(service.Removed==1&&EquipmentStatGraph.Owner(units.Item,out included)==null&&!included,"Detached original retains graph owner");
        Require(item.Parent==parent&&!item.Visible&&!item.Movable&&JsonSerializer.Serialize(ClassGear.State(item))==gear,"Transient graph changed original inventory/gear");
        Require(SkillHealth.Balance256(pet)==life&&ClassStats.Defense(pet)==defense,"Prepared graph unexpectedly applied live NPC stats");
        return new(pet.ControlMaster.Serial.Value,pet.Serial.Value,item.Serial.Value,gear,life,defense,c.LifeBonus256,c.Defense,c.WeaponDamage.Max);
    }
    private static void Check()
    {
        SourceCases();var owner=new PlayerMobile{Name="ItemStatAttachmentOwner",Body=0x190,Player=true};owner.AddItem(new Backpack());ClassCreation.Profiles.Assign(owner,DiabloClass.Necromancer);ClassCreation.Profiles.AdvanceLevel(owner,47);
        for(int n=0;n<2;n++){Require(ClassCreation.Profiles.TryTrain(owner.Serial,"necromancer.raiseskeleton",out _),"Persistent holder training failed");}
        FixtureSpace.Place(owner,new(6100,1710,0),Map.Trammel);
        Item[] items=[NamedGear.Create(NamedGear.Catalog.Items.Single(r=>r.SourceKey=="Spiritforge").Id,99,new Random(71),true),new Broadsword()];ClassGear.Profiles.Ensure(items[1]);
        var saved=new Saved[items.Length];var pets=new SkeletonGuardian[items.Length];
        for(int n=0;n<items.Length;n++)
        {
            var pet=new SkeletonGuardian{Name="Stat graph [persistent storage fixture]",Frozen=true,CantWalk=true};pet.Initialize(owner,2,0);Require(pet.SetControlMaster(owner),"Holder ownership failed");PersistentMinions.Profiles.Track(pet);pet.MoveToWorld(owner.Location,owner.Map);pet.AIObject?.AITimer.Stop();pets[n]=pet;
            var item=items[n];item.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);Require(IronGolemItems.TryAttach(owner,pet,item,out var why),"Original item attach failed: "+why);saved[n]=Exercise(pet,item);
        }
        Require(saved[0].ItemLife256>0&&saved[0].ItemLife256%256==64&&saved[0].ItemDefense>0&&saved[1].ItemMaximum==14,"Native item fixtures lost distinguishing numeric suppliers");
        owner.AutoStablePets();Require(pets.All(p=>p.ControlMaster==owner&&!p.IsStabled),"Stat graph holders entered ordinary stabling");
        File.WriteAllText(Manifest,JsonSerializer.Serialize(saved));foreach(var pet in pets){pet.Internalize();}owner.Internalize();
        Log.Information("Item stat attachment:12288source contexts/5stages/7routines match topology,owners,flags and ordered service traces;actual original armor/sword pass transient merge,repeat,damage toggle,detach and caller-array isolation;live NPC stat setters remain gated");
    }
    private static void Reload()
    {
        SourceCases();foreach(var saved in JsonSerializer.Deserialize<Saved[]>(File.ReadAllText(Manifest)))
        {
            var owner=World.FindMobile((Serial)saved.Owner) as PlayerMobile;var pet=World.FindMobile((Serial)saved.Pet) as BaseCreature;var item=World.FindItem((Serial)saved.Item);
            Require(owner is{Deleted:false}&&ClassStats.Level(owner)==47&&pet is{Alive:true,Deleted:false}&&pet.ControlMaster==owner&&item is{Deleted:false},"Saved original graph fixture missing");
            Require(Exercise(pet,item)==saved,"Saved original item/roll/numeric context changed");pet.AIObject?.AITimer.Stop();
        }
        Log.Information("Item stat attachment:saved original armor/sword preserve serials,private parents,complete gear and numeric suppliers;fresh transient graphs pass after actual reload without serialized graph replay or live NPC stat application");
    }
}
