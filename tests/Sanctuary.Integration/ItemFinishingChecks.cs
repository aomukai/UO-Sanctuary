// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
namespace Sanctuary.Integration;
public static class ItemFinishingChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(ItemFinishingChecks));
    private sealed record Held(string Kind,int Case,uint Item,ItemPropertyCreationState Graph,ItemFinishingMetadata Metadata=null);
    private sealed record Saved(Held[] Items,int Generation=0);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"item-finishing-fixtures.json");
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value);
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    public static void Configure(){Register("item-finishing-check",false);Register("item-finishing-reload-check",true);}
    private static void Register(string name,bool reload)=>ConsoleInputHandler.RegisterCommand(name,"Complete finishing controllers,current approved properties and original graphs",_=>Core.LoopContext.Post(()=>
    {try{Check(reload);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static int[][] Initial=>new[]{new[]{21,11},new[]{22,27},new[]{23,0},new[]{24,0},new[]{159,0},new[]{160,0},new[]{31,51},new[]{72,17},new[]{73,23}};
    private sealed class Frame(JsonElement input):ItemFinishingServices
    {
        private int I(int n)=>input[n].GetInt32();private bool Bit(int n)=>(I(4)&n)!=0;
        internal ulong ItemSeed=ulong.Parse(input[14].GetString()),UnitSeed=ulong.Parse(input[15].GetString());
        internal int File=-1,SuffixValue=-1,Staff,Charms,Properties,SocketsValue;internal uint Flags;
        internal readonly Dictionary<int,int> Stats=Initial.ToDictionary(r=>r[0],r=>r[1]);internal readonly List<int[]> Trace=new();
        private void T(params int[] values)=>Trace.Add(values);
        internal override bool HasItem=>!Bit(1024);internal override ItemFinishingBase Basis=>!HasItem||Bit(2048)?null:new(I(12),Bit(512));
        internal override int Type=>I(3);internal override bool IsType(int type)=>type switch{45=>Bit(1),50=>Bit(2),3=>Bit(4),37=>Bit(8),_=>Type==type};
        internal override int Format=>I(1);internal override int Quality=>I(2);internal override bool Throwable=>Bit(16);internal override bool HasDurability=>Bit(32);
        internal override bool HasSuperiorTable=>!Bit(4096);internal override int SuperiorCount=>I(10);
        internal override SuperiorItemRecord SuperiorRecord(int id)
        {
            T(11,id);if(id<0||id>=I(10)||(I(11)&(1<<id))==0){return null;}
            return id<8?CurrentItemFinishing.Catalogue.Superior[id]:new(id,false,false,false,false,false,false,false,false,false,false,[]);
        }
        internal override void SuperiorProperties(SuperiorItemRecord row){Properties+=row.Row+1;T(7,row.Row);}
        internal override bool HasInferiorTable=>!Bit(8192);internal override int InferiorCount=>4;
        internal override uint ItemLimited(int bound){uint v=SourceRandom.Limited(ref ItemSeed,bound);T(0,0,bound,unchecked((int)v));return v;}
        internal override uint UnitLimited(int bound){uint v=SourceRandom.Limited(ref UnitSeed,bound);T(0,1,bound,unchecked((int)v));return v;}
        internal override uint ItemRandom(){uint v=SourceRandom.Roll(ref ItemSeed);T(1,0,unchecked((int)v));return v;}
        internal override int BaseStat(int stat)=>Stats.GetValueOrDefault(stat);
        internal override int FullStat(int stat)=>BaseStat(stat)+(Bit(32768)?0:stat==73?4:stat==72?2:0);
        internal override int ItemOrSkillStat(int stat)=>BaseStat(stat)+(Bit(32768)?0:stat==159?9:stat==160?13:0);
        internal override void WriteStat(int stat,int value){Stats[stat]=value;T(3,stat,value);}
        internal override void FileIndex(int id){File=id;T(2,id);}
        internal override void StaffMods(){Staff++;T(6);}
        private sealed class Eth(Frame f):EquipmentPropertyServices
        {
            internal override EquipmentPropertyBase Basis=>new();internal override bool Weapon=>f.IsType(45);internal override bool Armor=>f.IsType(50);internal override bool Throwable=>f.Throwable;
            internal override int BaseValue(int stat)=>f.BaseStat(stat);internal override void SetBase(int stat,int value)=>f.WriteStat(stat,value);
            internal override void SetEthereal()=>f.Flag(0x400000,true);
            internal override bool GetOrCreateList(int state,int flags)=>throw new InvalidOperationException();internal override int ListValue(int stat,int layer)=>throw new InvalidOperationException();
            internal override void SetList(int stat,int value,int layer)=>throw new InvalidOperationException();internal override void AddList(int stat,int value,int layer)=>throw new InvalidOperationException();internal override int Roll(int bound)=>throw new InvalidOperationException();
        }
        internal override void Ethereality()=>EquipmentPropertyWriter.Ethereality(new Eth(this));
        internal override bool Quest=>Bit(256);internal override uint Flags1=>input[5].GetUInt32();internal override uint Flags2=>input[6].GetUInt32();
        internal override bool Socketable=>Bit(64);internal override bool Stackable=>Bit(128);internal override uint MaxSockets=>input[8].GetUInt32();
        internal override int Difficulty=>I(7);internal override uint StartSeed=>input[9].GetUInt32();
        internal override void Flag(uint flag,bool value){Flags=value?Flags|flag:Flags&~flag;T(4,(int)flag,value?1:0);}
        internal override void AddSockets(uint n){SocketsValue=(int)n;T(5,(int)n);}
        internal override bool HasDropUnit=>Bit(16384);internal override int DropUnitClass=>123;internal override int DropIndex=>77;
        internal override void Charm(){Charms++;T(8);}
        internal override int Book(bool scroll){T(9,scroll?1:0);return scroll?71:88;}
        internal override void Suffix(int slot,int id){Require(slot==0,"Suffix slot");SuffixValue=id;T(10,id);}
        internal int Execute()
        {
            switch(I(0))
            {
                case 0:return SourceItemFinishing.Superior(this);case 1:return SourceItemFinishing.InferiorCore(this);case 2:return SourceItemFinishing.InferiorModern(this);case 3:return SourceItemFinishing.Inferior(this);
                case 4:SourceItemFinishing.Sockets(this);return 1;case 5:SourceItemFinishing.Ethereal(this);return 1;case 6:SourceItemFinishing.NormalLegacy(this);return 1;
                case 7:SourceItemFinishing.NormalModern(this);return 1;case 8:SourceItemFinishing.Normal(this);return 1;
                case 9:return SourceItemFinishing.CanBeSuperior(this,CurrentItemFinishing.Catalogue.Superior[I(13)])?1:0;
                default:throw new InvalidOperationException();
            }
        }
        internal string State(int result)=>Json(new object[]{result,File,SuffixValue,Staff,Charms,Properties,SocketsValue,Flags,ItemSeed.ToString(),UnitSeed.ToString(),Stats.OrderBy(p=>p.Key).Select(p=>new[]{p.Key,p.Value}),Trace});
    }
    private static EquipmentStatValues Services()=>new(_=>4,_=>null,_=>null,(_,_,_,_,_)=>{});
    private static EquipmentStatValue[] Fields(IEnumerable<int[]> rows)=>rows.Select(r=>new EquipmentStatValue(EquipmentStatKey.Pack(r[0]),r[1])).ToArray();
    private static bool Same(EquipmentStatValue[] actual,JsonElement expected,bool packed=true)
    {
        var values=expected.EnumerateArray().ToDictionary(r=>packed?r[0].GetUInt32():EquipmentStatKey.Pack(r[0].GetInt32()),r=>r[1].GetInt32());
        return actual.Select(r=>r.Packed).Concat(values.Keys).Distinct().All(k=>EquipmentStatValues.Raw(actual,k)==values.GetValueOrDefault(k));
    }
    private static Item NewItem(int n)
    {var item=new Item(0x14F0){Name="Item finishing [unattached source fixture]",Visible=false,Movable=false};item.MoveToWorld(new(6040+n%12,2940+n/12,0),Map.Trammel);return item;}
    private static void Kernel(JsonElement root)
    {
        var inputs=root.GetProperty("inputs");var outputs=root.GetProperty("outputs");Require(inputs.GetArrayLength()==20548,"Finishing source count");
        for(int i=0;i<inputs.GetArrayLength();i++){var f=new Frame(inputs[i]);int result=f.Execute();Require(f.State(result)==outputs[i].GetRawText(),"Complete finishing source state/trace at "+i);}
    }
    private static readonly EquipmentPropertyBase[] PropertyBases=[new(7,14,0,0,0,0,0,0),new(0,0,59,94,0,0,0,0),new(2,3,0,0,4,9,0,0),new(0,0,0,0,0,0,158,172)];
    private static EquipmentStatValue[] BaseFields(EquipmentPropertyBase b)=>Fields(new[]{new[]{21,b.Minimum},new[]{22,b.Maximum},new[]{23,b.SecondaryMinimum},new[]{24,b.SecondaryMaximum},new[]{159,b.ThrowMinimum},new[]{160,b.ThrowMaximum},new[]{31,b.MinimumArmor}}.Where(r=>r[1]!=0));
    private static void Check(bool reload)
    {
        using var controls=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"item-finishing-cases.json")));using var props=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"item-finishing-properties-cases.json")));
        var root=controls.RootElement;var physics=props.RootElement;Kernel(root);var saved=reload?JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest)):new Saved([]);Require(!reload||saved.Generation<2,"Wrong finishing reload cohort");var rows=new List<Held>();
        if(reload)
        {
            foreach(var r in saved.Items)
            {
                var item=World.FindItem((Serial)r.Item);Require(item is{Deleted:false,Visible:false,Movable:false,Parent:null}&&item.Map==Map.Trammel,"Original finishing native identity changed");
                EquipmentPropertyBase basis;bool weapon,armor,throwable;
                if(r.Kind=="property"){int b=physics.GetProperty("cases")[r.Case][0].GetInt32();basis=PropertyBases[b];weapon=b!=3;armor=b==3;throwable=b==2;}
                else{var d=CurrentItemInitialization.For(r.Metadata.Parameters.Code);basis=d.PropertyBase;weapon=d.Weapon;armor=d.Armor;throwable=d.Throwable;}
                var c=ItemPropertyCreationContext.Restore(item,Services(),basis,weapon,armor,throwable,r.Graph);Require(c.Draws==0&&Json(c.Capture())==Json(r.Graph),"Finishing graph restore rerolled or lost raw data");
                if(r.Kind=="property")
                {
                    var expected=physics.GetProperty("cases")[r.Case];c.Apply(EquipmentPropertyWriter.Plan("hp"),new(1,5,0));Require(c.Seed==ulong.Parse(expected[8].GetString())&&Same(c.Capture().Properties,expected[9]),"Approved grade/ethereal restored property continuation");
                }
                else
                {
                    var o=root.GetProperty("nativeOutputs")[r.Case];Require(r.Metadata.Parameters.UnitSeed==ulong.Parse(o[9].GetString())&&r.Metadata.File==o[1].GetInt32()&&r.Metadata.ItemFlags==o[7].GetUInt32(),"Saved physical finishing metadata or separate unit seed changed");
                    var match=physics.GetProperty("selected").EnumerateArray().FirstOrDefault(v=>v[0].GetInt32()==r.Case);
                    if(match.ValueKind!=JsonValueKind.Undefined){c.Apply(EquipmentPropertyWriter.Plan("hp"),new(1,5,0));Require(c.Seed==ulong.Parse(match[5].GetString())&&Same(c.Capture().Properties,match[6]),"Current selected superior restored RNG/property continuation");}
                }
                rows.Add(r);
            }
        }
        else
        {
            int n=0;foreach(var expected in physics.GetProperty("cases").EnumerateArray())
            {
                int b=expected[0].GetInt32();string code=b==3?"qui":b==2?"jav":"ssd";var item=NewItem(rows.Count);var values=Services();var c=new ItemPropertyCreationContext(item,values,PropertyBases[b],b!=3,b==3,b==2,ulong.Parse(expected[2].GetString()),BaseFields(PropertyBases[b]));
                var f=new CurrentItemFinishing(item,c,values,new(code,100,3,1,0,0,8,511,SourceRandom.InitLow(17)));f.SuperiorProperties(CurrentItemFinishing.Catalogue.Superior[expected[1].GetInt32()]);if(expected[3].GetInt32()!=0){f.Ethereality();}
                Require(c.Seed==ulong.Parse(expected[4].GetString())&&Same(c.Capture().ItemBase,expected[5])&&Same(c.Capture().Properties,expected[6])&&c.Draws==expected[7].GetInt32(),"Current approved superior numeric/ethereal source composition at "+n);
                rows.Add(new("property",n++,item.Serial.Value,c.Capture()));c.Apply(EquipmentPropertyWriter.Plan("hp"),new(1,5,0));Require(c.Seed==ulong.Parse(expected[8].GetString())&&Same(c.Capture().Properties,expected[9]),"Approved superior next property RNG");
            }
            var inputs=root.GetProperty("nativeInputs");var outputs=root.GetProperty("nativeOutputs");var codes=root.GetProperty("nativeCodes");Require(inputs.GetArrayLength()==96,"Current native finishing context count");
            for(int i=0;i<inputs.GetArrayLength();i++)
            {
                var a=inputs[i];var o=outputs[i];string code=codes[i].GetString();var d=CurrentItemInitialization.For(code);var item=NewItem(rows.Count);var values=Services();var c=new ItemPropertyCreationContext(item,values,d.PropertyBase,d.Weapon,d.Armor,d.Throwable,ulong.Parse(a[14].GetString()),Fields(Initial));
                var f=new CurrentItemFinishing(item,c,values,new(code,a[1].GetInt32(),a[2].GetInt32(),1,a[7].GetInt32(),a[5].GetUInt32(),a[6].GetUInt32(),a[9].GetUInt32(),ulong.Parse(a[15].GetString())));
                int result=1;switch(a[0].GetInt32()){case 0:result=SourceItemFinishing.Superior(f);break;case 3:result=SourceItemFinishing.Inferior(f);break;case 4:SourceItemFinishing.Sockets(f);break;case 5:SourceItemFinishing.Ethereal(f);break;}
                Require(result==o[0].GetInt32()&&f.SelectedFile==o[1].GetInt32()&&f.UnitSeed==ulong.Parse(o[9].GetString())&&f.ItemFlags==o[7].GetUInt32(),"Current physical source result/metadata/unit seed at "+i);
                var match=physics.GetProperty("selected").EnumerateArray().FirstOrDefault(v=>v[0].GetInt32()==i);
                if(match.ValueKind!=JsonValueKind.Undefined){Require(c.Seed==ulong.Parse(match[2].GetString())&&Same(c.Capture().ItemBase,match[3])&&Same(c.Capture().Properties,match[4]),"Actual selected superior current numeric source composition at "+i);}
                else{Require(c.Seed==ulong.Parse(o[8].GetString())&&Same(c.Capture().ItemBase,o[10],false)&&c.Capture().Properties.Length==0,"Actual current inferior/socket/ethereal source graph at "+i);}
                rows.Add(new("current",i,item.Serial.Value,c.Capture(),f.Capture()));
            }
            // These unsupported bindings refuse before changing the supplied
            // graph;no valid caster/special base is filtered out of a pool.
            var original=rows[0];var held=World.FindItem((Serial)original.Item);var cx=ItemPropertyCreationContext.Restore(held,Services(),PropertyBases[0],true,false,false,original.Graph);string before=Json(cx.Capture());
            foreach(string code in new[]{"tbk"})
            {bool refused=false;try{_=new CurrentItemFinishing(held,cx,Services(),new(code,100,3,1,0,0,8,511,99));}catch(NotSupportedException){refused=true;}Require(refused&&Json(cx.Capture())==before,"Unbound current staffmod/special context mutated graph");}
        }
        Require(rows.Count==288,"Incomplete original finishing item cohort");File.WriteAllText(Manifest,Json(new Saved(rows.ToArray(),reload?saved.Generation+1:0)));
        Log.Information("Item finishing:20548source controls/14routines,5536current eligibility pairs;192approved numeric compositions and96current physical contexts on288original items;separate RNG,actual grade/ethereal fields,refusals and original snapshot/reload continuation;full factory remains separate");
    }
}
