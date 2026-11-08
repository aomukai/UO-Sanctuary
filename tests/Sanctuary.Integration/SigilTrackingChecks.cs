// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
namespace Sanctuary.Integration;

public static class SigilTrackingChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(SigilTrackingChecks));
    private static string Manifest=>Path.Join(Core.BaseDirectory,"sigil-tracking-fixtures.json");
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    public static void Configure()
    {
        foreach(bool reload in new[]{false,true}){string name=reload?"sigil-tracking-reload-check":"sigil-tracking-check";ConsoleInputHandler.RegisterCommand(name,"Current Sigil tracking,stable replacement and conditional timer",_=>Core.LoopContext.Post(()=>{try{Check(reload);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));}
    }
    private static int[] Ints(JsonElement e)=>e.EnumerateArray().Select(x=>x.GetInt32()).ToArray();
    private static bool Same(IEnumerable<int[]> actual,JsonElement expected)
    {var a=actual.ToArray();var b=expected.EnumerateArray().Select(Ints).ToArray();return a.Length==b.Length&&!a.Where((r,i)=>!r.SequenceEqual(b[i])).Any();}
    private static void Check(bool reload)
    {
        int generation=reload?int.Parse(File.ReadAllText(Manifest))+1:0;
        using var json=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"sigil-tracking-cases.json")));int n=0;var world=NpcWorldAllocation.Capture();
        foreach(var row in json.RootElement.GetProperty("cases").EnumerateArray())
        {
            var c=row.GetProperty("input");var expected=row.GetProperty("output");int I(string k)=>c.GetProperty(k).GetInt32();bool B(string k)=>c.GetProperty(k).GetBoolean();
            var entries=c.GetProperty("entries").EnumerateArray().ToArray();var state=new SigilTrackingState{Expiry=c.GetProperty("expiry").GetSingle(),Flags=c.GetProperty("flags").GetUInt32()};state.Stats[(355,0)]=entries.Length;
            var actors=new List<SigilTrackedMissile>();var lookup=new Dictionary<int,SigilTrackedMissile>();var lookups=new List<int>();int factories=0;
            for(int i=0;i<entries.Length;i++)
            {
                var e=entries[i];int guid=e[0].GetInt32();state.Stats[(354,i+1)]=guid;state.Stats[(355,i+1)]=e[1].GetInt32();var a=new SigilTrackedMissile(e[2].GetInt32(),e[3].GetBoolean(),5000);actors.Add(a);if(e[4].GetBoolean()){lookup[guid]=a;}
            }
            bool result=SigilTracking.Run(B("new")?I("id"):null,I("limit"),I("start"),I("end"),new(()=>{factories++;return new(B("list")?state:null,B("created"));},guid=>{lookups.Add(guid);return lookup.GetValueOrDefault(guid);}));
            Require(result==(expected.GetProperty("reschedule").GetInt32()!=0)&&factories==expected.GetProperty("factoryCalls").GetInt32()&&lookups.SequenceEqual(Ints(expected.GetProperty("lookups"))),"Current tracking acquisition/lookup order or reschedule flag differs");
            Require(Same(state.Stats.Select(e=>new[]{e.Key.Stat,e.Key.Layer,e.Value}),expected.GetProperty("stats"))&&Same(state.Writes,expected.GetProperty("writes")),"Current slot reuse,max index or ordered stat writes differ");
            Require(state.Expiry==expected.GetProperty("expiry").GetSingle()&&state.Flags==expected.GetProperty("flags").GetUInt32()&&actors.Select(a=>a.Lifetime).SequenceEqual(expected.GetProperty("lifetimes").EnumerateArray().Select(e=>e.GetSingle())),"Current lifetime-zero replacement or state expiry/flags differ");n++;
        }
        Require(n==111&&NpcWorldAllocation.Capture()==world,"Pure tracking allocated world state");
        File.WriteAllText(Manifest,generation.ToString());Require(generation<=2,"Unexpected fixture generation");
        Log.Information("Sigil tracking:111current core/sort contexts,ordered lookup/write traces,stable equal-time replacement,missing/invalid IDs,slot reuse,max index,lifetime-zero eviction and conditional timer result match. World stat-list allocation,owner-state removal and recipients remain separate.");
    }
}
