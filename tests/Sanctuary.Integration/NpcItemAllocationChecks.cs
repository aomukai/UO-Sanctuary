// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
namespace Sanctuary.Integration;
public static class NpcItemAllocationChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(NpcItemAllocationChecks));
    private sealed record Held(uint Item,string Text);private sealed record Saved(Held[] Items,int Generation=0);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"npc-item-allocation-fixtures.json");
    private static string Json<T>(T v)=>JsonSerializer.Serialize(v);
    private static void Require(bool b,string why){if(!b){throw new InvalidOperationException(why);}}
    public static void Configure(){Register("npc-item-allocation-check",false);Register("npc-item-allocation-reload-check",true);}
    private static void Register(string n,bool reload)=>ConsoleInputHandler.RegisterCommand(n,"Independent source item allocation and actual native items",_=>Core.LoopContext.Post(()=>
    {try{Check(reload);Log.Information("{Name}: passed",n);}catch(Exception e){Log.Error(e,"{Name}: failed",n);}}));
    private static void Differential()
    {
        using var d=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"npc-item-allocation-cases.json")));int count=0,items=0;
        foreach(var c in d.RootElement.EnumerateArray())
        {
            ulong game=ulong.Parse(c[0].GetString());foreach(var row in c[6].EnumerateArray())
            {
                if(row.ValueKind==JsonValueKind.Null){continue;}var a=NpcItemAllocations.Take(ref game);
                Require(a.UnitStart==row[1].GetUInt32()&&SourceRandom.InitLow(unchecked((int)a.UnitStart))==ulong.Parse(row[2].GetString())
                    &&a.ItemStart==row[3].GetUInt32()&&SourceRandom.InitLow(unchecked((int)a.ItemStart))==ulong.Parse(row[4].GetString()),"Full source allocator seed mismatch");items++;
            }
            Require(game==ulong.Parse(c[7].GetString()),"Source game continuation");count++;
        }
        Require(count==960&&items==2160,"Source allocation scope count");
    }
    private static void Check(bool reload)
    {
        Differential();Saved saved;
        if(!reload)
        {
            var rows=new System.Collections.Generic.List<Held>();
            foreach(int quality in new[]{4,6})foreach(string code in new[]{"ssd","rin","amu"})
            {
                var game=new SourceSeedStream(SourceRandom.InitLow(123+quality));ulong before=game.Value;
                var item=GeneratedNpcItem.CreateAllocated(code,85,quality,1,game);var s=item.Snapshot;
                ulong expected=before;var a=NpcItemAllocations.Take(ref expected);Require(s.Allocation==a&&game.Value==expected&&s.Metadata.Flags==0x82010,"Allocated identity/flags/seed ownership");
                var legacy=GeneratedNpcItem.Create(s.Request);
                try{Require(Json(s with{Allocation=null,Metadata=s.Metadata with{Flags=16}})==Json(legacy.Snapshot),"Factory flags changed qualified generation/seed result");}finally{legacy.Delete();}
                var bad=s with{Allocation=a with{GameAfter=a.GameAfter^1}};bool refused=false;try{GeneratedNpcItem.Validate(bad);}catch(InvalidDataException){refused=true;}Require(refused,"Corrupt allocation state accepted");
                item.MoveToWorld(new(6300+rows.Count,2500,0),Map.Trammel);rows.Add(new(item.Serial.Value,item.SavedText));
            }
            var stream=new SourceSeedStream(SourceRandom.InitLow(7));ulong seed=stream.Value;int n=World.Items.Count;bool rejected=false;
            try{GeneratedNpcItem.CreateAllocated("ssd",0,4,0,stream);}catch(NotSupportedException){rejected=true;}
            Require(rejected&&stream.Value==seed&&World.Items.Count==n,"Rejected context allocated or consumed game RNG");saved=new(rows.ToArray());
        }
        else
        {
            saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));Require(saved.Generation<2,"Allocation reload count");
            foreach(var row in saved.Items){var item=World.FindItem((Serial)row.Item)as GeneratedNpcItem;Require(item!=null&&item.SavedText==row.Text&&item.Snapshot.Allocation!=null,"Original allocated item or saved graph rerolled");}
            saved=saved with{Generation=saved.Generation+1};
        }
        File.WriteAllText(Manifest,Json(saved));
        Log.Information("NPC item allocation:960full older allocator contexts/2160successful source items;independent seeds and game continuation;6original new-factory items,legacy-quality equivalence,flags,refusal and original saves pass");
    }
}
