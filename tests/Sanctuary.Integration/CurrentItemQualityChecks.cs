// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
namespace Sanctuary.Integration;

public static class CurrentItemQualityChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(CurrentItemQualityChecks));
    private sealed record Held(int Case,uint Item,PreparedItemState Metadata,ItemPropertyCreationState Graph);
    private sealed record Saved(Held[] Items,int Generation=0);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"item-quality-current-fixtures.json");
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value);
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    public static void Configure(){Register("item-quality-current-check",false);Register("item-quality-current-reload-check",true);}
    private static void Register(string name,bool reload)=>ConsoleInputHandler.RegisterCommand(name,"Current prepared quality/automagic and original graphs",_=>Core.LoopContext.Post(()=>
    {try{Check(reload);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static EquipmentStatValues Services()=>new(_=>4,_=>null,_=>null,(_,_,_,_,_)=>{});
    private static EquipmentStatValue[] Initial=>new[]{(21,11),(22,27),(23,0),(24,0),(159,0),(160,0),(31,51),(72,17),(73,23)}.Select(p=>new EquipmentStatValue(EquipmentStatKey.Pack(p.Item1),p.Item2)).ToArray();
    private static bool Same(EquipmentStatValue[] values,JsonElement expected)
    {
        var wanted=expected.EnumerateArray().ToDictionary(v=>v[0].GetUInt32(),v=>v[1].GetInt32());
        return values.Select(v=>v.Packed).Concat(wanted.Keys).Distinct().All(k=>EquipmentStatValues.Raw(values,k)==wanted.GetValueOrDefault(k));
    }
    private static PreparedItemResult Run(Item item,JsonElement input)
    {
        string code=input[0].GetString();int level=input[1].GetInt32(),quality=input[2].GetInt32(),mode=input[7].GetInt32();
        ulong unit=ulong.Parse(input[4].GetString()),seed=ulong.Parse(input[5].GetString());uint start=input[6].GetUInt32();
        if(mode==0){return CurrentItemQuality.Prepare(item,Services(),new(code,level,quality,input[3].GetInt32(),unit,seed,start));}
        var d=CurrentItemInitialization.For(code);var graph=new ItemPropertyCreationContext(item,Services(),d.PropertyBase,d.Weapon,d.Armor,d.Throwable,seed,Initial);
        var affixes=new CurrentAffixState(new int[3],new int[3],0,0,false,[]);
        var row=mode==1?CurrentAutoMagic.Select(code,level,quality,input[8].GetInt32(),affixes,graph):CurrentAutoMagic.Catalogue.AutoMagic[input[8].GetInt32()];
        int id=row==null?0:CurrentAutoMagic.Id(row);if(row!=null){CurrentItemAffixes.ApplyModifiers(row.Modifiers,graph,level);}
        return new(graph,new(code,quality,quality,start,unit,0,0,id,0,affixes,id==0?[]:[id]),0);
    }
    private static void Verify(PreparedItemState meta,ItemPropertyCreationContext graph,JsonElement expected,string label)
    {
        Require(expected[0].GetInt32()==1&&meta.Quality==expected[1].GetInt32()&&meta.DropQuality==expected[2].GetInt32()
            &&meta.StartSeed==expected[3].GetUInt32()&&meta.UnitSeed==ulong.Parse(expected[4].GetString())&&meta.Variant==expected[5].GetInt32()
            &&meta.File==expected[6].GetInt32()&&meta.AutoAffix==expected[7].GetInt32()&&meta.Flags==expected[8].GetUInt32(),"Prepared quality metadata: "+label+" actual="+Json(meta)+" expected="+expected.GetRawText());
        var a=meta.Affixes;
        Require(a.Identified==((meta.Flags&16)!=0)&&Json(a.Prefixes)==expected[9].GetRawText()&&Json(a.Suffixes)==expected[10].GetRawText()
            &&a.RarePrefix==expected[11].GetInt32()&&a.RareSuffix==expected[12].GetInt32()&&Json(a.Applied)==expected[13].GetRawText()
            &&Json(meta.AutoApplied)==expected[14].GetRawText(),"Prepared quality affix order/names: "+label);
        var state=graph.Capture();Require(state.Seed==ulong.Parse(expected[15].GetString())&&state.HasProperties==expected[16].GetBoolean()
            &&!state.Ethereal&&Same(state.ItemBase,expected[17])&&Same(state.Properties,expected[18]),"Prepared quality raw graph/RNG: "+label+" actual="+Json(state)+" expected="+expected.GetRawText());
    }
    private static void Continuation(ItemPropertyCreationContext graph,JsonElement expected)
    {
        graph.Apply(EquipmentPropertyWriter.Plan("hp"),new(1,5,0));Require(graph.Seed==ulong.Parse(expected[21].GetString())&&Same(graph.Capture().Properties,expected[22]),"Prepared quality next HP-property continuation");
    }
    private static void Check(bool reload)
    {
        using var document=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"item-quality-current-cases.json")));var root=document.RootElement;var inputs=root.GetProperty("inputs");var outputs=root.GetProperty("outputs");var native=root.GetProperty("native").EnumerateArray().Select(v=>v.GetInt32()).ToArray();
        Require(inputs.GetArrayLength()==15820&&native.Length==2624,"Prepared quality source/native counts");
        if(!reload)
        {
            var scratch=new Item(0x14F0){Visible=false,Movable=false};
            try
            {
                for(int i=0;i<inputs.GetArrayLength();i++)
                {
                    var run=Run(scratch,inputs[i]);Verify(run.State,run.Graph,outputs[i],i.ToString());Require(run.Graph.Draws==outputs[i][19].GetInt32(),"Prepared property RNG draw count at "+i+" actual="+run.Graph.Draws+" expected="+outputs[i][19]);Continuation(run.Graph,outputs[i]);
                }
                foreach(var r in new[]{new PreparedItemRequest("ssd",0,3,0,1,2,3),new("ssd",1,0,0,1,2,3),new("ssd",1,5,0,1,2,3),new("ssd",1,7,0,1,2,3),new("ssd",1,3,3,1,2,3),new("ssd",1,3,0,1,0,3),new("hfh",1,3,0,1,2,3),new("gld",1,3,0,1,2,3)})
                {bool refused=false;try{CurrentItemQuality.Prepare(scratch,Services(),r);}catch(NotSupportedException){refused=true;}Require(refused,"Unsupported prepared quality context was admitted");}
            }
            finally{scratch.Delete();}
        }
        var saved=reload?JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest)):new Saved([]);Require(!reload||saved.Generation<2,"Prepared reload cohort");var rows=new List<Held>();
        for(int n=0;n<native.Length;n++)
        {
            int index=native[n];var input=inputs[index];var expected=outputs[index];Item item;ItemPropertyCreationContext graph;PreparedItemState meta;
            if(reload)
            {
                var original=saved.Items[n];Require(original.Case==index,"Prepared original context changed");item=World.FindItem((Serial)original.Item);Require(item is{Deleted:false,Visible:false,Movable:false,Parent:null}&&item.Map==Map.Trammel,"Prepared original item identity changed");
                var d=CurrentItemInitialization.For(input[0].GetString());graph=ItemPropertyCreationContext.Restore(item,Services(),d.PropertyBase,d.Weapon,d.Armor,d.Throwable,original.Graph);Require(graph.Draws==0&&Json(graph.Capture())==Json(original.Graph),"Prepared saved graph lost data/rerolled");meta=original.Metadata;
            }
            else
            {
                item=new Item(0x14F0){Name="Prepared quality [unattached source fixture]",Visible=false,Movable=false};item.MoveToWorld(new(5800+n%40,2700+n/40,0),Map.Trammel);var run=Run(item,input);graph=run.Graph;meta=run.State;
            }
            Verify(meta,graph,expected,index.ToString());rows.Add(new(index,item.Serial.Value,meta,graph.Capture()));Continuation(graph,expected);
        }
        Require(rows.Count==2624,"Incomplete original prepared-quality cohort");File.WriteAllText(Manifest,Json(new Saved(rows.ToArray(),reload?saved.Generation+1:0)));
        Log.Information("Current prepared quality:15820source cases;516bases/44automagic rows;2624original graphs,quality fallback,ordered properties,separate RNG and two-reload continuation;allocation/materialization/NPC delivery remain open");
    }
}
