// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
namespace Sanctuary.Integration;

public static class CurrentItemAffixChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(CurrentItemAffixChecks));
    private sealed record Held(int Case,uint Item,int Result,CurrentAffixState Metadata,ItemPropertyCreationState Graph);
    private sealed record Saved(Held[] Items,int Generation=0);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"current-item-affixes-fixtures.json");
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value);
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    public static void Configure(){Register("current-item-affixes-check",false);Register("current-item-affixes-reload-check",true);}
    private static void Register(string name,bool reload)=>ConsoleInputHandler.RegisterCommand(name,"Composed current magic/rare affixes and original graphs",_=>Core.LoopContext.Post(()=>
    {try{Check(reload);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static EquipmentStatValues Services()=>new(_=>4,_=>null,_=>null,(_,_,_,_,_)=>{});
    private static EquipmentStatValue[] Initial=>new[]{(21,11),(22,27),(23,0),(24,0),(159,0),(160,0),(31,51),(72,17),(73,23)}.Select(p=>new EquipmentStatValue(EquipmentStatKey.Pack(p.Item1),p.Item2)).ToArray();
    private static bool Same(EquipmentStatValue[] values,JsonElement expected)
    {
        var wanted=expected.EnumerateArray().ToDictionary(v=>v[0].GetUInt32(),v=>v[1].GetInt32());
        return values.Select(v=>v.Packed).Concat(wanted.Keys).Distinct().All(k=>EquipmentStatValues.Raw(values,k)==wanted.GetValueOrDefault(k));
    }
    private static (int Result,CurrentAffixState Metadata,ItemPropertyCreationContext Graph) Run(Item item,JsonElement input)
    {
        string code=input[0].GetString();var d=CurrentItemInitialization.For(code);var graph=new ItemPropertyCreationContext(item,Services(),d.PropertyBase,d.Weapon,d.Armor,d.Throwable,ulong.Parse(input[3].GetString()),Initial);
        var adapter=new CurrentItemAffixes(graph,new(code,100,input[2].GetInt32(),input[1].GetInt32(),0,0,input[15].GetUInt32()|8,511,99));
        int result=1;
        switch(input[4].GetInt32())
        {
            case 0:result=SourceItemAffixes.Magic(adapter,input[5].GetInt32(),input[8].GetInt32());break;
            case 1:result=SourceItemAffixes.Rare(adapter,[input[5].GetInt32(),input[6].GetInt32(),input[7].GetInt32()],[input[8].GetInt32(),input[9].GetInt32(),input[10].GetInt32()]);break;
            case 2:adapter.ApplyProperties(adapter.PropertyRecord(input[11].GetInt32()));break;
            case 3:CurrentTreePropertyWriter.Apply(EquipmentPropertyWriter.Plan("skilltab"),new(input[13].GetInt32(),input[14].GetInt32(),input[12].GetInt32()),graph);break;
            default:throw new InvalidOperationException();
        }
        return(result,adapter.Capture(),graph);
    }
    private static void Verify(int result,CurrentAffixState meta,ItemPropertyCreationContext graph,JsonElement expected,string label)
    {
        Require(result==expected[0].GetInt32()&&meta.Identified==((expected[1].GetInt32()&16)!=0)&&meta.RarePrefix==expected[2].GetInt32()&&meta.RareSuffix==expected[3].GetInt32(),"Current affix result/names/identified: "+label);
        Require(Json(meta.Prefixes)==expected[4].GetRawText()&&Json(meta.Suffixes)==expected[5].GetRawText()&&Json(meta.Applied)==expected[6].GetRawText(),"Current affix global IDs/property application order: "+label+" actual="+Json(meta)+" expected="+expected.GetRawText());
        var state=graph.Capture();Require(state.Seed==ulong.Parse(expected[7].GetString())&&state.HasProperties==expected[8].GetBoolean()&&Same(state.ItemBase,expected[9])&&Same(state.Properties,expected[10]),"Current affix exact raw graph/RNG/list: "+label+" actual="+Json(state)+" expected="+expected.GetRawText());
    }
    private static void Continuation(ItemPropertyCreationContext graph,JsonElement expected)
    {
        graph.Apply(EquipmentPropertyWriter.Plan("hp"),new(1,5,0));Require(graph.Seed==ulong.Parse(expected[12].GetString())&&Same(graph.Capture().Properties,expected[13]),"Composed affix next HP-property continuation");
    }
    private static void Check(bool reload)
    {
        using var document=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"current-item-affixes-cases.json")));var root=document.RootElement;var inputs=root.GetProperty("inputs");var outputs=root.GetProperty("outputs");var native=root.GetProperty("native").EnumerateArray().Select(v=>v.GetInt32()).ToArray();
        Require(inputs.GetArrayLength()==14888&&native.Length==1680,"Current composed source/native counts");
        if(!reload)
        {
            var scratch=new Item(0x14F0){Visible=false,Movable=false};
            try
            {
                for(int i=0;i<inputs.GetArrayLength();i++)
                {
                    var run=Run(scratch,inputs[i]);Verify(run.Result,run.Metadata,run.Graph,outputs[i],i.ToString());Require(run.Graph.Draws==outputs[i][11].GetInt32(),"Composed item RNG draw count at "+i);Continuation(run.Graph,outputs[i]);
                }
                // Unsupported separate random-tree semantics and invalid
                // ordinary tree parameters refuse without graph mutation.
                var sample=Run(scratch,inputs[native[0]]);string before=Json(sample.Graph.Capture());
                foreach(int parameter in new[]{-1,24})
                {bool refused=false;try{CurrentTreePropertyWriter.Apply(EquipmentPropertyWriter.Plan("skilltab"),new(1,3,parameter),sample.Graph);}catch(NotSupportedException){refused=true;}Require(refused&&Json(sample.Graph.Capture())==before,"Invalid tree context mutated graph");}
                bool randomRefused=false;try{CurrentTreePropertyWriter.Apply(EquipmentPropertyWriter.Plan("war-tab-rand"),new(1,3,21),sample.Graph);}catch(NotSupportedException){randomRefused=true;}Require(randomRefused&&Json(sample.Graph.Capture())==before,"Random Warlock tree property silently substituted");
            }
            finally{scratch.Delete();}
        }
        var saved=reload?JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest)):new Saved([]);Require(!reload||saved.Generation<2,"Composed affix reload cohort");var rows=new List<Held>();
        for(int n=0;n<native.Length;n++)
        {
            int index=native[n];var input=inputs[index];var expected=outputs[index];Item item;ItemPropertyCreationContext graph;CurrentAffixState meta;int result;
            if(reload)
            {
                var original=saved.Items[n];Require(original.Case==index,"Composed original context changed");item=World.FindItem((Serial)original.Item);Require(item is{Deleted:false,Visible:false,Movable:false,Parent:null}&&item.Map==Map.Trammel,"Composed original item identity changed");
                var d=CurrentItemInitialization.For(input[0].GetString());graph=ItemPropertyCreationContext.Restore(item,Services(),d.PropertyBase,d.Weapon,d.Armor,d.Throwable,original.Graph);Require(graph.Draws==0&&Json(graph.Capture())==Json(original.Graph),"Composed saved graph lost data/rerolled");meta=original.Metadata;result=original.Result;
            }
            else
            {
                item=new Item(0x14F0){Name="Current affixes [unattached source fixture]",Visible=false,Movable=false};item.MoveToWorld(new(5900+n%30,2700+n/30,0),Map.Trammel);var run=Run(item,input);graph=run.Graph;meta=run.Metadata;result=run.Result;
            }
            Verify(result,meta,graph,expected,index.ToString());rows.Add(new(index,item.Serial.Value,result,meta,graph.Capture()));Continuation(graph,expected);
        }
        Require(rows.Count==1680,"Incomplete original current-affix cohort");File.WriteAllText(Manifest,Json(new Saved(rows.ToArray(),reload?saved.Generation+1:0)));
        Log.Information("Current item affixes:14888composed source cases;692bases/988affix rows/89property codes/216approved tree controls;1680original graphs,global IDs,ordered properties,staffmods,RNG and two-reload continuation;full factory/NPC actors remain open");
    }
}
