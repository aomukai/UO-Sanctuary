// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
namespace Sanctuary.Integration;

public static class DefilerLinkNumberChecks
{
    private sealed record Saved(int Generation,string Profile,DefilerLinkShape Shape);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"defiler-link-numbers-fixtures.json");
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(DefilerLinkNumberChecks));
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    public static void Configure()
    {
        foreach(string name in new[]{"defiler-link-numbers-check","defiler-link-numbers-reload-check"})
        {string label=name;ConsoleInputHandler.RegisterCommand(label,"Pinned raw Health Link suppliers and common innate NPC ranks",_=>Core.LoopContext.Post(()=>
            {try{Check();var saved=label.EndsWith("-reload-check",StringComparison.Ordinal)?JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest)):new Saved(-1,"npc405-raw-unclamped-v1",DefilerLinkNumbers.Capture(25,25));
                Require(saved.Profile=="npc405-raw-unclamped-v1"&&saved.Shape==DefilerLinkNumbers.Capture(25,25),"Original raw-shape JSON profile/results changed on reload");
                File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{Generation=saved.Generation+1}));Log.Information("{Name}: passed",label);}catch(Exception e){Log.Error(e,"{Name}: failed",label);}}));}
    }
    private static void Check()
    {
        using var file=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"defiler-link-source-cases.json")));
        var percent=file.RootElement.EnumerateArray().Where(r=>r[0].GetInt32()==0).ToDictionary(r=>r[1].GetInt32(),r=>r[3].GetInt32());
        var limit=file.RootElement.EnumerateArray().Where(r=>r[0].GetInt32()==1).ToDictionary(r=>r[1].GetInt32(),r=>r[3].GetInt32());int count=0;
        for(int rank=1;rank<=255;rank++)for(int command=0;command<=1;command++)
        {
            var n=DefilerLinkNumbers.Capture(rank,rank,command);int level=InnateNpcSkills.Level(rank,command);
            Require(n.Rank==level&&n.SelfRank==level&&n.RawSharedPercent==percent[level]&&n.RawMaximumLinks==limit[level]
                &&n.PerCast==Math.Min(5,level+1)&&n.DurationFrames==500&&n.RadiusCells==25,"Health Link source expression/getter/range/lifetime projection differs");count++;
        }
        var seen=DefilerLinkNumbers.Capture(25,25);
        Require(seen.RawSharedPercent==55&&seen.RawMaximumLinks==5,"User current-build rank25tooltip differs");
        Require(DefilerLinkNumbers.Capture(1,25)is{Rank:1,SelfRank:25,PerCast:2,RawSharedPercent:55,RawMaximumLinks:5},"NPC skill rank and own self-skill reference were collapsed");
        Require(DefilerLinkNumbers.Capture(99,99)is{RawSharedPercent:61,RawMaximumLinks:6},"Unverified callback cap silently replaced the raw table formula");
        bool refused=false;try{_=DefilerLinkNumbers.Capture(0,1);}catch(NotSupportedException){refused=true;}Require(refused,"Missing NPC skill invented a Health Link rank");
        Require(!WarlockCombat.Implemented(CombatAbility.SummonDefiler),"Numeric foundation enabled unfinished Defiler link/controller");
        Log.Information("Defiler Health Link:{Count} source/getter contexts;198rawFogcomparisons;rank25=55percent/5links;high-rank callback caps explicitly unverified;no handler/event38",count);
    }
}
