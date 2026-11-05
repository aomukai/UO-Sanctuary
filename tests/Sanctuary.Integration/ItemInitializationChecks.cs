// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
namespace Sanctuary.Integration;

public static class ItemInitializationChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(ItemInitializationChecks));
    private static string Manifest=>Path.Join(Core.BaseDirectory,"item-initialization-fixtures.json");
    private sealed record Held(int Case,uint Item,CurrentItemInitialization Facts,int Extra,ulong UnitSeed,ItemPropertyCreationState BaseState);
    private sealed record Saved(Held[] Items,int Generation=0);
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    public static void Configure(){Register("item-initialization-check",false);Register("item-initialization-reload-check",true);}
    private static void Register(string name,bool reload)=>ConsoleInputHandler.RegisterCommand(name,"Complete source base initializer and original unattached native graphs",_=>Core.LoopContext.Post(()=>
    {try{Check(reload);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static bool Same(IDictionary<int,int> actual,JsonElement expected)=>actual.OrderBy(p=>p.Key).Select(p=>(p.Key,p.Value))
        .SequenceEqual(expected.EnumerateArray().Select(r=>(r[0].GetInt32(),r[1].GetInt32())));
    private sealed class Fixture(CurrentItemInitialization facts,JsonElement input,int extra):ItemInitializationServices
    {
        internal ulong Seed=ulong.Parse(input[1].GetString()),PropertySeed=ulong.Parse(input[12].GetString());internal readonly Dictionary<int,int> Stats=extra==0?new():new(){{254,extra}},Magic=new();internal readonly List<int[]> Trace=new();
        internal int Variant=-1,Elixirs;internal bool Identified,Item=true,Present=true,List=input[10].GetInt32()!=0;
        private void T(params int[] v)=>Trace.Add(v);
        internal override int Type=>facts.Type;internal override bool Quiver=>facts.Quiver;internal override bool Armor=>facts.Armor;internal override bool Weapon=>facts.Weapon;
        internal override bool Stackable=>facts.Stackable;internal override bool MagicOrBetter=>input[4].GetInt32()!=0;
        internal override int MinStack=>facts.MinStack;internal override int MaxStack=>facts.MaxStack(extra);internal override int SpawnStack=>facts.SpawnStack;internal override int Varieties=>facts.Varieties;
        internal override ItemInitializationBase Record=>Item&&Present?facts.Base:null;internal override bool HasItem=>Item;
        internal override uint Limited(int bound){uint v=SourceRandom.Limited(ref Seed,bound);T(0,bound,(int)v);return v;}
        internal override void Write(int stat,int value){Stats[stat]=value;T(1,stat,value);}
        internal override void Gold(int value){Stats[14]=value;T(2,value);}
        internal override void Elixir(){Elixirs++;T(3);}
        internal override void Variety(int value){Variant=value;T(4,value);}
        internal override int Quality(){int result=input[7].GetInt32();T(5,result);SourceRandom.Roll(ref PropertySeed);Item=input[8].GetInt32()!=0;Present=input[9].GetInt32()!=0;return result;}
        internal override bool HasMagicList=>List;
        internal override void AllocateMagicList(bool hasItem){T(6,hasItem?17:-1);List=input[11].GetInt32()!=0;}
        internal override void PostMagicList()=>T(7,List?1:0);
        internal override int Difficulty=>2;
        internal override void WriteMagic(int stat,int value){T(8,stat,value);if(List){Magic[stat]=value;}}
        internal override void Identify(){Identified=true;T(9);}
    }
    private static void Kernel(JsonElement root,CurrentItemInitialization[] facts)
    {
        var inputs=root.GetProperty("inputs");var outputs=root.GetProperty("outputs");int count=0;
        foreach(var i in inputs.EnumerateArray())
        {
            int basis=i[0].GetInt32();var d=root.GetProperty("descriptors")[basis];var f=new Fixture(facts[basis],i,d.GetProperty("ExtraStack").GetInt32());
            var drop=i[6].GetInt32()!=0?new ItemInitializationDrop(i[2].GetInt32(),i[3].GetInt32()):null;
            int result=SourceItemInitialization.Run(f,drop,i[5].GetInt32()!=0);var o=outputs[count];
            Require(result==o[0].GetInt32()&&f.Seed==ulong.Parse(o[1].GetString())&&Same(f.Stats,o[2])&&f.Variant==o[3].GetInt32()&&f.Elixirs==o[4].GetInt32()
                &&Same(f.Magic,o[5])&&f.List==(o[6].GetInt32()!=0)&&f.Identified==(o[7].GetInt32()!=0)&&f.Item==(o[9].GetInt32()!=0)&&f.PropertySeed==ulong.Parse(o[12].GetString()),"Complete initialization source state at "+count);
            var trace=o[8].EnumerateArray().Select(r=>r.EnumerateArray().Select(v=>v.GetInt32()).ToArray()).ToArray();
            Require(trace.Length==f.Trace.Count&&trace.Zip(f.Trace).All(p=>p.First.SequenceEqual(p.Second)),"Initialization action/RNG/quality/quest trace at "+count);count++;
        }
        Require(count==19296&&facts.Length==706,"Incomplete initialization source matrix");
        for(int i=0;i<692;i++)
        {var d=root.GetProperty("descriptors")[i];Require(JsonSerializer.Serialize(CurrentItemInitialization.For(d.GetProperty("Code").GetString()))==JsonSerializer.Serialize(facts[i]),"Current raw base/type descriptor mismatch at "+i);}
    }
    private static EquipmentStatValues Services()=>new(_=>4,_=>null,_=>null,(_,_,_,_,_)=>{});
    private static bool Raw(EquipmentStatValue[] actual,JsonElement expected)
    {
        var e=expected.EnumerateArray().Select(r=>new EquipmentStatValue(EquipmentStatKey.Pack(r[0].GetInt32()),r[1].GetInt32())).ToArray();
        return actual.Select(v=>v.Packed).Concat(e.Select(v=>v.Packed)).Distinct().All(k=>EquipmentStatValues.Raw(actual,k)==EquipmentStatValues.Raw(e,k));
    }
    private static void Continue(ItemPropertyCreationContext c,JsonElement expected)
    {
        Require(c.Seed==ulong.Parse(expected[12].GetString())&&Raw(c.Capture().ItemBase,expected[2])&&!c.Capture().HasProperties,"Native initialized base graph or seed changed");
        c.Apply(EquipmentPropertyWriter.Plan("hp"),new(1,5,0));
        Require(c.Seed==ulong.Parse(expected[10].GetString())&&EquipmentStatValues.Raw(c.Capture().Properties,EquipmentStatKey.Pack(7))==expected[11].GetInt32()*256,"Property writer did not continue the initialized item seed exactly");
    }
    private static void Check(bool reload)
    {
        using var source=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"item-initialization-cases.json")));var root=source.RootElement;
        var facts=root.GetProperty("descriptors").EnumerateArray().Select(d=>JsonSerializer.Deserialize<CurrentItemInitialization>(d.GetRawText())).ToArray();Kernel(root,facts);
        var saved=reload?JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest)):new Saved([]);Require(!reload||saved.Generation<2,"Wrong initialization reload cohort");
        var rows=new List<Held>();string[] codes=["ssd","qui","jav","aqv","amu","tbk","cap","buc","rin","key","sst","hxb"];
        if(reload)
        {
            foreach(var row in saved.Items)
            {
                var item=World.FindItem((Serial)row.Item);Require(item is{Deleted:false,Visible:false,Movable:false,Parent:null}&&item.Map==Map.Trammel,"Original initialization fixture identity changed");
                Require(row.UnitSeed==ulong.Parse(root.GetProperty("outputs")[row.Case][1].GetString()),"Saved unit/base seed changed");
                var d=row.Facts;var c=ItemPropertyCreationContext.Restore(item,Services(),d.PropertyBase,d.Weapon,d.Armor,d.Throwable,row.BaseState);
                Require(c.Draws==0&&JsonSerializer.Serialize(c.Capture())==JsonSerializer.Serialize(row.BaseState),"Base snapshot restore rerolled or altered fields");Continue(c,root.GetProperty("outputs")[row.Case]);rows.Add(row);
            }
        }
        else
        {
            foreach(string code in codes)for(int seedIndex=0;seedIndex<3;seedIndex++)
            {
                int basis=Enumerable.Range(0,692).Single(i=>root.GetProperty("descriptors")[i].GetProperty("Code").GetString()==code);int index=basis*24+seedIndex*8+1;
                var i=root.GetProperty("inputs")[index];var expected=root.GetProperty("outputs")[index];var d=CurrentItemInitialization.For(code);
                var item=new Item(0x14F0){Name="Initialized source base [unattached fixture]",Visible=false,Movable=false};item.MoveToWorld(new(6010+rows.Count%6,2940+rows.Count/6,0),Map.Trammel);
                var prepared=d.Prepare(item,Services(),ulong.Parse(i[1].GetString()),ulong.Parse(i[12].GetString()),drop:new(i[2].GetInt32(),i[3].GetInt32()));var c=prepared.Context;
                Require(prepared.Variant==expected[3].GetInt32()&&prepared.UnitSeed==ulong.Parse(expected[1].GetString())&&c.Draws==0&&prepared.BaseDraws==expected[8].EnumerateArray().Count(r=>r[0].GetInt32()==0&&r[1].GetInt32()>0),"Native base variant or RNG draw count changed");
                rows.Add(new(index,item.Serial.Value,d,0,prepared.UnitSeed,c.Capture()));Continue(c,expected);
                foreach(string unsupported in new[]{"gld","elx"})
                {
                    bool refused=false;try{CurrentItemInitialization.For(unsupported).Prepare(item,Services(),99,77);}catch(NotSupportedException){refused=true;}
                    Require(refused,"Unbound gold/elixir side effects silently admitted");
                }
            }
        }
        if(!reload)for(int basis=704;basis<706;basis++)for(int seedIndex=0;seedIndex<3;seedIndex++)
        {
            var inputs=root.GetProperty("inputs");
            // Select an actual supplied-input case:quality off,drop present,
            // the requested source seed;the other post-quality flags are idle.
            string seed=root.GetProperty("inputs")[seedIndex*8][1].GetString();
            int index=Enumerable.Range(16608,inputs.GetArrayLength()-16608).First(n=>inputs[n][0].GetInt32()==basis&&inputs[n][1].GetString()==seed&&inputs[n][5].GetInt32()==0&&inputs[n][6].GetInt32()==1);
            var i=inputs[index];var expected=root.GetProperty("outputs")[index];var d=facts[basis];int extra=root.GetProperty("descriptors")[basis].GetProperty("ExtraStack").GetInt32();
            var item=new Item(0x14F0){Name="Initialized stack operand [unattached fixture]",Visible=false,Movable=false};item.MoveToWorld(new(6010+rows.Count%6,2940+rows.Count/6,0),Map.Trammel);
            var prepared=d.Prepare(item,Services(),ulong.Parse(seed),ulong.Parse(i[12].GetString()),magic:true,extraStack:extra,drop:new(i[2].GetInt32(),i[3].GetInt32()));var c=prepared.Context;
            Require(EquipmentStatValues.Raw(c.Capture().ItemBase,EquipmentStatKey.Pack(254))==extra,"Initialized graph discarded its explicit extra-stack operand");
            Require(prepared.UnitSeed==ulong.Parse(expected[1].GetString())&&c.Draws==0,"Base initialization mutated property RNG or lost unit RNG");
            rows.Add(new(index,item.Serial.Value,d,extra,prepared.UnitSeed,c.Capture()));Continue(c,expected);
        }
        Require(rows.Count==42,"Incomplete original native initialization cohort");File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(rows.ToArray(),reload?saved.Generation+1:0)));
        Log.Information("Item initialization:19296complete source controls,692current bases/105types;42original unattached native item graphs retain exact base rolls and preserve separate base/property RNG and continue property RNG after reload;quality/full factory remains separate");
    }
}
