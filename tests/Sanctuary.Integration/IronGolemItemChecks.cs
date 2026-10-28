// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
namespace Sanctuary.Integration;
public static class IronGolemItemChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(IronGolemItemChecks));
    private static string Manifest=>Path.Join(Core.BaseDirectory,"iron-golem-items-fixtures.json");
    private sealed record Stored(IronGolemItemBinding Binding,string Gear,string Type,int Hue,int? Hits,int? MaximumHits);
    private sealed record Saved(uint Owner,Stored[] Items);
    private static void Require(bool b,string why){if(!b){throw new InvalidOperationException(why);}}
    private static int[] N(JsonElement e)=>e.EnumerateArray().Select(x=>x.GetInt32()).ToArray();
    public static void Configure()
    {
        Register("iron-golem-items-check",Check);Register("iron-golem-items-reload-check",Reload);
        EventSink.WorldLoad+=()=>{if(File.Exists(Manifest)){var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));foreach(var item in s.Items){if(World.FindMobile((Serial)item.Binding.Pet) is BaseCreature pet){pet.AIObject?.AITimer.Stop();}}}};
    }
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Iron Golem original-item eligibility/transfer/persistence",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private sealed class Source:IronGolemCastServices
    {
        internal int[] I;internal bool Flag,Attached;internal int Mode;internal int[] Request=new int[9];internal readonly List<int[]> Trace=new();
        internal Source(int[] i){I=i;Mode=i[1];}
        private void T(int id,int a=0,int b=0,int c=0)=>Trace.Add([id,a,b,c]);
        internal override bool ResolveTarget(){T(1);return I[0]>=0;}
        internal override int TargetType()=>I[0];
        internal override int TargetMode()=>Mode;
        internal override bool Metal(){T(2);return (I[2]&2)!=0;}
        internal override bool Identified(){T(3,I[3]);return I[3]!=0;}
        internal override bool HasStatOwner(){T(4,I[4]);return I[4]!=0;}
        internal override void SetDoFlag()=>Flag=true;
        internal override bool HasSkill(int skill){Require(skill==90,"Wrong source skill");T(6,I[5],Flag?0x100:0);return I[5]!=0;}
        internal override (int Monster,int Mode) ResolveSummon(int skill,int rank){Require(skill==90&&rank==15,"Wrong source summon context");T(7,I[6],1);return (I[6],1);}
        internal override byte PetType()=>checked((byte)I[9]);
        internal override int PetTypeCount()=>I[10];
        internal override int PetMaximum(int skill,int rank){T(8,I[11]);return I[11];}
        internal override Point2D TargetPoint()=>new(210,220);
        internal override bool Create(IronGolemSummonRequest r){Request=[r.AiSpecialState,r.Mode,r.Monster,r.PetType,r.Flags,r.Maximum,r.Point.X,r.Point.Y,1];T(9,r.Monster,r.PetType,r.Maximum);return I[7]!=0;}
        internal override void DeleteGroundUnit()=>T(10,4,29);
        internal override void FreeCollision()=>T(11);
        internal override void RemoveFromRoom()=>T(12);
        internal override (byte First,byte Second) BodyLocations()
        {T(2);return I[12] switch{0=>(1,1),1=>(4,5),2=>(3,3),3=>(9,9),_=>(0,0)};}
        internal override void SetCursorMode(){Mode=4;T(14,4);}
        internal override void EquipOriginal(byte body){T(15,29,body,1);if(I[8]!=0){Mode=1;Attached=true;}}
        internal override void BaseStats(int rank)=>T(16);
        internal override void PassiveStats(int skill,int rank)=>T(17);
        internal override void Resistance()=>T(18);
        internal override void UpdateClient(){T(19);T(20);}
        internal override void UpdateAi()=>T(21,42);
    }
    private static void Trace(Source s,JsonElement row,string why)
    {var expected=row.EnumerateArray().Select(N).ToArray();Require(s.Trace.Count==expected.Length&&s.Trace.Zip(expected).All(x=>x.First.SequenceEqual(x.Second)),why);}
    private static void SourceCases()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"iron-golem-items-source-probe.json")));int count=0,metadata=0,bases=0,metal=0;
        foreach(string group in new[]{"cast","metadata"})foreach(var row in doc.RootElement.GetProperty(group).EnumerateArray())
        {
            var s=new Source(N(row[0]));Require(IronGolemItemCast.Eligible(s)==(row[1].GetInt32()!=0),"Source Iron Golem start differs");Trace(s,row[4],"Source lazy item eligibility differs");s.Trace.Clear();
            bool result=IronGolemItemCast.Release(90,15,s);
            Require(new[]{result?1:0,s.Flag?0x100:0,s.Mode,s.Attached?1:0}.SequenceEqual(N(row[2]))&&s.Request.SequenceEqual(N(row[3])),"Source Iron Golem cast/item/request differs at "+count);
            Trace(s,row[5],"Source original-item/stat/client/AI order differs at "+count);if(group=="cast"){count++;}else{metadata++;}
        }
        foreach(var row in doc.RootElement.GetProperty("items").EnumerateArray())
        {
            var expected=JsonSerializer.Deserialize<IronGolemItemBase>(row.GetRawText());Require(IronGolemItems.Metadata(expected.Code)==expected,"Current item base metal/body metadata differs");bases++;if((expected.Flags&2)!=0){metal++;}
        }
        Require((count,metadata,bases,metal)==(6144,135,692,316),"Complete Iron Golem source scope missing");
        Require(!NecromancerCombat.Implemented(CombatAbility.IronGolem),"Incomplete full actor/property pipeline enabled");
    }
    private static BaseCreature Pet(PlayerMobile owner)
    {
        // Reuse the real persistent-minion holder. This is a storage fixture, not an Iron Golem actor.
        var p=new SkeletonGuardian();p.Initialize(owner,2,0);p.Frozen=p.CantWalk=true;p.ControlSlots=0;p.Name="Iron Golem item storage [fixture]";
        Require(p.SetControlMaster(owner),"Fixture control master failed");PersistentMinions.Profiles.Track(p);p.MoveToWorld(owner.Location,owner.Map);p.AIObject?.AITimer.Stop();return p;
    }
    private static void Ground(Item item,PlayerMobile owner)=>item.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);
    private static string Gear(Item item)=>JsonSerializer.Serialize(ClassGear.State(item));
    private static Stored Capture(BaseCreature pet,Item item)
    {
        var state=IronGolemItems.State(pet);Require(state!=null&&item.Parent==pet&&!item.Movable&&!item.Visible,"Original item not privately held by actor");
        return new(state,Gear(item),item.GetType().FullName,item.Hue,item is BaseWeapon w?w.HitPoints:item is BaseArmor a?a.HitPoints:null,item is BaseWeapon w2?w2.MaxHitPoints:item is BaseArmor a2?a2.MaxHitPoints:null);
    }
    private static void Check()
    {
        SourceCases();Require(IronGolemItems.Profiles!=null,"Item binding persistence not configured");
        var owner=new PlayerMobile{Name="IronGolemItemOwnerFixture",Body=0x190,Player=true};owner.AddItem(new Backpack());ClassCreation.Profiles.Assign(owner,DiabloClass.Necromancer);ClassCreation.Profiles.AdvanceLevel(owner,47);FixtureSpace.Place(owner,new(6100,1610,0),Map.Trammel);
        Require(ClassCreation.Profiles.TryTrain(owner.Serial,"necromancer.raiseskeleton",out _)&&ClassCreation.Profiles.TryTrain(owner.Serial,"necromancer.raiseskeleton",out _),"Persistent holder talent fixture failed");
        var first=Pet(owner);var second=Pet(owner);var sword=new Broadsword();var armor=new PlateChest();
        ClassGear.Profiles.Convert(sword,47,GearRarity.Rare,new Random(90));ClassGear.Profiles.Convert(armor,47,GearRarity.Rare,new Random(91));Ground(sword,owner);Ground(armor,owner);
        ClassGear.Profiles.Store(sword,ClassGear.State(sword) with{Identified=false});
        Require(!IronGolemItems.Eligible(sword,out _)&&sword.Parent==null&&!sword.Deleted,"Unidentified item accepted or mutated");
        ClassGear.Profiles.Store(sword,ClassGear.State(sword) with{Identified=true});ClassGear.Profiles.Store(armor,ClassGear.State(armor) with{Identified=true});
        sword.Hue=0x489;sword.MaxHitPoints=50;sword.HitPoints=13;armor.Hue=0x481;armor.MaxHitPoints=60;armor.HitPoints=17;
        string swordGear=Gear(sword),armorGear=Gear(armor);uint swordSerial=sword.Serial.Value,armorSerial=armor.Serial.Value;
        Require(IronGolemItems.TryAttach(owner,first,sword,out var why),"Original sword transfer failed: "+why);
        Require(sword.Serial.Value==swordSerial&&Gear(sword)==swordGear&&!sword.Deleted,"Transfer cloned/rerolled/deleted original sword");
        Require(!IronGolemItems.TryAttach(owner,second,sword,out _)&&sword.Parent==first,"Original item reused by a second actor");
        Require(IronGolemItems.TryAttach(owner,second,armor,out why),"Original armor transfer failed: "+why);
        Require(armor.Serial.Value==armorSerial&&Gear(armor)==armorGear,"Armor identity/roll changed");
        {
            var other=new Broadsword();
            Ground(other,owner);Require(!IronGolemItems.TryAttach(owner,first,other,out _)&&other.Parent==null&&other.Movable&&other.Visible,"Occupied binding consumed another item");
            other.Delete();
        }
        {var bow=new Bow();Ground(bow,owner);Require(!IronGolemItems.Eligible(bow,out _)&&!bow.Deleted&&bow.Parent==null,"Nonmetal source base accepted/mutated");bow.Delete();}
        {
            var protectedItem=new Broadsword();var refusedPet=Pet(owner);
            Ground(protectedItem,owner);protectedItem.Movable=false;
            Require(!IronGolemItems.TryAttach(owner,refusedPet,protectedItem,out _)&&protectedItem.Parent==null&&!protectedItem.Deleted,"Protected native item transferred");
            protectedItem.Delete();refusedPet.Delete();
        }
        {var inPack=new Broadsword();owner.Backpack.DropItem(inPack);Require(!IronGolemItems.Eligible(inPack,out _)&&inPack.Parent==owner.Backpack,"Stored item accepted/mutated");inPack.Delete();}
        var states=IronGolemItems.Snapshot();var captured=Task.Run(()=>{var w=new BufferWriter(new byte[1024*1024],true);IronGolemItems.Profiles.Serialize(w);return w.Buffer[..(int)w.Position];}).GetAwaiter().GetResult();
        var reader=new BufferReader(captured);Require(reader.ReadInt()==0,"Unexpected item binding version");var snapshot=JsonSerializer.Deserialize<IronGolemItemBinding[]>(reader.ReadString());
        Require(snapshot.SequenceEqual(states)&&IronGolemItems.Snapshot().SequenceEqual(states),"Worker serialization mutated item bindings");
        foreach(var bad in new[]{states[0] with{Pet=0},states[0] with{Item=1},states[0] with{Owner=states[0].Pet},states[0] with{BaseCode="unsupported-base"},states[0] with{BodyLocation=0},states[0]})
        {
            bool refused=false;try{IronGolemItems.Restore(states.Append(bad).ToArray());}catch(InvalidDataException){refused=true;}
            Require(refused&&IronGolemItems.Snapshot().SequenceEqual(states),"Invalid/duplicate binding restore was not atomic");
        }
        bool itemDuplicate=false;try{IronGolemItems.Restore(new[]{states[0],states[1] with{Item=states[0].Item}});}catch(InvalidDataException){itemDuplicate=true;}
        Require(itemDuplicate&&IronGolemItems.Snapshot().SequenceEqual(states),"Duplicate original item bound twice during restore");
        var doomed=Pet(owner);var spent=new Broadsword();Ground(spent,owner);Require(IronGolemItems.TryAttach(owner,doomed,spent,out why),why);IronGolemItems.DestroyBoundItem(doomed);
        Require(spent.Deleted&&IronGolemItems.State(doomed)==null,"Terminal item consumer returned or retained spent item");doomed.Delete();
        owner.AutoStablePets();
        Require(first.ControlMaster==owner&&second.ControlMaster==owner&&!first.IsStabled&&!second.IsStabled,"Persistent holder entered ordinary pet stabling");
        var saved=new Saved(owner.Serial.Value,[Capture(first,sword),Capture(second,armor)]);File.WriteAllText(Manifest,JsonSerializer.Serialize(saved));first.Internalize();second.Internalize();owner.Internalize();
        Log.Information("Iron Golem items:6144cast/135metadata source contexts,692current bases/316metal;original sword/armor identity,full gear rolls,private transfer/no reuse,terminal deletion,worker snapshot and atomic restore pass;full actor/property inheritance gated");
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));Require(World.FindMobile((Serial)saved.Owner) is PlayerMobile,"Saved owner missing");
        foreach(var s in saved.Items)
        {
            var pet=World.FindMobile((Serial)s.Binding.Pet) as BaseCreature;var item=World.FindItem((Serial)s.Binding.Item);
            Require(pet is{Alive:true,Deleted:false}&&item is{Deleted:false}&&pet.ControlMaster?.Serial.Value==saved.Owner,$"Saved actor/owner/original item missing: pet={s.Binding.Pet}/{pet?.GetType().Name}/alive={pet?.Alive}/deleted={pet?.Deleted}/controlled={pet?.Controlled}/master={pet?.Master?.Serial.Value}/control={pet?.ControlMaster?.Serial.Value}; item={s.Binding.Item}/{item?.GetType().Name}/deleted={item?.Deleted}/parent={item?.Parent}; owner={saved.Owner}");
            Require(Capture(pet,item)==s&&IronGolemItems.State(pet)==s.Binding,"Original item serial/type/hue/durability/full rolls or binding changed across actual reload");
            Require(!IronGolemItems.Eligible(item,out _),"Saved privately held item became reusable");pet.AIObject?.AITimer.Stop();
        }
        Log.Information("Iron Golem items:actual saved sword/armor retain original serial,parent,owner,visibility,move lock,durability and full gear rolls without clone/recast on reload");
    }
}
