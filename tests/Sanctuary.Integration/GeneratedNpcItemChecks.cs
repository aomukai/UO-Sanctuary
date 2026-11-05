// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
namespace Sanctuary.Integration;

public static class GeneratedNpcItemChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(GeneratedNpcItemChecks));
    // Identity and an equality witness only. No graph/metadata restore input
    // exists here:the native item serializer must restore its actual payload.
    private sealed record Held(int Case,uint Item,string SavedText);
    private sealed record Saved(Held[] Items,int Generation=0);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"generated-npc-item-fixtures.json");
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value);
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    public static void Configure(){Register("generated-npc-item-check",false);Register("generated-npc-item-reload-check",true);}
    private static void Register(string name,bool reload)=>ConsoleInputHandler.RegisterCommand(name,"Generated NPC native item creation and saved raw graphs",_=>Core.LoopContext.Post(()=>
    {try{Check(reload);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static PreparedItemRequest Request(JsonElement input)=>new(input[0].GetString(),input[1].GetInt32(),input[2].GetInt32(),input[3].GetInt32(),ulong.Parse(input[4].GetString()),ulong.Parse(input[5].GetString()),input[6].GetUInt32());
    private static void Refuses(GeneratedNpcItemState state)
    {bool refused=false;try{GeneratedNpcItem.Validate(state);}catch(InvalidDataException){refused=true;}Require(refused,"Malformed generated native state was accepted");}
    private static void Check(bool reload)
    {
        using var document=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"item-quality-current-cases.json")));var root=document.RootElement;var inputs=root.GetProperty("inputs");var outputs=root.GetProperty("outputs");
        var native=root.GetProperty("native").EnumerateArray().Select(v=>v.GetInt32()).Where(i=>inputs[i][7].GetInt32()==0).ToArray();Require(native.Length==2580,"Generated native source context count");
        if(!reload)
        {
            int count=World.Items.Count;
            foreach(var r in new[]{new PreparedItemRequest("ssd",0,3,0,1,2,3),new("ssd",1,0,0,1,2,3),new("ssd",1,5,0,1,2,3),new("ssd",1,7,0,1,2,3),new("ssd",1,3,3,1,2,3),new("ssd",1,3,0,1,0,3),new("hfh",1,3,0,1,2,3),new("gld",1,3,0,1,2,3)})
            {bool refused=false;try{GeneratedNpcItem.Create(r);}catch(NotSupportedException){refused=true;}Require(refused&&World.Items.Count==count,"Unsupported generated context allocated a world item");}
        }
        var saved=reload?JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest)):new Saved([]);Require(!reload||saved.Generation<2,"Generated native reload cohort");var rows=new List<Held>();
        for(int n=0;n<native.Length;n++)
        {
            int index=native[n];var request=Request(inputs[index]);GeneratedNpcItem item;
            if(reload)
            {
                var original=saved.Items[n];Require(original.Case==index,"Generated original context changed");item=World.FindItem((Serial)original.Item)as GeneratedNpcItem;
                Require(item is{Deleted:false,Visible:false,Movable:false,Parent:null}&&item.Map==Map.Trammel&&item.SavedText==original.SavedText,"Native generated item identity or actual saved payload changed");
            }
            else{item=GeneratedNpcItem.Create(request);item.MoveToWorld(new(5700+n%40,2700+n/40,0),Map.Trammel);}
            var state=item.Snapshot;Require(state.Request==request&&state.Version==0&&state.ReferenceVersion=="3.3.93847"&&item.Layer==Layer.Invalid,"Generated item request/version/presentation state");
            Require(item.Name.EndsWith("[generated equipment; art placeholder]",StringComparison.Ordinal)&&!ClassGear.Converted(item)&&ClassGear.State(item)==null,"Generated item incorrectly entered player gear factory");
            var graph=item.RestoreGraph(GeneratedNpcItem.CreationValues());Require(graph.Draws==0&&Json(graph.Capture())==Json(state.Graph),"Native restoration rerolled or lost fields");
            CurrentItemQualityChecks.Verify(state.Metadata,graph,outputs[index],"native "+index);CurrentItemQualityChecks.Continuation(graph,outputs[index]);Require(item.SavedText==Json(state),"Transient graph continuation edited stored item state");
            // Neither the decoded snapshot nor a transient graph can mutate
            // the original item by aliasing its arrays or cached payload.
            string before=item.SavedText;state.Metadata.Affixes.Prefixes[0]=int.MaxValue;
            if(state.Graph.ItemBase.Length!=0){state.Graph.ItemBase[0]=new(0,999);}
            Require(item.SavedText==before&&Json(item.Snapshot)==before,"Generated native snapshot leaked a mutable alias");
            rows.Add(new(index,item.Serial.Value,item.SavedText));
        }
        if(!reload)
        {
            var item=(GeneratedNpcItem)World.FindItem((Serial)rows[0].Item);var state=item.Snapshot;
            Refuses(state with{Version=1});Refuses(state with{ReferenceVersion="unproved"});Refuses(state with{Metadata=state.Metadata with{Code="ssd!"}});
            Refuses(state with{Metadata=state.Metadata with{Flags=0}});Refuses(state with{Metadata=state.Metadata with{Variant=-1}});
            Refuses(state with{Metadata=state.Metadata with{Affixes=state.Metadata.Affixes with{Prefixes=[int.MaxValue,0,0]}}});
            Refuses(state with{Metadata=state.Metadata with{AutoAffix=int.MaxValue}});Refuses(state with{Graph=state.Graph with{Seed=0}});
            Refuses(state with{Graph=state.Graph with{ItemBase=[new(0,1),new(0,2)]}});Refuses(state with{Graph=state.Graph with{Properties=[new(uint.MaxValue,1)],HasProperties=true}});
            Require(!item.OnDragLift(null)&&!item.CanEquip(null)&&!item.AllowSecureTrade(null,null,null,false),"Generated item exposed a player acquisition/equip route");
            var disposable=GeneratedNpcItem.Create(state.Request);uint serial=disposable.Serial.Value;disposable.Delete();Require(World.FindItem((Serial)serial)==null,"Generated deletion retained a world item");
            bool deleted=false;try{disposable.RestoreGraph(GeneratedNpcItem.CreationValues());}catch(InvalidOperationException){deleted=true;}Require(deleted,"Deleted generated item restored a graph");
        }
        File.WriteAllText(Manifest,Json(new Saved(rows.ToArray(),reload?saved.Generation+1:0)));
        Log.Information("Generated NPC items:2580original native serialized items/516bases/five qualities;source metadata/raw graphs/separate seeds and continuation retained without manifest restoration through two reloads;invalid contexts,alias isolation,player-route refusal and deletion pass;NPC equip/recipient delivery remain open");
    }
}
